// =============================================================================
// SNCEV Mobile App — DashboardCoberturasViewModel (App 2 Directiva)
// =============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SNCEV.MobileApp.ViewModels;

public class CoberturaKpi
{
    public string Titulo { get; set; } = string.Empty;
    public decimal Porcentaje { get; set; }
    public string SemaforoColor { get; set; } = "Green"; // Green, Yellow, Red
}

public partial class DashboardCoberturasViewModel : ObservableObject
{
    [ObservableProperty]
    private string _estadoSeleccionado = "Nacional";

    [ObservableProperty]
    private int _semanaActual = 27;

    public ObservableCollection<CoberturaKpi> Kpis { get; } = new();

    public DashboardCoberturasViewModel()
    {
        CargarKpis();
    }

    [RelayCommand]
    public void CargarKpis()
    {
        Kpis.Clear();
        Kpis.Add(new CoberturaKpi { Titulo = "BCG (Recién Nacidos)", Porcentaje = 97.4m, SemaforoColor = "Green" });
        Kpis.Add(new CoberturaKpi { Titulo = "Hexavalente (1ra Dosis)", Porcentaje = 92.1m, SemaforoColor = "Yellow" });
        Kpis.Add(new CoberturaKpi { Titulo = "SRP (1 año)", Porcentaje = 84.5m, SemaforoColor = "Red" });
        Kpis.Add(new CoberturaKpi { Titulo = "Neumocócica Conjugada", Porcentaje = 96.0m, SemaforoColor = "Green" });
    }
}
