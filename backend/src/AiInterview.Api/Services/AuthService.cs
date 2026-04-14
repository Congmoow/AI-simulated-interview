using AiInterview.Api.Constants;
using AiInterview.Api.DTOs.Auth;
using AiInterview.Api.DTOs.Common;
using AiInterview.Api.Mappings;
using AiInterview.Api.Middleware;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Options;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AiInterview.Api.Services;

public class AuthService(
    IUserRepository userRepository,
    ICatalogRepository catalogRepository,
    PasswordService passwordService,
    JwtTokenService jwtTokenService,
    IRefreshTokenStore refreshTokenStore,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRegisterRequest(request);

        if (await userRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, "用户名已存在");
        }

        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, "邮箱已存在");
        }

        if (!string.IsNullOrWhiteSpace(request.TargetPosition))
        {
            var position = await catalogRepository.GetPositionByCodeAsync(request.TargetPosition, cancellationToken);
            if (position is null)
            {
                throw new AppException(ErrorCodes.PositionNotFound, "目标岗位不存在");
            }
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = passwordService.HashPassword(request.Password),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            TargetPositionCode = request.TargetPosition,
            Role = AppRoles.User
        };

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return ApplicationMapper.ToRegisterResponse(user);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username.Trim(), cancellationToken);
        if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new AppException(ErrorCodes.InvalidCredentials, "用户名或密码错误", StatusCodes.Status401Unauthorized);
        }

        if (!user.IsActive)
        {
            throw new AppException(ErrorCodes.UserDisabled, "账户已被禁用", StatusCodes.Status403Forbidden);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await userRepository.SaveChangesAsync(cancellationToken);

        var refreshToken = jwtTokenService.CreateRefreshToken();
        await refreshTokenStore.StoreAsync(refreshToken, user.Id, jwtTokenService.GetRefreshTokenExpiry(), cancellationToken);

        return new LoginResponse
        {
            AccessToken = jwtTokenService.CreateAccessToken(user),
            ExpiresIn = _jwtOptions.AccessTokenExpiresMinutes * 60,
            RefreshToken = refreshToken,
            User = ApplicationMapper.ToCurrentUserDto(user)
        };
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppException(ErrorCodes.UserNotFound, "用户不存在", StatusCodes.Status404NotFound);

        return ApplicationMapper.ToCurrentUserDto(user);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppException(ErrorCodes.UserNotFound, "用户不存在", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.TargetPosition))
        {
            var position = await catalogRepository.GetPositionByCodeAsync(request.TargetPosition, cancellationToken);
            if (position is null)
            {
                throw new AppException(ErrorCodes.PositionNotFound, "目标岗位不存在");
            }
        }

        user.Email = string.IsNullOrWhiteSpace(request.Email) ? user.Email : request.Email.Trim();
        user.Phone = request.Phone?.Trim();
        user.TargetPositionCode = request.TargetPosition ?? user.TargetPositionCode;
        user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await userRepository.SaveChangesAsync(cancellationToken);
        var refreshedUser = await userRepository.GetByIdAsync(userId, cancellationToken) ?? user;
        return ApplicationMapper.ToCurrentUserDto(refreshedUser);
    }

    public async Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await refreshTokenStore.GetUserIdAsync(request.RefreshToken, cancellationToken);
        if (!userId.HasValue)
        {
            throw new AppException(ErrorCodes.InvalidRefreshToken, "刷新令牌无效", StatusCodes.Status401Unauthorized);
        }

        var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken)
            ?? throw new AppException(ErrorCodes.UserNotFound, "用户不存在", StatusCodes.Status404NotFound);

        return new RefreshTokenResponse
        {
            AccessToken = jwtTokenService.CreateAccessToken(user),
            ExpiresIn = _jwtOptions.AccessTokenExpiresMinutes * 60
        };
    }

    private static void ValidateRegisterRequest(RegisterRequest request)
    {
        var errors = new List<ApiError>();

        if (request.Username.Length is < 4 or > 20)
        {
            errors.Add(new ApiError { Field = "username", Message = "用户名长度需在4到20位之间" });
        }

        if (request.Password.Length is < 6 or > 20)
        {
            errors.Add(new ApiError { Field = "password", Message = "密码长度不能少于6位且不能超过20位" });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            errors.Add(new ApiError { Field = "email", Message = "邮箱格式不正确" });
        }

        if (errors.Count > 0)
        {
            throw new AppException(ErrorCodes.QuestionValidationFailed, "注册参数校验失败", StatusCodes.Status400BadRequest, errors);
        }
    }
}
