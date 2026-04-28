using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class PaymentMethodBase : ComponentBase
{
    [Inject] protected IPaymentMethodService PaymentMethodService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<PaymentMethodBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<PaymentMethodResponseDto> AllPaymentMethods { get; set; } = new();
    protected List<PaymentMethodResponseDto> SelectedPaymentMethods { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected AutoNext.Plotform.App.Backoffice.Models.Core.PaymentMethod? selectedPaymentMethod = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? TypeFilter { get; set; }
    protected string? AvailabilityFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableTypes { get; set; } = new();

    // Summary counts
    protected int ActiveCount => AllPaymentMethods.Count(m => m.IsActive);
    protected int InactiveCount => AllPaymentMethods.Count(m => !m.IsActive);
    protected int InstantCount => AllPaymentMethods.Count(m => m.IsInstant);

    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected bool HasActiveFilters => !string.IsNullOrEmpty(SearchTerm) ||
                                        TypeFilter != null ||
                                        AvailabilityFilter != null ||
                                        ActiveFilter != null;

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<PaymentMethodResponseDto> FilteredPaymentMethods => AllPaymentMethods
        .Where(m => string.IsNullOrEmpty(SearchTerm) ||
                    m.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    m.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(m => string.IsNullOrEmpty(TypeFilter) || m.Type == TypeFilter)
        .Where(m => AvailabilityFilter == null ||
                    (AvailabilityFilter == "sellers" && m.IsAvailableForSellers) ||
                    (AvailabilityFilter == "buyers" && m.IsAvailableForBuyers) ||
                    (AvailabilityFilter == "instant" && m.IsInstant))
        .Where(m => ActiveFilter == null || m.IsActive == ActiveFilter)
        .ToList();

    protected List<PaymentMethodResponseDto> PaginatedPaymentMethods => FilteredPaymentMethods
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedPaymentMethods.Any() &&
                                      PaginatedPaymentMethods.All(m => SelectedPaymentMethods.Contains(m));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("PaymentMethod page initialized");
        await LoadAllPaymentMethodsAsync();
    }

    protected async Task LoadAllPaymentMethodsAsync()
    {
        try
        {
            LoaderService.Show("Loading payment methods...");
            IsLoading = true;

            var methods = await PaymentMethodService.GetAllAsync();

            AllPaymentMethods = methods?.ToList() ?? new();
            TotalCount = FilteredPaymentMethods.Count;
            CurrentPage = 1;
            SelectedPaymentMethods.Clear();

            LoadFilterOptions();

            Logger.LogInformation("Loaded {Count} payment methods", AllPaymentMethods.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading payment methods");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load payment methods.");
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
        AvailableTypes = AllPaymentMethods
            .Where(m => !string.IsNullOrEmpty(m.Type))
            .Select(m => m.Type!)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredPaymentMethods.Count;
        SelectedPaymentMethods.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        TypeFilter = null;
        AvailabilityFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddPaymentMethodSidebar()
    {
        selectedPaymentMethod = new AutoNext.Plotform.App.Backoffice.Models.Core.PaymentMethod
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            IsInstant = false,
            IsAvailableForSellers = true,
            IsAvailableForBuyers = true,
            ProcessingFeePercentage = 0,
            ProcessingFeeFixed = 0,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditPaymentMethodSidebar(PaymentMethodResponseDto method)
    {
        selectedPaymentMethod = Mapper.Map<AutoNext.Plotform.App.Backoffice.Models.Core.PaymentMethod>(method);
        showSidebar = true;
    }

    protected async Task OnPaymentMethodSave(AutoNext.Plotform.App.Backoffice.Models.Core.PaymentMethod method)
    {
        try
        {
            LoaderService.Show(method.Id != Guid.Empty ? "Updating payment method..." : "Creating payment method...");

            if (method.Id != null && method.Id != Guid.Empty)
            {
                var updateDto = new PaymentMethodUpdateDto
                {
                    Name = method.Name,
                    Code = method.Code,
                    Type = method.Type,
                    IconUrl = method.IconUrl,
                    ProcessingFeePercentage = method.ProcessingFeePercentage,
                    ProcessingFeeFixed = method.ProcessingFeeFixed,
                    SettlementDays = method.SettlementDays,
                    IsInstant = method.IsInstant,
                    IsAvailableForSellers = method.IsAvailableForSellers,
                    IsAvailableForBuyers = method.IsAvailableForBuyers,
                    DisplayOrder = method.DisplayOrder,
                    IsActive = method.IsActive
                };

                var updated = await PaymentMethodService.UpdateAsync(method.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Payment method '{method.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = new PaymentMethodCreateDto
                {
                    Name = method.Name,
                    Code = method.Code,
                    Type = method.Type,
                    IconUrl = method.IconUrl,
                    ProcessingFeePercentage = method.ProcessingFeePercentage,
                    ProcessingFeeFixed = method.ProcessingFeeFixed,
                    SettlementDays = method.SettlementDays,
                    IsInstant = method.IsInstant,
                    IsAvailableForSellers = method.IsAvailableForSellers,
                    IsAvailableForBuyers = method.IsAvailableForBuyers,
                    DisplayOrder = method.DisplayOrder
                };

                await PaymentMethodService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Payment method '{method.Name}' created successfully");
            }

            showSidebar = false;
            selectedPaymentMethod = null;
            await LoadAllPaymentMethodsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving payment method");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save payment method: {ex.Message}");
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

    protected void OnPaymentMethodSelectionChanged(PaymentMethodResponseDto method, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedPaymentMethods.Contains(method))
                SelectedPaymentMethods.Add(method);
        }
        else
        {
            SelectedPaymentMethods.Remove(method);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedPaymentMethods.Count != PaginatedPaymentMethods.Count;

        if (selectAll)
        {
            foreach (var method in PaginatedPaymentMethods)
                if (!SelectedPaymentMethods.Contains(method))
                    SelectedPaymentMethods.Add(method);
        }
        else
        {
            foreach (var method in PaginatedPaymentMethods)
                SelectedPaymentMethods.Remove(method);
        }
    }

    protected async Task ToggleStatus(PaymentMethodResponseDto method)
    {
        try
        {
            LoaderService.Show($"Updating {method.Name} status...");
            var success = await PaymentMethodService.ToggleStatusAsync(method.Id, !method.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{method.Name} is now {(method.IsActive ? "inactive" : "active")}");
                await LoadAllPaymentMethodsAsync();
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

    protected async Task DeletePaymentMethod(PaymentMethodResponseDto method)
    {
        if (method.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete '{method.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {method.Name}...");
            var success = await PaymentMethodService.DeleteAsync(method.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{method.Name} deleted");
                await LoadAllPaymentMethodsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {method.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {method.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedPaymentMethods.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete {SelectedPaymentMethods.Count} payment methods? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var method in SelectedPaymentMethods.ToList())
            {
                try
                {
                    var success = await PaymentMethodService.DeleteAsync(method.Id);
                    if (!success) failed.Add(method.Name);
                }
                catch
                {
                    failed.Add(method.Name);
                }
            }

            SelectedPaymentMethods.Clear();

            if (failed.Any())
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            else
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected payment methods deleted");

            await LoadAllPaymentMethodsAsync();
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

    protected string GetFeeBadgeClass(decimal fee) =>
        fee == 0 ? "bg-success" :
        fee <= 2 ? "bg-info" :
        fee <= 5 ? "bg-warning text-dark" :
        "bg-danger";
}