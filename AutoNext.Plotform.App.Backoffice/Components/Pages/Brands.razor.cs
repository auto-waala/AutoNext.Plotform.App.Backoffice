using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class BrandsBase : ComponentBase
{
    [Inject] protected IBrandService BrandService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<BrandsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<BrandResponseDto> AllBrands { get; set; } = new();
    protected List<BrandResponseDto> SelectedBrands { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;

    protected Brand? selectedBrand = null;

    protected int ActiveCount => AllBrands.Count(b => b.IsActive);
    protected int InactiveCount => AllBrands.Count(b => !b.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<BrandResponseDto> PaginatedBrands => AllBrands
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedBrands.Any() && PaginatedBrands.All(b => SelectedBrands.Contains(b));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Brands page initialized");
        await LoadAllBrandsAsync();
    }

    protected async Task LoadAllBrandsAsync()
    {
        try
        {
            LoaderService.Show("Loading brands...");
            IsLoading = true;

            var brands = await BrandService.GetAllBrandsAsync();

            AllBrands = brands?.ToList() ?? new List<BrandResponseDto>();
            TotalCount = AllBrands.Count;
            CurrentPage = 1;
            SelectedBrands.Clear();

            Logger.LogInformation("Loaded {Count} brands", TotalCount);
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
        selectedBrand = new Brand
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            DisplayOrder = 0,
            ApplicableCategories = new List<string>(),
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditBrandSidebar(BrandResponseDto brand)
    {
        selectedBrand = Mapper.Map<Brand>(brand);
        showSidebar = true;
    }

    protected async Task OnBrandSave(Brand brand)
    {
        try
        {
            LoaderService.Show(brand.Id != Guid.Empty ? "Updating brand..." : "Creating brand...");

            // Clean data
            brand.ApplicableCategories = brand.ApplicableCategories?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            brand.Metadata = brand.Metadata?
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Generate slug if needed
            if (string.IsNullOrWhiteSpace(brand.Slug) && !string.IsNullOrWhiteSpace(brand.Name))
            {
                brand.Slug = GenerateSlug(brand.Name);
            }

            if (brand.Id != Guid.Empty && brand.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<BrandUpdateDto>(brand);
                var updated = await BrandService.UpdateBrandAsync(brand.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Brand '{brand.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<BrandCreateDto>(brand);
                await BrandService.CreateBrandAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Brand '{brand.Name}' created successfully");
            }

            showSidebar = false;
            selectedBrand = null;

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
        }
    }

    private string GenerateSlug(string name)
    {
        return name.ToLower()
                   .Replace(" ", "-")
                   .Replace("&", "and")
                   .Replace("'", "")
                   .Replace("\"", "")
                   .Replace(".", "")
                   .Replace(",", "")
                   .Replace("?", "")
                   .Replace("!", "")
                   .Replace("/", "-")
                   .Replace("\\", "-");
    }

    protected void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
    }

    protected void OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size))
        {
            ItemsPerPage = size;
            CurrentPage = 1;
        }
    }

    protected void OnBrandSelectionChanged(BrandResponseDto brand, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedBrands.Contains(brand))
                SelectedBrands.Add(brand);
        }
        else
        {
            SelectedBrands.Remove(brand);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedBrands.Count != PaginatedBrands.Count;

        if (selectAll)
        {
            foreach (var brand in PaginatedBrands)
            {
                if (!SelectedBrands.Contains(brand))
                    SelectedBrands.Add(brand);
            }
        }
        else
        {
            foreach (var brand in PaginatedBrands)
            {
                SelectedBrands.Remove(brand);
            }
        }
    }

    protected async Task DeleteBrand(BrandResponseDto brand)
    {
        if (brand.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{brand.Name}'?");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {brand.Name}...");
            await BrandService.DeleteBrandAsync(brand.Id);

            NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{brand.Name} deleted");
            await LoadAllBrandsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {brand.Name}");
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
                    await BrandService.DeleteBrandAsync(brand.Id);
                }
                catch
                {
                    failed.Add(brand.Name);
                }
            }

            SelectedBrands.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected brands deleted");
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