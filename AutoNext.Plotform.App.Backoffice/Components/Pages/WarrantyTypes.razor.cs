using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using System.Linq;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class WarrantyTypesBase : ComponentBase
{
    [Inject] protected IWarrantyTypeService WarrantyTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<WarrantyTypesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<WarrantyTypeResponseDto> AllWarrantyTypes { get; set; } = new();
    protected List<WarrantyTypeResponseDto> SelectedWarrantyTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected WarrantyType? selectedWarrantyType = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected bool? TransferableFilter { get; set; }
    protected bool? ActiveFilter { get; set; }
    protected int? MinDurationMonths { get; set; }
    protected int? MaxDurationMonths { get; set; }

    protected int ActiveCount => AllWarrantyTypes.Count(w => w.IsActive);
    protected int InactiveCount => AllWarrantyTypes.Count(w => !w.IsActive);
    protected int TransferableCount => AllWarrantyTypes.Count(w => w.IsTransferable);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<WarrantyTypeResponseDto> FilteredWarrantyTypes => AllWarrantyTypes
        .Where(w => string.IsNullOrEmpty(SearchTerm) ||
                     w.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     w.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(w => TransferableFilter == null || w.IsTransferable == TransferableFilter)
        .Where(w => ActiveFilter == null || w.IsActive == ActiveFilter)
        .Where(w => !MinDurationMonths.HasValue || (w.DurationMonths.HasValue && w.DurationMonths >= MinDurationMonths))
        .Where(w => !MaxDurationMonths.HasValue || (w.DurationMonths.HasValue && w.DurationMonths <= MaxDurationMonths))
        .ToList();

    protected List<WarrantyTypeResponseDto> PaginatedWarrantyTypes => FilteredWarrantyTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedWarrantyTypes.Any() && PaginatedWarrantyTypes.All(w => SelectedWarrantyTypes.Contains(w));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("WarrantyTypes page initialized");
        await LoadAllWarrantyTypesAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadAllWarrantyTypesAsync();
    }

    protected async Task LoadAllWarrantyTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading warranty types...");
            IsLoading = true;

            var warrantyTypes = await WarrantyTypeService.GetAllWarrantyTypesAsync();

            AllWarrantyTypes = warrantyTypes?.ToList() ?? new List<WarrantyTypeResponseDto>();
            TotalCount = FilteredWarrantyTypes.Count;
            CurrentPage = 1;
            SelectedWarrantyTypes.Clear();

            Logger.LogInformation("Loaded {Count} warranty types", AllWarrantyTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading warranty types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load warranty types.");
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
        TotalCount = FilteredWarrantyTypes.Count;
        SelectedWarrantyTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        TransferableFilter = null;
        ActiveFilter = null;
        MinDurationMonths = null;
        MaxDurationMonths = null;
        ApplyFilters();
    }

    protected void OpenAddWarrantyTypeSidebar()
    {
        selectedWarrantyType = new WarrantyType
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            IsTransferable = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditWarrantyTypeSidebar(WarrantyTypeResponseDto warrantyType)
    {
        selectedWarrantyType = Mapper.Map<WarrantyType>(warrantyType);
        showSidebar = true;
    }

    protected async Task OnWarrantyTypeSave(WarrantyType warrantyType)
    {
        try
        {
            LoaderService.Show(warrantyType.Id.HasValue && warrantyType.Id != Guid.Empty ? "Updating warranty type..." : "Creating warranty type...");

            if (warrantyType.Id.HasValue && warrantyType.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<WarrantyTypeUpdateDto>(warrantyType);
                var updated = await WarrantyTypeService.UpdateWarrantyTypeAsync(warrantyType.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Warranty type '{warrantyType.Name}' updated successfully");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update warranty type");
                    return;
                }
            }
            else
            {
                var createDto = Mapper.Map<WarrantyTypeCreateDto>(warrantyType);
                await WarrantyTypeService.CreateWarrantyTypeAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Warranty type '{warrantyType.Name}' created successfully");
            }

            showSidebar = false;
            selectedWarrantyType = null;

            await LoadAllWarrantyTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving warranty type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save warranty type: {ex.Message}");
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

    protected void OnWarrantyTypeSelectionChanged(WarrantyTypeResponseDto warrantyType, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedWarrantyTypes.Contains(warrantyType))
                SelectedWarrantyTypes.Add(warrantyType);
        }
        else
        {
            SelectedWarrantyTypes.Remove(warrantyType);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedWarrantyTypes.Count != PaginatedWarrantyTypes.Count;

        if (selectAll)
        {
            foreach (var warrantyType in PaginatedWarrantyTypes)
            {
                if (!SelectedWarrantyTypes.Contains(warrantyType))
                    SelectedWarrantyTypes.Add(warrantyType);
            }
        }
        else
        {
            foreach (var warrantyType in PaginatedWarrantyTypes)
            {
                SelectedWarrantyTypes.Remove(warrantyType);
            }
        }
    }

    protected async Task ToggleStatus(WarrantyTypeResponseDto warrantyType)
    {
        try
        {
            LoaderService.Show($"Updating {warrantyType.Name} status...");
            var success = await WarrantyTypeService.ToggleWarrantyTypeStatusAsync(warrantyType.Id, !warrantyType.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{warrantyType.Name} is now {(warrantyType.IsActive ? "inactive" : "active")}");
                await LoadAllWarrantyTypesAsync();
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

    protected async Task DeleteWarrantyType(WarrantyTypeResponseDto warrantyType)
    {
        if (warrantyType.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{warrantyType.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {warrantyType.Name}...");
            var success = await WarrantyTypeService.DeleteWarrantyTypeAsync(warrantyType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{warrantyType.Name} deleted");
                await LoadAllWarrantyTypesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {warrantyType.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {warrantyType.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedWarrantyTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedWarrantyTypes.Count} warranty types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var warrantyType in SelectedWarrantyTypes.ToList())
            {
                try
                {
                    var success = await WarrantyTypeService.DeleteWarrantyTypeAsync(warrantyType.Id);
                    if (!success)
                    {
                        failed.Add(warrantyType.Name);
                    }
                }
                catch
                {
                    failed.Add(warrantyType.Name);
                }
            }

            SelectedWarrantyTypes.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected warranty types deleted");
            }

            await LoadAllWarrantyTypesAsync();
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