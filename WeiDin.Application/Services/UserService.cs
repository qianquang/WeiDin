using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace WeiDin.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == username);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto createUserDto)
    {
        // 检查用户名和邮箱是否已存在
        if (await ExistsByUsernameAsync(createUserDto.Username))
            throw new InvalidOperationException("用户名已存在");

        if (await ExistsByEmailAsync(createUserDto.Email))
            throw new InvalidOperationException("邮箱已存在");

        var user = new User
        {
            Username = createUserDto.Username,
            Email = createUserDto.Email,
            PhoneNumber = createUserDto.PhoneNumber,
            PasswordHash = HashPassword(createUserDto.Password),
            Nickname = createUserDto.Nickname,
            Bio = createUserDto.Bio
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto updateUserDto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Nickname = updateUserDto.Nickname ?? user.Nickname;
        user.Avatar = updateUserDto.Avatar ?? user.Avatar;
        user.Bio = updateUserDto.Bio ?? user.Bio;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
            return false;

        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid id, ChangePasswordDto changePasswordDto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
            return false;

        if (!VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(changePasswordDto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return false;

        return VerifyPassword(password, user.PasswordHash);
    }

    public async Task<bool> SetOnlineStatusAsync(Guid id, bool isOnline)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
            return false;

        user.IsOnline = isOnline;
        user.LastSeen = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _unitOfWork.Users.ExistsAsync(u => u.Id == id);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _unitOfWork.Users.ExistsAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _unitOfWork.Users.ExistsAsync(u => u.Email == email);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPassword(string password, string hashedPassword)
    {
        return HashPassword(password) == hashedPassword;
    }
}
