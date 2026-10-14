
namespace SNCEV.MobileApp.Services;

public static class ApiSettings
{
#if DEBUG && ANDROID
    // Samsung conectado mediante adb reverse.
    public const string BaseUrl =
        "http://127.0.0.1:63731/v1/";
#else
    // Sustituir por el servidor HTTPS real antes de distribuir.
    public const string BaseUrl =
        "https://servidor-configurado.example/v1/";
#endif
}
