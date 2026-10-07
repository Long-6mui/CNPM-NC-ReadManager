using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Trang quản trị > "Truyện và chương" (Areas/Admin/Views/Stories/Index.cshtml của web)
public partial class AdminStoriesViewModel : AdminViewModelBase
{
    private readonly IAdminService _admin;

    public ObservableCollection<StorySummary> Stories { get; } = new();

    [ObservableProperty] private string countText = string.Empty;
    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private bool isRefreshing;

    public AdminStoriesViewModel(IAdminService admin, IAuthService auth) : base(auth)
    {
        _admin = admin;
    }

    [RelayCommand]
    private Task LoadAsync() => RunSafeAsync(ReloadCoreAsync);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await RunSafeAsync(ReloadCoreAsync);
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task AddAsync()
        => await Shell.Current.GoToAsync(nameof(Views.AdminStoryEditPage));

    [RelayCommand]
    private async Task EditAsync(StorySummary? story)
    {
        if (story is null) return;
        await Shell.Current.GoToAsync(nameof(Views.AdminStoryEditPage), new Dictionary<string, object>
        {
            { "id", story.StoryId }
        });
    }

    // Web hỏi xác nhận rồi báo API chưa có chức năng xóa truyện — app làm đúng như vậy
    [RelayCommand]
    private async Task DeleteAsync(StorySummary? story)
    {
        if (story is null) return;
        var ok = await Shell.Current.DisplayAlert("Xóa truyện", "Xóa hẳn truyện này cùng toàn bộ chương?", "Xóa", "Hủy");
        if (!ok) return;
        await Shell.Current.DisplayAlert("Xóa truyện", "API chưa có chức năng xóa truyện (cần Backend 3 bổ sung).", "Đóng");
    }

    private async Task ReloadCoreAsync()
    {
        if (!await EnsureAdminAsync()) return;

        var list = await _admin.GetAllStoriesAsync();
        Stories.Clear();
        foreach (var s in list) Stories.Add(s);

        CountText = $"{Stories.Count} truyện";
        ShowEmpty = Stories.Count == 0;
    }
}
