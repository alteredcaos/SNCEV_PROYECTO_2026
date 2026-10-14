
using System.Globalization;
using System.Text.RegularExpressions;

namespace SNCEV.MobileApp.Services;

public sealed record DatosCurpQr1(
    string Curp,
    string Nombre,
    string PrimerApellido,
    string SegundoApellido,
    string Sexo,
    DateTime FechaNacimiento,
    string EntidadNacimiento,
    string ClaveEntidad
);

public static class CurpQrParser1
{
    private static readonly Regex FormatoCurp = new(
        @"^[A-Z][AEIOUX][A-Z]{2}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(
        string? contenido,
        out DatosCurpQr1? datos)
    {
        datos = null;

        if (string.IsNullOrWhiteSpace(contenido))
            return false;

        var campos = contenido.Split('|');

        if (campos.Length < 9)
            return false;

        var curp = campos[0].Trim().ToUpperInvariant();

        if (!FormatoCurp.IsMatch(curp))
            return false;

        if (!string.IsNullOrWhiteSpace(campos[1]))
            return false;

        if (!DateTime.TryParseExact(
                campos[6].Trim(),
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var nacimiento))
            return false;

        datos = new DatosCurpQr1(
            Curp: curp,
            Nombre: campos[4].Trim(),
            PrimerApellido: campos[2].Trim(),
            SegundoApellido: campos[3].Trim(),
            Sexo: campos[5].Trim(),
            FechaNacimiento: nacimiento,
            EntidadNacimiento: campos[7].Trim(),
            ClaveEntidad: campos[8].Trim()
        );

        return true;
    }
}
