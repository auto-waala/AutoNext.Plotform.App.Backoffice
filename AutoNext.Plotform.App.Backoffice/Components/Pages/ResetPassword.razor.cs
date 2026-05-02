using AutoNext.Plotform.App.Backoffice.Integrations.AccessControl;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Radzen;
using System.ComponentModel.DataAnnotations;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class ResetPasswordBase : ComponentBase
    {
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IAuthService AuthService { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        [Parameter]
        [SupplyParameterFromQuery(Name = "token")]
        public string? Token { get; set; }

        protected bool showPassword = false;
        protected bool isLoading = false;

        protected ResetPasswordModel Model { get; set; } = new();

        protected async Task HandleResetPassword()
        {
            isLoading = true;
            if (string.IsNullOrEmpty(Token))
                return;

            var request = new ResetPasswordRequestDto
            {
                Token = Token,
                NewPassword = Model.NewPassword
            };

            var result = await AuthService.ResetPasswordAsync(request);

            if (result)
            {
                // redirect to login
                isLoading = false;
                NotificationService.Notify(NotificationSeverity.Success, "Success", "restpassword request successful. Please login with new password.");

                Navigation.NavigateTo("/login", true);
            }
        }

        protected void TogglePasswordVisibility()
        {
            showPassword = !showPassword;
        }

        // 🔹 View Model
        protected class ResetPasswordModel
        {
            [Required]
            public string NewPassword { get; set; } = string.Empty;

            [Required]
            [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }
    }
}
