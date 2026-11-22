using ReadManager.Mobile.Services;

namespace ReadManager.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Chế độ sáng/tối: đọc lựa chọn đã lưu (mặc định sáng như web) và nạp bảng màu tương ứng.
            // Đổi bằng nút 🌙/☀️ trên thanh header.
            ThemeService.Init(this);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
