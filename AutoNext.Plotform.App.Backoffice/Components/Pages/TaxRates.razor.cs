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
public class TaxRatesBase : ComponentBase
{
    [Inject] protected ITaxRateService TaxRateService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<TaxRatesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<TaxRateResponseDto> AllTaxRates { get; set; } = new();
    protected List<TaxRateResponseDto> SelectedTaxRates { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected TaxRate? selectedTaxRate = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? TaxTypeFilter { get; set; }
    protected string? CountryFilter { get; set; }
    protected bool? CompoundFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableTaxTypes { get; set; } = new();
    protected List<string> AvailableCountries { get; set; } = new();

    protected int ActiveCount => AllTaxRates.Count(tr => tr.IsActive);
    protected int InactiveCount => AllTaxRates.Count(tr => !tr.IsActive);
    protected int CompoundCount => AllTaxRates.Count(tr => tr.IsCompound);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<TaxRateResponseDto> FilteredTaxRates => AllTaxRates
        .Where(tr => string.IsNullOrEmpty(SearchTerm) ||
                     tr.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     tr.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(tr => string.IsNullOrEmpty(TaxTypeFilter) || tr.TaxType == TaxTypeFilter)
        .Where(tr => string.IsNullOrEmpty(CountryFilter) || tr.Country == CountryFilter)
        .Where(tr => CompoundFilter == null || tr.IsCompound == CompoundFilter)
        .Where(tr => ActiveFilter == null || tr.IsActive == ActiveFilter)
        .ToList();

    protected List<TaxRateResponseDto> PaginatedTaxRates => FilteredTaxRates
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedTaxRates.Any() && PaginatedTaxRates.All(tr => SelectedTaxRates.Contains(tr));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("TaxRates page initialized");
        await LoadAllTaxRatesAsync();
        await LoadFiltersAsync();
    }

    protected async Task LoadAllTaxRatesAsync()
    {
        try
        {
            LoaderService.Show("Loading tax rates...");
            IsLoading = true;

            var taxRates = await TaxRateService.GetAllAsync();

            AllTaxRates = taxRates?.ToList() ?? new List<TaxRateResponseDto>();
            TotalCount = FilteredTaxRates.Count;
            CurrentPage = 1;
            SelectedTaxRates.Clear();

            Logger.LogInformation("Loaded {Count} tax rates", AllTaxRates.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading tax rates");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load tax rates.");
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task LoadFiltersAsync()
    {
        try
        {
            var taxRates = AllTaxRates.Any() ? AllTaxRates : await TaxRateService.GetAllAsync();

            AvailableTaxTypes = taxRates?
                .Where(tr => !string.IsNullOrEmpty(tr.TaxType))
                .Select(tr => tr.TaxType!)
                .Distinct()
                .OrderBy(t => t)
                .ToList() ?? new List<string>();

            AvailableCountries = taxRates?
                .Where(tr => !string.IsNullOrEmpty(tr.Country))
                .Select(tr => tr.Country!)
                .Distinct()
                .OrderBy(c => c)
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading tax rate filters");
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredTaxRates.Count;
        SelectedTaxRates.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        TaxTypeFilter = null;
        CountryFilter = null;
        CompoundFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddTaxRateSidebar()
    {
        selectedTaxRate = new TaxRate
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            Code = string.Empty,
            RatePercentage = 0,
            IsActive = true,
            IsCompound = false,
            DisplayOrder = 0,
            EffectiveFrom = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditTaxRateSidebar(TaxRateResponseDto taxRate)
    {
        selectedTaxRate = Mapper.Map<TaxRate>(taxRate);
        showSidebar = true;
    }

    protected async Task OnTaxRateSave(TaxRate taxRate)
    {
        try
        {
            LoaderService.Show(taxRate.Id != null ? "Updating tax rate..." : "Creating tax rate...");

            if (taxRate.Id.HasValue && taxRate.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<TaxRateUpdateDto>(taxRate);
                var updated = await TaxRateService.UpdateAsync(taxRate.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Tax rate '{taxRate.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<TaxRateCreateDto>(taxRate);
                await TaxRateService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Tax rate '{taxRate.Name}' created successfully");
            }

            showSidebar = false;
            selectedTaxRate = null;

            await LoadAllTaxRatesAsync();
            await LoadFiltersAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving tax rate");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save tax rate: {ex.Message}");
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

    protected void OnTaxRateSelectionChanged(TaxRateResponseDto taxRate, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedTaxRates.Contains(taxRate))
                SelectedTaxRates.Add(taxRate);
        }
        else
        {
            SelectedTaxRates.Remove(taxRate);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedTaxRates.Count != PaginatedTaxRates.Count;

        if (selectAll)
        {
            foreach (var taxRate in PaginatedTaxRates)
            {
                if (!SelectedTaxRates.Contains(taxRate))
                    SelectedTaxRates.Add(taxRate);
            }
        }
        else
        {
            foreach (var taxRate in PaginatedTaxRates)
            {
                SelectedTaxRates.Remove(taxRate);
            }
        }
    }

    protected async Task ToggleStatus(TaxRateResponseDto taxRate)
    {
        try
        {
            LoaderService.Show($"Updating {taxRate.Name} status...");
            var success = await TaxRateService.ToggleStatusAsync(taxRate.Id, !taxRate.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{taxRate.Name} is now {(taxRate.IsActive ? "inactive" : "active")}");
                await LoadAllTaxRatesAsync();
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

    protected async Task DeleteTaxRate(TaxRateResponseDto taxRate)
    {
        if (taxRate.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{taxRate.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {taxRate.Name}...");
            var success = await TaxRateService.DeleteAsync(taxRate.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{taxRate.Name} deleted");
                await LoadAllTaxRatesAsync();
                await LoadFiltersAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {taxRate.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {taxRate.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedTaxRates.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedTaxRates.Count} tax rates? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var taxRate in SelectedTaxRates.ToList())
            {
                try
                {
                    var success = await TaxRateService.DeleteAsync(taxRate.Id);
                    if (!success)
                    {
                        failed.Add(taxRate.Name);
                    }
                }
                catch
                {
                    failed.Add(taxRate.Name);
                }
            }

            SelectedTaxRates.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected tax rates deleted");
            }

            await LoadAllTaxRatesAsync();
            await LoadFiltersAsync();
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