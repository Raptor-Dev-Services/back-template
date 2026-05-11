using Common.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Base;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IMediator Mediator;

    protected BaseApiController(IMediator mediator) => Mediator = mediator;
}
