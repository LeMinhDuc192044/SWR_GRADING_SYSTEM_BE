using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Health check endpoint — dùng cho load balancer / monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        server = "Backend.Server1",
        time = DateTime.UtcNow
    });
}
