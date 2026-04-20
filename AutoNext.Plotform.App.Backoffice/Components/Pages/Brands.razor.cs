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

            NotificationService.Notify(NotificationSeverity.Info,
                "Brands",
                "Welcome to the Brands page");

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

                NotificationService.Notify(NotificationSeverity.Success,
                    "Success",
                    $"Loaded {TotalCount} brands");

                Logger.LogInformation("Loaded {Count} brands successfully", TotalCount);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogError(ex, "Unauthorized access");

                NotificationService.Notify(NotificationSeverity.Error,
                    "Unauthorized",
                    "You are not authorized to view brands.");
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "API unreachable");

                NotificationService.Notify(NotificationSeverity.Error,
                    "Connection Error",
                    "Unable to reach server.");
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex, "Timeout");

                NotificationService.Notify(NotificationSeverity.Warning,
                    "Timeout",
                    "Request timed out.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error");

                NotificationService.Notify(NotificationSeverity.Error,
                    "Error",
                    "Failed to load brands.");
            }
            finally
            {
                LoaderService.Hide();
                IsLoading = false;
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

                NotificationService.Notify(NotificationSeverity.Info,
                    "Page Size",
                    $"Showing {size} items per page");

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

            NotificationService.Notify(NotificationSeverity.Info,
                "Selection",
                selectAll ? "All brands selected" : "Selection cleared");

            StateHasChanged();
        }

        protected void CreateNewBrand()
        {
            Navigation.NavigateTo("/brands/create");
        }

        protected void EditBrand(Brand brand)
        {
            if (brand?.Id is null) return;

            Navigation.NavigateTo($"/brands/edit/{brand.Id}");
        }

        protected async Task DeleteBrand(Brand brand)
        {
            if (brand?.Id is null) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", $"Delete '{brand.Name}'?");

            if (!confirmed) return;

            try
            {
                LoaderService.Show($"Deleting {brand.Name}...");

                await BrandService.DeleteBrandAsync(brand.Id.Value);

                NotificationService.Notify(NotificationSeverity.Success,
                    "Deleted",
                    $"{brand.Name} deleted");

                Logger.LogInformation("Deleted brand {BrandId}", brand.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete failed");

                NotificationService.Notify(NotificationSeverity.Error,
                    "Delete Failed",
                    $"Failed to delete {brand.Name}");
            }
            finally
            {
                LoaderService.Hide();
            }

            await LoadAllBrandsAsync();
        }

        protected async Task BulkDelete()
        {
            if (!SelectedBrands.Any()) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", $"Delete {SelectedBrands.Count} brands?");

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
                    NotificationService.Notify(NotificationSeverity.Warning,
                        "Partial Success",
                        $"Failed: {string.Join(", ", failed)}");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Success,
                        "Success",
                        "All selected brands deleted");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk delete error");

                NotificationService.Notify(NotificationSeverity.Error,
                    "Error",
                    "Bulk delete failed");
            }
            finally
            {
                LoaderService.Hide();
            }

            await LoadAllBrandsAsync();
        }
       
    }
}