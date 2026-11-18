using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Views;

public class HeartRankingPage : ContentPage
{
    private readonly ReaderActivityService reader;
    private readonly VerticalStackLayout rows = new() { Padding = 16, Spacing = 8 };
    public HeartRankingPage(ReaderActivityService reader)
    {
        this.reader = reader;
        Title = "Truyện được yêu thích";
        Content = new ScrollView { Content = rows };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        rows.Clear();
        try
        {
            var stories = await reader.Ranking();
            var rank = 0;
            foreach (var story in stories ?? [])
                rows.Add(StoryButton(story, $"{++rank}. {story.Title}   ♥ {story.Hearts}"));
            if (rank == 0) rows.Add(new Label { Text = "Chưa có truyện được thả tim." });
        }
        catch (Exception ex) { rows.Add(new Label { Text = ex.Message }); }
    }
    internal static Button StoryButton(RankedStory story, string text)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(StoryDetailPage),
            new Dictionary<string, object> { ["id"] = story.StoryId });
        return button;
    }
}
