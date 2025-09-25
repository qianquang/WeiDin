using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _userService = userService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        try
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用户注册时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "用户登录时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromBody] Guid userId)
    {
        try
        {
            await _userService.SetOnlineStatusAsync(userId, false);
            return Ok("退出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用户退出时发生错误，用户ID: {UserId}", userId);
            return StatusCode(500, "服务器内部错误");
        }
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



