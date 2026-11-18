using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Views;

public class ReaderGenresPage : ContentPage
{
    private readonly ReaderActivityService reader;
    private readonly VerticalStackLayout choices = new() { Spacing = 6 };
    private readonly Label message = new();
    private readonly Dictionary<int, CheckBox> boxes = new();
    private readonly Button save = new() { Text = "Lưu sở thích", IsEnabled = false };
    private readonly Button skip = new() { Text = "Để sau", IsVisible = false };
    public ReaderGenresPage(ReaderActivityService reader)
    {
        this.reader = reader;
        Title = "Sở thích đọc";
        save.Clicked += async (_, _) => await Submit(boxes.Where(x => x.Value.IsChecked).Select(x => x.Key).ToArray());
        skip.Clicked += async (_, _) => await Submit([]);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 12,
            Children = { new Label { Text = "Bạn thích đọc thể loại nào?", FontSize = 22 }, choices, message, save, skip } } };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var profile = await reader.Profile();
            var genres = await reader.Genres();
            choices.Clear();
            boxes.Clear();
            foreach (var genre in genres ?? [])
            {
                var box = new CheckBox { IsChecked = profile?.Genres.Contains(genre.GenreId) == true };
                boxes.Add(genre.GenreId, box);
                choices.Add(new HorizontalStackLayout { Children = { box, new Label { Text = genre.Name, VerticalOptions = LayoutOptions.Center } } });
            }
            save.IsEnabled = true;
            skip.IsVisible = profile?.Prompted == false;
        }
        catch (Exception ex) { message.Text = ex.Message; }
    }
    private async Task Submit(int[] ids)
    {
        save.IsEnabled = skip.IsEnabled = false;
        try { await reader.SaveGenres(ids); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { message.Text = ex.Message; }
        finally { save.IsEnabled = skip.IsEnabled = true; }
    }
}
