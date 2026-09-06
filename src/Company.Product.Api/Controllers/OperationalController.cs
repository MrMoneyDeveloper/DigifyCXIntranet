using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace Company.Product.Api.Controllers;

[ApiController]
[Route("api/v1/operational")]
public sealed class OperationalController : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        return Ok(new
        {
            Status = "ok",
            UtcNow = DateTime.UtcNow
        });
    }
}
