using ReadManager.Mobile.Services;
using ReadManager.Mobile.Views;

namespace ReadManager.Mobile
{
    public partial class AppShell : Shell
    {
        // Mục (tab) đang mở trước đó — dùng để "về trang đầu" của mục khi chuyển sang tab khác
        private ShellSection? _lastSection;

        public AppShell()
        {
            InitializeComponent();

            // Route "LoginPage" đã đăng ký qua ShellContent ở AppShell.xaml.
            // Các route dưới đây không nằm trong flyout, chỉ dùng để điều hướng
            // bằng Shell.Current.GoToAsync(...) (push hoặc "//" thay toàn bộ stack).
            Routing.RegisterRoute(nameof(ReaderGenresPage), typeof(ReaderGenresPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(StoryDetailPage), typeof(StoryDetailPage));
            Routing.RegisterRoute(nameof(ChapterListPage), typeof(ChapterListPage));
            Routing.RegisterRoute(nameof(ChapterReaderPage), typeof(ChapterReaderPage));

            // Sprint 2: gói đọc, mua truyện, đơn hàng, lịch sử thanh toán
            Routing.RegisterRoute(nameof(PlansPage), typeof(PlansPage));
            Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));
            Routing.RegisterRoute(nameof(OrderDetailPage), typeof(OrderDetailPage));
            Routing.RegisterRoute(nameof(OrderHistoryPage), typeof(OrderHistoryPage));

            // Sprint 3: thông báo, theo dõi, bình luận
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
            Routing.RegisterRoute(nameof(FollowedStoriesPage), typeof(FollowedStoriesPage));
            Routing.RegisterRoute(nameof(CommentsPage), typeof(CommentsPage));

            // Sprint 5: chuỗi đọc sách
            Routing.RegisterRoute(nameof(StreakPage), typeof(StreakPage));

            // Giao diện mới: trang tìm kiếm (nút 🔍 trên header) + trang "Đề xuất cho bạn"
            Routing.RegisterRoute(nameof(SearchPage), typeof(SearchPage));
            Routing.RegisterRoute(nameof(RecommendedPage), typeof(RecommendedPage));
            Routing.RegisterRoute(nameof(NewReleasesPage), typeof(NewReleasesPage));

            ApplyTheme();
            ThemeService.ThemeChanged += (_, _) => ApplyTheme();
        }

        // Màu thanh trên (các trang con) và menu dưới theo chế độ sáng/tối
        private void ApplyTheme()
        {
            var header = ThemeService.Get("WebHeader");
            var text = ThemeService.Get("WebText");
            var accent = ThemeService.Get("WebRoyal");

            Shell.SetBackgroundColor(this, header);
            Shell.SetForegroundColor(this, text);
            Shell.SetTitleColor(this, text);
            Shell.SetTabBarBackgroundColor(this, ThemeService.Get("WebTabBar"));
            Shell.SetTabBarForegroundColor(this, accent);
            Shell.SetTabBarTitleColor(this, accent);
            Shell.SetTabBarUnselectedColor(this, ThemeService.Get("WebMuted"));

            UpdateStatusBar();
        }

        // Trang chủ có header tối liền màu với hero "sách bay"; các trang khác theo màu header
        private void UpdateStatusBar()
        {
            if (CurrentPage is StoryListPage) StatusBarHelper.UseHero();
            else StatusBarHelper.UseDefault();
        }

        // Bấm menu dưới để sang mục khác: mục vừa rời đi được đưa về TRANG ĐẦU của nó
        // (vd: đang ở Tài khoản > Gói đọc > Thanh toán, sang Tủ sách rồi quay lại Tài khoản sẽ thấy trang Tài khoản).
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            UpdateStatusBar();

            var current = CurrentItem?.CurrentItem;
            var previous = _lastSection;
            _lastSection = current;

            if (previous is not null && !ReferenceEquals(previous, current) && previous.Navigation.NavigationStack.Count > 1)
            {
                Dispatcher.Dispatch(async () =>
                {
                    try { await previous.Navigation.PopToRootAsync(false); }
                    catch { /* mục đó đã được đóng */ }
                });
            }
        }
    }
}
