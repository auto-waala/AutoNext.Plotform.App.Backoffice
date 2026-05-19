using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages.Core;

[Authorize]
public class FeaturesBase : ComponentBase
{
    [Inject] protected IFeatureService FeatureService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<FeaturesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<FeatureResponseDto> AllFeatures { get; set; } = new();
    protected List<FeatureResponseDto> SelectedFeatures { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;

    protected Feature? selectedFeature = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? CategoryFilter { get; set; }
    protected string? SubCategoryFilter { get; set; }
    protected bool? StandardFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableCategories { get; set; } = new();
    protected List<string> AvailableSubCategories { get; set; } = new();

    protected int ActiveCount => AllFeatures.Count(f => f.IsActive);
    protected int InactiveCount => AllFeatures.Count(f => !f.IsActive);
    protected int StandardCount => AllFeatures.Count(f => f.IsStandard);
    protected int PremiumCount => AllFeatures.Count(f => !f.IsStandard);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<FeatureResponseDto> FilteredFeatures => AllFeatures
        .Where(f => string.IsNullOrEmpty(SearchTerm) ||
                     f.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     f.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(f => string.IsNullOrEmpty(CategoryFilter) || f.Category == CategoryFilter)
        .Where(f => string.IsNullOrEmpty(SubCategoryFilter) || f.SubCategory == SubCategoryFilter)
        .Where(f => StandardFilter == null || f.IsStandard == StandardFilter)
        .Where(f => ActiveFilter == null || f.IsActive == ActiveFilter)
        .ToList();

    protected List<FeatureResponseDto> PaginatedFeatures => FilteredFeatures
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedFeatures.Any() && PaginatedFeatures.All(f => SelectedFeatures.Contains(f));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Features page initialized");
        await LoadAllFeaturesAsync();
        await LoadCategoriesAsync();
    }

    protected async Task LoadAllFeaturesAsync()
    {
        try
        {
            LoaderService.Show("Loading features...");
            IsLoading = true;

            var features = await FeatureService.GetAllAsync();

            AllFeatures = features?.ToList() ?? new List<FeatureResponseDto>();
            TotalCount = FilteredFeatures.Count;
            CurrentPage = 1;
            SelectedFeatures.Clear();

            Logger.LogInformation("Loaded {Count} features", AllFeatures.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading features");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load features.");
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task LoadCategoriesAsync()
    {
        try
        {
            var features = AllFeatures.Any() ? AllFeatures : await FeatureService.GetAllAsync();

            AvailableCategories = features?
                .Where(f => !string.IsNullOrEmpty(f.Category))
                .Select(f => f.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading categories");
        }
    }

    protected void OnCategoryFilterChanged(ChangeEventArgs e)
    {
        CategoryFilter = e.Value?.ToString();
        SubCategoryFilter = null;
        LoadSubCategories();
        ApplyFilters();
    }

    protected void LoadSubCategories()
    {
        if (!string.IsNullOrEmpty(CategoryFilter))
        {
            AvailableSubCategories = AllFeatures
                .Where(f => f.Category == CategoryFilter && !string.IsNullOrEmpty(f.SubCategory))
                .Select(f => f.SubCategory!)
                .Distinct()
                .OrderBy(sc => sc)
                .ToList();
        }
        else
        {
            AvailableSubCategories.Clear();
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredFeatures.Count;
        SelectedFeatures.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CategoryFilter = null;
        SubCategoryFilter = null;
        StandardFilter = null;
        ActiveFilter = null;
        AvailableSubCategories.Clear();
        ApplyFilters();
    }

    protected void OpenAddFeatureSidebar()
    {
        selectedFeature = new Feature
        {

            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            Category = string.Empty,
            SubCategory = string.Empty,
            IsActive = true,
            IsStandard = false,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditFeatureSidebar(FeatureResponseDto feature)
    {
        selectedFeature = Mapper.Map<Feature>(feature);
        showSidebar = true;
    }

    protected async Task OnFeatureSave(Feature feature)
    {
        try
        {
            LoaderService.Show(feature.Id != Guid.Empty ? "Updating feature..." : "Creating feature...");


           

            if (feature.Id != Guid.Empty && feature.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<FeatureUpdateDto>(feature);
                var updated = await FeatureService.UpdateAsync(feature.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Feature '{feature.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<FeatureCreateDto>(feature);
                await FeatureService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Feature '{feature.Name}' created successfully");
            }

            showSidebar = false;
            selectedFeature = null;

            await LoadAllFeaturesAsync();
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving feature");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save feature: {ex.Message}");
        }
        finally
        {
            LoaderService.Hide();
        }
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

    protected void OnFeatureSelectionChanged(FeatureResponseDto feature, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedFeatures.Contains(feature))
                SelectedFeatures.Add(feature);
        }
        else
        {
            SelectedFeatures.Remove(feature);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedFeatures.Count != PaginatedFeatures.Count;

        if (selectAll)
        {
            foreach (var feature in PaginatedFeatures)
            {
                if (!SelectedFeatures.Contains(feature))
                    SelectedFeatures.Add(feature);
            }
        }
        else
        {
            foreach (var feature in PaginatedFeatures)
            {
                SelectedFeatures.Remove(feature);
            }
        }
    }

    protected async Task ToggleStatus(FeatureResponseDto feature)
    {
        try
        {
            LoaderService.Show($"Updating {feature.Name} status...");
            var success = await FeatureService.ToggleStatusAsync(feature.Id, !feature.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{feature.Name} is now {(feature.IsActive ? "inactive" : "active")}");
                await LoadAllFeaturesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update status");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling status");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update status");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task DeleteFeature(FeatureResponseDto feature)
    {
        if (feature.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{feature.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {feature.Name}...");
            var success = await FeatureService.DeleteAsync(feature.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{feature.Name} deleted");
                await LoadAllFeaturesAsync();
                await LoadCategoriesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {feature.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {feature.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedFeatures.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedFeatures.Count} features? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var feature in SelectedFeatures.ToList())
            {
                try
                {
                    var success = await FeatureService.DeleteAsync(feature.Id);
                    if (!success)
                    {
                        failed.Add(feature.Name);
                    }
                }
                catch
                {
                    failed.Add(feature.Name);
                }
            }

            SelectedFeatures.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected features deleted");
            }

            await LoadAllFeaturesAsync();
            await LoadCategoriesAsync();
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

    protected string GetStatusBadgeClass(bool isActive)
    {
        return isActive ? "bg-success" : "bg-secondary";
    }

    protected string GetStatusText(bool isActive)
    {
        return isActive ? "Active" : "Inactive";
    }

    protected string GetFeatureTypeBadgeClass(bool isStandard)
    {
        return isStandard ? "bg-primary" : "bg-warning text-dark";
    }

    protected string GetFeatureTypeText(bool isStandard)
    {
        return isStandard ? "Standard" : "Premium";
    }

    
}