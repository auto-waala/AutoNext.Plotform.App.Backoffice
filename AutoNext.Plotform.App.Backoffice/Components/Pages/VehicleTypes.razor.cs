using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class VehicleTypesBase : ComponentBase
{
    [Inject] protected IVehicleTypeService VehicleTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<VehicleTypesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<VehicleTypeResponseDto> AllTypes { get; set; } = new();
    protected List<VehicleTypeResponseDto> SelectedTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected VehicleType? selectedType = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected bool? ActiveFilter { get; set; }
    protected string? SortOrderFilter { get; set; }

    protected int ActiveCount => AllTypes.Count(t => t.IsActive);
    protected int InactiveCount => AllTypes.Count(t => !t.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<VehicleTypeResponseDto> FilteredTypes => AllTypes
        .Where(t => string.IsNullOrEmpty(SearchTerm) ||
                     t.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     t.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(t => ActiveFilter == null || t.IsActive == ActiveFilter)
        .OrderBy(t => SortOrderFilter == "asc" ? t.SortOrder : (SortOrderFilter == "desc" ? -t.SortOrder : t.SortOrder))
        .ToList();

    protected List<VehicleTypeResponseDto> PaginatedTypes => FilteredTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedTypes.Any() && PaginatedTypes.All(t => SelectedTypes.Contains(t));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("VehicleTypes page initialized");
        await LoadAllTypesAsync();
    }

    protected async Task LoadAllTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading vehicle types...");
            IsLoading = true;

            var types = await VehicleTypeService.GetAllAsync();

            AllTypes = types?.ToList() ?? new List<VehicleTypeResponseDto>();
            TotalCount = FilteredTypes.Count;
            CurrentPage = 1;
            SelectedTypes.Clear();

            Logger.LogInformation("Loaded {Count} vehicle types", AllTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading vehicle types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle types.");
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
        TotalCount = FilteredTypes.Count;
        SelectedTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        ActiveFilter = null;
        SortOrderFilter = null;
        ApplyFilters();
    }

    protected void OpenAddTypeSidebar()
    {
        selectedType = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditTypeSidebar(VehicleTypeResponseDto type)
    {
        selectedType = Mapper.Map<VehicleType>(type);
        showSidebar = true;
    }

    protected async Task OnTypeSave(VehicleType type)
    {
        try
        {
            LoaderService.Show(type.Id != null ? "Updating type..." : "Creating type...");

            if (type.Id.HasValue && type.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<VehicleTypeUpdateDto>(type);
                var updated = await VehicleTypeService.UpdateAsync(updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Type '{type.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<VehicleTypeCreateDto>(type);
                await VehicleTypeService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Type '{type.Name}' created successfully");
            }

            showSidebar = false;
            selectedType = null;

            await LoadAllTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving vehicle type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save type: {ex.Message}");
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

    protected void OnTypeSelectionChanged(VehicleTypeResponseDto type, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedTypes.Contains(type))
                SelectedTypes.Add(type);
        }
        else
        {
            SelectedTypes.Remove(type);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedTypes.Count != PaginatedTypes.Count;

        if (selectAll)
        {
            foreach (var type in PaginatedTypes)
            {
                if (!SelectedTypes.Contains(type))
                    SelectedTypes.Add(type);
            }
        }
        else
        {
            foreach (var type in PaginatedTypes)
            {
                SelectedTypes.Remove(type);
            }
        }
    }

    protected async Task ToggleStatus(VehicleTypeResponseDto type)
    {
        try
        {
            LoaderService.Show($"Updating {type.Name} status...");
            var success = await VehicleTypeService.ToggleActiveAsync(type.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{type.Name} is now {(type.IsActive ? "inactive" : "active")}");
                await LoadAllTypesAsync();
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

    protected async Task DeleteType(VehicleTypeResponseDto type)
    {
        if (type.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{type.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {type.Name}...");
            var success = await VehicleTypeService.DeleteAsync(type.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{type.Name} deleted");
                await LoadAllTypesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {type.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {type.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedTypes.Count} types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var type in SelectedTypes.ToList())
            {
                try
                {
                    var success = await VehicleTypeService.DeleteAsync(type.Id);
                    if (!success)
                    {
                        failed.Add(type.Name);
                    }
                }
                catch
                {
                    failed.Add(type.Name);
                }
            }

            SelectedTypes.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected types deleted");
            }

            await LoadAllTypesAsync();
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