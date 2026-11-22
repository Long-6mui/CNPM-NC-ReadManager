using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Views.Controls;

// Hero "sách bay" cho trang chủ (giống hero 3D của web):
//  - 1 cuốn sách bìa cứng LỚN ở giữa (có gáy, ruột giấy, dải đánh dấu trang), tự đổi sang truyện kế tiếp mỗi vài giây.
//  - Vuốt trái -> lật sang cuốn sau, vuốt phải -> quay lại cuốn trước (hướng lật theo hướng vuốt).
//  - 4 bìa truyện vệ tinh bay lơ lửng tự động (sóng sin 3 trục như marvel-3d-engine.js), bấm vào để chuyển sang truyện đó.
public partial class FloatingHeroView : ContentView
{
    private const double StageHeight = 360;
    private const double BookWidth = 188;   // sách to hơn bản cũ (150 x 222)
    private const double BookHeight = 280;
    private const double BookTop = 30;
    private const double AutoSwitchSeconds = 5.5;
    private const int MaxSlides = 5;

    // Cấu hình từng thẻ vệ tinh: vị trí gốc (tỉ lệ), kích thước, xoay gốc, tốc độ & pha sóng
    private sealed record Orbit(double Fx, double Fy, double W, double H, double RotY, double RotZ, double Speed, double Phase);

    private static readonly Orbit[] Orbits =
    {
        new(0.00, 0.06, 74, 108, -22,  7, 1.05, 0.0),
        new(0.80, 0.03, 70, 102,  20, -6, 0.92, 1.7),
        new(-0.02, 0.58, 68, 100, 22,  9, 1.15, 3.3),
        new(0.82, 0.60, 72, 106, -19, -8, 0.98, 4.9),
    };

    // 5 bảng màu bìa chữ (giống art-cover-1..5 của web)
    private static readonly (string A, string B)[] Themes =
    {
        ("#7C3AED", "#312E81"), ("#E11D48", "#7F1D1D"), ("#0EA5E9", "#1E3A8A"),
        ("#10B981", "#064E3B"), ("#F59E0B", "#78350F"),
    };

    // Font chữ có chân của app (resource SerifFont trong WebTheme.xaml là OnPlatform nên phải tra theo khóa)
    private static string? SerifFont
        => Application.Current?.Resources.TryGetValue("SerifFont", out var v) == true ? v as string : null;

    private readonly List<Border> _satellites = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Grid? _book;
    private Border? _bookCover;
    private IReadOnlyList<StorySummary> _stories = Array.Empty<StorySummary>();
    private int _index;
    private bool _switching;
    private double _lastSwitch;
    private double _swayStart;
    private bool _built;
    private IDispatcherTimer? _timer;

    public event EventHandler<StorySummary>? StoryTapped;
    public event EventHandler? BrowseTapped;

    public FloatingHeroView()
    {
        InitializeComponent();
        Stage.SizeChanged += (_, _) => LayoutStage();
    }

    // ===================== DỮ LIỆU =====================
    public void SetStories(IEnumerable<StorySummary> stories)
    {
        var list = stories.Take(MaxSlides).ToList();
        if (_built && list.Select(s => s.StoryId).SequenceEqual(_stories.Select(s => s.StoryId))) return;

        _stories = list;
        _index = 0;
        Build();
        Apply();
    }

    // ===================== DỰNG SÂN KHẤU =====================
    private void Build()
    {
        Stage.Children.Clear();
        _satellites.Clear();

        if (_stories.Count == 0) return;

        // Thẻ vệ tinh (chỉ tạo khi có đủ truyện để hiển thị)
        var satCount = Math.Min(Orbits.Length, Math.Max(0, _stories.Count - 1));
        for (var i = 0; i < satCount; i++)
        {
            var o = Orbits[i];
            var idx = i;
            var card = new Border
            {
                WidthRequest = o.W,
                HeightRequest = o.H,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#3DFFFFFF"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Opacity = 0.93,
                Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(0, 10), Radius = 16, Opacity = 0.6f }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowAsync(SatelliteStoryIndex(idx), +1);
            card.GestureRecognizers.Add(tap);
            AttachSwipe(card);
            _satellites.Add(card);
            Stage.Children.Add(card);
        }

        // Sách chính ở giữa
        _book = BuildBook();
        Stage.Children.Add(_book);

        _built = true;
        _lastSwitch = _clock.Elapsed.TotalSeconds;
        LayoutStage();
    }

    // Cuốn sách bìa cứng: ruột giấy (cạnh phải + cạnh dưới), bìa, gáy, vệt bóng, dải đánh dấu trang
    private Grid BuildBook()
    {
        _bookCover = new Border
        {
            StrokeThickness = 0,
            Margin = new Thickness(0, 0, 9, 7),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4, 12, 12, 4) },
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(0, 22), Radius = 30, Opacity = 0.8f }
        };

        // Ruột giấy: khối màu kem lệch sang phải/xuống dưới + vài đường kẻ trang
        var pages = new Grid
        {
            Margin = new Thickness(10, 6, 0, 0),
            BackgroundColor = Color.FromArgb("#EDE6D3")
        };
        for (var i = 1; i <= 3; i++)
        {
            pages.Children.Add(new BoxView
            {
                Color = Color.FromArgb("#D6CDB6"),
                WidthRequest = 1,
                HorizontalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 8, i * 2.5, 8)
            });
            pages.Children.Add(new BoxView
            {
                Color = Color.FromArgb("#D6CDB6"),
                HeightRequest = 1,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(8, 0, 8, i * 2)
            });
        }

        // Gáy sách: dải tối bên trái + vệt sáng mảnh
        var spine = new BoxView
        {
            WidthRequest = 13,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 0, 0, 7),
            InputTransparent = true,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#66000000"), 0f),
                    new GradientStop(Color.FromArgb("#22FFFFFF"), 0.55f),
                    new GradientStop(Color.FromArgb("#33000000"), 1f)
                },
                new Point(0, 0), new Point(1, 0))
        };

        var sheen = new BoxView
        {
            InputTransparent = true,
            Margin = new Thickness(0, 0, 9, 7),
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#38FFFFFF"), 0f),
                    new GradientStop(Color.FromArgb("#00FFFFFF"), 0.42f),
                    new GradientStop(Color.FromArgb("#26000000"), 1f)
                },
                new Point(0, 0), new Point(1, 1))
        };

        // Dải ruy-băng đánh dấu trang thò ra ở mép trên
        var ribbon = new Border
        {
            WidthRequest = 14,
            HeightRequest = 46,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#E11D48"),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -6, 34, 0),
            InputTransparent = true,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(2, 2, 0, 0) },
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(0, 3), Radius = 6, Opacity = 0.5f }
        };

        var book = new Grid { WidthRequest = BookWidth, HeightRequest = BookHeight, AnchorX = 0.5, AnchorY = 0.5 };
        book.Children.Add(pages);
        book.Children.Add(_bookCover);
        book.Children.Add(spine);
        book.Children.Add(sheen);
        book.Children.Add(ribbon);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { if (_stories.Count > 0) StoryTapped?.Invoke(this, _stories[_index]); };
        book.GestureRecognizers.Add(tap);
        AttachSwipe(book);   // vuốt ngay trên cuốn sách cũng lật được
        return book;
    }

    private void LayoutStage()
    {
        var w = Stage.Width;
        if (w <= 0 || !_built || _book is null) return;

        AbsoluteLayout.SetLayoutBounds(_book, new Rect(w / 2 - BookWidth / 2, BookTop, BookWidth, BookHeight));
        AbsoluteLayout.SetLayoutFlags(_book, AbsoluteLayoutFlags.None);

        for (var i = 0; i < _satellites.Count; i++)
        {
            var o = Orbits[i];
            AbsoluteLayout.SetLayoutBounds(_satellites[i], new Rect(w * o.Fx, StageHeight * o.Fy, o.W, o.H));
            AbsoluteLayout.SetLayoutFlags(_satellites[i], AbsoluteLayoutFlags.None);
        }
    }

    // ===================== NỘI DUNG =====================
    private int SatelliteStoryIndex(int satIdx) => (_index + 1 + satIdx) % _stories.Count;

    private void Apply()
    {
        if (_stories.Count == 0) return;
        var s = _stories[_index];

        if (_bookCover is not null) _bookCover.Content = BuildCover(s, BookWidth);
        for (var i = 0; i < _satellites.Count; i++)
            _satellites[i].Content = BuildCover(_stories[SatelliteStoryIndex(i)], Orbits[i].W);

        var genre = s.GenreNames.FirstOrDefault() ?? s.AccessLabel;
        PillLabel.Text = $"{genre}  •  {(_index == 0 ? "Mới cập nhật" : "Truyện nổi bật")}";
        TitleLabel.Text = s.Title;
        MetaLabel.Text = s.PublishedChapterCount > 0
            ? $"{(string.IsNullOrWhiteSpace(s.AuthorName) ? "Khuyết danh" : s.AuthorName)}  •  {s.PublishedChapterCount} chương"
            : (string.IsNullOrWhiteSpace(s.AuthorName) ? "Khuyết danh" : s.AuthorName);
        BlurbLabel.Text = s.PublishedChapterCount > 0
            ? $"Tác phẩm thuộc thể loại {genre}, hiện đã cập nhật {s.PublishedChapterCount} chương trên thư viện."
            : $"Tác phẩm tuyển chọn thuộc thể loại {genre}, sẵn sàng để bạn khám phá và lưu vào tủ sách.";

        // Chấm chỉ vị trí
        DotsRow.Children.Clear();
        for (var i = 0; i < _stories.Count; i++)
        {
            var idx = i;
            var dot = new Border
            {
                HeightRequest = 6,
                WidthRequest = i == _index ? 22 : 6,
                StrokeThickness = 0,
                BackgroundColor = i == _index ? Color.FromArgb("#E11D48") : Color.FromArgb("#55FFFFFF"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(3) }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowAsync(idx, idx > _index ? +1 : -1);
            dot.GestureRecognizers.Add(tap);
            DotsRow.Children.Add(dot);
        }
    }

    // Bìa: ảnh bìa nếu có, không thì bìa chữ trên nền gradient (giống cd-typo-cover của web)
    private static View BuildCover(StorySummary s, double width)
    {
        var theme = Themes[Math.Abs(s.StoryId) % Themes.Length];
        var grid = new Grid
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(theme.A), 0f),
                    new GradientStop(Color.FromArgb(theme.B), 1f)
                },
                new Point(0, 0), new Point(1, 1))
        };

        var big = width >= 120;
        if (s.HasCover)
        {
            grid.Children.Add(new Image { Source = s.CoverUrl, Aspect = Aspect.AspectFill });
        }
        else
        {
            var box = new VerticalStackLayout { Padding = new Thickness(big ? 14 : 8), VerticalOptions = LayoutOptions.Center, Spacing = 6 };
            box.Children.Add(new Label
            {
                Text = (s.GenreNames.FirstOrDefault() ?? s.AccessLabel).ToUpperInvariant(),
                TextColor = Color.FromArgb("#CCFFFFFF"),
                FontSize = big ? 9.5 : 7.5,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center
            });
            box.Children.Add(new Label
            {
                Text = s.Title,
                TextColor = Colors.White,
                FontSize = big ? 21 : 11.5,
                FontAttributes = FontAttributes.Bold,
                FontFamily = SerifFont,
                HorizontalTextAlignment = TextAlignment.Center,
                MaxLines = 4,
                LineBreakMode = LineBreakMode.TailTruncation
            });
            box.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(s.AuthorName) ? "Khuyết danh" : s.AuthorName,
                TextColor = Color.FromArgb("#B3FFFFFF"),
                FontSize = big ? 10 : 8,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            });
            grid.Children.Add(box);
        }

        // Dải tên truyện mờ ở đáy thẻ vệ tinh (khi có ảnh bìa)
        if (!big && s.HasCover)
        {
            grid.Children.Add(new Label
            {
                Text = s.Title,
                TextColor = Colors.White,
                FontSize = 9.5,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#99000000"),
                Padding = new Thickness(6, 4),
                VerticalOptions = LayoutOptions.End,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }
        return grid;
    }

    // ===================== CHUYỂN SÁCH =====================
    // dir = +1: sang cuốn sau (sách lật về bên trái); dir = -1: về cuốn trước (lật về bên phải)
    private async Task ShowAsync(int newIndex, int dir)
    {
        if (_switching || _book is null || _stories.Count < 2 || newIndex == _index) return;
        _switching = true;
        try
        {
            var outAngle = dir > 0 ? -90 : 90;
            var shift = dir > 0 ? -36 : 36;
            await Task.WhenAll(
                _book.RotateYTo(outAngle, 210, Easing.CubicIn),
                _book.TranslateTo(shift, _book.TranslationY, 210, Easing.CubicIn),
                _book.ScaleTo(0.92, 210, Easing.CubicIn),
                TextPanel.FadeTo(0, 170));

            _index = newIndex;
            Apply();

            _book.RotationY = -outAngle;
            _book.TranslationX = -shift;
            await Task.WhenAll(
                _book.RotateYTo(0, 320, Easing.CubicOut),
                _book.TranslateTo(0, _book.TranslationY, 320, Easing.CubicOut),
                _book.ScaleTo(1, 380, Easing.SpringOut),
                TextPanel.FadeTo(1, 240));
        }
        catch { /* trang đang đóng giữa chừng */ }
        finally
        {
            _switching = false;
            _lastSwitch = _clock.Elapsed.TotalSeconds;
            _swayStart = _lastSwitch;
            TextPanel.Opacity = 1;
            if (_book is not null) { _book.TranslationX = 0; _book.Scale = 1; }
        }
    }

    // Gắn cử chỉ vuốt trái/phải cho một view trên sân khấu (sách chính, thẻ vệ tinh).
    // Cần gắn riêng vì view con có TapGesture sẽ "giữ" lượt chạm, sân khấu phía sau không nhận được.
    private void AttachSwipe(View view)
    {
        var left = new SwipeGestureRecognizer { Direction = SwipeDirection.Left, Threshold = 40 };
        left.Swiped += OnSwiped;
        var right = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = 40 };
        right.Swiped += OnSwiped;
        view.GestureRecognizers.Add(left);
        view.GestureRecognizers.Add(right);
    }

    // Vuốt trên sân khấu: trái = cuốn sau, phải = cuốn trước
    private async void OnSwiped(object? sender, SwipedEventArgs e)
    {
        if (_stories.Count < 2) return;
        if (SwipeHint.Opacity > 0) _ = SwipeHint.FadeTo(0, 400);

        if (e.Direction == SwipeDirection.Left)
            await ShowAsync((_index + 1) % _stories.Count, +1);
        else if (e.Direction == SwipeDirection.Right)
            await ShowAsync((_index - 1 + _stories.Count) % _stories.Count, -1);
    }

    // ===================== HOẠT ẢNH =====================
    public void Start()
    {
        if (_timer is not null) return;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);   // ~30 khung hình/giây, đủ mượt mà nhẹ máy
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Tick()
    {
        if (!_built || Stage.Width <= 0) return;
        var t = _clock.Elapsed.TotalSeconds;

        // Hào quang trôi nhẹ
        Nebula.TranslationX = Math.Sin(t * 0.4) * 14;
        Nebula.TranslationY = Math.Cos(t * 0.33) * 10;
        Nebula.Scale = 1 + Math.Sin(t * 0.6) * 0.06;
        Nebula2.TranslationX = Math.Cos(t * 0.35) * 12;
        Nebula2.Scale = 1 + Math.Cos(t * 0.5) * 0.05;

        // Thẻ vệ tinh bay lơ lửng: sóng sin 3 trục (trôi X/Y + xoay + nhấp nhô kích thước thay cho trục Z)
        for (var i = 0; i < _satellites.Count; i++)
        {
            var o = Orbits[i];
            var w = t * o.Speed + o.Phase;
            var card = _satellites[i];
            card.TranslationX = Math.Cos(w * 0.85) * 11 + Math.Sin(w * 0.42) * 5;
            card.TranslationY = Math.Sin(w * 1.15) * 15 + Math.Cos(w * 0.55) * 5;
            card.Rotation = o.RotZ + Math.Sin(w * 0.7) * 4;
            card.RotationY = o.RotY + Math.Cos(w * 0.8) * 9;
            card.RotationX = Math.Sin(w * 0.95) * 5;
            card.Scale = 1 + Math.Sin(w * 0.8) * 0.06;
        }

        if (_book is null) return;

        // Sách chính nhấp nhô + lắc nhẹ (không can thiệp khi đang lật)
        if (!_switching)
        {
            var ramp = Math.Clamp((t - _swayStart) / 1.0, 0, 1);
            _book.TranslationY = Math.Sin(t * 1.6) * 7;
            _book.RotationY = Math.Sin(t * 0.9) * 9 * ramp;
            _book.Rotation = Math.Sin(t * 0.7) * 1.6;

            // Tự đổi truyện
            if (_stories.Count > 1 && t - _lastSwitch > AutoSwitchSeconds)
                _ = ShowAsync((_index + 1) % _stories.Count, +1);
        }
    }

    private void OnReadClicked(object? sender, EventArgs e)
    {
        if (_stories.Count > 0) StoryTapped?.Invoke(this, _stories[_index]);
    }

    private void OnBrowseClicked(object? sender, EventArgs e) => BrowseTapped?.Invoke(this, EventArgs.Empty);
}
