// =============================================================================
// SNCEV Jobs — Servicio de Semana Epidemiológica Mexicana (SINAVE / InDRE)
// Regla NOM: Inicia en domingo. La SE-01 es la primera semana que contiene el
// primer miércoles de enero. Un año epidemiológico tiene 52 o 53 semanas.
// =============================================================================

namespace SNCEV.Jobs.Services;

public static class SemanaEpidemiologicaService
{
    public static DateTime DomingoInicioSE1(int anio)
    {
        DateTime primerDia = new DateTime(anio, 1, 1);
        int diasHastaMiercoles = ((int)DayOfWeek.Wednesday - (int)primerDia.DayOfWeek + 7) % 7;
        DateTime primerMiercoles = primerDia.AddDays(diasHastaMiercoles);
        int diasHastaDomingo = (int)primerMiercoles.DayOfWeek; // Sunday = 0
        return primerMiercoles.AddDays(-diasHastaDomingo);
    }

    public static int TotalSemanasAnio(int anio)
    {
        DateTime d0 = DomingoInicioSE1(anio);
        DateTime d1 = DomingoInicioSE1(anio + 1);
        return (int)(d1 - d0).TotalDays / 7;
    }

    public static (int Anio, int Semana) FechaASemanaEpi(DateTime fecha)
    {
        DateTime f = fecha.Date;
        int[] aniosAProbar = [f.Year, f.Year - 1, f.Year + 1];

        foreach (int anio in aniosAProbar)
        {
            DateTime d0 = DomingoInicioSE1(anio);
            int deltaDias = (int)(f - d0).TotalDays;

            if (deltaDias < 0) continue;

            int semana = (deltaDias / 7) + 1;
            if (semana >= 1 && semana <= TotalSemanasAnio(anio))
            {
                return (anio, semana);
            }
        }

        throw new InvalidOperationException($"No se pudo determinar la Semana Epidemiológica para {fecha:yyyy-MM-dd}");
    }

    public static (DateTime Inicio, DateTime Fin) SemanaEpiARango(int anio, int semana)
    {
        DateTime inicio = DomingoInicioSE1(anio).AddDays((semana - 1) * 7);
        DateTime fin = inicio.AddDays(6);
        return (inicio, fin);
    }

    public static (int Anio, int Semana) SemanaAnterior()
    {
        return FechaASemanaEpi(DateTime.Today.AddDays(-7));
    }

    public static List<(int Anio, int Semana)> SemanasEnRango(DateTime desde, DateTime hasta)
    {
        HashSet<(int, int)> vistas = new();
        List<(int, int)> resultado = new();
        DateTime cursor = desde.Date;

        while (cursor <= hasta.Date)
        {
            var par = FechaASemanaEpi(cursor);
            if (vistas.Add(par))
            {
                resultado.Add(par);
            }
            cursor = cursor.AddDays(7);
        }

        return resultado;
    }
}
