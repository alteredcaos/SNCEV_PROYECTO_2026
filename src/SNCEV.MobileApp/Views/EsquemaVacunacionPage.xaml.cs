
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

public partial class EsquemaVacunacionPage : ContentPage
{
    private readonly Guid _pacienteId;
    private readonly EsquemaVacunacionService _servicio;
    private bool _cargado;

    public EsquemaVacunacionPage(
        Guid pacienteId,
        EsquemaVacunacionService servicio)
    {
        InitializeComponent();
        _pacienteId = pacienteId;
        _servicio = servicio;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_cargado)
            return;

        _cargado = true;
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        Indicador.IsVisible = true;
        Indicador.IsRunning = true;
        PanelDatos.IsVisible = false;

        try
        {
            var esquema = await _servicio.ObtenerAsync(
                _pacienteId);

            LblPaciente.Text = esquema.NombrePaciente;

            LblEvaluacion.Text =
                $"Evaluado: {esquema.FechaEvaluacion:dd/MM/yyyy HH:mm}";

            LblAplicadas.Text =
                $"Aplicadas ({esquema.Aplicadas.Count})";

            LblAplicables.Text =
                $"Aplicables ({esquema.Aplicables.Count})";

            LblProgramadas.Text =
                $"Programadas ({esquema.Programadas.Count})";

            CvAplicadas.ItemsSource = esquema.Aplicadas;
            CvAplicables.ItemsSource = esquema.Aplicables;
            CvProgramadas.ItemsSource = esquema.Programadas;

            LblEstado.Text =
                esquema.RequierenVerificacion.Count > 0
                    ? $"Hay {esquema.RequierenVerificacion.Count} " +
                      "reglas que requieren verificación."
                    : "";

            PanelDatos.IsVisible = true;
        }
        catch (UnauthorizedAccessException)
        {
            LblEstado.Text =
                "Sesión expirada. Inicia sesión nuevamente.";
            _cargado = false;
        }
        catch (InvalidOperationException)
        {
            LblEstado.Text =
                "No tienes permisos para consultar este esquema.";
        }
        catch (HttpRequestException)
        {
            LblEstado.Text =
                "No se pudo conectar con SNCEV.Api.";
            _cargado = false;
        }
        catch (TaskCanceledException)
        {
            LblEstado.Text =
                "La consulta excedió el tiempo de espera.";
            _cargado = false;
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"Error esquema: {ex.GetType().Name}");
#endif
            LblEstado.Text =
                "No fue posible cargar el esquema.";
            _cargado = false;
        }
        finally
        {
            Indicador.IsRunning = false;
            Indicador.IsVisible = false;
        }
    }
}
