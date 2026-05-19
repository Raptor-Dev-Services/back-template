using Common.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Shared.Web;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IMediator Mediator;
    protected BaseApiController(IMediator mediator) => Mediator = mediator;
}
