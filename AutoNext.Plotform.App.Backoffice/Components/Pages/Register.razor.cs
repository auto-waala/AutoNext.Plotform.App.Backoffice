using AutoNext.Plotform.App.Backoffice.Integrations.AccessControl;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class RegisterBase : ComponentBase
    {
        [Inject] protected IAuthService AuthService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected ILogger<RegisterBase> Logger { get; set; } = default!;

        protected RegisterUserDto Model { get; set; } = new();
        protected string ConfirmPassword { get; set; } = string.Empty;

        protected bool isLoading = false;
        protected bool showPassword = false;
        protected string errorMessage = string.Empty;
        protected string successMessage = string.Empty;

        protected bool PasswordsMatch => Model.Password == ConfirmPassword;

        protected void TogglePassword()
        {
            showPassword = !showPassword;
        }

        protected async Task HandleRegister()
        {
            if (!PasswordsMatch)
            {
                errorMessage = "Passwords do not match";
                return;
            }

            isLoading = true;
            errorMessage = string.Empty;

            try
            {
                var result = await AuthService.RegisterAsync(Model);

                if (result != null)
                {
                    successMessage = "Registration successful! Redirecting to login...";

                    await Task.Delay(1500);
                    Navigation.NavigateTo("/login", true);
                }
                else
                {
                    errorMessage = "Registration failed";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Registration error");
                errorMessage = "Something went wrong. Please try again.";
            }
            finally
            {
                isLoading = false;
            }
        }
    }
}