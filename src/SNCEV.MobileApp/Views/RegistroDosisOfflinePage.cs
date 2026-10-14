using SNCEV.MobileApp.Models;
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

// Fase 3C.1: guarda únicamente en SQLite. No realiza sincronización.
public sealed class RegistroDosisOfflinePage : ContentPage
{
    private readonly OfflineRegistroService _registro;
    private readonly Guid _pacienteId;
    private readonly int _vacunaId;
    private readonly int _numeroDosis;
    private readonly string _vacuna;
    private readonly DatePicker _fecha = new() { Date = DateTime.Today, MaximumDate = DateTime.Today };
    private readonly Entry _lote = new() { Placeholder = "Lote (obligatorio)" };
    private readonly Entry _laboratorio = new() { Placeholder = "Laboratorio" };
    private string? _cluesSeleccionada;
    private readonly Label _cluesTexto = new() { Text = "Ninguna unidad seleccionada", TextColor = Colors.White };
    private readonly Button _buscarClues = new() { Text = "Buscar unidad por nombre o CLUES" };
    private readonly Editor _observaciones = new() { Placeholder = "Observaciones (opcional)", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 90 };
    private readonly Button _guardar = new() { Text = "Guardar en dispositivo", BackgroundColor = Color.FromArgb("#287A36"), TextColor = Colors.White };

    public RegistroDosisOfflinePage(OfflineRegistroService registro, Guid pacienteId,
        int vacunaId, int numeroDosis, string vacuna)
    {
        _registro = registro;
        _pacienteId = pacienteId;
        _vacunaId = vacunaId;
        _numeroDosis = numeroDosis;
        _vacuna = vacuna;
        Title = "Registro offline";
        BackgroundColor = Color.FromArgb("#101820");
        _guardar.Clicked += GuardarClicked;
        _buscarClues.Clicked += BuscarCluesClicked;
        var cancelar = new Button { Text = "Cancelar" };
        cancelar.Clicked += async (_, _) => await Navigation.PopModalAsync();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20), Spacing = 12,
                Children =
                {
                    new Label { Text = $"{_vacuna} · dosis {_numeroDosis}", FontSize = 23, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Label { Text = "Solo pruebas con pacientes ficticios. Este registro quedará pendiente en SQLite; no es una dosis confirmada por el servidor.", TextColor = Colors.Orange },
                    new Label { Text = "Fecha de aplicación", TextColor = Colors.White },
                    _fecha, _lote, _laboratorio, _buscarClues, _cluesTexto, _observaciones, _guardar, cancelar
                }
            }
        };
    }

    
private async void BuscarCluesClicked(object? sender, EventArgs e)
    {
        var catalogo = IPlatformApplication.Current?.Services.GetService<CatalogoCluesSyncService>();
        if (catalogo is null)
        {
            await DisplayAlertAsync("Servicio no disponible", "CatalogoCluesSyncService no registrado.", "Aceptar");
            return;
        }
        await Navigation.PushModalAsync(new NavigationPage(
            new SeleccionarCluesOfflinePage(catalogo, unidad =>
            {
                _cluesSeleccionada = unidad.Clues;
                _cluesTexto.Text = $"{unidad.NombreUnidad} | {unidad.Clues} | {unidad.Municipio}";
            })));
    }

    private async void GuardarClicked(object? sender, EventArgs e)
{
    if (string.IsNullOrWhiteSpace(_lote.Text) ||
        string.IsNullOrWhiteSpace(_cluesSeleccionada))
    {
        await DisplayAlertAsync(
            "Campos obligatorios",
            "Indica lote y CLUES.",
            "Aceptar");
        return;
    }

    if (!await DisplayAlertAsync(
        "Confirmar prueba local",
        "¿Confirmas que se trata de un paciente ficticio y que deseas guardar esta dosis solamente en SQLite?",
        "Guardar", "Cancelar"))
        return;

    _guardar.IsEnabled = false;

    try
    {
        if (await _registro.ExisteDosisPendienteAsync(_pacienteId, _vacunaId, _numeroDosis))
        {
            await DisplayAlertAsync("Dosis pendiente", "Esta dosis ya tiene un registro local pendiente. No se duplicó.", "Aceptar");
            return;
        }
        var dosis = new VacunaAplicadaLocal
        {
            Id = Guid.NewGuid(),
            PacienteId = _pacienteId,
            VacunaId = _vacunaId,
            VacunaNombre = _vacuna,
            NumeroDosis = _numeroDosis,
            FechaAplicacion = _fecha.Date ?? DateTime.Today,

            // CORRECCIÓN: guardar el lote capturado
            Lote = _lote.Text.Trim(),

            Laboratorio = _laboratorio.Text?.Trim() ?? "",
            UnidadMedicaClues = _cluesSeleccionada!.Trim().ToUpperInvariant(),
            Observaciones = _observaciones.Text?.Trim() ?? "",
            Sincronizado = false
        };

        var operacionId = await _registro.GuardarDosisAsync(dosis);
        var pendientes = await _registro.PendientesAsync();

        await DisplayAlertAsync(
            "Guardado local correcto",
            $"Dosis local: {dosis.Id}\n" +
            $"Operación: {operacionId}\n" +
            $"Pendientes: {pendientes}\n\n" +
            "No se envió al servidor.",
            "Aceptar");

        await Navigation.PopModalAsync();
    }
    catch (Exception ex)
    {
        await DisplayAlertAsync(
            "Error al guardar",
            ex.Message,
            "Aceptar");
    }
    finally
    {
        _guardar.IsEnabled = true;
    }
}

}
