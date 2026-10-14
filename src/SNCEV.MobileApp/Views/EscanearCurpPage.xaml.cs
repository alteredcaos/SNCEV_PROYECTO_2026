
using ZXing.Net.Maui;
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

public partial class EscanearCurpPage : ContentPage
{
    private readonly Func<DatosCurpQr, Task> _alEscanear;
    private bool _procesando;

    public EscanearCurpPage(
        Func<DatosCurpQr, Task> alEscanear)
    {
        InitializeComponent();

        _alEscanear = alEscanear;

        LectorQr.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };

        LectorQr.IsDetecting = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var permiso = await Permissions.RequestAsync<
            Permissions.Camera>();

        if (permiso != PermissionStatus.Granted)
        {
            LblEstado.Text =
                "Debes autorizar el uso de la cámara.";
            return;
        }

        if (!_procesando)
            LectorQr.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        LectorQr.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(
        object sender,
        BarcodeDetectionEventArgs e)
    {
        if (_procesando)
            return;

        foreach (var codigo in e.Results)
        {
            if (!CurpQrParser.TryParse(
                    codigo.Value, out var datos))
                continue;

            _procesando = true;
            LectorQr.IsDetecting = false;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Navigation.PopAsync();
                    await _alEscanear(datos!);
                }
                catch
                {
                    _procesando = false;
                    LblEstado.Text =
                        "No fue posible procesar la CURP.";
                }
            });

            break;
        }
    }

    private async void OnCancelarClicked(
        object sender,
        EventArgs e)
    {
        LectorQr.IsDetecting = false;
        await Navigation.PopAsync();
    }
}
