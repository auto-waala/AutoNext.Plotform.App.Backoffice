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
public class InspectionChecklistBase : ComponentBase
{
    [Inject] protected IInspectionChecklistService ChecklistService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<InspectionChecklistBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<InspectionChecklistResponseDto> AllChecklists { get; set; } = new();
    protected List<InspectionChecklistResponseDto> SelectedChecklists { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected AutoNext.Plotform.App.Backoffice.Models.Core.InspectionChecklist? selectedChecklist = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? CategoryFilter { get; set; }
    protected bool? CriticalFilter { get; set; }
    protected int? WeightageFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableCategories { get; set; } = new();

    protected int ActiveCount => AllChecklists.Count(cl => cl.IsActive);
    protected int InactiveCount => AllChecklists.Count(cl => !cl.IsActive);
    protected int CriticalCount => AllChecklists.Count(cl => cl.IsCritical);
    protected double AverageWeightage => AllChecklists.Any() ? AllChecklists.Average(cl => cl.Weightage) : 0;
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);
    protected bool HasActiveFilters => !string.IsNullOrEmpty(SearchTerm) ||
                                        CategoryFilter != null ||
                                        CriticalFilter != null ||
                                        WeightageFilter != null ||
                                        ActiveFilter != null;

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<InspectionChecklistResponseDto> FilteredChecklists => AllChecklists
        .Where(cl => string.IsNullOrEmpty(SearchTerm) ||
                     cl.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     cl.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(cl => string.IsNullOrEmpty(CategoryFilter) || cl.Category == CategoryFilter)
        .Where(cl => CriticalFilter == null || cl.IsCritical == CriticalFilter)
        .Where(cl => WeightageFilter == null || cl.Weightage == WeightageFilter)
        .Where(cl => ActiveFilter == null || cl.IsActive == ActiveFilter)
        .ToList();

    protected List<InspectionChecklistResponseDto> PaginatedChecklists => FilteredChecklists
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedChecklists.Any() &&
                                      PaginatedChecklists.All(cl => SelectedChecklists.Contains(cl));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("InspectionChecklist page initialized");
        await LoadAllChecklistsAsync();
        await LoadCategoriesAsync();
    }

    protected async Task LoadAllChecklistsAsync()
    {
        try
        {
            LoaderService.Show("Loading inspection checklists...");
            IsLoading = true;

            var checklists = await ChecklistService.GetAllAsync();

            AllChecklists = checklists?.ToList() ?? new List<InspectionChecklistResponseDto>();
            TotalCount = FilteredChecklists.Count;
            CurrentPage = 1;
            SelectedChecklists.Clear();

            Logger.LogInformation("Loaded {Count} inspection checklists", AllChecklists.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading inspection checklists");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load inspection checklists.");
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
            // Get distinct categories from loaded checklists
            AvailableCategories = AllChecklists
                .Where(cl => !string.IsNullOrEmpty(cl.Category))
                .Select(cl => cl.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading categories");
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredChecklists.Count;
        SelectedChecklists.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CategoryFilter = null;
        CriticalFilter = null;
        WeightageFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddChecklistSidebar()
    {
        selectedChecklist = new AutoNext.Plotform.App.Backoffice.Models.Core.InspectionChecklist
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            IsCritical = false,
            Weightage = 1,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditChecklistSidebar(InspectionChecklistResponseDto checklist)
    {
        selectedChecklist = Mapper.Map<AutoNext.Plotform.App.Backoffice.Models.Core.InspectionChecklist>(checklist);
        showSidebar = true;
    }

    protected async Task OnChecklistSave(AutoNext.Plotform.App.Backoffice.Models.Core.InspectionChecklist checklist)
    {
        try
        {
            LoaderService.Show(checklist.Id != Guid.Empty ? "Updating checklist..." : "Creating checklist...");

            if (checklist.Id != Guid.Empty && checklist.Id != Guid.Empty)
            {
                var updateDto = new InspectionChecklistUpdateDto
                {
                    Name = checklist.Name,
                    Code = checklist.Code,
                    Category = checklist.Category,
                    IsCritical = checklist.IsCritical,
                    Weightage = checklist.Weightage,
                    DisplayOrder = checklist.DisplayOrder,
                    IsActive = checklist.IsActive
                };

                var updated = await ChecklistService.UpdateAsync(checklist.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Inspection checklist '{checklist.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = new InspectionChecklistCreateDto
                {
                    Name = checklist.Name,
                    Code = checklist.Code,
                    Category = checklist.Category,
                    IsCritical = checklist.IsCritical,
                    Weightage = checklist.Weightage,
                    DisplayOrder = checklist.DisplayOrder
                };

                await ChecklistService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Inspection checklist '{checklist.Name}' created successfully");
            }

            showSidebar = false;
            selectedChecklist = null;

            await LoadAllChecklistsAsync();
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving inspection checklist");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save inspection checklist: {ex.Message}");
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

    protected void OnChecklistSelectionChanged(InspectionChecklistResponseDto checklist, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedChecklists.Contains(checklist))
                SelectedChecklists.Add(checklist);
        }
        else
        {
            SelectedChecklists.Remove(checklist);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedChecklists.Count != PaginatedChecklists.Count;

        if (selectAll)
        {
            foreach (var checklist in PaginatedChecklists)
            {
                if (!SelectedChecklists.Contains(checklist))
                    SelectedChecklists.Add(checklist);
            }
        }
        else
        {
            foreach (var checklist in PaginatedChecklists)
            {
                SelectedChecklists.Remove(checklist);
            }
        }
    }

    protected async Task ToggleStatus(InspectionChecklistResponseDto checklist)
    {
        try
        {
            LoaderService.Show($"Updating {checklist.Name} status...");
            var success = await ChecklistService.ToggleStatusAsync(checklist.Id, !checklist.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{checklist.Name} is now {(checklist.IsActive ? "inactive" : "active")}");
                await LoadAllChecklistsAsync();
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

    protected async Task DeleteChecklist(InspectionChecklistResponseDto checklist)
    {
        if (checklist.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete '{checklist.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {checklist.Name}...");
            var success = await ChecklistService.DeleteAsync(checklist.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted",
                    $"{checklist.Name} deleted");
                await LoadAllChecklistsAsync();
                await LoadCategoriesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to delete {checklist.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to delete {checklist.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedChecklists.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete {SelectedChecklists.Count} inspection checklists? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var checklist in SelectedChecklists.ToList())
            {
                try
                {
                    var success = await ChecklistService.DeleteAsync(checklist.Id);
                    if (!success)
                    {
                        failed.Add(checklist.Name);
                    }
                }
                catch
                {
                    failed.Add(checklist.Name);
                }
            }

            SelectedChecklists.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected checklists deleted");
            }

            await LoadAllChecklistsAsync();
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

    protected string GetWeightageBadgeClass(int weightage)
    {
        return weightage >= 8 ? "bg-danger" :
               weightage >= 5 ? "bg-warning text-dark" :
               "bg-info";
    }

    protected string GetCriticalColor(bool isCritical)
    {
        return isCritical ? "linear-gradient(135deg, #dc3545 0%, #c82333 100%)" :
                            "linear-gradient(135deg, #28a745 0%, #20c997 100%)";
    }
}