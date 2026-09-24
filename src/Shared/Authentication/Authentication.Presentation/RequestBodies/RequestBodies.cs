namespace Authentication.Presentation.RequestBodies;

// Los cuerpos llevan SOLO lo que el cliente puede decidir. El tenant, el actor y los permisos salen del JWT;
// un cuerpo que los aceptara (como el antiguo RegisterBody con TenantId y Role) es una escalada de privilegios.

public sealed record LoginBody(string Email, string Password);

public sealed record RefreshBody(string RefreshToken);

public sealed record LogoutBody(string RefreshToken);

public sealed record ForgotPasswordBody(string Email);

public sealed record ResetPasswordBody(string Token, string NewPassword);

public sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);

public sealed record BootstrapTenantBody(
    string TenantName,
    string TenantSlug,
    string AdminEmail,
    string AdminPassword,
    string AdminFullName);

public sealed record InviteUserBody(string Email, string FullName, IReadOnlyList<string>? RoleCodes);
