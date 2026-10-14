using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

/// <summary>Único punto de entrada para captura nominal offline v2.</summary>
public sealed class OfflineRegistroService
{
    private readonly SyncV2Outbox _outbox;
    public OfflineRegistroService(SyncV2Outbox outbox) => _outbox = outbox;

    public Task InicializarAsync() => _outbox.InicializarAsync();
    public Task<Guid> GuardarPacienteAsync(PacienteLocal paciente) => _outbox.GuardarPacienteAsync(paciente);
    public Task<Guid> GuardarDosisAsync(VacunaAplicadaLocal dosis) => _outbox.GuardarDosisAsync(dosis);
    public Task<List<VacunaAplicadaLocal>> ObtenerDosisPendientesAsync(Guid pacienteId)
        => _outbox.ObtenerDosisPendientesAsync(pacienteId);
    public Task<bool> ExisteDosisPendienteAsync(Guid pacienteId, int vacunaId, int numeroDosis)
        => _outbox.ExisteDosisPendienteAsync(pacienteId, vacunaId, numeroDosis);
    public Task<int> PendientesAsync() => _outbox.ContarPendientesAsync();
}
