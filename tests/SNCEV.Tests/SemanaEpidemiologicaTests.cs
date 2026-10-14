// =============================================================================
// SNCEV Tests — Pruebas Unitarias de Semana Epidemiológica Mexicana (SINAVE/InDRE)
// =============================================================================

using SNCEV.Jobs.Services;
using Xunit;

namespace SNCEV.Tests;

public class SemanaEpidemiologicaTests
{
    [Theory]
    [InlineData(2026, 1, 1, 2025, 53)]   // 1 ene 2026 -> SE-53/2025
    [InlineData(2026, 1, 4, 2026, 1)]    // 4 ene 2026 -> SE-01/2026 (primer domingo)
    [InlineData(2026, 7, 6, 2026, 27)]   // 6 jul 2026 -> SE-27/2026
    [InlineData(2026, 12, 26, 2026, 51)] // 26 dic 2026 -> SE-51/2026
    [InlineData(2025, 12, 28, 2025, 53)] // 28 dic 2025 -> SE-53/2025 (2025 tiene 53 semanas)
    public void FechaASemanaEpi_RetornaValoresOficialesSINAVE(int y, int m, int d, int expAnio, int expSemana)
    {
        var fecha = new DateTime(y, m, d);
        var (anio, semana) = SemanaEpidemiologicaService.FechaASemanaEpi(fecha);

        Assert.Equal(expAnio, anio);
        Assert.Equal(expSemana, semana);
    }

    [Fact]
    public void TotalSemanasAnio_CalculaCorrectamente52y53Semanas()
    {
        Assert.Equal(53, SemanaEpidemiologicaService.TotalSemanasAnio(2025));
        Assert.Equal(52, SemanaEpidemiologicaService.TotalSemanasAnio(2026));
    }

    [Fact]
    public void SemanaEpiARango_RetornaDomingoASabadoCorrectamente()
    {
        var (inicio, fin) = SemanaEpidemiologicaService.SemanaEpiARango(2026, 27);
        Assert.Equal(DayOfWeek.Sunday, inicio.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, fin.DayOfWeek);
        Assert.Equal(new DateTime(2026, 7, 5), inicio);
        Assert.Equal(new DateTime(2026, 7, 11), fin);
    }
}
