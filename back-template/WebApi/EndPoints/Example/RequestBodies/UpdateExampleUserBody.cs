namespace WebApi.EndPoints.Example.RequestBodies;

public sealed record UpdateExampleUserBody(string FullName, string Department, string Notes);
