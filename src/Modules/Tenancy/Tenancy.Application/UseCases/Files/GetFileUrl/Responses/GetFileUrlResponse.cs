using Common.Messaging;
using Common.Results;

namespace Tenancy.Application.UseCases.Files.GetFileUrl.Responses;

public abstract record GetFileUrlResponse : IResponse;

public sealed record GetFileUrlSuccess(FileUrlDto Data) : GetFileUrlResponse, ISuccess<FileUrlDto>;

public sealed record GetFileUrlNotFoundFailure(string Message) : GetFileUrlResponse, INotFoundFailure;
