using System.Security.Claims;

namespace ITClubOrg.Services;

public static class UserDivision
{
    public static bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole("SuperAdmin");

    public static int? GetDivisionId(ClaimsPrincipal user)
    {
        if (IsSuperAdmin(user))
        {
            return null;
        }

        var divisionId = user.FindFirstValue("division_id");
        if (!user.IsInRole("DivisionAdmin")
            || !int.TryParse(divisionId, out var parsedDivisionId)
            || parsedDivisionId <= 0)
        {
            throw new InvalidOperationException("The signed-in account does not have a valid division assignment.");
        }

        return parsedDivisionId;
    }
}
