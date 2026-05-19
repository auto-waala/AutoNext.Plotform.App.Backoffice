using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using AutoNext.Plotform.App.Backoffice.Models.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages.Listings
{
    [Authorize]
    public class PremiumVehiclesBase : ComponentBase
    {
        [Inject] protected IPremiumVehicleService PremiumVehicleService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<PremiumVehiclesBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected IVehicleTypeService VehicleTypeService { get; set; } = default!;

        // Data
        protected List<PremiumVehicleResponseDto> AllVehicles { get; set; } = new();
        protected List<PremiumVehicleResponseDto> SelectedVehicles { get; set; } = new();
        protected List<BrandResponseDto> Brands { get; set; } = new();
        protected List<VehicleTypeResponseDto> VehicleTypes { get; set; } = new();
        protected string PriorityDesc { get; set; } = "priority_desc";

        // Pagination
        protected int ItemsPerPage { get; set; } = 12;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        // Filters
        protected string SearchTerm { get; set; } = string.Empty;
        protected string StatusFilter { get; set; } = string.Empty;
        protected string VehicleTypeFilter { get; set; } = string.Empty;
        protected int MinPriorityFilter { get; set; } = 0;
        protected string SortBy { get; set; } = "priority_desc";

        // Modal states
        protected bool ShowAddEditModal { get; set; } = false;
        protected bool ShowBulkPriorityModal { get; set; } = false;
        protected bool ShowActivateModal { get; set; } = false;
        protected bool IsEditMode { get; set; } = false;
        protected PremiumVehicleResponseDto? SelectedVehicle { get; set; } = null;
        protected PremiumVehicleResponseDto? ActivateVehicleModel { get; set; } = null;
        protected DateTime? ActivateEndDate { get; set; }
        protected int BulkPriorityValue { get; set; } = 5;
        protected PremiumVehicleRequestDto FormModel { get; set; } = new();

        protected IEnumerable<int> PageSizeOptions = new[] { 12, 24, 48, 96 };

        // Computed Properties
        protected int TotalPages => (int)Math.Ceiling((double)FilteredVehicles.Count / ItemsPerPage);
        protected int ActiveCount => AllVehicles.Count(v => v.IsActive && !v.IsExpired);
        protected int ExpiredCount => AllVehicles.Count(v => v.IsExpired);

        protected List<PremiumVehicleResponseDto> FilteredVehicles => AllVehicles
            .Where(v => string.IsNullOrEmpty(SearchTerm) ||
                        v.BrandName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.ModelName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.VehicleType.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
            .Where(v => string.IsNullOrEmpty(StatusFilter) ||
                        (StatusFilter == "active" && v.IsActive && !v.IsExpired) ||
                        (StatusFilter == "inactive" && !v.IsActive) ||
                        (StatusFilter == "expired" && v.IsExpired))
            .Where(v => string.IsNullOrEmpty(VehicleTypeFilter) || v.VehicleType == VehicleTypeFilter)
            .Where(v => MinPriorityFilter == 0 || v.Priority >= MinPriorityFilter)
            .OrderBy(v => SortBy switch
            {
                "priority_asc" => v.Priority,
                "price_asc" => v.Price?.Amount ?? 0,
                "newest" => DateTime.MaxValue.Ticks - v.CreatedAt.Ticks,
                "views" => 0 - (v.Engagement?.Views ?? 0),
                _ => 0 - v.Priority
            })
            .ThenBy(v => SortBy switch
            {
                "priority_desc" => 0 - v.Priority,
                "price_desc" => 0 - (v.Price?.Amount ?? 0),
                _ => 0
            })
            .ToList();

        protected List<PremiumVehicleResponseDto> PaginatedVehicles => FilteredVehicles
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
            Logger.LogInformation("PremiumVehicle page initialized");
            await LoadVehiclesAsync();
            await LoadBrandsAsync();
            await LoadVehicleTypesAsync();
        }

        protected async Task LoadVehiclesAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var result = await PremiumVehicleService.GetAllAsync(1, 1000);
                AllVehicles = result?.Items?.ToList() ?? new List<PremiumVehicleResponseDto>();
                TotalCount = AllVehicles.Count;
                CurrentPage = 1;
                SelectedVehicles.Clear();

                Logger.LogInformation("Loaded {Count} premium vehicles", AllVehicles.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading premium vehicles");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load premium vehicles.");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected async Task LoadBrandsAsync()
        {
            try
            {
                var result = await BrandService.GetAllBrandsAsync();
                Brands = result?.ToList() ?? new List<BrandResponseDto>();
                Logger.LogInformation("Loaded {Count} brands", Brands.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading brands");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load brands.");
            }
        }

        protected async Task LoadVehicleTypesAsync()
        {
            try
            {
                var result = await VehicleTypeService.GetAllAsync();
                VehicleTypes = result?.ToList() ?? new List<VehicleTypeResponseDto>();
                Logger.LogInformation("Loaded {Count} vehicle types", VehicleTypes.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading vehicle types");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle types.");
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
            MinPriorityFilter = 0;
            SortBy = "priority_desc";
            ApplyFilters();
        }

        protected void OpenAddModal()
        {
            IsEditMode = false;
            SelectedVehicle = null;
            FormModel = new PremiumVehicleRequestDto
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                Priority = 5,
                Price = new PremiumVehiclePriceInfoDto(),
                Location = new PremiumVehicleLocationInfoDto(),
                Seller = new SellerInfoDto(),
                KeySpecifications = new PremiumVehicleKeySpecificationsDto(),
                TestDrive = new PremiumVehicleTestDriveInfoDto(),
                ListingDetails = new PremiumVehicleListingDetailsDto(),
                Condition = new PremiumVehicleConditionDto()
            };
            ShowAddEditModal = true;
        }

        protected void OpenEditModal(PremiumVehicleResponseDto vehicle)
        {
            Navigation.NavigateTo($"/premium-vehicles/{vehicle.Id}/edit");
        }

        protected void CloseModal()
        {
            ShowAddEditModal = false;
            SelectedVehicle = null;
            FormModel = new PremiumVehicleRequestDto();
        }

        protected async Task SaveVehicle()
        {
            try
            {
                if (IsEditMode && SelectedVehicle != null)
                {
                    var updated = await PremiumVehicleService.UpdateAsync(SelectedVehicle.Id, FormModel);
                    if (updated != null)
                    {
                        NotificationService.Notify(NotificationSeverity.Success, "Updated",
                            $"{FormModel.BrandName} {FormModel.ModelName} updated successfully");
                    }
                }
                else
                {
                    var created = await PremiumVehicleService.CreateAsync(FormModel);
                    NotificationService.Notify(NotificationSeverity.Success, "Created",
                        $"{FormModel.BrandName} {FormModel.ModelName} added as premium vehicle");
                }

                CloseModal();
                await LoadVehiclesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving premium vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to save: {ex.Message}");
            }
        }

        protected async Task ActivateVehicle(PremiumVehicleResponseDto vehicle)
        {
            ActivateVehicleModel = vehicle;
            ActivateEndDate = vehicle.EndDate ?? DateTime.UtcNow.AddDays(30);
            ShowActivateModal = true;
        }

        protected async Task ConfirmActivate()
        {
            if (ActivateVehicleModel == null) return;

            try
            {
                var result = await PremiumVehicleService.ActivateAsync(ActivateVehicleModel.Id, ActivateEndDate);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Activated",
                        $"{ActivateVehicleModel.BrandName} {ActivateVehicleModel.ModelName} activated");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error activating vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to activate.");
            }
            finally
            {
                CloseActivateModal();
            }
        }

        protected void CloseActivateModal()
        {
            ShowActivateModal = false;
            ActivateVehicleModel = null;
            ActivateEndDate = null;
        }

        protected async Task DeactivateVehicle(PremiumVehicleResponseDto vehicle)
        {
            try
            {
                var result = await PremiumVehicleService.DeactivateAsync(vehicle.Id);
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

        protected async Task DeleteVehicle(PremiumVehicleResponseDto vehicle)
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Delete premium vehicle '{vehicle.BrandName} {vehicle.ModelName}'? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                var result = await PremiumVehicleService.DeleteAsync(vehicle.Id);
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
                $"Delete {SelectedVehicles.Count} premium vehicles? This cannot be undone.");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                foreach (var vehicle in SelectedVehicles.ToList())
                {
                    try
                    {
                        var success = await PremiumVehicleService.DeleteAsync(vehicle.Id);
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

        protected void BulkUpdatePriority()
        {
            if (!SelectedVehicles.Any()) return;
            BulkPriorityValue = 5;
            ShowBulkPriorityModal = true;
        }

        protected async Task ExecuteBulkPriorityUpdate()
        {
            var priorityUpdates = SelectedVehicles.ToDictionary(v => v.Id, v => BulkPriorityValue);

            try
            {
                var result = await PremiumVehicleService.BulkUpdatePriorityAsync(priorityUpdates);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Updated",
                        $"Priority updated for {SelectedVehicles.Count} vehicles");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk priority update error");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update priorities");
            }
            finally
            {
                CloseBulkPriorityModal();
            }
        }

        protected void CloseBulkPriorityModal()
        {
            ShowBulkPriorityModal = false;
        }

        protected void OnVehicleSelectionChanged(PremiumVehicleResponseDto vehicle, bool isChecked)
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

        protected string FormatNumber(long number)
        {
            if (number >= 1_000_000) return $"{number / 1_000_000:F1}M";
            if (number >= 1_000) return $"{number / 1_000:F1}K";
            return number.ToString();
        }

        protected string GetPriorityColor(int priority)
        {
            if (priority >= 8) return "#dc3545";
            if (priority >= 5) return "#fd7e14";
            if (priority >= 3) return "#ffc107";
            return "#6c757d";
        }

        protected string GetThumbnail(PremiumVehicleResponseDto vehicle)
        {
            if (!string.IsNullOrEmpty(vehicle.ThumbnailImage))
                return vehicle.ThumbnailImage;

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

        protected void AddPro()
        {
            FormModel.Pros.Add(new PremiumVehicleProConItemDto { Pro = string.Empty });
            StateHasChanged();
        }

        protected void RemovePro(int index)
        {
            if (index >= 0 && index < FormModel.Pros.Count)
            {
                FormModel.Pros.RemoveAt(index);
                StateHasChanged();
            }
        }

        protected void AddCon()
        {
            FormModel.Cons.Add(new PremiumVehicleProConItemDto { Con = string.Empty });
            StateHasChanged();
        }

        protected void RemoveCon(int index)
        {
            if (index >= 0 && index < FormModel.Cons.Count)
            {
                FormModel.Cons.RemoveAt(index);
                StateHasChanged();
            }
        }
    }
}