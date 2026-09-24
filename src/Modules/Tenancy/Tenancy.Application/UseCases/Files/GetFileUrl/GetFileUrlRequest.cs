using Common.Messaging;
using Tenancy.Application.UseCases.Files.GetFileUrl.Responses;

namespace Tenancy.Application.UseCases.Files.GetFileUrl;

/// <summary>URL prefirmada de lectura para una clave del tenant del token.</summary>
public sealed record GetFileUrlRequest(string ObjectKey) : IRequest<GetFileUrlResponse>;
