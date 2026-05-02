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
public class VehicleConditionsBase : ComponentBase
{
    [Inject] protected IVehicleConditionService VehicleConditionService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<VehicleConditionsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<VehicleConditionResponseDto> AllConditions { get; set; } = new();
    protected List<VehicleConditionResponseDto> SelectedConditions { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected VehicleCondition? selectedCondition = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected bool? ActiveFilter { get; set; }
    protected string? DisplayOrderFilter { get; set; }

    protected int ActiveCount => AllConditions.Count(c => c.IsActive);
    protected int InactiveCount => AllConditions.Count(c => !c.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<VehicleConditionResponseDto> FilteredConditions => AllConditions
        .Where(c => string.IsNullOrEmpty(SearchTerm) ||
                     c.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     c.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(c => ActiveFilter == null || c.IsActive == ActiveFilter)
        .OrderBy(c => DisplayOrderFilter == "asc" ? c.DisplayOrder : (DisplayOrderFilter == "desc" ? -c.DisplayOrder : c.DisplayOrder))
        .ToList();

    protected List<VehicleConditionResponseDto> PaginatedConditions => FilteredConditions
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedConditions.Any() && PaginatedConditions.All(c => SelectedConditions.Contains(c));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("VehicleConditions page initialized");
        await LoadAllConditionsAsync();
    }

    protected async Task LoadAllConditionsAsync()
    {
        try
        {
            LoaderService.Show("Loading vehicle conditions...");
            IsLoading = true;

            var conditions = await VehicleConditionService.GetAllAsync();

            AllConditions = conditions?.ToList() ?? new List<VehicleConditionResponseDto>();
            TotalCount = FilteredConditions.Count;
            CurrentPage = 1;
            SelectedConditions.Clear();

            Logger.LogInformation("Loaded {Count} vehicle conditions", AllConditions.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading vehicle conditions");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle conditions.");
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
        TotalCount = FilteredConditions.Count;
        SelectedConditions.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        ActiveFilter = null;
        DisplayOrderFilter = null;
        ApplyFilters();
    }

    protected void OpenAddConditionSidebar()
    {
        selectedCondition = new VehicleCondition
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditConditionSidebar(VehicleConditionResponseDto condition)
    {
        selectedCondition = Mapper.Map<VehicleCondition>(condition);
        showSidebar = true;
    }

    protected async Task OnConditionSave(VehicleCondition condition)
    {
        try
        {
            LoaderService.Show(condition.Id != null ? "Updating condition..." : "Creating condition...");

            if (condition.Id.HasValue && condition.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<VehicleConditionUpdateDto>(condition);
                var updated = await VehicleConditionService.UpdateAsync(condition.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Condition '{condition.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<VehicleConditionCreateDto>(condition);
                await VehicleConditionService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Condition '{condition.Name}' created successfully");
            }

            showSidebar = false;
            selectedCondition = null;

            await LoadAllConditionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving vehicle condition");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save condition: {ex.Message}");
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

    protected void OnConditionSelectionChanged(VehicleConditionResponseDto condition, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedConditions.Contains(condition))
                SelectedConditions.Add(condition);
        }
        else
        {
            SelectedConditions.Remove(condition);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedConditions.Count != PaginatedConditions.Count;

        if (selectAll)
        {
            foreach (var condition in PaginatedConditions)
            {
                if (!SelectedConditions.Contains(condition))
                    SelectedConditions.Add(condition);
            }
        }
        else
        {
            foreach (var condition in PaginatedConditions)
            {
                SelectedConditions.Remove(condition);
            }
        }
    }

    protected async Task ToggleStatus(VehicleConditionResponseDto condition)
    {
        try
        {
            LoaderService.Show($"Updating {condition.Name} status...");
            var success = await VehicleConditionService.ToggleStatusAsync(condition.Id, !condition.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{condition.Name} is now {(condition.IsActive ? "inactive" : "active")}");
                await LoadAllConditionsAsync();
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

    protected async Task DeleteCondition(VehicleConditionResponseDto condition)
    {
        if (condition.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{condition.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {condition.Name}...");
            var success = await VehicleConditionService.DeleteAsync(condition.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{condition.Name} deleted");
                await LoadAllConditionsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {condition.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {condition.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedConditions.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedConditions.Count} conditions? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var condition in SelectedConditions.ToList())
            {
                try
                {
                    var success = await VehicleConditionService.DeleteAsync(condition.Id);
                    if (!success)
                    {
                        failed.Add(condition.Name);
                    }
                }
                catch
                {
                    failed.Add(condition.Name);
                }
            }

            SelectedConditions.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected conditions deleted");
            }

            await LoadAllConditionsAsync();
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