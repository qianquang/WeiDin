using Microsoft.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户列表时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound("用户不存在");

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户信息时发生错误，用户ID: {UserId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto createUserDto)
    {
        try
        {
            var user = await _userService.CreateAsync(createUserDto);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建用户时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, UpdateUserDto updateUserDto)
    {
        try
        {
            var user = await _userService.UpdateAsync(id, updateUserDto);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户信息时发生错误，用户ID: {UserId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        try
        {
            var result = await _userService.DeleteAsync(id);
            if (!result)
                return NotFound("用户不存在");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除用户时发生错误，用户ID: {UserId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/change-password")]
    public async Task<ActionResult> ChangePassword(Guid id, ChangePasswordDto changePasswordDto)
    {
        try
        {
            var result = await _userService.ChangePasswordAsync(id, changePasswordDto);
            if (!result)
                return BadRequest("密码修改失败");

            return Ok("密码修改成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码时发生错误，用户ID: {UserId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/online-status")]
    public async Task<ActionResult> SetOnlineStatus(Guid id, [FromBody] bool isOnline)
    {
        try
        {
            var result = await _userService.SetOnlineStatusAsync(id, isOnline);
            if (!result)
                return NotFound("用户不存在");

            return Ok("状态更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新在线状态时发生错误，用户ID: {UserId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }
}



