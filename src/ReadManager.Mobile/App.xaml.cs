using ReadManager.Mobile.Services;

namespace ReadManager.Mobile
{
    public partial class App : Application
    {
        private readonly NewChapterWatcher _watcher;

        public App(NewChapterWatcher watcher)
        {
            InitializeComponent();
            _watcher = watcher;

            // Chế độ sáng/tối: đọc lựa chọn đã lưu (mặc định sáng như web) và nạp bảng màu tương ứng.
            // Đổi bằng nút 🌙/☀️ trên thanh header.
            ThemeService.Init(this);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Báo chương mới cho truyện đang theo dõi: hỏi định kỳ; biết app đang mở hay chạy nền
            window.Created += (_, _) => _watcher.Start(Dispatcher);
            window.Stopped += (_, _) => NewChapterWatcher.InForeground = false;
            window.Resumed += (_, _) =>
            {
                NewChapterWatcher.InForeground = true;
                _ = _watcher.CheckAsync();
            };
            return window;
        }
    }
}
