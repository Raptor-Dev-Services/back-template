using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;

namespace Authentication.Application.Sessions;

/// <summary>
/// Verifica un segundo factor contra una credencial con 2FA ACTIVO: un codigo TOTP (con anti-replay) o, si no
/// lo es, un codigo de recuperacion (que se consume). Lo comparten el segundo paso del login y la desactivacion
/// del 2FA. Modifica la credencial o el codigo usado pero NO guarda.
/// </summary>
internal sealed class SecondFactor(
    ITotpService totp,
    ISecretProtector protector,
    IRecoveryCodes recoveryCodes,
    ITwoFactorRecoveryCodeRepository codes)
{
    public async Task<bool> VerifyAsync(UserCredential credential, string code, CancellationToken cancellationToken)
    {
        if (!credential.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(credential.TotpSecretProtected) || string.IsNullOrWhiteSpace(code))
            return false;

        var secret = protector.TryUnprotect(credential.TotpSecretProtected);
        if (secret is not null && totp.VerifyStep(secret, code, credential.LastTotpStep) is { } step)
        {
            // Anti-replay: el paso queda consumido; el mismo codigo reintentado ya no lo supera.
            credential.LastTotpStep = step;
            return true;
        }

        var hash = recoveryCodes.Hash(code);
        var match = (await codes.ListUsableAsync(credential.Id, cancellationToken)).FirstOrDefault(c => c.CodeHash == hash);
        if (match is null)
            return false;

        match.Consume(DateTime.UtcNow);
        return true;
    }
}
