namespace Authentication.Presentation.RequestBodies;

public sealed record RegisterBody(
    string Email,
    string Password,
    long   TenantId,
    long   BranchId,
    string FullName,
    string Role = "User");
