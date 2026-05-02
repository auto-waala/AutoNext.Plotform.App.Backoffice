
namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class ForgotPasswordResponseDto
    {
        public bool? IsValid { get; set; } = false;
        public string ResetPasswordToken { get; set; } = string.Empty;
    }
}
