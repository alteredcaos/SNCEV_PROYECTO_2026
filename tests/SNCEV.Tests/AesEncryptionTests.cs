// =============================================================================
// SNCEV Tests — Pruebas Unitarias de Cifrado AES-256-GCM
// =============================================================================

using Microsoft.Extensions.Configuration;
using SNCEV.Api.Services;
using Xunit;

namespace SNCEV.Tests;

public class AesEncryptionTests
{
    private readonly AesEncryptionService _aesService;

    public AesEncryptionTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Security:AesMasterKey", "TEST_MASTER_KEY_32BYTES_LONG_12345!"}
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _aesService = new AesEncryptionService(config);
    }

    [Fact]
    public void CifradoYDescifrado_MantieneIntegridadDeTextoPlano()
    {
        string curpOriginal = "ABCD123456HDFRRR09";
        byte[] cifrado = _aesService.Encrypt(curpOriginal);

        Assert.NotEmpty(cifrado);

        string deschifrado = _aesService.Decrypt(cifrado);
        Assert.Equal(curpOriginal, deschifrado);
    }
}
