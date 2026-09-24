using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.Files.UploadFile.Responses;

public abstract record UploadFileResponse : IResponse;

public sealed record UploadFileSuccess(UploadedFileDto Data) : UploadFileResponse, ISuccess<UploadedFileDto>;

public sealed record UploadFileValidationFailure(string Message) : UploadFileResponse, IValidationFailure;

public sealed record UploadFileForbiddenFailure(string Message) : UploadFileResponse, IForbiddenFailure;
