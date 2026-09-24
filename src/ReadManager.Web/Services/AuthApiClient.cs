using System.Net.Http.Headers;
using System.Net.Http.Json;
namespace ReadManager.Web.Services;
public record AuthUser(int UserId,string Email,string DisplayName,string Role);
public record LoginResult(string AccessToken,DateTimeOffset ExpiresAt,AuthUser User);
public class AuthApiClient(HttpClient client)
{
 public Task<HttpResponseMessage> LoginAsync(string email,string password)=>client.PostAsJsonAsync("api/auth/login",new {email,password});
 public async Task<HttpResponseMessage> MeAsync(string token){using var request=new HttpRequestMessage(HttpMethod.Get,"api/auth/me");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await client.SendAsync(request);}
 public async Task<HttpResponseMessage> LogoutAsync(string token){using var request=new HttpRequestMessage(HttpMethod.Post,"api/auth/logout");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await client.SendAsync(request);}
}
