<<<<<<< Updated upstream
using Microsoft.Extensions.Logging;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;
using ReadManager.Mobile.ViewModels;
using ReadManager.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;
=======
﻿using Microsoft.Extensions.Logging;
>>>>>>> Stashed changes

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

            // Đăng ký Service kết nối API
            builder.Services.AddSingleton<ReadManager.Mobile.Services.ApiService>();

<<<<<<< Updated upstream
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
            builder.Services.AddTransient<BrowseViewModel>();
            builder.Services.AddTransient<AccountViewModel>();
            builder.Services.AddTransient<ChapterListViewModel>();
            builder.Services.AddTransient<ChapterReaderViewModel>();

            // ---- Pages ----
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<StoryListPage>();
            builder.Services.AddTransient<StoryDetailPage>();
            builder.Services.AddTransient<BrowsePage>();
            builder.Services.AddTransient<AccountPage>();
            builder.Services.AddTransient<ChapterListPage>();
            builder.Services.AddTransient<ChapterReaderPage>();
=======
            // Đăng ký ViewModel và View
            builder.Services.AddTransient<ReadManager.Mobile.ViewModels.HomeViewModel>();
            builder.Services.AddTransient<MainPage>();
>>>>>>> Stashed changes

            return builder.Build();
        }
    }
}