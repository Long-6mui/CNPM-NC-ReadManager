using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ReadManager.Api.Data;
using ReadManager.Api.Entities;

internal static class ReaderActivityChecks
{
    public static async Task<string> RunAsync(Uri baseAddress, string alice, string bob, AppDbContext db, Story story, Chapter chapter)
    {
        using var member = new HttpClient { BaseAddress = baseAddress };
        using var other = new HttpClient { BaseAddress = baseAddress };
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice);
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob);
        static void Require(bool result, string description)
        {
            if (!result) throw new Exception(description);
            Console.WriteLine("PASS: " + description);
        }

        var heart = $"api/reader/favorites/{story.StoryId}";
        (await member.PutAsync(heart, null)).EnsureSuccessStatusCode();
        (await member.PutAsync(heart, null)).EnsureSuccessStatusCode();
        (await other.PutAsync(heart, null)).EnsureSuccessStatusCode();
        async Task<int> Hearts() => (await member.GetFromJsonAsync<JsonElement>("api/reader/ranking"))[0].GetProperty("hearts").GetInt32();
        Require(await Hearts() == 2, "Ranking counts distinct accounts");
        (await other.DeleteAsync(heart)).EnsureSuccessStatusCode();
        Require(await Hearts() == 1, "Removing a heart updates ranking");

        var genre = new Genre { Name = "Mystery", Slug = "mystery" };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        db.StoryGenres.Add(new StoryGenre { StoryId = story.StoryId, GenreId = genre.GenreId });
        await db.SaveChangesAsync();
        (await member.PutAsJsonAsync("api/reader/genres", new[] { genre.GenreId })).EnsureSuccessStatusCode();
        var recs = await member.GetFromJsonAsync<JsonElement>("api/reader/recommendations");
        Require(recs.GetArrayLength() == 1 && recs[0].GetProperty("storyId").GetInt32() == story.StoryId, "Recommendations use selected genre");
        (await other.PutAsJsonAsync("api/reader/genres", Array.Empty<int>())).EnsureSuccessStatusCode();
        var skipped = await other.GetFromJsonAsync<JsonElement>("api/reader/profile");
        Require(skipped.GetProperty("prompted").GetBoolean() && skipped.GetProperty("genres").GetArrayLength() == 0, "Skipping is remembered separately per account");

        var path = $"api/reader/chapters/{chapter.ChapterId}";
        Require((await other.PostAsync(path + "/start", null)).StatusCode == HttpStatusCode.Forbidden, "Locked chapter cannot earn coins");
        var session = await member.PostAsync(path + "/start", null);
        session.EnsureSuccessStatusCode();
        var ticket = (await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString();
        var claim = new { ticket, percent = 100 };
        Require((await member.PostAsJsonAsync(path + "/finish", claim)).StatusCode == HttpStatusCode.BadRequest, "Early reward rejected");
        await Task.Delay(TimeSpan.FromSeconds(61));
        Require((await member.PostAsJsonAsync(path + "/finish", new { ticket, percent = 50 })).StatusCode == HttpStatusCode.BadRequest, "Partial chapter rejected");
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            var response = await member.PostAsJsonAsync(path + "/finish", claim);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("granted").GetBoolean();
        }));
        Require(outcomes.Count(granted => granted) == 1, "Only one concurrent claim displays the reward");
        var profile = await member.GetFromJsonAsync<JsonElement>("api/reader/profile");
        Require(profile.GetProperty("coins").GetInt32() == 10, "Concurrent claims earn exactly 10 coins");
        return ticket!;
    }
}
