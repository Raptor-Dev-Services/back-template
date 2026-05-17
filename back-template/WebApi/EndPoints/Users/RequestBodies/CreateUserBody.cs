namespace WebApi.EndPoints.Users.RequestBodies;

public sealed record CreateUserBody(string FullName, string Email, string Password, string Role);
