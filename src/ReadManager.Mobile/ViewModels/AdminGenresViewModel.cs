using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Trang quản trị > "Thể loại" (Areas/Admin/Views/Genres/Index.cshtml của web)
public partial class AdminGenresViewModel : AdminViewModelBase
{
    private readonly IAdminService _admin;

    public ObservableCollection<Genre> Genres { get; } = new();

    [ObservableProperty] private string newGenreName = string.Empty;
    [ObservableProperty] private bool showEmpty;

    public AdminGenresViewModel(IAdminService admin, IAuthService auth) : base(auth)
    {
        _admin = admin;
    }

    [RelayCommand]
    private Task LoadAsync() => RunSafeAsync(async () =>
    {
        if (!await EnsureAdminAsync()) return;
        await ReloadAsync();
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        ToastMessage = null;
        var name = NewGenreName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            ErrorMessage = "Vui lòng nhập tên thể loại.";
            return;
        }

        await RunSafeAsync(async () =>
        {
            await _admin.CreateGenreAsync(name);
            NewGenreName = string.Empty;
            ToastMessage = "Đã thêm thể loại.";
            await ReloadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteAsync(Genre? genre)
    {
        if (genre is null) return;
        ToastMessage = null;

        var ok = await Shell.Current.DisplayAlert("Xóa thể loại", "Xóa thể loại này?", "Xóa", "Hủy");
        if (!ok) return;

        await RunSafeAsync(async () =>
        {
            await _admin.DeleteGenreAsync(genre.GenreId);
            ToastMessage = "Đã xóa thể loại.";
            await ReloadAsync();
        });
    }

    private async Task ReloadAsync()
    {
        var list = await _admin.GetGenresAsync();
        Genres.Clear();
        foreach (var g in list) Genres.Add(g);
        ShowEmpty = Genres.Count == 0;
    }
}
