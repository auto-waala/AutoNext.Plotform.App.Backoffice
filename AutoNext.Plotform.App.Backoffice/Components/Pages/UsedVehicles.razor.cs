using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class UsedVehiclesBase : ComponentBase
    {
        [Inject] protected IUsedVehiclesService UsedVehiclesService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<UsedVehiclesBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        // Data
        protected List<UsedVehiclesResponseDto> AllVehicles { get; set; } = new();
        protected List<UsedVehiclesResponseDto> SelectedVehicles { get; set; } = new();

        // Pagination
        protected int ItemsPerPage { get; set; } = 12;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        // Filters
        protected string SearchTerm { get; set; } = string.Empty;
        protected string StatusFilter { get; set; } = string.Empty;
        protected string VehicleTypeFilter { get; set; } = string.Empty;
        protected decimal MinPriceFilter { get; set; } = 0;
        protected decimal MaxPriceFilter { get; set; } = 0;
        protected string SortBy { get; set; } = "newest";

        // Modal states
        protected bool ShowActivateModal { get; set; } = false;
        protected UsedVehiclesResponseDto? ActivateVehicleModel { get; set; } = null;

        protected IEnumerable<int> PageSizeOptions = new[] { 12, 24, 48, 96 };

        // Computed Properties
        protected int TotalPages => (int)Math.Ceiling((double)FilteredVehicles.Count / ItemsPerPage);
        protected int ActiveCount => AllVehicles.Count(v => v.IsActive);

        protected List<UsedVehiclesResponseDto> FilteredVehicles => AllVehicles
            .Where(v => string.IsNullOrEmpty(SearchTerm) ||
                        v.BrandName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.ModelName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.VehicleType.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
            .Where(v => string.IsNullOrEmpty(StatusFilter) ||
                        (StatusFilter == "active" && v.IsActive) ||
                        (StatusFilter == "inactive" && !v.IsActive))
            .Where(v => string.IsNullOrEmpty(VehicleTypeFilter) || v.VehicleType == VehicleTypeFilter)
            .Where(v => MinPriceFilter == 0 || (v.Price?.Amount ?? 0) >= MinPriceFilter)
            .Where(v => MaxPriceFilter == 0 || (v.Price?.Amount ?? 0) <= MaxPriceFilter)
            .OrderBy(v => SortBy switch
            {
                "price_asc" => (v.Price?.Amount ?? 0),
                "year_desc" => int.TryParse(v.KeySpecifications?.YearOfManufacture, out var year) ? -year : 0,
                _ => 0
            }).ToList();

        protected List<UsedVehiclesResponseDto> PaginatedVehicles => FilteredVehicles
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected List<string> UniqueVehicleTypes => AllVehicles
            .Select(v => v.VehicleType)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("UsedVehicles page initialized");
            await LoadVehiclesAsync();
        }

        protected async Task LoadVehiclesAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var result = await UsedVehiclesService.GetAllAsync(1, 1000);
                AllVehicles = result?.Items?.ToList() ?? new List<UsedVehiclesResponseDto>();
                TotalCount = AllVehicles.Count;
                CurrentPage = 1;
                SelectedVehicles.Clear();

                Logger.LogInformation("Loaded {Count} used vehicles", AllVehicles.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading used vehicles");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load used vehicles.");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void ApplyFilters()
        {
            CurrentPage = 1;
            TotalCount = FilteredVehicles.Count;
            StateHasChanged();
        }

        protected void ClearFilters()
        {
            SearchTerm = string.Empty;
            StatusFilter = string.Empty;
            VehicleTypeFilter = string.Empty;
            MinPriceFilter = 0;
            MaxPriceFilter = 0;
            SortBy = "newest";
            ApplyFilters();
        }

        protected async Task ActivateVehicle(UsedVehiclesResponseDto vehicle)
        {
            ActivateVehicleModel = vehicle;
            ShowActivateModal = true;
        }
        protected void EditVehicle(UsedVehiclesResponseDto vehicle)
        {
            Navigation.NavigateTo($"/used-vehicles/{vehicle.Id}/edit");
        }
        protected async Task ConfirmActivate()
        {
            
        }

        protected void CloseActivateModal()
        {
            ShowActivateModal = false;
            ActivateVehicleModel = null;
        }

        protected async Task DeactivateVehicle(UsedVehiclesResponseDto vehicle)
        {
            try
            {
                var result = await UsedVehiclesService.DeactivateAsync(vehicle.Id);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Deactivated",
                        $"{vehicle.BrandName} {vehicle.ModelName} deactivated");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deactivating vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to deactivate.");
            }
        }

        protected async Task DeleteVehicle(UsedVehiclesResponseDto vehicle)
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Delete used car '{vehicle.BrandName} {vehicle.ModelName}'? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                var result = await UsedVehiclesService.DeleteAsync(vehicle.Id);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Deleted",
                        $"{vehicle.BrandName} {vehicle.ModelName} deleted");
                    await LoadVehiclesAsync();
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to delete vehicle.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to delete vehicle.");
            }
        }

        protected async Task BulkDelete()
        {
            if (!SelectedVehicles.Any()) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Delete {SelectedVehicles.Count} used cars? This cannot be undone.");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                foreach (var vehicle in SelectedVehicles.ToList())
                {
                    try
                    {
                        var success = await UsedVehiclesService.DeleteAsync(vehicle.Id);
                        if (!success) failed.Add($"{vehicle.BrandName} {vehicle.ModelName}");
                    }
                    catch
                    {
                        failed.Add($"{vehicle.BrandName} {vehicle.ModelName}");
                    }
                }

                SelectedVehicles.Clear();

                if (failed.Any())
                    NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                        $"Failed: {string.Join(", ", failed)}");
                else
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "All selected vehicles deleted");

                await LoadVehiclesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk delete error");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Bulk delete failed");
            }
        }

        protected void OnVehicleSelectionChanged(UsedVehiclesResponseDto vehicle, bool isChecked)
        {
            if (isChecked)
            {
                if (!SelectedVehicles.Contains(vehicle))
                    SelectedVehicles.Add(vehicle);
            }
            else
            {
                SelectedVehicles.Remove(vehicle);
            }
        }

        protected void GoToPage(int page)
        {
            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            StateHasChanged();
        }

        protected void OnPageSizeChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var size))
            {
                ItemsPerPage = size;
                CurrentPage = 1;
            }
        }

        // Helper Methods
        protected string FormatPrice(decimal price)
        {
            if (price >= 10_000_000) return $"₹{price / 10_000_000:F2} Cr";
            if (price >= 100_000) return $"₹{price / 100_000:F2} L";
            return $"₹{price:N0}";
        }

        protected string GetThumbnail(UsedVehiclesResponseDto vehicle)
        {
            if (!string.IsNullOrEmpty(vehicle.Thumbnail))
                return vehicle.Thumbnail;

            var primary = vehicle.Images?.FirstOrDefault(i => i.IsPrimary)?.FileUrl
                          ?? vehicle.Images?.FirstOrDefault()?.FileUrl;

            return primary ?? "https://placehold.co/400x250?text=No+Image";
        }

        protected string GetStarHtml(double rating)
        {
            var full = (int)Math.Floor(rating);
            var half = rating - full >= 0.5;
            var stars = new System.Text.StringBuilder();
            for (int i = 0; i < full; i++) stars.Append("★");
            if (half) stars.Append("½");
            var empty = 5 - full - (half ? 1 : 0);
            for (int i = 0; i < empty; i++) stars.Append("☆");
            return stars.ToString();
        }
    }
}