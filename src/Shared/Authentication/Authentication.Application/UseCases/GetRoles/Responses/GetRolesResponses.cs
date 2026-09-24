using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.GetRoles.Responses;

public abstract record GetRolesResponse : IResponse;

public sealed record GetRolesSuccess(IReadOnlyList<RoleDto> Data) : GetRolesResponse, ISuccess<IReadOnlyList<RoleDto>>;
