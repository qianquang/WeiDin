using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace WeiDin.Application.Services;

public class UserService : ApplicationService, IUserService
{
    private readonly IRepository<User, Guid> _userRepository;
    private readonly IMapper _mapper;

    public UserService(IRepository<User, Guid> userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.FindAsync(id);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        var user = await _userRepository.FirstOrDefaultAsync(u => u.Username == username);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        var user = await _userRepository.FirstOrDefaultAsync(u => u.Email == email);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _userRepository.GetListAsync();
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto createUserDto)
    {
        // 检查用户名、邮箱和手机号是否已存在
        if (await ExistsByUsernameAsync(createUserDto.Username))
            throw new InvalidOperationException("用户名已存在");

        if (await ExistsByEmailAsync(createUserDto.Email))
            throw new InvalidOperationException("邮箱已存在");

        if (await ExistsByPhoneNumberAsync(createUserDto.PhoneNumber))
            throw new InvalidOperationException("手机号已被注册");

        var user = new User
        {
            Username = createUserDto.Username,
            Email = createUserDto.Email,
            PhoneNumber = createUserDto.PhoneNumber,
            PasswordHash = HashPassword(createUserDto.Password),
            Nickname = createUserDto.Nickname,
            Bio = createUserDto.Bio
        };

        await _userRepository.InsertAsync(user, autoSave: true);
        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto updateUserDto)
    {
        var user = await _userRepository.FindAsync(id);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Nickname = updateUserDto.Nickname ?? user.Nickname;
        user.Avatar = updateUserDto.Avatar ?? user.Avatar;
        user.Bio = updateUserDto.Bio ?? user.Bio;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, autoSave: true);

        return _mapper.Map<UserDto>(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _userRepository.FindAsync(id);
        if (user == null)
            return false;

        await _userRepository.DeleteAsync(user);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid id, ChangePasswordDto changePasswordDto)
    {
        var user = await _userRepository.FindAsync(id);
        if (user == null)
            return false;

        if (!VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(changePasswordDto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, autoSave: true);
        return true;
    }

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await _userRepository.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return false;

        return VerifyPassword(password, user.PasswordHash);
    }

    public async Task<bool> SetOnlineStatusAsync(Guid id, bool isOnline)
    {
        var user = await _userRepository.FindAsync(id);
        if (user == null)
            return false;

        user.IsOnline = isOnline;
        user.LastSeen = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, autoSave: true);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _userRepository.AnyAsync(u => u.Id == id);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _userRepository.AnyAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _userRepository.AnyAsync(u => u.Email == email);
    }

    public async Task<bool> ExistsByPhoneNumberAsync(string phoneNumber)
    {
        return await _userRepository.AnyAsync(u => u.PhoneNumber == phoneNumber);
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



