using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.GetMyAccount.Responses;

public abstract record GetMyAccountResponse : IResponse;

public sealed record GetMyAccountSuccess(AccountDto Data) : GetMyAccountResponse, ISuccess<AccountDto>;

public sealed record GetMyAccountNotFoundFailure(string Message) : GetMyAccountResponse, INotFoundFailure;
