namespace Application.Dto.Auth;

public sealed record TokenDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
