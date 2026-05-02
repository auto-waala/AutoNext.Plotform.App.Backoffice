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
public class TransmissionsBase : ComponentBase
{
    [Inject] protected ITransmissionService TransmissionService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<TransmissionsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<TransmissionResponseDto> AllTransmissions { get; set; } = new();
    protected List<TransmissionResponseDto> SelectedTransmissions { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected Transmission? selectedTransmission = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected int? GearsFilter { get; set; }
    protected bool? ActiveFilter { get; set; }
    protected string? SortOrderFilter { get; set; }

    protected List<int> AvailableGears { get; set; } = new();

    protected int ActiveCount => AllTransmissions.Count(t => t.IsActive);
    protected int InactiveCount => AllTransmissions.Count(t => !t.IsActive);
    protected int GearsCountTotal => AllTransmissions.Count(t => t.GearsCount.HasValue);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<TransmissionResponseDto> FilteredTransmissions => AllTransmissions
        .Where(t => string.IsNullOrEmpty(SearchTerm) ||
                     t.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     t.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(t => !GearsFilter.HasValue || t.GearsCount == GearsFilter)
        .Where(t => ActiveFilter == null || t.IsActive == ActiveFilter)
        .OrderBy(t => SortOrderFilter == "asc" ? t.SortOrder : (SortOrderFilter == "desc" ? -t.SortOrder : t.SortOrder))
        .ToList();

    protected List<TransmissionResponseDto> PaginatedTransmissions => FilteredTransmissions
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedTransmissions.Any() && PaginatedTransmissions.All(t => SelectedTransmissions.Contains(t));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Transmissions page initialized");
        await LoadAllTransmissionsAsync();
        LoadGearsFilter();
    }

    protected async Task LoadAllTransmissionsAsync()
    {
        try
        {
            LoaderService.Show("Loading transmissions...");
            IsLoading = true;

            var transmissions = await TransmissionService.GetAllAsync();

            AllTransmissions = transmissions?.ToList() ?? new List<TransmissionResponseDto>();
            TotalCount = FilteredTransmissions.Count;
            CurrentPage = 1;
            SelectedTransmissions.Clear();

            Logger.LogInformation("Loaded {Count} transmissions", AllTransmissions.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading transmissions");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load transmissions.");
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected void LoadGearsFilter()
    {
        AvailableGears = AllTransmissions
            .Where(t => t.GearsCount.HasValue)
            .Select(t => t.GearsCount.Value)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredTransmissions.Count;
        SelectedTransmissions.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        GearsFilter = null;
        ActiveFilter = null;
        SortOrderFilter = null;
        ApplyFilters();
    }

    protected void OpenAddTransmissionSidebar()
    {
        selectedTransmission = new Transmission
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

    protected void OpenEditTransmissionSidebar(TransmissionResponseDto transmission)
    {
        selectedTransmission = Mapper.Map<Transmission>(transmission);
        showSidebar = true;
    }

    protected async Task OnTransmissionSave(Transmission transmission)
    {
        try
        {
            LoaderService.Show(transmission.Id != null ? "Updating transmission..." : "Creating transmission...");

            if (transmission.Id.HasValue && transmission.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<TransmissionUpdateDto>(transmission);
                var updated = await TransmissionService.UpdateAsync(updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Transmission '{transmission.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<TransmissionCreateDto>(transmission);
                await TransmissionService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Transmission '{transmission.Name}' created successfully");
            }

            showSidebar = false;
            selectedTransmission = null;

            await LoadAllTransmissionsAsync();
            LoadGearsFilter();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving transmission");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save transmission: {ex.Message}");
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

    protected void OnTransmissionSelectionChanged(TransmissionResponseDto transmission, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedTransmissions.Contains(transmission))
                SelectedTransmissions.Add(transmission);
        }
        else
        {
            SelectedTransmissions.Remove(transmission);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedTransmissions.Count != PaginatedTransmissions.Count;

        if (selectAll)
        {
            foreach (var transmission in PaginatedTransmissions)
            {
                if (!SelectedTransmissions.Contains(transmission))
                    SelectedTransmissions.Add(transmission);
            }
        }
        else
        {
            foreach (var transmission in PaginatedTransmissions)
            {
                SelectedTransmissions.Remove(transmission);
            }
        }
    }

    protected async Task ToggleStatus(TransmissionResponseDto transmission)
    {
        try
        {
            LoaderService.Show($"Updating {transmission.Name} status...");
            var success = await TransmissionService.ToggleActiveAsync(transmission.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{transmission.Name} is now {(transmission.IsActive ? "inactive" : "active")}");
                await LoadAllTransmissionsAsync();
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

    protected async Task DeleteTransmission(TransmissionResponseDto transmission)
    {
        if (transmission.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{transmission.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {transmission.Name}...");
            var success = await TransmissionService.DeleteAsync(transmission.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{transmission.Name} deleted");
                await LoadAllTransmissionsAsync();
                LoadGearsFilter();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {transmission.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {transmission.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedTransmissions.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedTransmissions.Count} transmissions? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var transmission in SelectedTransmissions.ToList())
            {
                try
                {
                    var success = await TransmissionService.DeleteAsync(transmission.Id);
                    if (!success)
                    {
                        failed.Add(transmission.Name);
                    }
                }
                catch
                {
                    failed.Add(transmission.Name);
                }
            }

            SelectedTransmissions.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected transmissions deleted");
            }

            await LoadAllTransmissionsAsync();
            LoadGearsFilter();
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