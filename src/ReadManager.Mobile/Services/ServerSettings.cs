namespace ReadManager.Mobile.Services;

// Hộp thoại "Địa chỉ máy chủ API": để demo trên điện thoại thật mà không phải sửa code / build lại.
public static class ServerSettings
{
    public static async Task PromptAsync()
    {
        var shell = Shell.Current;
        var input = await shell.DisplayPromptAsync(
            "Địa chỉ máy chủ API",
            "Máy ảo Android: 10.0.2.2:5283\nĐiện thoại thật: IP máy chạy API, vd 192.168.1.25:5283\n(Để trống = mặc định)",
            "Kiểm tra & lưu", "Hủy",
            initialValue: ApiSettings.BaseUrl, keyboard: Keyboard.Url);
        if (input is null) return;   // Hủy

        if (string.IsNullOrWhiteSpace(input))
        {
            ApiSettings.SetBaseUrl(null);
            await shell.DisplayAlert("Địa chỉ máy chủ", "Đã quay về địa chỉ mặc định. Hãy đóng hẳn ứng dụng rồi mở lại để áp dụng.", "Đóng");
            return;
        }

        var url = ApiSettings.Normalize(input);
        if (url is null)
        {
            await shell.DisplayAlert("Địa chỉ máy chủ", "Địa chỉ không hợp lệ. Ví dụ đúng: 192.168.1.25:5283", "Đóng");
            return;
        }

        // Thử gọi API thể loại (không cần đăng nhập) bằng địa chỉ mới
        var reachable = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await http.GetAsync(url + "genres");
            reachable = response.IsSuccessStatusCode;
        }
        catch { /* không kết nối được */ }

        if (!reachable)
        {
            var keep = await shell.DisplayAlert("Không kết nối được",
                $"Không gọi được {url}\nKiểm tra: API đã chạy chưa, điện thoại và máy tính cùng Wi-Fi chưa, tường lửa có chặn cổng 5283 không.\n\nVẫn lưu địa chỉ này?",
                "Vẫn lưu", "Hủy");
            if (!keep) return;
        }

        ApiSettings.SetBaseUrl(url);
        await shell.DisplayAlert("Đã lưu", $"Máy chủ: {url}\nHãy đóng hẳn ứng dụng (vuốt tắt) rồi mở lại để áp dụng.", "Đóng");
    }
}
