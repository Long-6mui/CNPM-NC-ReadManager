using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.Services;

public record ReaderProfile(int Exp, int Level, bool Prompted, int[] Genres);
public record RankedStory(int StoryId, string Title, int Hearts);
public record RewardResult(bool Granted);
public record ReadingTicket(string Ticket);

public class ReaderActivityService(ApiClient api, ILibraryService library, IAuthService auth)
{
    public Task<ReaderProfile?> Profile() => api.GetAsync<ReaderProfile>("reader/profile");
    public Task<List<RankedStory>?> Ranking() => api.GetAsync<List<RankedStory>>("reader/ranking");
    public Task<List<RankedStory>?> Recommendations() => api.GetAsync<List<RankedStory>>("reader/recommendations");
    public Task<List<Genre>?> Genres() => api.GetAsync<List<Genre>>("genres");
    public Task SaveGenres(int[] ids) => api.PutAsync<int[], object>("reader/genres", ids);
    public Task<ReadingTicket?> Start(int id) => api.PostAsync<object, ReadingTicket>($"reader/chapters/{id}/start", new {});
    public Task<RewardResult?> Finish(int id, string ticket, decimal percent) =>
        api.PostAsync<object, RewardResult>($"reader/chapters/{id}/finish", new { ticket, percent });
    public Task SetFavorite(int id, bool liked) => liked
        ? api.PutAsync<object, object>($"reader/favorites/{id}", new {})
        : api.DeleteAsync($"reader/favorites/{id}");

    public async Task<IReadOnlyList<LibraryEntry>> Favorites()
    {
        if (auth.CurrentUser is null) return library.GetFavorites();
        // Remove a legacy entry only after the server accepted it or confirmed the story is gone.
        foreach (var item in library.GetFavorites().ToArray())
        {
            try { await SetFavorite(item.StoryId, true); }
            catch (ApiException ex) when (ex.StatusCode == 404) { }
            library.RemoveFavorite(item.StoryId);
        }
        return await api.GetAsync<List<LibraryEntry>>("reader/favorites") ?? new();
    }
}
