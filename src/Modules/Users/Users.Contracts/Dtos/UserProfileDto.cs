namespace Users.Contracts.Dtos;

public sealed record UserProfileDto(
    Guid     PublicId,
    string   FullName,
    bool     IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
