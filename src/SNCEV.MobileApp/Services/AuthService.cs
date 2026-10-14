
using System.Net.Http.Json;
using System.Text.Json;
using SNCEV.MobileApp.Models.Auth;

namespace SNCEV.MobileApp.Services;

public sealed class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> LoginAsync(
        string username,
        string password)
    {
        using var response = await _http.PostAsJsonAsync(
            "auth/login",
            new { username, password });

        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content
            .ReadFromJsonAsync<LoginResponse>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (string.IsNullOrWhiteSpace(result?.AccessToken))
            return false;

        await SecureStorage.Default.SetAsync(
            "sncev_access_token",
            result.AccessToken);

        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            await SecureStorage.Default.SetAsync(
                "sncev_refresh_token",
                result.RefreshToken);
        }

        return true;
    }
}
