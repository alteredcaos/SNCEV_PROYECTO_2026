using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

// Pantalla temporal de diagnóstico: descargar con red y buscar sin red.
public sealed class DiagnosticoCluesPage : ContentPage
{
    private readonly CatalogoCluesSyncService _sync;
    private readonly Label _estado = new() { Text = "Sin verificar" };
    private readonly Entry _buscar = new() { Placeholder = "Buscar CLUES, nombre o municipio" };
    private readonly VerticalStackLayout _resultados = new() { Spacing = 6 };
    private readonly Button _descargar = new() { Text = "Descargar catálogo desde API" };



    public DiagnosticoCluesPage(CatalogoCluesSyncService sync)
    {
        _sync = sync;
        Title = "Prueba CLUES offline";

        // NUEVO: Botón Cerrar en la barra superior
        var cerrar = new ToolbarItem
        {
            Text = "Cerrar"
        };

        cerrar.Clicked += async (_, _) =>
            await Navigation.PopModalAsync();

        ToolbarItems.Add(cerrar);

        // Código original: se conserva
        _descargar.Clicked += DescargarClicked;

        _buscar.TextChanged += async (_, _) =>
            await MostrarLocalesAsync();

        var verLocal = new Button
        {
            Text = "Consultar SQLite (sin internet)"
        };

        verLocal.Clicked += async (_, _) =>
            await MostrarLocalesAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18),
                Spacing = 12,
                Children =
            {
                new Label
                {
                    Text = "Catálogo CLUES — diagnóstico"
                },
                _descargar,
                verLocal,
                _estado,
                _buscar,
                _resultados
            }
            }
        };
    }



    private async void DescargarClicked(object? sender, EventArgs e)
    {
        _descargar.IsEnabled = false;
        try
        {
            var n = await _sync.ActualizarAsync();
            _estado.Text = $"Descarga y reemplazo SQLite correctos: {n} unidades";
            await MostrarLocalesAsync();
        }
        catch (Exception ex)
        {
            _estado.Text = $"Error de descarga (caché conservado): {ex.Message}";
        }
        finally { _descargar.IsEnabled = true; }
    }

    private async Task MostrarLocalesAsync()
    {
        try
        {
            var all = await _sync.ObtenerLocalesAsync();
            var q = (_buscar.Text ?? "").Trim();
            var matches = all.Where(x =>
                string.IsNullOrEmpty(q) ||
                x.Clues.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.NombreUnidad.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Municipio.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(30).ToList();
            _resultados.Children.Clear();
            foreach (var x in matches)
                _resultados.Children.Add(new Label { Text = $"{x.NombreUnidad} | {x.Clues} | {x.Municipio}" });
            _estado.Text = $"SQLite: {all.Count} unidades; coincidencias visibles: {matches.Count} (máx. 30)";
        }
        catch (Exception ex) { _estado.Text = "Error leyendo SQLite: " + ex.Message; }
    }
}
