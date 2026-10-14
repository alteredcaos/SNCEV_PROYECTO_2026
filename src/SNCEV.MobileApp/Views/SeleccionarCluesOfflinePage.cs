using SNCEV.MobileApp.Models;
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

public sealed class SeleccionarCluesOfflinePage : ContentPage
{
    private readonly CatalogoCluesSyncService _catalogo;
    private readonly Action<EstablecimientoLocal> _seleccionar;
    private readonly Entry _buscar = new() { Placeholder = "Nombre, clave CLUES o municipio" };
    private readonly VerticalStackLayout _resultados = new() { Spacing = 8 };
    private readonly Label _estado = new() { TextColor = Colors.White };
    private List<EstablecimientoLocal> _unidades = new();

    public SeleccionarCluesOfflinePage(CatalogoCluesSyncService catalogo,
        Action<EstablecimientoLocal> seleccionar)
    {
        _catalogo = catalogo;
        _seleccionar = seleccionar;
        Title = "Seleccionar unidad CLUES";
        BackgroundColor = Color.FromArgb("#101820");
        ToolbarItems.Add(new ToolbarItem("Cerrar", null, async () => await Navigation.PopModalAsync()));
        _buscar.TextChanged += (_, _) => MostrarResultados();
        Content = new VerticalStackLayout
        {
            Padding = new Thickness(16), Spacing = 12,
            Children =
            {
                new Label { Text = "Buscar en catálogo guardado en SQLite (sin internet)", TextColor = Colors.White },
                _buscar, _estado,
                new ScrollView { Content = _resultados, VerticalOptions = LayoutOptions.FillAndExpand }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _unidades = await _catalogo.ObtenerLocalesAsync();
            _estado.Text = _unidades.Count == 0
                ? "No hay unidades descargadas. Descarga el catálogo cuando tengas conexión."
                : $"{_unidades.Count} unidades locales disponibles";
            MostrarResultados();
        }
        catch (Exception ex) { _estado.Text = "Error al consultar SQLite: " + ex.Message; }
    }

    private void MostrarResultados()
    {
        _resultados.Children.Clear();
        var q = (_buscar.Text ?? "").Trim();
        var encontrados = _unidades.Where(x =>
            string.IsNullOrEmpty(q) ||
            x.Clues.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            x.NombreUnidad.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            x.Municipio.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(30).ToList();
        foreach (var unidad in encontrados)
        {
            var item = unidad;
            var boton = new Button
            {
                Text = $"{item.NombreUnidad}\n{item.Clues} · {item.Municipio}",
                BackgroundColor = Color.FromArgb("#263C50"), TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Fill, LineBreakMode = LineBreakMode.WordWrap
            };
            boton.Clicked += async (_, _) =>
            {
                _seleccionar(item);
                await Navigation.PopModalAsync();
            };
            _resultados.Children.Add(boton);
        }
        if (_unidades.Count > 0)
            _estado.Text = $"SQLite: {_unidades.Count} unidades; coincidencias: {encontrados.Count} (máx. 30)";
    }
}
