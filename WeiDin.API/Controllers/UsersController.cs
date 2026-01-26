using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
public class UsersController : AbpControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
            return NotFound("用户不存在");

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto createUserDto)
    {
        var user = await _userService.CreateAsync(createUserDto);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, UpdateUserDto updateUserDto)
    {
        var user = await _userService.UpdateAsync(id, updateUserDto);
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        var result = await _userService.DeleteAsync(id);
        if (!result)
            return NotFound("用户不存在");

        return NoContent();
    }

    [HttpPost("{id}/change-password")]
    public async Task<ActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordDto changePasswordDto)
    {
        var result = await _userService.ChangePasswordAsync(id, changePasswordDto);
        if (!result)
            return BadRequest("密码修改失败");

        return Ok("密码修改成功");
    }

    [HttpPost("{id}/online-status")]
    public async Task<ActionResult> SetOnlineStatus(Guid id, [FromBody] bool isOnline)
    {
        var result = await _userService.SetOnlineStatusAsync(id, isOnline);
        if (!result)
            return NotFound("用户不存在");

        return Ok("状态更新成功");
    }
}



