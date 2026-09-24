using Common.Messaging;
using Tenancy.Application.UseCases.Files.GetFileUrls.Responses;

namespace Tenancy.Application.UseCases.Files.GetFileUrls;

/// <summary>URLs prefirmadas para varias claves de una vez.</summary>
public sealed record GetFileUrlsRequest(IReadOnlyList<string>? ObjectKeys) : IRequest<GetFileUrlsResponse>;
