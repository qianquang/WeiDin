using Volo.Abp.Application.Services;
using WeiDin.Application.DTOs;

namespace WeiDin.Application.Interfaces;

public interface IUserService : IApplicationService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByUsernameAsync(string username);
    Task<UserDto?> GetByEmailAsync(string email);
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto> CreateAsync(CreateUserDto createUserDto);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto updateUserDto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ChangePasswordAsync(Guid id, ChangePasswordDto changePasswordDto);
    Task<bool> ValidatePasswordAsync(string username, string password);
    Task<bool> SetOnlineStatusAsync(Guid id, bool isOnline);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email);
}



