using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class GoogleLoginDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }
}
