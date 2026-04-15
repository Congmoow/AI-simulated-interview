using AiInterview.Api.Constants;
using AiInterview.Api.Middleware;
using System.Security.Claims;

namespace AiInterview.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (Guid.TryParse(value, out var userId))
        {
            return userId;
        }

        throw new AppException(ErrorCodes.InvalidToken, "无效的用户令牌", StatusCodes.Status401Unauthorized);
    }

    public static string GetUserRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role")
            ?? AppRoles.User;
    }

    public static string GetUsername(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("username")
            ?? principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? "unknown";
    }
}
