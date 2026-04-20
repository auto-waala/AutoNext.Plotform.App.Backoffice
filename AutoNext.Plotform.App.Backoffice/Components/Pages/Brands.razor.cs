using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class BrandsBase : ComponentBase
    {
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected LoaderService LoaderService { get; set; } = default!;
        [Inject] protected ILogger<BrandsBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        protected List<Brand> AllBrands { get; set; } = new();
        protected int ItemsPerPage { get; set; } = 10;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        // Sidebar properties
        protected bool showSidebar = false;
        protected Brand? selectedBrand = null;

        protected int ActiveCount => AllBrands.Count(b => b.IsActive);
        protected int InactiveCount => AllBrands.Count(b => !b.IsActive);
        protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

        protected List<Brand> PaginatedBrands => AllBrands
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };
        protected List<Brand> SelectedBrands { get; set; } = new();

        protected bool AllPageSelected =>
            PaginatedBrands.Any() && PaginatedBrands.All(b => b.IsSelected);

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("Brands page initialized");
            await LoadAllBrandsAsync();
        }

        protected async Task LoadAllBrandsAsync()
        {
            try
            {
                Logger.LogInformation("Loading all brands...");
                LoaderService.Show("Loading brands...");
                IsLoading = true;

                var brands = await BrandService.GetAllBrandsAsync();
                AllBrands = brands?.ToList() ?? new List<Brand>();
                TotalCount = AllBrands.Count;
                CurrentPage = 1;
                SelectedBrands.Clear();

                Logger.LogInformation("Loaded {Count} brands successfully", TotalCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading brands");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load brands.");
            }
            finally
            {
                LoaderService.Hide();
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void OpenAddBrandSidebar()
        {
            selectedBrand = null;
            showSidebar = true;
            StateHasChanged();
        }

        protected void OpenEditBrandSidebar(Brand brand)
        {
            selectedBrand = brand;
            showSidebar = true;
            StateHasChanged();
        }

        protected async Task OnBrandSave(Brand brand)
        {
            try
            {
                LoaderService.Show(brand.Id.HasValue && brand.Id.Value != Guid.Empty ? "Updating brand..." : "Creating brand...");

                // Clean up empty categories
                if (brand.ApplicableCategories != null)
                {
                    brand.ApplicableCategories = brand.ApplicableCategories
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct()
                        .ToList();
                }

                // Clean up metadata
                if (brand.Metadata != null)
                {
                    brand.Metadata = brand.Metadata
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }

                if (brand.Id.HasValue && brand.Id.Value != Guid.Empty)
                {
                    await BrandService.UpdateBrandAsync(brand.Id.Value, brand);
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Brand '{brand.Name}' updated successfully");
                }
                else
                {
                    brand.Id = Guid.NewGuid();
                    brand.CreatedAt = DateTime.UtcNow;
                    await BrandService.CreateBrandAsync(brand);
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Brand '{brand.Name}' created successfully");
                }

                // Close sidebar
                showSidebar = false;
                selectedBrand = null;

                // Reload data
                await LoadAllBrandsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving brand");
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to save brand: {ex.Message}");
            }
            finally
            {
                LoaderService.Hide();
                StateHasChanged();
            }
        }

        protected void GoToPage(int page)
        {
            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            StateHasChanged();
        }

        protected async Task OnPageSizeChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var size))
            {
                ItemsPerPage = size;
                CurrentPage = 1;
                StateHasChanged();
            }
        }

        protected void OnBrandSelectionChanged(Brand brand, bool isChecked)
        {
            brand.IsSelected = isChecked;

            if (isChecked)
            {
                if (!SelectedBrands.Contains(brand))
                    SelectedBrands.Add(brand);
            }
            else
            {
                SelectedBrands.Remove(brand);
            }

            StateHasChanged();
        }

        protected void ToggleAllSelection()
        {
            bool selectAll = !AllPageSelected;

            foreach (var brand in PaginatedBrands)
            {
                brand.IsSelected = selectAll;

                if (selectAll)
                {
                    if (!SelectedBrands.Contains(brand))
                        SelectedBrands.Add(brand);
                }
                else
                {
                    SelectedBrands.Remove(brand);
                }
            }

            StateHasChanged();
        }

        protected async Task DeleteBrand(Brand brand)
        {
            if (brand?.Id is null) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{brand.Name}'?");
            if (!confirmed) return;

            try
            {
                LoaderService.Show($"Deleting {brand.Name}...");
                await BrandService.DeleteBrandAsync(brand.Id.Value);

                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{brand.Name} deleted");
                await LoadAllBrandsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete failed");
                NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", $"Failed to delete {brand.Name}");
            }
            finally
            {
                LoaderService.Hide();
            }
        }

        protected async Task BulkDelete()
        {
            if (!SelectedBrands.Any()) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedBrands.Count} brands?");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                LoaderService.Show("Bulk deleting...");

                foreach (var brand in SelectedBrands.ToList())
                {
                    try
                    {
                        if (brand?.Id is null) continue;
                        await BrandService.DeleteBrandAsync(brand.Id.Value);
                    }
                    catch
                    {
                        failed.Add(brand.Name ?? "Unknown");
                    }
                }

                SelectedBrands.Clear();

                if (failed.Any())
                {
                    NotificationService.Notify(NotificationSeverity.Warning, "Partial Success", $"Failed: {string.Join(", ", failed)}");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success", "All selected brands deleted");
                }

                await LoadAllBrandsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk delete error");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Bulk delete failed");
            }
            finally
            {
                LoaderService.Hide();
            }
        }
    }
}