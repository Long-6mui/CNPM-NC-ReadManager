using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Màn "Gói đọc" (theo Views/Plans/PlansIndex.cshtml của web): danh sách gói đang bán + nút Mua / gia hạn.
public partial class PlansViewModel : BaseViewModel
{
    private readonly IPlanService _plans;
    private readonly IReadingAccessService _access;
    private readonly IAuthService _auth;

    public ObservableCollection<ReadingPlan> Plans { get; } = new();
    public ObservableCollection<PlanEntitlement> MyPlans { get; } = new();

    [ObservableProperty] private bool hasMyPlans;
    [ObservableProperty] private bool hasAlert;
    [ObservableProperty] private string alertMessage = string.Empty;
    [ObservableProperty] private bool showEmpty;

    public PlansViewModel(IPlanService plans, IReadingAccessService access, IAuthService auth)
    {
        _plans = plans;
        _access = access;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            var list = await _plans.GetPlansAsync();
            Plans.Clear();
            foreach (var p in list) Plans.Add(p);
            ShowEmpty = Plans.Count == 0;
        });

        // Gói đang dùng (chỉ khi đã đăng nhập). Lỗi ở đây không làm hỏng danh sách gói.
        MyPlans.Clear();
        try
        {
            if (await _auth.IsLoggedInAsync())
            {
                var purchases = await _access.GetPurchasesAsync();
                foreach (var p in purchases.Plans) MyPlans.Add(p);
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { ErrorMessage = "Chưa tải được gói đang dùng. Vui lòng tải lại hoặc đăng nhập lại."; }
        HasMyPlans = MyPlans.Count > 0;

        // Gói sắp hết hạn (<= 7 ngày): nhắc gia hạn (gia hạn khi còn hạn được cộng dồn)
        var soon = MyPlans.Where(p => p.IsExpiringSoon).OrderBy(p => p.ExpiresAt).FirstOrDefault();
        HasAlert = soon is not null;
        AlertMessage = soon is null
            ? string.Empty
            : $"Gói “{soon.PlanName}” sắp hết hạn (còn {soon.DaysLeft} ngày). Bấm “Mua / gia hạn” bên dưới để cộng dồn thời gian.";
    }

    [RelayCommand]
    private async Task BuyAsync(ReadingPlan? plan)
    {
        if (plan is null || !plan.CanBuy) return;

        await Shell.Current.GoToAsync(nameof(Views.CheckoutPage), new Dictionary<string, object>
        {
            { "planId", plan.PlanId }
        });
    }

    [RelayCommand]
    private async Task OpenHistoryAsync() => await Shell.Current.GoToAsync(nameof(Views.OrderHistoryPage));
}
