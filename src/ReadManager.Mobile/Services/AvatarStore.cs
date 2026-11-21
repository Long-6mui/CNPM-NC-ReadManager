using Microsoft.Maui.Storage;

namespace ReadManager.Mobile.Services;

// Ảnh đại diện chọn từ máy được lưu đường dẫn theo từng tài khoản (Preferences "avatar.{userId}").
// Dùng chung cho trang Tài khoản, trang Chuỗi, bình luận của chính mình.
public static class AvatarStore
{
    public static string Key(int userId) => $"avatar.{userId}";

    public static ImageSource? Load(int userId)
    {
        var path = Preferences.Default.Get(Key(userId), string.Empty);
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? ImageSource.FromFile(path) : null;
    }

    public static string Initial(string? name)
        => string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
}
