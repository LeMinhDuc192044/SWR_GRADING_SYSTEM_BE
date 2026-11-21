using Application.DTOs.AuthDTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.ToActionResult(this, 200, "Login successful.");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!_env.IsDevelopment() && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Failure(StatusCodes.Status403Forbidden, "Chỉ quản trị viên mới có quyền tạo tài khoản.", "FORBIDDEN"));
        }

        var result = await _authService.RegisterAsync(request, ct);
        return result.ToActionResult(this, StatusCodes.Status201Created, "Registration successful.");
    }

    [HttpGet("monitors")]
    [Authorize(Roles = "Admin,Examiner,Manager")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _authService.GetAllAsync(ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data!))
            : StatusCode(StatusCodes.Status400BadRequest,
                ApiResponse.Failure(StatusCodes.Status400BadRequest, result.Error ?? "Request failed."));
    }
}

