
using SNCEV.MobileApp.ViewModels;
using SNCEV.MobileApp.Services;
using SNCEV.MobileApp.Models;
namespace SNCEV.MobileApp.Views;

public partial class MainPage : ContentPage
{
    private readonly DashboardCoberturasViewModel _dashboardVm = new();

    private readonly PacientesService _pacientesService;

    private bool _buscando = false;

    public MainPage()
    {
        InitializeComponent();

        // Mantener los indicadores del dashboard
        CvKpis.ItemsSource = _dashboardVm.Kpis;

        // Servicio de búsqueda conectado a SNCEV.Api
        _pacientesService = new PacientesService(
            new HttpClient
            {
                BaseAddress = new Uri(ApiSettings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            });
    }

    // =========================================
    // BÚSQUEDA NOMINAL DE PACIENTES
    // =========================================

  

private async Task BuscarPacienteAsync(string criterio)
    {
        if (_buscando || string.IsNullOrWhiteSpace(criterio))
            return;

        _buscando = true;

        try
        {
            PanelResultados.IsVisible = false;
            CvPacientes.ItemsSource = null;

            var resultado =
                await _pacientesService.BuscarAsync(criterio);

            if (resultado.Total == 0)
            {
                await DisplayAlertAsync(
                    "Paciente no encontrado",
                    "No existen coincidencias con el criterio indicado.",
                    "Aceptar");

                return;
            }

            CvPacientes.ItemsSource = resultado.Items;

            LblTotalPacientes.Text =
                $"Pacientes encontrados: {resultado.Total}";

            PanelResultados.IsVisible = true;
        }
        catch (UnauthorizedAccessException)
        {
            await DisplayAlertAsync(
                "Sesión expirada",
                "Debes iniciar sesión nuevamente.",
                "Aceptar");
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync(
                "Error de conexión",
                "No se pudo consultar SNCEV.Api.",
                "Aceptar");
        }
        catch (TaskCanceledException)
        {
            await DisplayAlertAsync(
                "Tiempo de espera",
                "La consulta tardó demasiado.",
                "Aceptar");
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"Error de búsqueda: {ex.GetType().Name}");
#endif
            await DisplayAlertAsync(
                "Error",
                "No fue posible completar la búsqueda.",
                "Aceptar");
        }
        finally
        {
            _buscando = false;
        }
    }

    

    // =========================================
    // BÚSQUEDA MANUAL
    // =========================================

    private async void OnBuscarPacienteClicked(
        object sender,
        EventArgs e)
    {
        string criterio = TxtBuscar.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(criterio))
        {
            await DisplayAlertAsync(
                "Búsqueda",
                "Introduce una CURP o un nombre.",
                "Aceptar");

            return;
        }

        await BuscarPacienteAsync(criterio);
    }

    // =========================================
    // ESCÁNER QR DE RENAPO
    // =========================================


    private async void OnEscanearCurpClicked(
        object sender,
        EventArgs e)
    {
        var pagina = new EscanearCurpPage(
            async datos =>
            {
                TxtBuscar.Text = datos.Curp;

                await BuscarPacienteAsync(datos.Curp);
            });

        await Navigation.PushAsync(pagina);
    }


    // =========================================
    // BOTONES DEL DASHBOARD
    // =========================================

    private async void OnApp1Clicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "App 1: Captura Clínica",
            "Modo de búsqueda, expediente y captura de dosis.",
            "OK");
    }

    private async void OnApp2Clicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "App 2: Dashboard Directivo",
            "Tablero de coberturas y alertas de rezago.",
            "OK");
    }

    // =========================================
    // CAMBIO DE TEXTO EN EL BUSCADOR
    // =========================================

    private void OnBuscarTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        // Reservado para búsqueda dinámica.
        // Por ahora utilizamos el botón Buscar.
    }



    private async void OnPacienteSeleccionado(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault()
            is not PacienteResumen paciente)
            return;

        CvPacientes.SelectedItem = null;

        var pagina = new PacienteDetallePage(
            paciente.Id,
            _pacientesService);

        await Navigation.PushAsync(pagina);
    }

    private async void AbrirDiagnosticoCluesClicked(
    object sender, EventArgs e)
    {
        var servicio = IPlatformApplication.Current!
            .Services
            .GetRequiredService<CatalogoCluesSyncService>();

        await Navigation.PushModalAsync(
            new NavigationPage(
                new DiagnosticoCluesPage(servicio)));
    }
}
