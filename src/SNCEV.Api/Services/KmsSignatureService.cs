// =============================================================================
// SNCEV API — Servicio de Firma Digital KMS / HSM para Registros Clínicos
// Cumplimiento NOM-024-SSA3-2012
// =============================================================================

using System.Security.Cryptography;
using System.Text;

namespace SNCEV.Api.Services;

public class KmsSignatureService
{
    private readonly RSA _rsa;

    public KmsSignatureService()
    {
        _rsa = RSA.Create(2048);
    }

    public string FirmarRegistroDosis(Guid dosisId, string curp, string vacunaClave, string lote, DateTime fecha)
    {
        string rawData = $"{dosisId}|{curp}|{vacunaClave}|{lote}|{fecha:yyyy-MM-dd}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(rawData);

        byte[] signature = _rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    public bool VerificarFirmaDosis(Guid dosisId, string curp, string vacunaClave, string lote, DateTime fecha, string firmaBase64)
    {
        if (string.IsNullOrEmpty(firmaBase64)) return false;

        string rawData = $"{dosisId}|{curp}|{vacunaClave}|{lote}|{fecha:yyyy-MM-dd}";
        byte[] dataBytes = Encoding.UTF8.GetBytes(rawData);
        byte[] signatureBytes = Convert.FromBase64String(firmaBase64);

        return _rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
