using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages.Listings
{
    [Authorize]
    public class UpcomingVehiclesBase : ComponentBase
    {
        [Inject] protected IUpcomingVehiclesService UpcomingVehicleService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<UpcomingVehiclesBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected IVehicleTypeService VehicleTypeService { get; set; } = default!;

        // Data
        protected List<UpcomingVehicleResponseDto> AllVehicles { get; set; } = new();
        protected List<UpcomingVehicleResponseDto> SelectedVehicles { get; set; } = new();
        protected List<BrandResponseDto> Brands { get; set; } = new();
        protected List<VehicleTypeResponseDto> VehicleTypes { get; set; } = new();

        // Pagination
        protected int ItemsPerPage { get; set; } = 12;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        // Filters
        protected string SearchTerm { get; set; } = string.Empty;
        protected string StatusFilter { get; set; } = string.Empty;
        protected string VehicleTypeFilter { get; set; } = string.Empty;
        protected string LaunchPeriodFilter { get; set; } = string.Empty;
        protected int MinPriorityFilter { get; set; } = 0;
        protected string SortBy { get; set; } = "launch_date_asc";


        // Modal states
        protected bool ShowAddEditModal { get; set; } = false;
        protected bool ShowBulkPriorityModal { get; set; } = false;
        protected bool ShowActivateModal { get; set; } = false;
        protected bool IsEditMode { get; set; } = false;
        protected UpcomingVehicleResponseDto? SelectedVehicle { get; set; } = null;
        protected UpcomingVehicleResponseDto? ActivateVehicleModel { get; set; } = null;
        protected DateTime? ActivateLaunchDate { get; set; }
        protected int BulkPriorityValue { get; set; } = 5;
        protected UpcomingVehicleRequestDto FormModel { get; set; } = new();

        protected IEnumerable<int> PageSizeOptions = new[] { 12, 24, 48, 96 };

        // Launch Period Options
        protected List<string> LaunchPeriods = new()
        {
            "Q1 2024", "Q2 2024", "Q3 2024", "Q4 2024",
            "Q1 2025", "Q2 2025", "Q3 2025", "Q4 2025",
            "Early 2024", "Mid 2024", "Late 2024",
            "Early 2025", "Mid 2025", "Late 2025"
        };

        // Computed Properties
        protected int TotalPages => (int)Math.Ceiling((double)FilteredVehicles.Count / ItemsPerPage);
        protected int ActiveCount => AllVehicles.Count(v => v.IsActive && v.IsUpcoming);
        protected int LaunchedCount => AllVehicles.Count(v => v.IsLaunched);
        protected int FeaturedCount => AllVehicles.Count(v => v.IsFeatured);

        protected List<UpcomingVehicleResponseDto> FilteredVehicles => AllVehicles
            .Where(v => string.IsNullOrEmpty(SearchTerm) ||
                        v.BrandName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.ModelName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.VehicleType.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
            .Where(v => string.IsNullOrEmpty(StatusFilter) ||
                        (StatusFilter == "active" && v.IsActive && v.IsUpcoming) ||
                        (StatusFilter == "inactive" && !v.IsActive) ||
                        (StatusFilter == "launched" && v.IsLaunched))
            .Where(v => string.IsNullOrEmpty(VehicleTypeFilter) || v.VehicleType == VehicleTypeFilter)
            .Where(v => string.IsNullOrEmpty(LaunchPeriodFilter) || v.LaunchPeriod == LaunchPeriodFilter)
            .Where(v => MinPriorityFilter == 0 || v.Priority >= MinPriorityFilter)
            .OrderBy(v => SortBy switch
            {
                "priority_asc" => v.Priority,
                "priority_desc" => 0 - v.Priority,
                "launch_date_asc" => v.LaunchDate.Ticks,
                "launch_date_desc" => DateTime.MaxValue.Ticks - v.LaunchDate.Ticks,
                "rating_desc" => 0 - v.Rating,
                "newest" => DateTime.MaxValue.Ticks - v.CreatedAt.Ticks,
                _ => v.LaunchDate.Ticks
            })
            .ToList();

        protected List<UpcomingVehicleResponseDto> PaginatedVehicles => FilteredVehicles
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected List<string> UniqueVehicleTypes => AllVehicles
            .Select(v => v.VehicleType)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        protected List<string> UniqueLaunchPeriods => AllVehicles
            .Select(v => v.LaunchPeriod)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("UpcomingVehicle page initialized");
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

                var result = await UpcomingVehicleService.GetAllAsync(1, 1000);
                AllVehicles = result?.Items?.ToList() ?? new List<UpcomingVehicleResponseDto>();
                TotalCount = AllVehicles.Count;
                CurrentPage = 1;
                SelectedVehicles.Clear();

                Logger.LogInformation("Loaded {Count} upcoming vehicles", AllVehicles.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading upcoming vehicles");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load upcoming vehicles.");
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
            LaunchPeriodFilter = string.Empty;
            MinPriorityFilter = 0;
            SortBy = "launch_date_asc";
            ApplyFilters();
        }

        protected void OpenAddModal()
        {
            IsEditMode = false;
            SelectedVehicle = null;
            FormModel = new UpcomingVehicleRequestDto
            {
                LaunchDate = DateTime.UtcNow.AddMonths(3),
                LaunchPeriod = "Q1 2025",
                IsFeatured = false,
                IsActive = true,
                IsLaunched = false,
                Priority = 5,
                Price = new UpcomingVehiclePriceInfoDto(),
                Location = new UpcomingVehicleLocationInfoDto(),
                Seller = new SellerInfoDto(),
                KeySpecifications = new UpcomingVehicleKeySpecificationsDto(),
                TestDrive = new UpcomingVehicleTestDriveInfoDto(),
                ListingDetails = new UpcomingVehicleListingDetailsDto(),
                Condition = new UpcomingVehicleConditionDto(),
                Images = new List<ImageDto>(),
                Videos = new List<VideoDto>(),
                Shorts = new List<VideoDto>(),
                TopFeatures = new List<UpcomingVehicleFeatureItemDto>(),
                StandOutFeatures = new List<UpcomingVehicleFeatureItemDto>(),
                Pros = new List<UpcomingVehicleProConItemDto>(),
                Cons = new List<UpcomingVehicleProConItemDto>(),
                Tags = new List<UpcomingVehicleTagItemDto>(),
                Badges = new List<string>(),
                Variants = new List<VariantDetailDto>()
            };
            ShowAddEditModal = true;
        }

        protected void OpenEditModal(UpcomingVehicleResponseDto vehicle)
        {
            Navigation.NavigateTo($"/upcoming-vehicles/{vehicle.Id}/edit");
        }

        protected void CloseModal()
        {
            ShowAddEditModal = false;
            SelectedVehicle = null;
            FormModel = new UpcomingVehicleRequestDto();
        }

        protected async Task SaveVehicle()
        {
            try
            {
                if (IsEditMode && SelectedVehicle != null)
                {
                    var updated = await UpcomingVehicleService.UpdateAsync(SelectedVehicle.Id, FormModel);
                    if (updated != null)
                    {
                        NotificationService.Notify(NotificationSeverity.Success, "Updated",
                            $"{FormModel.BrandName} {FormModel.ModelName} updated successfully");
                    }
                }
                else
                {
                    var created = await UpcomingVehicleService.CreateAsync(FormModel);
                    NotificationService.Notify(NotificationSeverity.Success, "Created",
                        $"{FormModel.BrandName} {FormModel.ModelName} added as upcoming vehicle");
                }

                CloseModal();
                await LoadVehiclesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving upcoming vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to save: {ex.Message}");
            }
        }

        protected async Task ActivateVehicle(UpcomingVehicleResponseDto vehicle)
        {
            ActivateVehicleModel = vehicle;
            ActivateLaunchDate = vehicle.LaunchDate;
            ShowActivateModal = true;
        }

        protected async Task ConfirmActivate()
        {
            if (ActivateVehicleModel == null) return;

            try
            {
                var result = await UpcomingVehicleService.ActivateAsync(ActivateVehicleModel.Id, ActivateLaunchDate);
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
            ActivateLaunchDate = null;
        }

        protected async Task DeactivateVehicle(UpcomingVehicleResponseDto vehicle)
        {
            try
            {
                var result = await UpcomingVehicleService.DeactivateAsync(vehicle.Id);
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

        protected async Task MarkAsLaunched(UpcomingVehicleResponseDto vehicle)
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Mark '{vehicle.BrandName} {vehicle.ModelName}' as launched? This will archive the vehicle.");
            if (!confirmed) return;

            try
            {
                var result = await UpcomingVehicleService.MarkAsLaunchedAsync(vehicle.Id);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Launched",
                        $"{vehicle.BrandName} {vehicle.ModelName} marked as launched");
                    await LoadVehiclesAsync();
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to mark as launched.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error marking as launched");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to mark as launched.");
            }
        }

        protected async Task DeleteVehicle(UpcomingVehicleResponseDto vehicle)
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Delete upcoming vehicle '{vehicle.BrandName} {vehicle.ModelName}'? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                var result = await UpcomingVehicleService.DeleteAsync(vehicle.Id);
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
                $"Delete {SelectedVehicles.Count} upcoming vehicles? This cannot be undone.");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                foreach (var vehicle in SelectedVehicles.ToList())
                {
                    try
                    {
                        var success = await UpcomingVehicleService.DeleteAsync(vehicle.Id);
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
                var result = await UpcomingVehicleService.BulkUpdatePriorityAsync(priorityUpdates);
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

        protected async Task BulkMarkAsFeatured()
        {
            if (!SelectedVehicles.Any()) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Mark {SelectedVehicles.Count} upcoming vehicles as featured?");
            if (!confirmed) return;

            var featuredUpdates = SelectedVehicles.ToDictionary(v => v.Id, v => true);

            try
            {
                var result = await UpcomingVehicleService.BulkUpdateFeaturedAsync(featuredUpdates);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Updated",
                        $"{SelectedVehicles.Count} vehicles marked as featured");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk featured update error");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update featured status");
            }
        }

        protected void OnVehicleSelectionChanged(UpcomingVehicleResponseDto vehicle, bool isChecked)
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

        protected string GetLaunchStatusBadge(UpcomingVehicleResponseDto vehicle)
        {
            if (vehicle.IsLaunched) return "bg-secondary";
            if (vehicle.LaunchDate <= DateTime.UtcNow) return "bg-danger";
            if (vehicle.LaunchDate <= DateTime.UtcNow.AddDays(30)) return "bg-warning text-dark";
            return "bg-success";
        }

        protected string GetLaunchStatusText(UpcomingVehicleResponseDto vehicle)
        {
            if (vehicle.IsLaunched) return "Launched";
            if (vehicle.LaunchDate <= DateTime.UtcNow) return "Past Due";
            if (vehicle.LaunchDate <= DateTime.UtcNow.AddDays(30)) return "Launching Soon";
            return "Upcoming";
        }

        protected string GetThumbnail(UpcomingVehicleResponseDto vehicle)
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

        protected int DaysUntilLaunch(DateTime launchDate)
        {
            var days = (int)(launchDate - DateTime.UtcNow).TotalDays;
            return days > 0 ? days : 0;
        }

        protected void AddPro()
        {
            FormModel.Pros.Add(new UpcomingVehicleProConItemDto { Pro = string.Empty });
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
            FormModel.Cons.Add(new UpcomingVehicleProConItemDto { Con = string.Empty });
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