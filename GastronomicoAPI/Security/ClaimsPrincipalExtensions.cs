using System.Security.Claims;

namespace RotiseriaAPI.Security;

public static class ClaimTypesEx
{
    public const string BusinessId = "business_id";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetBusinessId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypesEx.BusinessId) ?? user.FindFirstValue("businessId");
        return int.TryParse(value, out var id) ? id : 0;
    }

    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : 0;
    }
}
