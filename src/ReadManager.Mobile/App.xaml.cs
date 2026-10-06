namespace ReadManager.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Light; // web dùng nền sáng; tránh chữ trắng trên nền trắng khi máy bật chế độ tối
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}