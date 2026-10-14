// =============================================================================
// SNCEV API — Servicio de Cifrado AES-256-GCM para Datos Sensibles del Paciente
// Estándar: NOM-024-SSA3-2012
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SNCEV.Api.Services;

public class AesEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration config)
    {
        string rawKey = config["Security:AesMasterKey"] ?? "SNCEV_2026_MASTER_SECRET_KEY_AES256_32BYTES!";
        _key = Encoding.UTF8.GetBytes(rawKey.PadRight(32).Substring(0, 32));
    }

    public byte[] Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return Array.Empty<byte>();

        byte[] nonce = new byte[12]; // 96 bits GCM nonce
        RandomNumberGenerator.Fill(nonce);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[16]; // 128 bits auth tag

        using var aesGcm = new AesGcm(_key, tagSizeInBytes: 16);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Concatenar: nonce (12B) + tag (16B) + ciphertext
        byte[] result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);

        return result;
    }

    public string Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length < 28) return string.Empty;

        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        byte[] cipherBytes = new byte[encryptedData.Length - 28];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, 12);
        Buffer.BlockCopy(encryptedData, 12, tag, 0, 16);
        Buffer.BlockCopy(encryptedData, 28, cipherBytes, 0, cipherBytes.Length);

        byte[] plainBytes = new byte[cipherBytes.Length];
        using var aesGcm = new AesGcm(_key, tagSizeInBytes: 16);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
