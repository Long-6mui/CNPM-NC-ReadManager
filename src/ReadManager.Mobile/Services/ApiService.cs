using System.Net.Http.Json;
using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    // Đổi cổng 5000 thành cổng đang chạy thực tế của ReadManager.Web
    private const string BaseUrl = "http://10.0.2.2:5000/api/";

    public ApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<HomeData?> GetHomeDataAsync()
    {
        try
        {
            // Gọi endpoint API trả về JSON của trang chủ
            return await _httpClient.GetFromJsonAsync<HomeData>("home");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi kết nối API: {ex.Message}");
            return null;
        }
    }
}