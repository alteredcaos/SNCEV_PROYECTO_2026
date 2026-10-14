using System.Net.Http;
using SNCEV.MobileApp.Models;
using System.Text.Json;
using SNCEV.MobileApp.Models.Expediente;
using SNCEV.MobileApp.Models.Vacunacion;
using SNCEV.MobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
namespace SNCEV.MobileApp.Views;

using Microsoft.Maui.Devices.Sensors;
public partial class PacienteDetallePage : ContentPage
{
    private readonly DomiciliosService _domiciliosService;
    private Location? _ubicacionDomicilio;
    private readonly Guid _pacienteId;
    private readonly HttpClient _httpClient;
    private readonly ExpedienteService _expedienteService;

    private readonly EsquemaVacunacionService _esquemaService;
    private bool _cargando;
    private bool _expedienteCargado;
    private bool _esquemaCargado;
    private string _pestanaActual = "expediente";

    // Conserva la firma que MainPage ya utiliza.
    public PacienteDetallePage(Guid pacienteId, PacientesService pacientesService)
    {
        InitializeComponent();
        _pacienteId = pacienteId;
        _ = pacientesService; // Compatibilidad; el expediente usa su endpoint ampliado.
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiSettings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };
        _expedienteService = new ExpedienteService(_httpClient);
        _esquemaService = new EsquemaVacunacionService(_httpClient);
        _domiciliosService = new DomiciliosService(_httpClient);
        MostrarPestana("expediente");
    }
    private async Task CargarDomicilioVigenteAsync()
    {
        _ubicacionDomicilio = null;
        BtnUbicacion.IsEnabled = false;

        var domicilio = await _domiciliosService.ObtenerVigenteAsync(_pacienteId);

        if (domicilio is null)
        {
            LblDomicilioEstado.Text = "Sin domicilio vigente registrado";
            LblCoordenadas.Text = "Coordenadas: No registradas";
            return;
        }

        LblDomicilioEstado.Text = "Domicilio vigente registrado";

        LblEstadoResidencia.Text =
            "Estado: " + Valor(domicilio.EstadoNombre);

        LblMunicipio.Text =
            "Municipio / Alcaldía: " + Valor(domicilio.MunicipioNombre);

        LblLocalidad.Text =
            "Localidad: " + Valor(domicilio.Localidad);

        LblColonia.Text =
            "Colonia: " + Valor(domicilio.Colonia);

        LblCodigoPostal.Text =
            "Código postal: " + Valor(domicilio.CodigoPostal);

        var direccion = string.Join(" ", new[]
{
    domicilio.Calle,
    domicilio.NumeroExterior,
    string.IsNullOrWhiteSpace(domicilio.NumeroInterior)
        ? null
        : $"Int. {domicilio.NumeroInterior}"
}.Where(x => !string.IsNullOrWhiteSpace(x)));

        LblDireccion.Text = "Calle y número: " + Valor(direccion);

        LblReferencia.Text =
            "Referencia del domicilio: " + Valor(domicilio.DireccionTexto);
        LblAgeb.Text = "AGEB: " + Valor(domicilio.AgebClave);
        LblCvegeo.Text = "CVEGEO: " + Valor(domicilio.Cvegeo);
        LblUnidadCercana.Text =
            "CLUES sugerida: " + Valor(domicilio.CluesSugerida);

        if (domicilio.Latitud is double lat &&
            domicilio.Longitud is double lon &&
            double.IsFinite(lat) &&
            double.IsFinite(lon) &&
            lat >= -90 && lat <= 90 &&
            lon >= -180 && lon <= 180 &&
            !(lat == 0 && lon == 0))
        {
            _ubicacionDomicilio = new Location(lat, lon);
            LblCoordenadas.Text = $"Coordenadas: {lat:F6}, {lon:F6}";
            BtnUbicacion.IsEnabled = true;
        }
        else
        {
            LblCoordenadas.Text = "Coordenadas: No registradas";
        }
    }
    private async void OnVerUbicacionClicked(object sender, EventArgs e)
    {
        if (_ubicacionDomicilio is null)
            return;

        try
        {
            await Map.Default.OpenAsync(
                _ubicacionDomicilio,
                new MapLaunchOptions
                {
                    Name = "Domicilio del paciente",
                    NavigationMode = NavigationMode.None
                });
        }
        catch (Exception)
        {
            await DisplayAlert(
                "Mapa no disponible",
                "No fue posible abrir la ubicación.",
                "Aceptar");
        }
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_expedienteCargado)
            await CargarExpedienteAsync();
        if (_pestanaActual == "esquema") await ActualizarPendientesLocalesAsync();
    }

    private void MostrarPestana(string pestana)
    {
        _pestanaActual = pestana;
        PanelExpediente.IsVisible = pestana == "expediente" && _expedienteCargado;
        PanelEsquema.IsVisible = pestana == "esquema" && _esquemaCargado;
        BtnExpediente.BackgroundColor = Color.FromArgb(pestana == "expediente" ? "#1976D2" : "#37474F");
        BtnEsquema.BackgroundColor = Color.FromArgb(pestana == "esquema" ? "#1976D2" : "#37474F");
    }

    private void MostrarEstado(string mensaje)
    {
        LblEstado.Text = mensaje;
        LblEstado.IsVisible = !string.IsNullOrWhiteSpace(mensaje);
    }

    private static string Valor(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "No registrado" : texto;

    private static string EdadTexto(DateTime nacimiento)
    {
        var hoy = DateTime.Today;
        var fecha = nacimiento.Date;
        if (fecha > hoy) return "Fecha de nacimiento por verificar";
        int anios = hoy.Year - fecha.Year;
        if (fecha > hoy.AddYears(-anios)) anios--;
        var aniversario = fecha.AddYears(anios);
        int meses = 0;
        while (meses < 11 && aniversario.AddMonths(meses + 1) <= hoy) meses++;
        int dias = (hoy - aniversario.AddMonths(meses)).Days;
        return $"{anios} años, {meses} meses, {dias} días";
    }

    // El contrato exacto de domicilioVigente aún no se ha compartido.
    // Se leen sólo claves reconocibles; no se geocodifica ni se infieren coordenadas.
    private static string? Campo(JsonElement? domicilio, params string[] claves)
    {
        if (domicilio is not { ValueKind: JsonValueKind.Object } objeto)
            return null;
        foreach (var clave in claves)
        {
            foreach (var propiedad in objeto.EnumerateObject())
            {
                if (!string.Equals(propiedad.Name, clave, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (propiedad.Value.ValueKind == JsonValueKind.String)
                    return propiedad.Value.GetString();
                if (propiedad.Value.ValueKind == JsonValueKind.Number)
                    return propiedad.Value.ToString();
            }
        }
        return null;
    }

    private async Task CargarExpedienteAsync()
    {
        if (_cargando) return;
        _cargando = true;
        IndicadorCarga.IsVisible = IndicadorCarga.IsRunning = true;
        BtnActualizar.IsEnabled = BtnExpediente.IsEnabled = BtnEsquema.IsEnabled = false;
        try
        {
            MostrarEstado("");
            ExpedientePacienteResponse expediente =
                await _expedienteService.ObtenerAsync(_pacienteId);
            var paciente = expediente.Paciente;
            if (paciente is null)
                throw new JsonException("El expediente no contiene paciente.");

            LblNombre.Text = paciente.NombreCompleto;
            var curp = paciente.Curp ?? "";

            LblCurp.Text = string.IsNullOrWhiteSpace(paciente.Curp)
                ? "CURP: No registrada"
                : $"CURP: {paciente.Curp.Trim().ToUpperInvariant()}";

            LblNacimiento.Text = "Nacimiento: " + paciente.FechaNacimientoTexto;
            var hoy = DateTime.Today;
            var nacimiento = paciente.FechaNacimiento.Date;

            int edad = hoy.Year - nacimiento.Year;

            if (nacimiento > hoy.AddYears(-edad))
                edad--;

            LblEdad.Text = $"Edad: {edad} años";

            LblEntidadNacimiento.Text =
                "Entidad de nacimiento: " +
                (paciente.EntidadNacimiento ?? "No registrada");
            LblEdad.Text = "Edad: " + EdadTexto(paciente.FechaNacimiento);
            LblSexo.Text = "Sexo: " + (paciente.Sexo switch
            {
                "M" or "H" => "Masculino",
                "F" => "Femenino",
                _ => Valor(paciente.Sexo)
            });
            LblEntidadNacimiento.Text = "Entidad de nacimiento: " + Valor(paciente.EntidadNacimiento);
            LblTipoCartilla.Text = "Cartilla: " + Valor(paciente.TipoCartilla);

            var domicilio = expediente.DomicilioVigente;
            bool tieneDomicilio = domicilio is { ValueKind: JsonValueKind.Object };
            LblDomicilioEstado.Text = tieneDomicilio
                ? "Domicilio vigente estructurado disponible"
                : "Sin domicilio vigente estructurado; se muestran datos del registro nominal";
            LblEstadoResidencia.Text = "Estado: " + Valor(
                Campo(domicilio, "estado", "estado_nombre", "estado_residencia") ?? paciente.EstadoResidencia);
            LblMunicipio.Text = "Municipio / Alcaldía: " + Valor(
                Campo(domicilio, "municipio", "municipio_nombre", "alcaldia") ?? paciente.MunicipioResidencia);
            LblLocalidad.Text = "Localidad: " + Valor(
                Campo(domicilio, "localidad", "localidad_nombre") ?? paciente.Localidad);
            LblColonia.Text = "Colonia: " + Valor(Campo(domicilio, "colonia", "colonia_nombre"));
            LblCodigoPostal.Text = "Código postal: " + Valor(Campo(domicilio, "codigo_postal", "codigoPostal", "cp"));
            LblDireccion.Text = "Dirección: " + Valor(
                Campo(domicilio, "direccion", "calle_numero", "domicilio") ?? paciente.Direccion);
            LblReferencia.Text = "Referencia: " + Valor(Campo(domicilio, "referencia", "referencias"));
            LblAgeb.Text = "AGEB: " + Valor(
                Campo(domicilio, "ageb", "ageb_clave") ?? paciente.AgebClave);
            LblCvegeo.Text = "CVEGEO: " + Valor(Campo(domicilio, "cvegeo", "cve_geo"));
            var lat = Campo(domicilio, "latitud", "lat");
            var lon = Campo(domicilio, "longitud", "lon", "lng");
            LblCoordenadas.Text = "Coordenadas: " +
                (lat is not null && lon is not null ? $"{lat}, {lon}" : "No registradas");
            LblUnidadCercana.Text = "Unidad cercana: " + Valor(
                Campo(domicilio, "unidad_cercana", "unidadCercana", "clues_sugerida"));
            // Sin mapa hasta validar el contrato de coordenadas y permisos.
            //BtnUbicacion.IsEnabled = false;
            try
            {
                await CargarDomicilioVigenteAsync();
            }
            catch (Exception ex)
            {
                _ubicacionDomicilio = null;
                BtnUbicacion.IsEnabled = false;
                LblDomicilioEstado.Text =
                    "No fue posible consultar el domicilio vigente: " +
                    DescribirError(ex);
            }

            var vacunas = expediente.VacunasAplicadas ?? new();
            BindableLayout.SetItemsSource(ListaVacunas, vacunas);
            LblTotalVacunas.Text = $"Historial de vacunación ({vacunas.Count})";
            LblSinVacunas.IsVisible = vacunas.Count == 0;
            int mediciones = expediente.Somatometria?.Count ?? 0;
            LblSomatometria.Text = mediciones == 0
                ? "Sin mediciones registradas."
                : $"{mediciones} medición(es) registrada(s); detalle pendiente de integrar.";

            _expedienteCargado = true;
            MostrarPestana(_pestanaActual);
        }
        catch (Exception ex)
        {
            _expedienteCargado = false;
            PanelExpediente.IsVisible = false;
            MostrarEstado(DescribirError(ex));
        }
        finally
        {
            IndicadorCarga.IsVisible = IndicadorCarga.IsRunning = false;
            BtnActualizar.IsEnabled = BtnExpediente.IsEnabled = BtnEsquema.IsEnabled = true;
            _cargando = false;
        }
    }

    // Muestra dosis locales pendientes sin confundirlas con aplicaciones confirmadas.
    private async Task ActualizarPendientesLocalesAsync()
    {
        var registro = IPlatformApplication.Current?.Services.GetService<OfflineRegistroService>();
        if (registro is null) return;
        try
        {
            var pendientes = await registro.ObtenerDosisPendientesAsync(_pacienteId);
            ListaDosisPendientes.Children.Clear();
            foreach (var dosis in pendientes.OrderByDescending(x => x.FechaAplicacion))
            {
                ListaDosisPendientes.Children.Add(new Label
                {
                    Text = $"{dosis.VacunaNombre} · dosis {dosis.NumeroDosis} | {dosis.FechaAplicacion:dd/MM/yyyy} | {dosis.UnidadMedicaClues}\nGuardada en SQLite · pendiente de sincronización",
                    TextColor = Color.FromArgb("#FFCC80"),
                    FontSize = 15
                });
            }
            LblPendientesLocales.Text = $"Dosis locales pendientes: {pendientes.Count}";
            LblPendientesLocales.IsVisible = true;
            ListaDosisPendientes.IsVisible = pendientes.Count > 0;
        }
        catch (Exception ex)
        {
            LblPendientesLocales.Text = "No se pudieron consultar dosis locales: " + ex.Message;
            LblPendientesLocales.IsVisible = true;
        }
    }

    private async Task CargarEsquemaAsync()
    {
        if (_cargando) return;
        _cargando = true;
        IndicadorCarga.IsVisible = IndicadorCarga.IsRunning = true;
        BtnActualizar.IsEnabled = BtnExpediente.IsEnabled = BtnEsquema.IsEnabled = false;
        try
        {
            MostrarEstado("");
            EsquemaVacunacionResponse esquema = await _esquemaService.ObtenerAsync(_pacienteId);
            LblFechaEvaluacion.Text = "Evaluación: " + esquema.FechaEvaluacion.ToString("dd/MM/yyyy HH:mm");
            LblNumeroAplicadas.Text = esquema.Aplicadas.Count.ToString();
            await ActualizarPendientesLocalesAsync();
            LblNumeroAplicables.Text = esquema.Aplicables.Count.ToString();
            LblNumeroProgramadas.Text = esquema.Programadas.Count.ToString();
            BindableLayout.SetItemsSource(ListaAplicables, esquema.Aplicables);
            BindableLayout.SetItemsSource(ListaProgramadas, esquema.Programadas);
            LblSinAplicables.IsVisible = esquema.Aplicables.Count == 0;
            LblSinProgramadas.IsVisible = esquema.Programadas.Count == 0;
            var avisos = new List<string>();
            if (esquema.RequierenVerificacion.Count > 0)
                avisos.Add($"{esquema.RequierenVerificacion.Count} regla(s) requieren verificación.");
            int advertencias = esquema.Programadas.Count(x =>
                x.ReglaMarcadaParaVerificacion ||
                (x.Motivo?.Contains("verificación", StringComparison.OrdinalIgnoreCase) ?? false));
            if (advertencias > 0)
                avisos.Add($"{advertencias} dosis programada(s) tienen advertencias de verificación.");
            LblAdvertencias.Text = string.Join("\n", avisos);
            LblAdvertencias.IsVisible = avisos.Count > 0;
            _esquemaCargado = true;
            MostrarPestana(_pestanaActual);
        }
        catch (Exception ex)
        {
            _esquemaCargado = false;
            PanelEsquema.IsVisible = false;
            MostrarEstado(DescribirError(ex));
        }
        finally
        {
            IndicadorCarga.IsVisible = IndicadorCarga.IsRunning = false;
            BtnActualizar.IsEnabled = BtnExpediente.IsEnabled = BtnEsquema.IsEnabled = true;
            _cargando = false;
        }
    }

    // Fase 3C.1: formulario local, sin llamada a Sync Push.
    private async void OnDosisAplicableTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.BindingContext is not VacunaProgramadaDto dosis)
            return;
        if (dosis.VacunaId <= 0 || dosis.NumeroDosis <= 0 || _pacienteId == Guid.Empty)
        {
            await DisplayAlertAsync("Datos incompletos", "No es posible identificar la dosis.", "Aceptar");
            return;
        }
        var servicio = Handler?.MauiContext?.Services.GetService<OfflineRegistroService>();
        if (servicio is null)
        {
            await DisplayAlertAsync("Servicio no disponible", "OfflineRegistroService no está registrado.", "Aceptar");
            return;
        }
        if (await servicio.ExisteDosisPendienteAsync(_pacienteId, dosis.VacunaId, dosis.NumeroDosis))
        {
            await DisplayAlertAsync("Dosis pendiente", "Ya existe un registro local pendiente para esta dosis.", "Aceptar");
            return;
        }
        await Navigation.PushModalAsync(new RegistroDosisOfflinePage(
            servicio, _pacienteId, dosis.VacunaId, dosis.NumeroDosis, dosis.Vacuna));
    }

    private async void OnExpedienteClicked(object sender, EventArgs e)
    {
        MostrarPestana("expediente");
        if (!_expedienteCargado) await CargarExpedienteAsync();
    }

    private async void OnEsquemaClicked(object sender, EventArgs e)
    {
        MostrarPestana("esquema");
        if (!_esquemaCargado) await CargarEsquemaAsync();
        await ActualizarPendientesLocalesAsync();
    }

    private async void OnActualizarClicked(object sender, EventArgs e)
    {
        if (_cargando) return;
        if (_pestanaActual == "expediente") await CargarExpedienteAsync();
        else await CargarEsquemaAsync();
    }

    private static string DescribirError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Sesión expirada. Inicia sesión nuevamente.",
        InvalidOperationException => "No tienes permisos para consultar estos datos.",
        HttpRequestException => "No fue posible conectar con SNCEV.Api.",
        TaskCanceledException => "La consulta excedió el tiempo de espera.",
        JsonException => "La respuesta de la API no tiene el formato esperado.",
        _ => "Ocurrió un error al consultar la información."
    };

    private async void OnVerEnMapaClicked(
    object sender,
    EventArgs e)
    {
        try
        {
            // Sustituiremos estos valores por los
            // datos reales obtenidos desde SNCEV.Api.
            string direccion =
                "Av. Ejemplo 125, Ciudad de México";

            var ubicacion = new Placemark
            {
                Thoroughfare = direccion,
                CountryName = "México"
            };

            await Map.Default.OpenAsync(
                ubicacion,
                new MapLaunchOptions
                {
                    Name = "Domicilio del paciente",
                    NavigationMode = NavigationMode.None
                });
        }
        catch (Exception)
        {
            await DisplayAlert(
                "Mapa no disponible",
                "No fue posible abrir la ubicación.",
                "Aceptar");
        }
    }


}
