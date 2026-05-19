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
public class ServiceTypeBase : ComponentBase
{
    [Inject] protected IServiceTypeService ServiceTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<ServiceTypeBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<ServiceTypeResponseDto> AllServiceTypes { get; set; } = new();
    protected List<ServiceTypeResponseDto> SelectedServiceTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected AutoNext.Plotform.App.Backoffice.Models.Core.ServiceType? selectedServiceType = null;

    // Filters
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? CategoryFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableCategories { get; set; } = new();

    // Summary
    protected int ActiveCount => AllServiceTypes.Count(s => s.IsActive);
    protected int InactiveCount => AllServiceTypes.Count(s => !s.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected bool HasActiveFilters => !string.IsNullOrEmpty(SearchTerm) ||
                                        CategoryFilter != null ||
                                        ActiveFilter != null;

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<ServiceTypeResponseDto> FilteredServiceTypes => AllServiceTypes
        .Where(s => string.IsNullOrEmpty(SearchTerm) ||
                    s.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    s.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(s => string.IsNullOrEmpty(CategoryFilter) || s.Category == CategoryFilter)
        .Where(s => ActiveFilter == null || s.IsActive == ActiveFilter)
        .ToList();

    protected List<ServiceTypeResponseDto> PaginatedServiceTypes => FilteredServiceTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedServiceTypes.Any() &&
                                      PaginatedServiceTypes.All(s => SelectedServiceTypes.Contains(s));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("ServiceType page initialized");
        await LoadAllServiceTypesAsync();
    }

    protected async Task LoadAllServiceTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading service types...");
            IsLoading = true;

            var serviceTypes = await ServiceTypeService.GetAllAsync();

            AllServiceTypes = serviceTypes?.ToList() ?? new();
            TotalCount = FilteredServiceTypes.Count;
            CurrentPage = 1;
            SelectedServiceTypes.Clear();

            LoadFilterOptions();

            Logger.LogInformation("Loaded {Count} service types", AllServiceTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading service types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load service types.");
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void LoadFilterOptions()
    {
        AvailableCategories = AllServiceTypes
            .Where(s => !string.IsNullOrEmpty(s.Category))
            .Select(s => s.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredServiceTypes.Count;
        SelectedServiceTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CategoryFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddServiceTypeSidebar()
    {
        selectedServiceType = new AutoNext.Plotform.App.Backoffice.Models.Core.ServiceType
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditServiceTypeSidebar(ServiceTypeResponseDto serviceType)
    {
        selectedServiceType = Mapper.Map<AutoNext.Plotform.App.Backoffice.Models.Core.ServiceType>(serviceType);
        showSidebar = true;
    }

    protected async Task OnServiceTypeSave(AutoNext.Plotform.App.Backoffice.Models.Core.ServiceType serviceType)
    {
        try
        {
            LoaderService.Show(serviceType.Id != Guid.Empty ? "Updating service type..." : "Creating service type...");

            if (serviceType.Id != null && serviceType.Id != Guid.Empty)
            {
                var updateDto = new ServiceTypeUpdateDto
                {
                    Name = serviceType.Name,
                    Code = serviceType.Code,
                    Category = serviceType.Category,
                    IntervalMonths = serviceType.IntervalMonths,
                    IntervalKm = serviceType.IntervalKm,
                    Description = serviceType.Description,
                    IconUrl = serviceType.IconUrl,
                    DisplayOrder = serviceType.DisplayOrder,
                    IsActive = serviceType.IsActive
                };

                var updated = await ServiceTypeService.UpdateAsync(serviceType.Id.Value, updateDto);

                if (updated != null)
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Service type '{serviceType.Name}' updated successfully");
            }
            else
            {
                var createDto = new ServiceTypeCreateDto
                {
                    Name = serviceType.Name,
                    Code = serviceType.Code,
                    Category = serviceType.Category,
                    IntervalMonths = serviceType.IntervalMonths,
                    IntervalKm = serviceType.IntervalKm,
                    Description = serviceType.Description,
                    IconUrl = serviceType.IconUrl,
                    DisplayOrder = serviceType.DisplayOrder
                };

                await ServiceTypeService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Service type '{serviceType.Name}' created successfully");
            }

            showSidebar = false;
            selectedServiceType = null;
            await LoadAllServiceTypesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving service type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save service type: {ex.Message}");
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

    protected void OnServiceTypeSelectionChanged(ServiceTypeResponseDto serviceType, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedServiceTypes.Contains(serviceType))
                SelectedServiceTypes.Add(serviceType);
        }
        else
        {
            SelectedServiceTypes.Remove(serviceType);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedServiceTypes.Count != PaginatedServiceTypes.Count;

        if (selectAll)
        {
            foreach (var s in PaginatedServiceTypes)
                if (!SelectedServiceTypes.Contains(s))
                    SelectedServiceTypes.Add(s);
        }
        else
        {
            foreach (var s in PaginatedServiceTypes)
                SelectedServiceTypes.Remove(s);
        }
    }

    protected async Task ToggleStatus(ServiceTypeResponseDto serviceType)
    {
        try
        {
            LoaderService.Show($"Updating {serviceType.Name} status...");
            var success = await ServiceTypeService.ToggleStatusAsync(serviceType.Id, !serviceType.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{serviceType.Name} is now {(serviceType.IsActive ? "inactive" : "active")}");
                await LoadAllServiceTypesAsync();
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

    protected async Task DeleteServiceType(ServiceTypeResponseDto serviceType)
    {
        if (serviceType.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete '{serviceType.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {serviceType.Name}...");
            var success = await ServiceTypeService.DeleteAsync(serviceType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted",
                    $"{serviceType.Name} deleted");
                await LoadAllServiceTypesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to delete {serviceType.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to delete {serviceType.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedServiceTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete {SelectedServiceTypes.Count} service types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var s in SelectedServiceTypes.ToList())
            {
                try
                {
                    var success = await ServiceTypeService.DeleteAsync(s.Id);
                    if (!success) failed.Add(s.Name);
                }
                catch
                {
                    failed.Add(s.Name);
                }
            }

            SelectedServiceTypes.Clear();

            if (failed.Any())
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            else
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected service types deleted");

            await LoadAllServiceTypesAsync();
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

    protected string GetStatusBadgeClass(bool isActive) =>
        isActive ? "bg-success" : "bg-secondary";

    protected string GetStatusText(bool isActive) =>
        isActive ? "Active" : "Inactive";
}