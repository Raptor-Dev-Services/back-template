namespace WebApi.EndPoints.Example.RequestBodies;

public sealed record InsertExampleUserBody(string FullName, string Email, string Department, string Notes);
