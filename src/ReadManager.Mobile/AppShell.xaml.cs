using ReadManager.Mobile.Views;

namespace ReadManager.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Route "LoginPage" đã đăng ký qua ShellContent ở AppShell.xaml.
            // Các route dưới đây không nằm trong flyout, chỉ dùng để điều hướng
            // bằng Shell.Current.GoToAsync(...) (push hoặc "//" thay toàn bộ stack).
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(StoryListPage), typeof(StoryListPage));
            Routing.RegisterRoute(nameof(StoryDetailPage), typeof(StoryDetailPage));
            Routing.RegisterRoute(nameof(ChapterListPage), typeof(ChapterListPage));
            Routing.RegisterRoute(nameof(ChapterReaderPage), typeof(ChapterReaderPage));
        }
    }
}
