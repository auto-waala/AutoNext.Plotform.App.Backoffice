using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

[Authorize]
public class FuelTypesBase : ComponentBase
{
    [Inject] protected IFuelTypeService FuelTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<FuelTypesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<FuelTypeResponseDto> AllFuelTypes { get; set; } = new();
    protected List<FuelTypeResponseDto> SelectedFuelTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;

    protected FuelType? selectedFuelType = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected bool? ActiveFilter { get; set; }

    protected int ActiveCount => AllFuelTypes.Count(ft => ft.IsActive);
    protected int InactiveCount => AllFuelTypes.Count(ft => !ft.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<FuelTypeResponseDto> FilteredFuelTypes => AllFuelTypes
        .Where(ft => string.IsNullOrEmpty(SearchTerm) ||
                     ft.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     ft.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     (ft.Description != null && ft.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)))
        .Where(ft => ActiveFilter == null || ft.IsActive == ActiveFilter)
        .OrderBy(ft => ft.SortOrder)
        .ThenBy(ft => ft.Name)
        .ToList();

    protected List<FuelTypeResponseDto> PaginatedFuelTypes => FilteredFuelTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedFuelTypes.Any() && PaginatedFuelTypes.All(ft => SelectedFuelTypes.Contains(ft));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("FuelTypes page initialized");
        await LoadAllFuelTypesAsync();
    }

    protected async Task LoadAllFuelTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading fuel types...");
            IsLoading = true;

            var fuelTypes = await FuelTypeService.GetAllAsync(onlyActive: false);

            AllFuelTypes = fuelTypes?.ToList() ?? new List<FuelTypeResponseDto>();
            TotalCount = FilteredFuelTypes.Count;
            CurrentPage = 1;
            SelectedFuelTypes.Clear();

            Logger.LogInformation("Loaded {Count} fuel types", AllFuelTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading fuel types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load fuel types.");
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
        TotalCount = FilteredFuelTypes.Count;
        SelectedFuelTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddFuelTypeSidebar()
    {
        selectedFuelType = new FuelType
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditFuelTypeSidebar(FuelTypeResponseDto fuelType)
    {
        selectedFuelType = Mapper.Map<FuelType>(fuelType);
        showSidebar = true;
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
            StateHasChanged();
        }
    }

    protected void OnFuelTypeSelectionChanged(FuelTypeResponseDto fuelType, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedFuelTypes.Contains(fuelType))
                SelectedFuelTypes.Add(fuelType);
        }
        else
        {
            SelectedFuelTypes.Remove(fuelType);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedFuelTypes.Count != PaginatedFuelTypes.Count;

        if (selectAll)
        {
            foreach (var fuelType in PaginatedFuelTypes)
            {
                if (!SelectedFuelTypes.Contains(fuelType))
                    SelectedFuelTypes.Add(fuelType);
            }
        }
        else
        {
            foreach (var fuelType in PaginatedFuelTypes)
            {
                SelectedFuelTypes.Remove(fuelType);
            }
        }
    }

    protected async Task ToggleStatus(FuelTypeResponseDto fuelType)
    {
        try
        {
            LoaderService.Show($"Updating {fuelType.Name} status...");
            var success = await FuelTypeService.ToggleActiveAsync(fuelType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{fuelType.Name} is now {(fuelType.IsActive ? "inactive" : "active")}");
                await LoadAllFuelTypesAsync();
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

    protected async Task DeleteFuelType(FuelTypeResponseDto fuelType)
    {
        if (fuelType.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{fuelType.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {fuelType.Name}...");
            var success = await FuelTypeService.DeleteAsync(fuelType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{fuelType.Name} deleted");
                await LoadAllFuelTypesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {fuelType.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {fuelType.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedFuelTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedFuelTypes.Count} fuel types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var fuelType in SelectedFuelTypes.ToList())
            {
                try
                {
                    var success = await FuelTypeService.DeleteAsync(fuelType.Id);
                    if (!success)
                    {
                        failed.Add(fuelType.Name);
                    }
                }
                catch
                {
                    failed.Add(fuelType.Name);
                }
            }

            SelectedFuelTypes.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected fuel types deleted");
            }

            await LoadAllFuelTypesAsync();
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

    protected async Task ReorderFuelTypes(Dictionary<Guid, int> orderMap)
    {
        try
        {
            LoaderService.Show("Reordering fuel types...");

            // Update each fuel type with new sort order
            foreach (var fuelType in AllFuelTypes)
            {
                if (orderMap.ContainsKey(fuelType.Id))
                {
                    var updateDto = new FuelTypeUpdateDto
                    {
                        Id = fuelType.Id,
                        Name = fuelType.Name,
                        Code = fuelType.Code,
                        Description = fuelType.Description,
                        IconUrl = fuelType.IconUrl,
                        SortOrder = orderMap[fuelType.Id],
                        IsActive = fuelType.IsActive
                    };
                    await FuelTypeService.UpdateAsync(updateDto);
                }
            }

            NotificationService.Notify(NotificationSeverity.Success, "Success", "Fuel types reordered successfully");
            await LoadAllFuelTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reordering fuel types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to reorder fuel types");
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

    // Add these methods to your FuelTypesBase class

    protected void OnActiveFilterChanged(ChangeEventArgs e)
    {
        if (e.Value?.ToString() == "true")
            ActiveFilter = true;
        else if (e.Value?.ToString() == "false")
            ActiveFilter = false;
        else
            ActiveFilter = null;

        ApplyFilters();
    }

    // Also add this to fix the Id check issue in OnFuelTypeSave
    protected async Task OnFuelTypeSave(FuelType fuelType)
    {
        try
        {
            LoaderService.Show(fuelType.Id != Guid.Empty ? "Updating fuel type..." : "Creating fuel type...");

            if (fuelType.Id != Guid.Empty && fuelType.Id != Guid.Empty) // Fix: This condition is redundant
            {
                var updateDto = new FuelTypeUpdateDto
                {
                    Id = fuelType.Id.Value,
                    Name = fuelType.Name,
                    Code = fuelType.Code,
                    Description = fuelType.Description,
                    IconUrl = fuelType.IconUrl,
                    SortOrder = fuelType.SortOrder,
                    IsActive = fuelType.IsActive
                };

                var updated = await FuelTypeService.UpdateAsync(updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Fuel type '{fuelType.Name}' updated successfully");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update fuel type");
                }
            }
            else
            {
                var createDto = new FuelTypeCreateDto
                {
                    Name = fuelType.Name,
                    Code = fuelType.Code,
                    Description = fuelType.Description,
                    IconUrl = fuelType.IconUrl,
                    SortOrder = fuelType.SortOrder
                };

                await FuelTypeService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Fuel type '{fuelType.Name}' created successfully");
            }

            showSidebar = false;
            selectedFuelType = null;

            await LoadAllFuelTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving fuel type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save fuel type: {ex.Message}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }
}