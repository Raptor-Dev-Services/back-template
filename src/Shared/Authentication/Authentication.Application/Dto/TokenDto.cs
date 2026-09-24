namespace Authentication.Application.Dto;

public sealed record TokenDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
