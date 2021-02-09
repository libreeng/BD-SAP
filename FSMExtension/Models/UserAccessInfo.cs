using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FSMExtension.Models
{
    /// <summary>
    /// Wraps the Connections API's "token" along with the requesting user's email address.
    /// This is returned after successful authentication with the AuthController.
    /// </summary>
    public class UserAccessInfo : PageModel
    {
        public UserAccessInfo(string email, string token)
        {
            Email = email;
            Token = token;
        }

        public string Email { get; }

        public string Token { get; }
    }
}
