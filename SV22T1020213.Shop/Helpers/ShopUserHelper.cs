using System.Security.Claims;

namespace SV22T1020213.Shop.Helpers
{
    public static class ShopUserHelper
    {
        public static ShopUserData? GetUserData(this ClaimsPrincipal principal)
        {
            try
            {
                if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
                    return null;

                var userData = new ShopUserData();

                userData.UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                userData.UserName = principal.FindFirstValue(ClaimTypes.Name);
                userData.DisplayName = principal.FindFirstValue("DisplayName");
                userData.Email = principal.FindFirstValue(ClaimTypes.Email);

                return userData;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ShopUserData
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
    }
}