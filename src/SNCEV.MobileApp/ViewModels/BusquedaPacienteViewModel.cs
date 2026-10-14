// =============================================================================
// SNCEV Mobile App — ViewModels App 1 & App 2
// =============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SNCEV.MobileApp.Models;
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.ViewModels;

public partial class BusquedaPacienteViewModel : ObservableObject
{
    private readonly DatabaseHelper _db = DatabaseHelper.Instance;

    [ObservableProperty]
    private string _criterioBusqueda = string.Empty;

    [ObservableProperty]
    private bool _estaCargando;

    public ObservableCollection<PacienteLocal> Pacientes { get; } = new();

    [RelayCommand]
    public async Task BuscarAsync()
    {
        EstaCargando = true;
        Pacientes.Clear();

        var lista = await _db.ObtenerPacientesAsync(CriterioBusqueda);
        foreach (var p in lista)
        {
            Pacientes.Add(p);
        }

        EstaCargando = false;
    }
}
