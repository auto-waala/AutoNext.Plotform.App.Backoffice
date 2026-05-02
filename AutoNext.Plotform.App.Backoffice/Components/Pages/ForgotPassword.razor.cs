using AutoNext.Plotform.App.Backoffice.Integrations.AccessControl;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class ForgotPasswordBase : ComponentBase
    {
        [Inject] protected ILogger<ForgotPasswordBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected IAuthService AuthService { get; set; } = default!;
        protected ForgotPasswordModel forgotPasswordModel { get; set; } = new ForgotPasswordModel();

        protected string errorMessage = string.Empty;

        protected bool isLoading = false;
        protected class ForgotPasswordModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address")]
            public string Email { get; set; } = string.Empty;
        }


        protected async Task HandleGoogleLogin()
        {
            try
            {
                // Your Google login implementation
                Logger.LogInformation("Google login initiated");
                NotificationService.Notify(NotificationSeverity.Info, "Info", "Google login initiated");
                // Navigation.NavigateTo("/api/auth/google", true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Google login error");
                errorMessage = "Google login failed. Please try again.";
            }
        }
        protected async Task HandleForgotPasswordSubmit()
        {
            try
            {
                isLoading = true;
                Logger.LogInformation("Forgot password request initiated for email: {Email}", forgotPasswordModel.Email);
                NotificationService.Notify(NotificationSeverity.Info, "Info", $"Forgot password request initiated for email: {forgotPasswordModel.Email}");
                // Your forgot password implementation

                ForgotPasswordDto forgotPasswordDto = new ForgotPasswordDto
                {
                    Email = forgotPasswordModel.Email
                };
                var response = await AuthService.ForgotPasswordAsync(forgotPasswordDto);
                if(response.IsValid == true)
                {
                    Logger.LogInformation("Forgot password request successful for email: {Email}", forgotPasswordModel.Email);
                    NotificationService.Notify(NotificationSeverity.Success, "Success", "Forgot password request successful. Please check your email.");
                    Navigation.NavigateTo($"/reset-password?token={Uri.EscapeDataString(response.ResetPasswordToken)}", true);
                }
                else
                {
                    Logger.LogWarning("Forgot password request failed for email: {Email}. Response: {Response}", forgotPasswordModel.Email, response);
                    errorMessage = "Forgot password request failed. Please try again.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Forgot password error");
                errorMessage = "Forgot password request failed. Please try again.";
            }
            finally
            {
                isLoading = false;
            }
        }
    }

}
