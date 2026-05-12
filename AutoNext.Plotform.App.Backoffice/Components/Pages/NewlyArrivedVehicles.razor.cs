using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class NewlyArrivedVehiclesBase : ComponentBase
    {
        [Inject] protected INewlyArrivedService NewlyArrivedService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<NewlyArrivedVehiclesBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected IVehicleTypeService VehicleTypeService { get; set; } = default!;

        protected List<BrandResponseDto> Brands { get; set; } = new();
        protected List<VehicleTypeResponseDto> VehicleTypes { get; set; } = new();
        protected List<NewlyArrivedResponseDto> AllVehicles { get; set; } = new();
        protected List<NewlyArrivedResponseDto> SelectedVehicles { get; set; } = new();

        protected int ItemsPerPage { get; set; } = 12;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;
        protected bool ShowAddEditModal { get; set; } = false;
        protected NewlyArrivedResponseDto? SelectedVehicle { get; set; } = null;
        protected NewlyArrivedRequestDto FormModel { get; set; } = new();
        protected bool IsEditMode { get; set; } = false;

        // Filters
        protected string SearchTerm { get; set; } = string.Empty;
        protected string ArrivalPeriodFilter { get; set; } = string.Empty;
        protected string VehicleTypeFilter { get; set; } = string.Empty;
        protected string FeaturedFilter { get; set; } = string.Empty;
        protected string SortBy { get; set; } = "newest";

        protected int TotalPages => (int)Math.Ceiling((double)FilteredVehicles.Count / ItemsPerPage);
        protected int FeaturedCount => AllVehicles.Count(v => v.Featured);
        protected int PublishedCount => AllVehicles.Count;

        protected IEnumerable<int> PageSizeOptions = new[] { 12, 24, 48 };
        protected string defaultValues = "newest";
        protected List<NewlyArrivedResponseDto> FilteredVehicles => AllVehicles
            .Where(v => string.IsNullOrEmpty(SearchTerm) ||
                        v.BrandName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.ModelName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        v.VehicleType.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
            .Where(v => string.IsNullOrEmpty(ArrivalPeriodFilter) || v.ArrivalPeriod == ArrivalPeriodFilter)
            .Where(v => string.IsNullOrEmpty(VehicleTypeFilter) || v.VehicleType == VehicleTypeFilter)
            .Where(v => string.IsNullOrEmpty(FeaturedFilter) ||
                        (FeaturedFilter == "true" && v.Featured) ||
                        (FeaturedFilter == "false" && !v.Featured))
            .OrderByDescending(v => SortBy == "newest" ? v.ArrivalDate : DateTime.MinValue)
            .ThenBy(v => SortBy == "price_asc" ? v.MinPrice : 0)
            .ThenByDescending(v => SortBy == "price_desc" ? v.MinPrice : 0)
            .ThenByDescending(v => SortBy == "rating" ? (decimal)v.Rating : 0)
            .ToList();

        protected List<NewlyArrivedResponseDto> PaginatedVehicles => FilteredVehicles
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected List<string> UniqueVehicleTypes => AllVehicles
            .Select(v => v.VehicleType)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("NewlyArrivedVehicles page initialized");
            await LoadVehiclesAsync();
            await LoadBandsAsync();
            await LoadVehicleTypesAsync();
        }

        protected async Task LoadVehiclesAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var result = await NewlyArrivedService.GetAllAsync(1, 1000);
                AllVehicles = result?.Items?.ToList() ?? new List<NewlyArrivedResponseDto>();
                TotalCount = AllVehicles.Count;
                CurrentPage = 1;
                SelectedVehicles.Clear();

                Logger.LogInformation("Loaded {Count} newly arrived vehicles", AllVehicles.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading newly arrived vehicles");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicles.");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }
        protected async Task LoadBandsAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var result = await BrandService.GetAllBrandsAsync();
                Brands = result?.ToList() ?? new List<BrandResponseDto>();


                Logger.LogInformation("Loaded {Count} brands", Brands.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading brands");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load brands.");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected async Task LoadVehicleTypesAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var result = await VehicleTypeService.GetAllAsync();

                VehicleTypes = result?.ToList() ?? new List<VehicleTypeResponseDto>();


                Logger.LogInformation("Loaded {Count} VehicleTypes", Brands.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading VehicleTypes");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load VehicleTypes.");
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
            ArrivalPeriodFilter = string.Empty;
            VehicleTypeFilter = string.Empty;
            FeaturedFilter = string.Empty;
            SortBy = "newest";
            ApplyFilters();
        }

        protected void OpenAddModal()
        {
            IsEditMode = false;
            SelectedVehicle = null;
            FormModel = new NewlyArrivedRequestDto();
            ShowAddEditModal = true;
        }

        protected void OpenEditModal(NewlyArrivedResponseDto vehicle)
        {
            IsEditMode = true;
            SelectedVehicle = vehicle;
            FormModel = new NewlyArrivedRequestDto
            {
                BrandName = vehicle.BrandName,
                ModelName = vehicle.ModelName,
                VehicleType = vehicle.VehicleType,
                BodyType = vehicle.BodyType,
                MinPrice = vehicle.MinPrice,
                MaxPrice = vehicle.MaxPrice,
                ArrivalPeriod = vehicle.ArrivalPeriod,
                Rating = vehicle.Rating,
                ReviewCount = vehicle.ReviewCount,
                PageTitle = vehicle.PageTitle,
                DescriptionText = vehicle.DescriptionText,
                Emi = vehicle.Emi,
                Images = vehicle.Images,
                Videos = vehicle.Videos,
                Variants = vehicle.Variants,
            };
            ShowAddEditModal = true;
        }

        protected void CloseModal()
        {
            ShowAddEditModal = false;
            SelectedVehicle = null;
            FormModel = new NewlyArrivedRequestDto();
        }

        protected async Task SaveVehicle()
        {
            try
            {
                if (IsEditMode && SelectedVehicle != null)
                {
                    var updated = await NewlyArrivedService.UpdateAsync(SelectedVehicle.Id, FormModel);
                    if (updated != null)
                    {
                        NotificationService.Notify(NotificationSeverity.Success, "Updated",
                            $"{FormModel.BrandName} {FormModel.ModelName} updated successfully");
                    }
                }
                else
                {
                    var created = await NewlyArrivedService.CreateAsync(FormModel, "admin");
                    NotificationService.Notify(NotificationSeverity.Success, "Created",
                        $"{FormModel.BrandName} {FormModel.ModelName} added successfully");
                }

                CloseModal();
                await LoadVehiclesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to save: {ex.Message}");
            }
        }

        protected async Task PublishVehicle(NewlyArrivedResponseDto vehicle)
        {
            try
            {
                var result = await NewlyArrivedService.PublishAsync(vehicle.Id, "admin");
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Published",
                        $"{vehicle.BrandName} {vehicle.ModelName} published");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error publishing vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to publish.");
            }
        }

        protected async Task UnpublishVehicle(NewlyArrivedResponseDto vehicle)
        {
            try
            {
                var result = await NewlyArrivedService.UnpublishAsync(vehicle.Id);
                if (result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Unpublished",
                        $"{vehicle.BrandName} {vehicle.ModelName} unpublished");
                    await LoadVehiclesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error unpublishing vehicle");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to unpublish.");
            }
        }

        protected async Task DeleteVehicle(NewlyArrivedResponseDto vehicle)
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                $"Delete '{vehicle.BrandName} {vehicle.ModelName}'? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                var result = await NewlyArrivedService.DeleteAsync(vehicle.Id);
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
                $"Delete {SelectedVehicles.Count} vehicles? This cannot be undone.");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                foreach (var vehicle in SelectedVehicles.ToList())
                {
                    try
                    {
                        var success = await NewlyArrivedService.DeleteAsync(vehicle.Id);
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

        protected void OnVehicleSelectionChanged(NewlyArrivedResponseDto vehicle, bool isChecked)
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

        protected string FormatPrice(decimal price)
        {
            if (price >= 10000000) return $"₹{price / 10000000:F2} Cr";
            if (price >= 100000) return $"₹{price / 100000:F2} L";
            return $"₹{price:N0}";
        }

        protected string GetArrivalPeriodBadgeClass(string period) => period?.ToLower() switch
        {
            "weekly" => "bg-success",
            "monthly" => "bg-primary",
            "yearly" => "bg-warning text-dark",
            _ => "bg-secondary"
        };

        protected string GetThumbnail(NewlyArrivedResponseDto vehicle)
        {
            if (!string.IsNullOrEmpty(vehicle.ThumbnailImage))
                return vehicle.ThumbnailImage;

            var primary = vehicle.Images?.FirstOrDefault(i => i.IsPrimary)?.Url
                          ?? vehicle.Images?.FirstOrDefault()?.Url;

            return primary ?? "https://placehold.co/400x250?text=No+Image";
        }

        protected string GetStarHtml(double rating)
        {
            var full = (int)Math.Floor(rating);
            var half = rating - full >= 0.5;
            var stars = new System.Text.StringBuilder();
            for (int i = 0; i < full; i++) stars.Append("★");
            if (half) stars.Append("½");
            return stars.ToString();
        }
    }
}