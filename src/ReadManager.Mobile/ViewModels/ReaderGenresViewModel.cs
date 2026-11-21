using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.ViewModels;

// CHỌN SỞ THÍCH ĐỌC (thể loại yêu thích)
// - Hiện tự động ngay sau khi đăng ký xong (và lần đăng nhập đầu nếu chưa chọn).
// - Mở lại được ở Tài khoản > "Sở thích đọc".
// - Lưu lên máy chủ (PUT api/reader/genres) nên đăng nhập lại vẫn giữ nguyên.
public partial class ReaderGenresViewModel : BaseViewModel
{
    private readonly ReaderActivityService _reader;

    public ObservableCollection<ChipItem> Genres { get; } = new();

    [ObservableProperty] private string selectedText = "Chưa chọn thể loại nào";
    [ObservableProperty] private bool canSave;
    [ObservableProperty] private bool canSkip;     // chỉ hiện "Để sau" khi đây là lần hỏi đầu tiên

    public ReaderGenresViewModel(ReaderActivityService reader) => _reader = reader;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            var profile = await _reader.Profile();
            var genres = await _reader.Genres() ?? new List<Genre>();
            var chosen = profile?.Genres ?? Array.Empty<int>();

            Genres.Clear();
            foreach (var g in genres)
                Genres.Add(new ChipItem(g.Slug, g.Name, g.GenreId) { IsSelected = chosen.Contains(g.GenreId) });

            CanSkip = profile?.Prompted == false;
            UpdateSummary();
        });
    }

    [RelayCommand]
    private void Toggle(ChipItem? chip)
    {
        if (chip is null) return;
        chip.IsSelected = !chip.IsSelected;
        UpdateSummary();
    }

    [RelayCommand]
    private Task SaveAsync() => SubmitAsync(Genres.Where(c => c.IsSelected && c.Id.HasValue).Select(c => c.Id!.Value).ToArray());

    [RelayCommand]
    private Task SkipAsync() => SubmitAsync(Array.Empty<int>());

    private async Task SubmitAsync(int[] ids)
    {
        await RunSafeAsync(async () =>
        {
            await _reader.SaveGenres(ids);
            RecommendationService.NotifyPreferencesChanged();   // trang chủ tải lại "Đề xuất cho bạn"
            await Shell.Current.GoToAsync("..");
        });
    }

    private void UpdateSummary()
    {
        var n = Genres.Count(c => c.IsSelected);
        SelectedText = n == 0 ? "Chưa chọn thể loại nào" : $"Đã chọn {n} thể loại";
        CanSave = n > 0;
    }
}
