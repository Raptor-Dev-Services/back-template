namespace Application.Dto.Users;

public sealed record UserDto(
    Guid     PublicId,
    string   FullName,
    string   Email,
    string   Role,
    bool     IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
