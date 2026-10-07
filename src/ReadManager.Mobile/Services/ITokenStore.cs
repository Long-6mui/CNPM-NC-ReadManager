namespace ReadManager.Mobile.Services;

public interface ITokenStore
{
    Task<string?> GetTokenAsync();
    Task SaveTokenAsync(string token);
    Task ClearAsync();
}
