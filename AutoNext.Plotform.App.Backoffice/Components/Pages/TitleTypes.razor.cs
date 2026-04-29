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

public class TitleTypesBase : ComponentBase
{
    [Inject] protected ITitleTypeService TitleTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<TitleTypesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<TitleTypeResponseDto> AllTitleTypes { get; set; } = new();
    protected List<TitleTypeResponseDto> SelectedTitleTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected TitleType? selectedTitleType = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected bool? CleanFilter { get; set; }
    protected bool? AffectsValueFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected int ActiveCount => AllTitleTypes.Count(tt => tt.IsActive);
    protected int InactiveCount => AllTitleTypes.Count(tt => !tt.IsActive);
    protected int CleanCount => AllTitleTypes.Count(tt => tt.IsClean);
    protected int SalvorCount => AllTitleTypes.Count(tt => !tt.IsClean);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<TitleTypeResponseDto> FilteredTitleTypes => AllTitleTypes
        .Where(tt => string.IsNullOrEmpty(SearchTerm) ||
                     tt.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     tt.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(tt => CleanFilter == null || tt.IsClean == CleanFilter)
        .Where(tt => AffectsValueFilter == null || tt.AffectsValue == AffectsValueFilter)
        .Where(tt => ActiveFilter == null || tt.IsActive == ActiveFilter)
        .ToList();

    protected List<TitleTypeResponseDto> PaginatedTitleTypes => FilteredTitleTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedTitleTypes.Any() && PaginatedTitleTypes.All(tt => SelectedTitleTypes.Contains(tt));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("TitleTypes page initialized");
        await LoadAllTitleTypesAsync();
    }

    protected async Task LoadAllTitleTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading title types...");
            IsLoading = true;

            var titleTypes = await TitleTypeService.GetAllAsync();

            AllTitleTypes = titleTypes?.ToList() ?? new List<TitleTypeResponseDto>();
            TotalCount = FilteredTitleTypes.Count;
            CurrentPage = 1;
            SelectedTitleTypes.Clear();

            Logger.LogInformation("Loaded {Count} title types", AllTitleTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading title types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load title types.");
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
        TotalCount = FilteredTitleTypes.Count;
        SelectedTitleTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CleanFilter = null;
        AffectsValueFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddTitleTypeSidebar()
    {
        selectedTitleType = new TitleType
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            IsClean = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditTitleTypeSidebar(TitleTypeResponseDto titleType)
    {
        selectedTitleType = Mapper.Map<TitleType>(titleType);
        showSidebar = true;
    }

    protected async Task OnTitleTypeSave(TitleType titleType)
    {
        try
        {
            LoaderService.Show(titleType.Id != null ? "Updating title type..." : "Creating title type...");

            if (titleType.Id.HasValue && titleType.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<TitleTypeUpdateDto>(titleType);
                var updated = await TitleTypeService.UpdateAsync(titleType.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Title type '{titleType.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<TitleTypeCreateDto>(titleType);
                await TitleTypeService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Title type '{titleType.Name}' created successfully");
            }

            showSidebar = false;
            selectedTitleType = null;

            await LoadAllTitleTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving title type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save title type: {ex.Message}");
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

    protected void OnTitleTypeSelectionChanged(TitleTypeResponseDto titleType, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedTitleTypes.Contains(titleType))
                SelectedTitleTypes.Add(titleType);
        }
        else
        {
            SelectedTitleTypes.Remove(titleType);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedTitleTypes.Count != PaginatedTitleTypes.Count;

        if (selectAll)
        {
            foreach (var titleType in PaginatedTitleTypes)
            {
                if (!SelectedTitleTypes.Contains(titleType))
                    SelectedTitleTypes.Add(titleType);
            }
        }
        else
        {
            foreach (var titleType in PaginatedTitleTypes)
            {
                SelectedTitleTypes.Remove(titleType);
            }
        }
    }

    protected async Task ToggleStatus(TitleTypeResponseDto titleType)
    {
        try
        {
            LoaderService.Show($"Updating {titleType.Name} status...");
            var success = await TitleTypeService.ToggleStatusAsync(titleType.Id, !titleType.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{titleType.Name} is now {(titleType.IsActive ? "inactive" : "active")}");
                await LoadAllTitleTypesAsync();
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

    protected async Task DeleteTitleType(TitleTypeResponseDto titleType)
    {
        if (titleType.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{titleType.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {titleType.Name}...");
            var success = await TitleTypeService.DeleteAsync(titleType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{titleType.Name} deleted");
                await LoadAllTitleTypesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {titleType.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {titleType.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedTitleTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedTitleTypes.Count} title types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var titleType in SelectedTitleTypes.ToList())
            {
                try
                {
                    var success = await TitleTypeService.DeleteAsync(titleType.Id);
                    if (!success)
                    {
                        failed.Add(titleType.Name);
                    }
                }
                catch
                {
                    failed.Add(titleType.Name);
                }
            }

            SelectedTitleTypes.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected title types deleted");
            }

            await LoadAllTitleTypesAsync();
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