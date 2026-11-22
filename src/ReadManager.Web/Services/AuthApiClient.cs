using System.Net.Http.Headers;
using System.Net.Http.Json;
namespace ReadManager.Web.Services;
public record AuthUser(int UserId,string Email,string DisplayName,string Role);
public record LoginResult(string AccessToken,DateTimeOffset ExpiresAt,AuthUser User);
public record AccountState(string? AccountStatus,string? StatusReason);
public class AuthApiClient(HttpClient client)
{
 public Task<HttpResponseMessage> RegisterAsync(string displayName,string email,string password)=>client.PostAsJsonAsync("api/auth/register",new {displayName,email,password});
 public Task<HttpResponseMessage> LoginAsync(string email,string password)=>client.PostAsJsonAsync("api/auth/login",new {email,password});
 public async Task<HttpResponseMessage> MeAsync(string token){using var request=new HttpRequestMessage(HttpMethod.Get,"api/auth/me");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await client.SendAsync(request);}
 // PB36 — trạng thái hiện tại của chính người đăng nhập (đọc từ DB qua API); lỗi/hết phiên → null
 public async Task<AccountState?> StateAsync(string? token){if(string.IsNullOrEmpty(token))return null;using var response=await MeAsync(token);return response.IsSuccessStatusCode?await response.Content.ReadFromJsonAsync<AccountState>():null;}
 public async Task<HttpResponseMessage> LogoutAsync(string token){using var request=new HttpRequestMessage(HttpMethod.Post,"api/auth/logout");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await client.SendAsync(request);}
}
