using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using ReadManager.Mobile.Models;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.Services.Auth;

namespace ReadManager.Mobile.ViewModels;

// Tải nhiều chương: chọn file .txt/.zip hoặc dán văn bản -> "Kiểm tra" (xem trước) -> "Lưu"
// (Areas/Admin/Views/Stories/BulkImport.cshtml của web)
[QueryProperty(nameof(StoryId), "storyId")]
[QueryProperty(nameof(StoryTitle), "storyTitle")]
public partial class AdminBulkImportViewModel : AdminViewModelBase
{
    private readonly IAdminService _admin;
    private List<FileResult> _files = new();

    public ObservableCollection<string> Errors { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<ChapterPreviewRow> Preview { get; } = new();

    [ObservableProperty] private int storyId;
    [ObservableProperty] private string storyTitle = string.Empty;

    [ObservableProperty] private string rawText = string.Empty;
    [ObservableProperty] private bool markFree;
    [ObservableProperty] private bool publish = true;       // true = công khai ngay, false = lưu nháp
    [ObservableProperty] private bool overwriteExisting;    // true = ghi đè chương trùng số

    [ObservableProperty] private string fileNamesText = "Chưa chọn file nào.";
    [ObservableProperty] private bool hasFiles;

    [ObservableProperty] private bool canSave;              // chỉ bật sau khi "Kiểm tra" không còn lỗi
    [ObservableProperty] private bool hasErrors;
    [ObservableProperty] private bool hasWarnings;
    [ObservableProperty] private bool hasPreview;
    [ObservableProperty] private string resultMessage = string.Empty;

    public AdminBulkImportViewModel(IAdminService admin, IAuthService auth) : base(auth)
    {
        _admin = admin;
    }

    // Đổi dữ liệu nhập sau khi kiểm tra -> phải kiểm tra lại mới được lưu
    partial void OnRawTextChanged(string value) => CanSave = false;
    partial void OnMarkFreeChanged(bool value) => CanSave = false;
    partial void OnPublishChanged(bool value) => CanSave = false;
    partial void OnOverwriteExistingChanged(bool value) => CanSave = false;

    [RelayCommand]
    private Task LoadAsync() => RunSafeAsync(async () => { await EnsureAdminAsync(); });

    [RelayCommand]
    private async Task PickFilesAsync()
    {
        try
        {
            var picked = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn file .txt hoặc .zip"
            });
            var list = picked?.ToList();
            if (list is null || list.Count == 0) return;

            _files = list;
            FileNamesText = string.Join(", ", _files.Select(f => f.FileName));
            HasFiles = true;
            CanSave = false;
        }
        catch (Exception)
        {
            ErrorMessage = "Không mở được bộ chọn file.";
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        _files = new List<FileResult>();
        FileNamesText = "Chưa chọn file nào.";
        HasFiles = false;
        CanSave = false;
    }

    [RelayCommand]
    private Task CheckAsync() => SendAsync(save: false);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave) return;
        await SendAsync(save: true);
    }

    private async Task SendAsync(bool save)
    {
        if (string.IsNullOrWhiteSpace(RawText) && _files.Count == 0)
        {
            ErrorMessage = "Hãy chọn file hoặc dán văn bản chương.";
            return;
        }

        await RunSafeAsync(async () =>
        {
            // Gói thành form giống web: Files, Text, AccessLevel, PublicationStatus, OverwriteExisting, Save
            using var form = new MultipartFormDataContent();
            foreach (var file in _files)
            {
                var part = new StreamContent(await file.OpenReadAsync());
                part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(part, "Files", file.FileName);
            }
            if (!string.IsNullOrWhiteSpace(RawText)) form.Add(new StringContent(RawText), "Text");
            form.Add(new StringContent(MarkFree ? "Free" : "Paid"), "AccessLevel");
            form.Add(new StringContent(Publish ? "Published" : "Draft"), "PublicationStatus");
            form.Add(new StringContent(OverwriteExisting ? "true" : "false"), "OverwriteExisting");
            form.Add(new StringContent(save ? "true" : "false"), "Save");

            var result = await _admin.UploadChapterFilesAsync(StoryId, form);
            Show(result);

            if (result.Saved)
            {
                await Shell.Current.DisplayAlert("Tải nhiều chương", result.Message, "Đóng");
                await Shell.Current.GoToAsync(".."); // về trang sửa truyện, danh sách chương tự nạp lại
            }
        });
    }

    private void Show(UploadFilesResult result)
    {
        Errors.Clear();
        Warnings.Clear();
        Preview.Clear();
        foreach (var e in result.Errors) Errors.Add(e);
        foreach (var w in result.Warnings) Warnings.Add(w);
        foreach (var c in result.Chapters) Preview.Add(c);

        HasErrors = Errors.Count > 0;
        HasWarnings = Warnings.Count > 0;
        HasPreview = Preview.Count > 0;
        CanSave = result.CanSave && !result.Saved;

        ResultMessage = result.Saved
            ? result.Message
            : result.CanSave
                ? "Kiểm tra xong, không có lỗi. Bấm “Lưu các chương” để lưu."
                : "Có lỗi, hãy sửa rồi kiểm tra lại.";
    }
}
