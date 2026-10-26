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

        }

        // Bấm menu dưới để sang mục khác: mục vừa rời đi được đưa về TRANG ĐẦU của nó
        // (vd: đang ở Tài khoản > Gói đọc > Thanh toán, sang Tủ sách rồi quay lại Tài khoản sẽ thấy trang Tài khoản).
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);

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
