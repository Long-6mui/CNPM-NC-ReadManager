using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Genres;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public enum DeleteGenreResult
{
    Success,
    NotFound,
    InUse
}

public interface IGenreService
{
    Task<List<GenreDto>> GetAllAsync();
    Task<GenreDto> CreateAsync(CreateGenreDto dto);
    Task<DeleteGenreResult> DeleteAsync(int id);
}

public class GenreService : IGenreService
{
    private readonly AppDbContext _db;

    public GenreService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<GenreDto>> GetAllAsync()
    {
        // Đếm StoryCount bằng subquery tương quan (correlated subquery) ngay trong
        // câu Select — EF Core dịch thành 1 câu SQL duy nhất, không bị N+1 query.
        return await _db.Genres
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto
            {
                GenreId = g.GenreId,
                Name = g.Name,
                Slug = g.Slug,
                StoryCount = _db.StoryGenres.Count(sg => sg.GenreId == g.GenreId)
            })
            .ToListAsync();
    }

    public async Task<GenreDto> CreateAsync(CreateGenreDto dto)
    {
        var name = dto.Name.Trim();

        if (await _db.Genres.AnyAsync(g => g.Name == name))
            throw new InvalidOperationException($"Thể loại \"{name}\" đã tồn tại.");

        var genre = new Genre
        {
            Name = name,
            Slug = await BuildUniqueSlugAsync(name)
        };

        _db.Genres.Add(genre);
        await _db.SaveChangesAsync();

        return new GenreDto
        {
            GenreId = genre.GenreId,
            Name = genre.Name,
            Slug = genre.Slug,
            StoryCount = 0
        };
    }

    public async Task<DeleteGenreResult> DeleteAsync(int id)
    {
        var genre = await _db.Genres.FindAsync(id);
        if (genre is null)
            return DeleteGenreResult.NotFound;

        // Kiểm tra trước thay vì để MySQL ném lỗi khóa ngoại (StoryGenres -> Genres
        // đang là DeleteBehavior.Restrict) — để controller trả lỗi thân thiện hơn.
        var inUse = await _db.StoryGenres.AnyAsync(sg => sg.GenreId == id);
        if (inUse)
            return DeleteGenreResult.InUse;

        _db.Genres.Remove(genre);
        await _db.SaveChangesAsync();
        return DeleteGenreResult.Success;
    }

    private async Task<string> BuildUniqueSlugAsync(string name)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var suffix = 2;

        while (await _db.Genres.AnyAsync(g => g.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    // Chuyển tên tiếng Việt có dấu thành slug an toàn cho URL, ví dụ:
    // "Kiếm Hiệp" -> "kiem-hiep". Cùng cách làm với ZipStoryImporter.cs để nhất quán.
    private static string Slugify(string value)
    {
        var lower = value.ToLowerInvariant().Replace('đ', 'd').Replace('Đ', 'd');
        var normalized = lower.Normalize(NormalizationForm.FormD);

        var stripped = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        var slug = Regex.Replace(stripped.ToString(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "the-loai" : slug;
    }
}