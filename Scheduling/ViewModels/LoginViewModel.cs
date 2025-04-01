using System.ComponentModel.DataAnnotations;

namespace Scheduling.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
        public bool KeepLoggedIn { get; set; }
    }
}
