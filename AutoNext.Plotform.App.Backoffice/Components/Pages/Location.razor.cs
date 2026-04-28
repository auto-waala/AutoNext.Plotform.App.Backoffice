using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class LocationBase : ComponentBase
{
    [Inject] protected ILocationService LocationService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<LocationBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<LocationResponseDto> AllLocations { get; set; } = new();
    protected List<LocationResponseDto> SelectedLocations { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected AutoNext.Plotform.App.Backoffice.Models.Core.Location? selectedLocation = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? CountryFilter { get; set; }
    protected string? StateFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableCountries { get; set; } = new();
    protected List<string> AvailableStates { get; set; } = new();

    protected int ActiveCount => AllLocations.Count();
    protected int InactiveCount => AllLocations.Count();
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected bool HasActiveFilters => !string.IsNullOrEmpty(SearchTerm) ||
                                        CountryFilter != null ||
                                        StateFilter != null ||
                                        ActiveFilter != null;

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<LocationResponseDto> FilteredLocations => AllLocations
        .Where(l => string.IsNullOrEmpty(SearchTerm) ||
                    l.CityName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    l.StateName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    l.CountryName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(l => string.IsNullOrEmpty(CountryFilter) || l.CountryName == CountryFilter)
        .Where(l => string.IsNullOrEmpty(StateFilter) || l.StateName == StateFilter)
        .Where(l => ActiveFilter == null)
        .ToList();

    protected List<LocationResponseDto> PaginatedLocations => FilteredLocations
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedLocations.Any() &&
                                      PaginatedLocations.All(l => SelectedLocations.Contains(l));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Location page initialized");
        await LoadAllLocationsAsync();
    }

    protected async Task LoadAllLocationsAsync()
    {
        try
        {
            LoaderService.Show("Loading locations...");
            IsLoading = true;

            var locations = await LocationService.GetAllLocationsAsync();

            AllLocations = locations?.ToList() ?? new();
            TotalCount = FilteredLocations.Count;
            CurrentPage = 1;
            SelectedLocations.Clear();

            LoadFilterOptions();

            Logger.LogInformation("Loaded {Count} locations", AllLocations.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading locations");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load locations.");
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
        AvailableCountries = AllLocations
            .Select(l => l.CountryName)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        AvailableStates = AllLocations
            .Select(l => l.StateName)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredLocations.Count;
        SelectedLocations.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CountryFilter = null;
        StateFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddLocationSidebar()
    {
        selectedLocation = new AutoNext.Plotform.App.Backoffice.Models.Core.Location
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditLocationSidebar(LocationResponseDto location)
    {
        selectedLocation = Mapper.Map<AutoNext.Plotform.App.Backoffice.Models.Core.Location>(location);
        showSidebar = true;
    }

    protected async Task OnLocationSave(AutoNext.Plotform.App.Backoffice.Models.Core.Location location)
    {
        try
        {
            LoaderService.Show(location.Id != Guid.Empty ? "Updating location..." : "Creating location...");

            if (location.Id != null && location.Id != Guid.Empty)
            {
                // Update: map to create DTO (adjust to UpdateDto if you have one)
                var updateDto = new LocationCreateDto
                {
                    CountryName = location.CountryName,
                    CountryCode = location.CountryCode,
                    StateName = location.StateName,
                    StateCode = location.StateCode,
                    CityName = location.CityName,
                    District = location.District,
                    Pincode = location.Pincode,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };

                var updated = await LocationService.CreateLocationAsync(updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Location '{location.CityName}' updated successfully");
                }
            }
            else
            {
                var createDto = new LocationCreateDto
                {
                    CountryName = location.CountryName,
                    CountryCode = location.CountryCode,
                    StateName = location.StateName,
                    StateCode = location.StateCode,
                    CityName = location.CityName,
                    District = location.District,
                    Pincode = location.Pincode,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };

                await LocationService.CreateLocationAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Location '{location.CityName}' created successfully");
            }

            showSidebar = false;
            selectedLocation = null;
            await LoadAllLocationsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving location");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save location: {ex.Message}");
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

    protected void OnLocationSelectionChanged(LocationResponseDto location, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedLocations.Contains(location))
                SelectedLocations.Add(location);
        }
        else
        {
            SelectedLocations.Remove(location);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedLocations.Count != PaginatedLocations.Count;

        if (selectAll)
        {
            foreach (var location in PaginatedLocations)
                if (!SelectedLocations.Contains(location))
                    SelectedLocations.Add(location);
        }
        else
        {
            foreach (var location in PaginatedLocations)
                SelectedLocations.Remove(location);
        }
    }

    protected async Task ToggleStatus(LocationResponseDto location)
    {
        try
        {
            LoaderService.Show($"Updating {location.CityName} status...");

            // Delete + recreate pattern if no dedicated toggle endpoint exists
            // Replace with a dedicated ToggleStatus API call if available
            NotificationService.Notify(NotificationSeverity.Info, "Info",
                "Toggle status requires a dedicated API endpoint.");
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

    protected async Task DeleteLocation(LocationResponseDto location)
    {
        if (location.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete '{location.CityName}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {location.CityName}...");
            var success = await LocationService.DeleteLocationAsync(location.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted",
                    $"{location.CityName} deleted");
                await LoadAllLocationsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to delete {location.CityName}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to delete {location.CityName}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedLocations.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete {SelectedLocations.Count} locations? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var location in SelectedLocations.ToList())
            {
                try
                {
                    var success = await LocationService.DeleteLocationAsync(location.Id);
                    if (!success) failed.Add(location.CityName);
                }
                catch
                {
                    failed.Add(location.CityName);
                }
            }

            SelectedLocations.Clear();

            if (failed.Any())
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            else
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected locations deleted");

            await LoadAllLocationsAsync();
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