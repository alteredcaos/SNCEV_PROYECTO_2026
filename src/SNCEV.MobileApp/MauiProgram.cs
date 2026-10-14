using ZXing.Net.Maui.Controls;


namespace SNCEV.MobileApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
             .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont(
                    "OpenSans-Regular.ttf",
                    "OpenSansRegular");

                fonts.AddFont(
                    "OpenSans-Semibold.ttf",
                    "OpenSansSemibold");
            });
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(
        Services.ApiSettings.BaseUrl),

            Timeout = TimeSpan.FromSeconds(20)
        });

        builder.Services.AddTransient<
            Services.AuthService>();
        builder.Services.AddTransient<Services.PacientesService>();
        builder.Services.AddTransient<Services.EsquemaVacunacionService>();
        builder.Services.AddTransient<Services.ExpedienteService>();
        // Offline-first: instancia compartida y JWT guardado en SecureStorage.
        builder.Services.AddSingleton<Services.SyncV2Outbox>(sp =>
            new Services.SyncV2Outbox(
                sp.GetRequiredService<HttpClient>(),
                () => SecureStorage.Default.GetAsync("sncev_access_token")));
        builder.Services.AddSingleton<Services.OfflineRegistroService>();
        builder.Services.AddSingleton<Services.CatalogoCluesSyncService>();
        return builder.Build();
    }
}
