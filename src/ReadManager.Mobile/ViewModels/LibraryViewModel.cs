using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tab "Tủ sách": 2 mục — "Đã xem gần đây" (lịch sử) và "Yêu thích".
public partial class LibraryViewModel : BaseViewModel
{
    private readonly ILibraryService _library;
    private readonly IAuthService _auth;

    public ObservableCollection<LibraryEntry> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTab0))]
    [NotifyPropertyChangedFor(nameof(IsTab1))]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    private int selectedTab;

    [ObservableProperty] private bool showEmpty;
    [ObservableProperty] private string emptyTitle = string.Empty;
    [ObservableProperty] private string emptyHint = string.Empty;
    [ObservableProperty] private string countText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClear))]
    private bool hasItems;

    public bool IsTab0 => SelectedTab == 0;
    public bool IsTab1 => SelectedTab == 1;
    public bool ShowClear => IsTab0 && HasItems;

    public LibraryViewModel(ILibraryService library, IAuthService auth)
    {
        _library = library;
        _auth = auth;
    }

    // Gọi mỗi lần tab hiện ra để cập nhật sau khi xem truyện / bấm yêu thích
    [RelayCommand]
    private async Task LoadAsync()
    {
        try { await _auth.IsLoggedInAsync(); } // nạp CurrentUser để chọn đúng tủ sách của tài khoản
        catch { /* mất mạng: dùng tủ sách theo thông tin hiện có */ }
        Show();
    }

    [RelayCommand]
    private void SelectTab(string? index)
    {
        SelectedTab = int.TryParse(index, out var i) ? i : 0;
        Show();
    }

    [RelayCommand]
    private async Task OpenAsync(LibraryEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync(nameof(Views.StoryDetailPage), new Dictionary<string, object>
        {
            { "id", entry.StoryId }
        });
    }

    // "Đọc tiếp" -> mở lại chương đang đọc dở
    [RelayCommand]
    private async Task ContinueReadingAsync(LibraryEntry? entry)
    {
        if (entry is null || entry.LastChapterId <= 0) return;
        await Shell.Current.GoToAsync(nameof(Views.ChapterReaderPage), new Dictionary<string, object>
        {
            { "chapterId", entry.LastChapterId }
        });
    }

    [RelayCommand]
    private void Remove(LibraryEntry? entry)
    {
        if (entry is null) return;
        if (IsTab0) _library.RemoveFromHistory(entry.StoryId);
        else _library.RemoveFavorite(entry.StoryId);
        Show();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var ok = await Shell.Current.DisplayAlert("Xóa lịch sử", "Xóa toàn bộ truyện đã xem gần đây?", "Xóa", "Hủy");
        if (!ok) return;
        _library.ClearHistory();
        Show();
    }

    private void Show()
    {
        var source = IsTab0 ? _library.GetHistory() : _library.GetFavorites();

        Items.Clear();
        foreach (var entry in source) Items.Add(entry);

        HasItems = Items.Count > 0;
        ShowEmpty = !HasItems;
        CountText = HasItems ? $"{Items.Count} truyện" : string.Empty;
        EmptyTitle = IsTab0 ? "Bạn chưa có truyện nào đã xem." : "Bạn chưa lưu truyện nào.";
        EmptyHint = IsTab0
            ? "Mở một truyện bất kỳ, truyện sẽ hiện ở đây."
            : "Bấm “♡ Yêu thích” ở trang chi tiết truyện để lưu vào đây.";
    }
}
