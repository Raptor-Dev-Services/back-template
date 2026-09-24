using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.BootstrapTenant.Responses;

public abstract record BootstrapTenantResponse : IResponse;

public sealed record BootstrapTenantSuccess(BootstrapResultDto Data) : BootstrapTenantResponse, ISuccess<BootstrapResultDto>;

/// <summary>No hay secreto configurado: el bootstrap esta apagado (403, para que el operador sepa que falta).</summary>
public sealed record BootstrapTenantDisabledFailure(string Message) : BootstrapTenantResponse, IForbiddenFailure;

public sealed record BootstrapTenantInvalidSecretFailure(string Message) : BootstrapTenantResponse, IUnauthorizedFailure;

public sealed record BootstrapTenantValidationFailure(string Message) : BootstrapTenantResponse, IValidationFailure;

/// <summary>El tenant ya tiene usuarios, o el correo ya existe: el bootstrap no reemplaza a nadie.</summary>
public sealed record BootstrapTenantConflictFailure(string Message) : BootstrapTenantResponse, IConflictFailure;
