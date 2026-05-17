namespace WebApi.EndPoints.Auth.RequestBodies;

public sealed record LoginBody(string Email, string Password);
