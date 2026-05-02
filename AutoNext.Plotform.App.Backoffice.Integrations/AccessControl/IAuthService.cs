using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.AccessControl
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterUserDto request);
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
        Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<bool> LogoutAsync(Guid userId, string refreshToken);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request);
        Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordDto request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request);
        Task<AuthResponseDto?> GoogleLoginAsync(GoogleLoginRequestDto request);
        Task<bool> SendVerificationOtpAsync(string email, string purpose);
        Task<bool> VerifyOtpAsync(VerifyOtpRequestDto request);
    }
}
