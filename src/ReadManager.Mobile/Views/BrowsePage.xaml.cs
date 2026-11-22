using Microsoft.Maui.Controls.Shapes;
using ReadManager.Mobile.Behaviors;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

// Tab "Khám phá": chuỗi đọc theo ngày + xu (xem ExploreViewModel)
public partial class BrowsePage : ContentPage
{
    private readonly ExploreViewModel _viewModel;
    private bool _firstAppear = true;

    public BrowsePage(ExploreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.Days.CollectionChanged += (_, _) => Dispatcher.Dispatch(BuildDays);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = Header.RefreshAsync();
        if (_firstAppear)
        {
            _firstAppear = false;
            _ = Motion.PageEnterAsync(Body);
        }
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _ = PulseFlameAsync();
    }

    // 7 ô ngày chia đều chiều ngang thẻ (giống mục "7 ngày gần nhất" ở trang Chuỗi)
    private void BuildDays()
    {
        DaysRow.Children.Clear();
        var days = _viewModel.Days.ToList();
        for (var i = 0; i < days.Count && i < 7; i++)
        {
            var cell = DayCell(days[i]);
            Grid.SetColumn(cell, i);
            DaysRow.Children.Add(cell);
            _ = Motion.RevealAsync(cell, i * 40);
        }
    }

    private static Border DayCell(StreakDay day)
    {
        var cell = new Border
        {
            Padding = new Thickness(0, 8),
            StrokeThickness = day.IsToday ? 1.5 : 0,
            Stroke = day.IsToday ? Color.FromArgb("#FB7185") : Colors.Transparent,
            BackgroundColor = day.Read ? Color.FromArgb("#40F97316") : Color.FromArgb("#14FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) }
        };
        cell.Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = day.Label,
                    FontSize = 10.5,
                    TextColor = Color.FromArgb("#B7C0D6"),
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = day.Read ? "🔥" : (day.IsToday ? "•" : "○"),
                    FontSize = 17,
                    TextColor = Color.FromArgb("#8CFFFFFF"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
        return cell;
    }

    // Ngọn lửa "phập phồng" nhẹ một nhịp khi vào trang
    private async Task PulseFlameAsync()
    {
        try
        {
            await Flame.ScaleTo(1.25, 220, Easing.CubicOut);
            await Flame.ScaleTo(1, 420, Easing.SpringOut);
        }
        catch { Flame.Scale = 1; }
    }
}
