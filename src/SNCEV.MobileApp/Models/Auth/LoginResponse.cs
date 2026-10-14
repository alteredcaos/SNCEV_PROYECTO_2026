
using System.Text.Json;

namespace SNCEV.MobileApp.Models.Auth;

public sealed class LoginResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public JsonElement User { get; set; }
}
