using Microsoft.Maui.Controls.Shapes;
using ReadManager.Mobile.Behaviors;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

// Tab "Khám phá": điểm danh 7 ngày, chuỗi đọc, nhiệm vụ hằng ngày, shop đổi voucher (xem ExploreViewModel)
public partial class BrowsePage : ContentPage
{
    private readonly ExploreViewModel _viewModel;
    private bool _firstAppear = true;

    public BrowsePage(ExploreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.Days.CollectionChanged += (_, _) => Dispatcher.Dispatch(BuildStreakDays);
        _viewModel.CheckInDays.CollectionChanged += (_, _) => Dispatcher.Dispatch(BuildCheckInDays);
        _viewModel.Celebrate += (_, text) => Dispatcher.Dispatch(() => _ = ShowToastAsync(text));
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
        _ = PulseAsync(Flame);
    }

    // ===================== 7 Ô ĐIỂM DANH (T2..CN) =====================
    private void BuildCheckInDays()
    {
        CheckInRow.Children.Clear();
        var days = _viewModel.CheckInDays.ToList();
        for (var i = 0; i < days.Count && i < 7; i++)
        {
            var cell = CheckInCell(days[i]);
            Grid.SetColumn(cell, i);
            CheckInRow.Children.Add(cell);
            _ = Motion.RevealAsync(cell, i * 35);
        }
    }

    private View CheckInCell(CheckInDay day)
    {
        var box = new Border
        {
            HeightRequest = 74,
            Padding = new Thickness(0, 6),
            StrokeThickness = day.IsAvailable ? 2 : 0,
            Stroke = day.CellStroke,
            BackgroundColor = day.CellColor,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = day.Icon,
                        FontSize = day.IsAvailable ? 20 : 24,
                        Opacity = day.IconOpacity,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = day.IsClaimed ? "✓" : $"+{day.Amount}",
                        FontSize = 12,
                        FontFamily = "BeVietnamBold",
                        TextColor = day.IsAvailable ? Color.FromArgb("#D97706") : day.LabelColor,
                        IsVisible = day.IsAvailable || day.IsClaimed || day.Amount > 10,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        var label = new Label
        {
            Text = day.Label,
            FontSize = 11,
            FontFamily = day.IsToday ? "BeVietnamBold" : "BeVietnamMedium",
            TextColor = day.LabelColor,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var cell = new VerticalStackLayout { Spacing = 6, Children = { box, label } };

        // Ô hôm nay chưa nhận: bấm để điểm danh + nhún nhẹ liên tục để gợi ý bấm
        if (day.IsAvailable)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await Motion.PopAsync(box);
                if (_viewModel.CheckInCommand.CanExecute(null)) await _viewModel.CheckInCommand.ExecuteAsync(null);
            };
            cell.GestureRecognizers.Add(tap);
            _ = WiggleAsync(box);
        }
        return cell;
    }

    private static async Task WiggleAsync(VisualElement view)
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                await Task.Delay(900);
                await view.ScaleTo(1.08, 160, Easing.CubicOut);
                await view.ScaleTo(1, 260, Easing.SpringOut);
            }
        }
        catch { view.Scale = 1; }
    }

    // ===================== CHUỖI 7 NGÀY =====================
    private void BuildStreakDays()
    {
        DaysRow.Children.Clear();
        var days = _viewModel.Days.ToList();
        for (var i = 0; i < days.Count && i < 7; i++)
        {
            var cell = StreakCell(days[i]);
            Grid.SetColumn(cell, i);
            DaysRow.Children.Add(cell);
            _ = Motion.RevealAsync(cell, i * 40);
        }
    }

    private static Border StreakCell(StreakDay day) => new()
    {
        Padding = new Thickness(0, 7),
        StrokeThickness = day.IsToday ? 1.5 : 0,
        Stroke = day.IsToday ? Color.FromArgb("#FB7185") : Colors.Transparent,
        BackgroundColor = day.Read ? Color.FromArgb("#40F97316") : Color.FromArgb("#14FFFFFF"),
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
        Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = day.Label, FontSize = 10.5, TextColor = Color.FromArgb("#B7C0D6"), HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = day.Read ? "🔥" : (day.IsToday ? "•" : "○"), FontSize = 16,
                            TextColor = Color.FromArgb("#8CFFFFFF"), HorizontalTextAlignment = TextAlignment.Center }
            }
        }
    };

    // ===================== HIỆU ỨNG =====================
    // Thông báo "+10 xu" trượt xuống, nảy nhẹ rồi mờ đi
    private async Task ShowToastAsync(string text)
    {
        ToastLabel.Text = text;
        Toast.Opacity = 0;
        Toast.TranslationY = -20;
        Toast.Scale = 0.9;
        Toast.IsVisible = true;
        await Task.WhenAll(
            Toast.FadeTo(1, 220),
            Toast.TranslateTo(0, 0, 320, Easing.CubicOut),
            Toast.ScaleTo(1, 380, Easing.SpringOut));
        await Task.Delay(1800);
        if (ToastLabel.Text != text) return;
        await Task.WhenAll(Toast.FadeTo(0, 300), Toast.TranslateTo(0, -16, 300));
        if (ToastLabel.Text == text) Toast.IsVisible = false;
    }

    private static async Task PulseAsync(VisualElement view)
    {
        try
        {
            await view.ScaleTo(1.25, 220, Easing.CubicOut);
            await view.ScaleTo(1, 420, Easing.SpringOut);
        }
        catch { view.Scale = 1; }
    }
}
