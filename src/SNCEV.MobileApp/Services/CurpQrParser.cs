
using System.Globalization;
using System.Text.RegularExpressions;

namespace SNCEV.MobileApp.Services;

public sealed record DatosCurpQr(
    string Curp,
    string Nombre,
    string PrimerApellido,
    string SegundoApellido,
    string Sexo,
    DateTime FechaNacimiento,
    string EntidadNacimiento,
    string ClaveEntidad
);

public static class CurpQrParser
{
    private static readonly Regex CurpRegex = new(
        @"^[A-Z][AEIOUX][A-Z]{2}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(
        string? contenido,
        out DatosCurpQr? datos)
    {
        datos = null;

        if (string.IsNullOrWhiteSpace(contenido))
            return false;

        var campos = contenido.Split('|');

        if (campos.Length < 9 ||
            !string.IsNullOrWhiteSpace(campos[1]))
            return false;

        string curp = campos[0].Trim().ToUpperInvariant();

        if (!CurpRegex.IsMatch(curp))
            return false;

        if (!DateTime.TryParseExact(
            campos[6].Trim(),
            "dd/MM/yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime fecha))
            return false;

        if (string.IsNullOrWhiteSpace(campos[2]) ||
            string.IsNullOrWhiteSpace(campos[4]))
            return false;

        datos = new DatosCurpQr(
            curp,
            campos[4].Trim(),
            campos[2].Trim(),
            campos[3].Trim(),
            campos[5].Trim(),
            fecha,
            campos[7].Trim(),
            campos[8].Trim()
        );

        return true;
    }
}
