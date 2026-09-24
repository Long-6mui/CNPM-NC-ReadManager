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
            });

            builder.Services.AddSingleton<IAuthService, AuthService>(); // singleton để giữ CurrentUser xuyên suốt phiên
            builder.Services.AddTransient<IStoryService, StoryService>();
            builder.Services.AddTransient<IChapterService, ChapterService>();

            // ---- ViewModels ----
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<StoryListViewModel>();
            builder.Services.AddTransient<StoryDetailViewModel>();
            builder.Services.AddTransient<ChapterListViewModel>();
            builder.Services.AddTransient<ChapterReaderViewModel>();

            // ---- Pages ----
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<StoryListPage>();
            builder.Services.AddTransient<StoryDetailPage>();
            builder.Services.AddTransient<ChapterListPage>();
            builder.Services.AddTransient<ChapterReaderPage>();

            return builder.Build();
        }
    }
}
