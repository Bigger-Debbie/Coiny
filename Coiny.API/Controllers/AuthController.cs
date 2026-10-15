using Coiny.API.DTOs.Auth;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Coiny.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var token = _tokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email ?? string.Empty
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        return Ok(new CurrentUserResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return Unauthorized("Invalid email or password");

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: false);

        if (!result.Succeeded)
            return Unauthorized("Invalid email or password");

        var token = _tokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email ?? string.Empty
        });
    }
}
