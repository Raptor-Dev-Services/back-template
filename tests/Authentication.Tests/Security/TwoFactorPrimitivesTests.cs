using Authentication.Domain.Abstractions;
using Authentication.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OtpNet;
using Xunit;

namespace Authentication.Tests.Security;

public sealed class TwoFactorPrimitivesTests
{
    private static readonly TotpOptions Options = new() { EncryptionKey = "unit-tests-totp-key-0123456789abcdef-0123" };

    [Fact]
    public void El_secreto_cifrado_se_recupera_y_cada_cifrado_es_distinto()
    {
        var protector = new AesSecretProtector(Options, NullLogger<AesSecretProtector>.Instance);

        var a = protector.Protect("JBSWY3DPEHPK3PXP");
        var b = protector.Protect("JBSWY3DPEHPK3PXP");

        Assert.NotEqual(a, b); // nonce aleatorio: el mismo secreto no deja la misma huella
        Assert.Equal("JBSWY3DPEHPK3PXP", protector.TryUnprotect(a));
    }

    [Fact]
    public void Un_cifrado_manipulado_o_con_otra_clave_no_se_descifra()
    {
        var protector = new AesSecretProtector(Options, NullLogger<AesSecretProtector>.Instance);
        var other = new AesSecretProtector(new TotpOptions { EncryptionKey = "otra-clave-completamente-distinta-0123456789" }, NullLogger<AesSecretProtector>.Instance);
        var protectedValue = protector.Protect("JBSWY3DPEHPK3PXP");

        var bytes = Convert.FromBase64String(protectedValue);
        bytes[^1] ^= 0x01;

        Assert.Null(protector.TryUnprotect(Convert.ToBase64String(bytes)));
        Assert.Null(other.TryUnprotect(protectedValue));
        Assert.Null(protector.TryUnprotect("esto-no-es-base64!"));
    }

    [Fact]
    public void Sin_clave_de_cifrado_no_arranca()
    {
        Assert.Throws<InvalidOperationException>(() => new AesSecretProtector(new TotpOptions(), NullLogger<AesSecretProtector>.Instance));
    }

    [Fact]
    public void El_mismo_paso_TOTP_no_se_acepta_dos_veces()
    {
        var totp = new TotpService(Options, NullLogger<TotpService>.Instance);
        var secret = totp.GenerateSecret();
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);

        var step = totp.VerifyStep(secret, code, minStepExclusive: 0);

        Assert.NotNull(step);
        Assert.Null(totp.VerifyStep(secret, code, minStepExclusive: step!.Value));
        Assert.Null(totp.VerifyStep(secret, "000000x", 0));
        Assert.Null(totp.VerifyStep("no-es-base32-!!", code, 0));
    }

    [Fact]
    public void Los_codigos_de_recuperacion_se_comparan_sin_importar_guion_ni_mayusculas()
    {
        var codes = new RecoveryCodes();
        var generated = codes.Generate(10);

        Assert.Equal(10, generated.Distinct().Count());
        Assert.All(generated, c => Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", c));
        Assert.Equal(codes.Hash(generated[0]), codes.Hash(" " + generated[0].Replace("-", "").ToLowerInvariant() + " "));
        Assert.DoesNotContain(generated[0], codes.Hash(generated[0]));
    }
}
