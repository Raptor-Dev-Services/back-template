using Common.Messaging;
using Common.Results;

namespace Tenancy.Application.UseCases.Files.GetFileUrls.Responses;

public abstract record GetFileUrlsResponse : IResponse;

public sealed record GetFileUrlsSuccess(FileUrlsDto Data) : GetFileUrlsResponse, ISuccess<FileUrlsDto>;

public sealed record GetFileUrlsValidationFailure(string Message) : GetFileUrlsResponse, IValidationFailure;
