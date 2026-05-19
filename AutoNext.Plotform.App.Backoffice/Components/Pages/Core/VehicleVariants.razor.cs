using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using System.Linq;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

[Authorize]
public class VehicleVariantsBase : ComponentBase
{
    [Inject] protected IVehicleVariantService VariantService { get; set; } = default!;
    [Inject] protected IVehicleModelService ModelService { get; set; } = default!;
    [Inject] protected IFuelTypeService FuelTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<VehicleVariantsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<VehicleVariantResponseDto> AllVariants { get; set; } = new();
    protected List<VehicleVariantResponseDto> SelectedVariants { get; set; } = new();
    protected List<VehicleModelResponseDto> AvailableModels { get; set; } = new();
    protected List<FuelTypeResponseDto> AvailableFuelTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;

    protected VehicleVariant? selectedVariant = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected Guid? ModelFilter { get; set; }
    protected bool? AvailableFilter { get; set; }
    protected bool? ActiveFilter { get; set; }
    protected Guid? FuelTypeFilter { get; set; }

    protected int ActiveCount => AllVariants.Count(v => v.IsActive);
    protected int InactiveCount => AllVariants.Count(v => !v.IsActive);
    protected int AvailableCount => AllVariants.Count(v => v.IsAvailable);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<VehicleVariantResponseDto> FilteredVariants => AllVariants
        .Where(v => string.IsNullOrEmpty(SearchTerm) ||
                     v.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     v.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     v.ModelName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(v => ModelFilter == null || v.ModelId == ModelFilter)
        .Where(v => AvailableFilter == null || v.IsAvailable == AvailableFilter)
        .Where(v => ActiveFilter == null || v.IsActive == ActiveFilter)
        .Where(v => FuelTypeFilter == null || v.FuelTypeId == FuelTypeFilter)
        .ToList();

    protected List<VehicleVariantResponseDto> PaginatedVariants => FilteredVariants
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedVariants.Any() && PaginatedVariants.All(v => SelectedVariants.Contains(v));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("VehicleVariants page initialized");
        await LoadLookupDataAsync();
        await LoadAllVariantsAsync();
    }

    protected async Task LoadLookupDataAsync()
    {
        try
        {
            var models = await ModelService.GetAllAsync();
            AvailableModels = models?.ToList() ?? new List<VehicleModelResponseDto>();

            var fuelTypes = await FuelTypeService.GetAllAsync();
            AvailableFuelTypes = fuelTypes?.ToList() ?? new List<FuelTypeResponseDto>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading lookup data");
        }
    }

    protected async Task LoadAllVariantsAsync()
    {
        try
        {
            LoaderService.Show("Loading vehicle variants...");
            IsLoading = true;

            var variants = await VariantService.GetAllVariantsAsync();

            AllVariants = variants?.ToList() ?? new List<VehicleVariantResponseDto>();
            TotalCount = FilteredVariants.Count;
            CurrentPage = 1;
            SelectedVariants.Clear();

            Logger.LogInformation("Loaded {Count} vehicle variants", AllVariants.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading vehicle variants");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle variants.");
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredVariants.Count;
        SelectedVariants.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        ModelFilter = null;
        AvailableFilter = null;
        ActiveFilter = null;
        FuelTypeFilter = null;
        ApplyFilters();
    }

    protected void OpenAddVariantSidebar()
    {
        selectedVariant = new VehicleVariant
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            IsAvailable = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditVariantSidebar(VehicleVariantResponseDto variant)
    {
        selectedVariant = Mapper.Map<VehicleVariant>(variant);
        showSidebar = true;
    }

    protected async Task OnVariantSave(VehicleVariant variant)
    {
        try
        {
            LoaderService.Show(variant.Id.HasValue && variant.Id != Guid.Empty ? "Updating variant..." : "Creating variant...");

            if (variant.Id.HasValue && variant.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<VehicleVariantUpdateDto>(variant);
                var updated = await VariantService.UpdateVariantAsync(variant.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Variant '{variant.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<VehicleVariantCreateDto>(variant);
                await VariantService.CreateVariantAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Variant '{variant.Name}' created successfully");
            }

            showSidebar = false;
            selectedVariant = null;

            await LoadAllVariantsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving variant");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save variant: {ex.Message}");
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

    protected void OnVariantSelectionChanged(VehicleVariantResponseDto variant, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedVariants.Contains(variant))
                SelectedVariants.Add(variant);
        }
        else
        {
            SelectedVariants.Remove(variant);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedVariants.Count != PaginatedVariants.Count;

        if (selectAll)
        {
            foreach (var variant in PaginatedVariants)
            {
                if (!SelectedVariants.Contains(variant))
                    SelectedVariants.Add(variant);
            }
        }
        else
        {
            foreach (var variant in PaginatedVariants)
            {
                SelectedVariants.Remove(variant);
            }
        }
    }

    protected async Task ToggleStatus(VehicleVariantResponseDto variant)
    {
        try
        {
            LoaderService.Show($"Updating {variant.Name} status...");
            var success = await VariantService.ToggleVariantStatusAsync(variant.Id, !variant.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{variant.Name} is now {(variant.IsActive ? "inactive" : "active")}");
                await LoadAllVariantsAsync();
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

    protected async Task ToggleAvailability(VehicleVariantResponseDto variant)
    {
        try
        {
            LoaderService.Show($"Updating {variant.Name} availability...");
            var success = await VariantService.ToggleAvailabilityAsync(variant.Id, !variant.IsAvailable);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{variant.Name} is now {(variant.IsAvailable ? "unavailable" : "available")}");
                await LoadAllVariantsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update availability");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling availability");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update availability");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task DeleteVariant(VehicleVariantResponseDto variant)
    {
        if (variant.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{variant.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {variant.Name}...");
            var success = await VariantService.DeleteVariantAsync(variant.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{variant.Name} deleted");
                await LoadAllVariantsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {variant.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {variant.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedVariants.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedVariants.Count} variants? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var variant in SelectedVariants.ToList())
            {
                try
                {
                    var success = await VariantService.DeleteVariantAsync(variant.Id);
                    if (!success)
                    {
                        failed.Add(variant.Name);
                    }
                }
                catch
                {
                    failed.Add(variant.Name);
                }
            }

            SelectedVariants.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected variants deleted");
            }

            await LoadAllVariantsAsync();
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
}