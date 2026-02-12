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
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
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
            // 处理业务逻辑错误（用户名已存在、邮箱已存在、手机号已存在等）
            return BadRequest(new { message = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            // 处理数据库约束异常（唯一索引冲突等）
            var innerException = ex.InnerException?.Message ?? "";
            
            string errorMessage = "注册失败";
            if (innerException.Contains("Username") || innerException.Contains("IX_Users_Username"))
            {
                errorMessage = "用户名已存在";
            }
            else if (innerException.Contains("Email") || innerException.Contains("IX_Users_Email"))
            {
                errorMessage = "邮箱已被注册";
            }
            else if (innerException.Contains("PhoneNumber") || innerException.Contains("IX_Users_PhoneNumber"))
            {
                errorMessage = "手机号已被注册";
            }
            
            return BadRequest(new { message = errorMessage });
        }
        catch (Exception)
        {
            // 处理其他未知错误
            return StatusCode(500, new { message = "注册失败，请稍后重试" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        var isValid = await _userService.ValidatePasswordAsync(loginDto.Username, loginDto.Password);
        if (!isValid)
            return Unauthorized(new { message = "用户名或密码错误" });

        var user = await _userService.GetByUsernameAsync(loginDto.Username);
        if (user == null)
            return Unauthorized(new { message = "用户不存在" });

        // 在线状态由 ChatHub.OnConnectedAsync 在 SignalR 连接建立时自动设置，无需在此处理

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
        // 在线状态由 ChatHub.OnDisconnectedAsync 在 SignalR 断开时自动设置为离线，无需在此处理
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
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("nickname")]
    public string? Nickname { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("bio")]
    public string? Bio { get; set; }
}

public class AuthResponseDto
{
    public UserDto User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
}



