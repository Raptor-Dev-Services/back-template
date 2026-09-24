namespace Authentication.Presentation.RequestBodies;

public sealed record RegisterBody(
    string Email,
    string Password,
    long   TenantId,
    string FullName,
    string Role = "User");
