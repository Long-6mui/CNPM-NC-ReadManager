namespace ReadManager.Mobile.Services;

public interface ITokenStore
{
    Task<string?> GetTokenAsync();
    Task SaveTokenAsync(string token);
    Task ClearAsync();
}

// Lưu JWT bằng SecureStorage của MAUI (mã hoá theo nền tảng: Keystore/Keychain...)
public class SecureTokenStore : ITokenStore
{
    private const string TokenKey = "auth_token";

    public Task<string?> GetTokenAsync() => SecureStorage.Default.GetAsync(TokenKey);

    public Task SaveTokenAsync(string token) => SecureStorage.Default.SetAsync(TokenKey, token);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
