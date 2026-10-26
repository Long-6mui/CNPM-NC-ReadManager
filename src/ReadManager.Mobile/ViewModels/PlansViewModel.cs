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
        catch { /* bỏ qua */ }
        HasMyPlans = MyPlans.Count > 0;
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
