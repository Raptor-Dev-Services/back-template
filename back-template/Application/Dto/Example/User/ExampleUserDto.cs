namespace Application.Dto.Example.User;

public sealed record ExampleUserDto(
    Guid     UserId,
    string   FullName,
    string   Email,
    string   Department,
    string   Notes,
    bool     IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
