using AutoNext.Plotform.App.Backoffice.Models.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    [Authorize]
    public class ShippingOptionBase : ComponentBase, IDisposable
    {
        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
        [Parameter] public AutoNext.Plotform.App.Backoffice.Models.Core.ShippingOption? ShippingOptionToEdit { get; set; }
        [Parameter] public EventCallback<AutoNext.Plotform.App.Backoffice.Models.Core.ShippingOption> OnSave { get; set; }

        protected AutoNext.Plotform.App.Backoffice.Models.Core.ShippingOption ShippingOptionModel { get; set; } = new();
        protected bool IsSubmitting { get; set; }

        protected string ApplicableVehicleTypesString
        {
            get => ShippingOptionModel.ApplicableVehicleTypes ?? string.Empty;
            set => ShippingOptionModel.ApplicableVehicleTypes = value;
        }

        protected bool IsEditMode => ShippingOptionModel.Id.HasValue && ShippingOptionModel.Id != Guid.Empty;

        protected override void OnParametersSet()
        {
            InitializeModel();
        }

        protected virtual void InitializeModel()
        {
            if (ShippingOptionToEdit != null && ShippingOptionToEdit.Id.HasValue)
            {
                // Deep clone the shipping option for editing
                ShippingOptionModel = new AutoNext.Plotform.App.Backoffice.Models.Core.ShippingOption
                {
                    Id = ShippingOptionToEdit.Id,
                    Name = ShippingOptionToEdit.Name,
                    Code = ShippingOptionToEdit.Code,
                    Description = ShippingOptionToEdit.Description,
                    Provider = ShippingOptionToEdit.Provider,
                    EstimatedDaysMin = ShippingOptionToEdit.EstimatedDaysMin,
                    EstimatedDaysMax = ShippingOptionToEdit.EstimatedDaysMax,
                    BaseCost = ShippingOptionToEdit.BaseCost,
                    CostPerKm = ShippingOptionToEdit.CostPerKm,
                    IsTrackingAvailable = ShippingOptionToEdit.IsTrackingAvailable,
                    IsInsuranceAvailable = ShippingOptionToEdit.IsInsuranceAvailable,
                    ApplicableVehicleTypes = ShippingOptionToEdit.ApplicableVehicleTypes,
                    DisplayOrder = ShippingOptionToEdit.DisplayOrder,
                    IsActive = ShippingOptionToEdit.IsActive,
                    CreatedAt = ShippingOptionToEdit.CreatedAt,
                    UpdatedAt = ShippingOptionToEdit.UpdatedAt,
                    IsSelected = ShippingOptionToEdit.IsSelected
                };
            }
            else
            {
                // Reset form for new shipping option
                ShippingOptionModel = new AutoNext.Plotform.App.Backoffice.Models.Core.ShippingOption
                {
                    Id = Guid.NewGuid(),
                    IsActive = true,
                    IsTrackingAvailable = false,
                    IsInsuranceAvailable = false,
                    DisplayOrder = 0,
                    ApplicableVehicleTypes = string.Empty
                };
            }
        }

        protected void OnCodeInput(ChangeEventArgs e)
        {
            if (e.Value != null && !string.IsNullOrEmpty(e.Value.ToString()))
            {
                ShippingOptionModel.Code = e.Value.ToString()!.ToUpperInvariant();
            }
        }

        protected virtual async Task HandleValidSubmit()
        {
            try
            {
                IsSubmitting = true;
                StateHasChanged();

                // Ensure audit fields are set
                ShippingOptionModel.UpdatedAt = DateTime.UtcNow;

                if (!ShippingOptionModel.Id.HasValue || ShippingOptionModel.Id == Guid.Empty)
                {
                    ShippingOptionModel.Id = Guid.NewGuid();
                    ShippingOptionModel.CreatedAt = DateTime.UtcNow;
                }

                await OnSave.InvokeAsync(ShippingOptionModel);
                await Close();
            }
            finally
            {
                IsSubmitting = false;
                StateHasChanged();
            }
        }

        protected virtual async Task Close()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(false);
            StateHasChanged();
        }

        public virtual void Dispose()
        {
            // Clean up if needed
        }
    }
}