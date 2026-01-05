using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
public class AuthController : AbpControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public AuthController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        var createUserDto = new CreateUserDto
        {
            Username = registerDto.Username,
            Email = registerDto.Email,
            PhoneNumber = registerDto.PhoneNumber,
            Password = registerDto.Password,
            Nickname = registerDto.Nickname,
            Bio = registerDto.Bio
        };

        var user = await _userService.CreateAsync(createUserDto);
        var token = GenerateJwtToken(user);

        return Ok(new AuthResponseDto
        {
            User = user,
            Token = token
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        var isValid = await _userService.ValidatePasswordAsync(loginDto.Username, loginDto.Password);
        if (!isValid)
            return Unauthorized("用户名或密码错误");

        var user = await _userService.GetByUsernameAsync(loginDto.Username);
        if (user == null)
            return Unauthorized("用户不存在");

        // 更新在线状态
        await _userService.SetOnlineStatusAsync(user.Id, true);

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponseDto
        {
            User = user,
            Token = token
        });
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromBody] Guid userId)
    {
        await _userService.SetOnlineStatusAsync(userId, false);
        return Ok("退出成功");
    }

    private string GenerateJwtToken(UserDto user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        var issuer = jwtSettings["Issuer"] ?? "WeiDin";
        var audience = jwtSettings["Audience"] ?? "WeiDinUsers";
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("nickname", user.Nickname ?? user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Bio { get; set; }
}

public class AuthResponseDto
{
    public UserDto User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
}



