using Microsoft.Extensions.Logging;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;
using ReadManager.Mobile.ViewModels;
using ReadManager.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ReadManager.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ---- Services ----
            builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();

            // ApiClient dùng HttpClient factory để tránh lỗi socket exhaustion khi tạo HttpClient thủ công
            builder.Services.AddHttpClient<ApiClient>(client =>
            {
                client.BaseAddress = new Uri(ApiSettings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            });

            builder.Services.AddSingleton<IAuthService, AuthService>(); // singleton để giữ CurrentUser xuyên suốt phiên
            builder.Services.AddTransient<IStoryService, StoryService>();
            builder.Services.AddTransient<IChapterService, ChapterService>();
            builder.Services.AddSingleton<ILibraryService, LibraryService>(); // Tủ sách lưu trên máy

            // Sprint 2: gói đọc + đơn hàng + thanh toán
            builder.Services.AddTransient<IPlanService, PlanService>();
            builder.Services.AddTransient<IOrderService, OrderService>();
            // Thanh toán (Backend 4): xem ApiSettings.UsePaymentApi
            if (ApiSettings.UsePaymentApi)
                builder.Services.AddTransient<IPaymentService, ApiPaymentService>();
            else
                builder.Services.AddSingleton<IPaymentService, PendingPaymentService>();

            // Quyền đọc / thư viện đã mua (Backend 2): xem ApiSettings.UseReadingAccessApi
            builder.Services.AddTransient<OrderBasedReadingAccessService>();
            if (ApiSettings.UseReadingAccessApi)
                builder.Services.AddTransient<IReadingAccessService, ApiReadingAccessService>();
            else
                builder.Services.AddTransient<IReadingAccessService>(sp => sp.GetRequiredService<OrderBasedReadingAccessService>());

            // ---- ViewModels ----
            // Sprint 3: cộng đồng (Backend 3) + tiến độ đọc (Backend 2, chưa có -> dữ liệu trên máy)
            builder.Services.AddTransient<ICommunityService, CommunityService>();
            builder.Services.AddTransient<ReportFlow>();
            builder.Services.AddTransient<LocalReadingProgressService>();
            if (ApiSettings.UseReadingProgressApi)
                builder.Services.AddTransient<IReadingProgressService, ApiReadingProgressService>();
            else
                builder.Services.AddTransient<IReadingProgressService>(sp => sp.GetRequiredService<LocalReadingProgressService>());

            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<StoryListViewModel>();
            builder.Services.AddTransient<StoryDetailViewModel>();
            builder.Services.AddTransient<BrowseViewModel>();
            builder.Services.AddTransient<AccountViewModel>();
            builder.Services.AddTransient<ChapterListViewModel>();
            builder.Services.AddTransient<ChapterReaderViewModel>();
            builder.Services.AddTransient<LibraryViewModel>();
            builder.Services.AddTransient<PlansViewModel>();
            builder.Services.AddTransient<CheckoutViewModel>();
            builder.Services.AddTransient<OrderDetailViewModel>();
            builder.Services.AddTransient<OrderHistoryViewModel>();
            builder.Services.AddTransient<NotificationsViewModel>();
            builder.Services.AddTransient<FollowedStoriesViewModel>();
            builder.Services.AddTransient<CommentsViewModel>();

            // ---- Pages ----
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<StoryListPage>();
            builder.Services.AddTransient<StoryDetailPage>();
            builder.Services.AddTransient<BrowsePage>();
            builder.Services.AddTransient<AccountPage>();
            builder.Services.AddTransient<ChapterListPage>();
            builder.Services.AddTransient<ChapterReaderPage>();
            builder.Services.AddTransient<LibraryPage>();
            builder.Services.AddTransient<PlansPage>();
            builder.Services.AddTransient<CheckoutPage>();
            builder.Services.AddTransient<OrderDetailPage>();
            builder.Services.AddTransient<OrderHistoryPage>();
            builder.Services.AddTransient<NotificationsPage>();
            builder.Services.AddTransient<FollowedStoriesPage>();
            builder.Services.AddTransient<CommentsPage>();

            return builder.Build();
        }
    }
}
