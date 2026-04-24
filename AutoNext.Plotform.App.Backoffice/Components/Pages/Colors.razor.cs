using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class ColorsBase : ComponentBase
{
    [Inject] protected IColorService ColorService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<ColorsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<ColorResponseDto> AllColors { get; set; } = new();
    protected List<ColorResponseDto> SelectedColors { get; set; } = new();
    protected List<ColorResponseDto> FilteredColors { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? StatusFilter { get; set; } = null; // "active", "inactive", null for all

    protected bool showSidebar = false;
    protected Color? selectedColor = null;

    protected int ActiveCount => AllColors.Count(c => c.IsActive);
    protected int InactiveCount => AllColors.Count(c => !c.IsActive);
    protected int TotalPages => (int)Math.Ceiling((double)FilteredColors.Count / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<ColorResponseDto> PaginatedColors => FilteredColors
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedColors.Any() && PaginatedColors.All(c => SelectedColors.Contains(c));
    protected bool IsFiltered => !string.IsNullOrWhiteSpace(SearchTerm) || !string.IsNullOrEmpty(StatusFilter);

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Colors page initialized");
        await LoadAllColorsWithRetryAsync();
    }

    protected async Task LoadAllColorsWithRetryAsync(int retryCount = 0)
    {
        const int maxRetries = 3;

        try
        {
            LoaderService.Show("Loading colors...");
            IsLoading = true;

            var colors = await ColorService.GetAllColorsAsync();

            if (colors == null || !colors.Any())
            {
                if (retryCount < maxRetries - 1)
                {
                    Logger.LogWarning("No colors returned, retrying... Attempt {RetryCount}/{MaxRetries}",
                        retryCount + 1, maxRetries);
                    await Task.Delay(1000 * (retryCount + 1)); // Exponential backoff
                    await LoadAllColorsWithRetryAsync(retryCount + 1);
                    return;
                }

                NotificationService.Notify(NotificationSeverity.Warning, "No Data",
                    "No colors found or service temporarily unavailable. Please refresh the page.");
                AllColors = new List<ColorResponseDto>();
                FilteredColors = new List<ColorResponseDto>();
            }
            else
            {
                AllColors = colors.ToList();
                TotalCount = AllColors.Count;
                ApplyFilters();
                CurrentPage = 1;
                SelectedColors.Clear();
                Logger.LogInformation("Loaded {Count} colors successfully", TotalCount);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Loaded {TotalCount} colors successfully");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable"))
        {
            Logger.LogError(ex, "Service unavailable error");
            NotificationService.Notify(NotificationSeverity.Error, "Service Unavailable",
                "The color service is currently unavailable. Please try again in a few moments.");
            AllColors = new List<ColorResponseDto>();
            FilteredColors = new List<ColorResponseDto>();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("500") || ex.Message.Contains("503"))
        {
            if (retryCount < maxRetries - 1)
            {
                Logger.LogWarning(ex, "HTTP error, retrying... Attempt {RetryCount}/{MaxRetries}",
                    retryCount + 1, maxRetries);
                await Task.Delay(2000 * (retryCount + 1));
                await LoadAllColorsWithRetryAsync(retryCount + 1);
                return;
            }

            Logger.LogError(ex, "Error loading colors after {MaxRetries} retries", maxRetries);
            NotificationService.Notify(NotificationSeverity.Error, "Connection Error",
                "Failed to connect to the color service. Please check your network connection and try again.");
            AllColors = new List<ColorResponseDto>();
            FilteredColors = new List<ColorResponseDto>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error loading colors");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"An unexpected error occurred: {ex.Message}");
            AllColors = new List<ColorResponseDto>();
            FilteredColors = new List<ColorResponseDto>();
        }
        finally
        {
            LoaderService.Hide();
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void ApplyFilters()
    {
        var query = AllColors.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(c =>
                c.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                (c.HexCode?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply status filter
        if (!string.IsNullOrEmpty(StatusFilter))
        {
            bool isActive = StatusFilter == "active";
            query = query.Where(c => c.IsActive == isActive);
        }

        FilteredColors = query.ToList();
        TotalCount = FilteredColors.Count;

        // Reset to first page if current page exceeds total pages
        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = 1;
        }
    }

    protected void OnSearchTermChanged(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? string.Empty;
        CurrentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    protected void OnStatusFilterChanged(ChangeEventArgs e)
    {
        StatusFilter = e.Value?.ToString();
        CurrentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        StatusFilter = null;
        CurrentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    protected void OpenAddColorSidebar()
    {
        selectedColor = new Color
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            DisplayOrder = AllColors.Count + 1,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditColorSidebar(ColorResponseDto color)
    {
        selectedColor = Mapper.Map<Color>(color);
        showSidebar = true;
    }

    protected async Task OnColorSave(Color color)
    {
        if (color == null) return;

        try
        {
            LoaderService.Show(color.Id != Guid.Empty ? "Updating color..." : "Creating color...");

            if (color.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<ColorUpdateDto>(color);
                var updated = await ColorService.UpdateColorAsync(color.Id, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Color '{color.Name}' updated successfully");
                }
                else
                {
                    throw new InvalidOperationException("Failed to update color");
                }
            }
            else
            {
                var createDto = Mapper.Map<ColorCreateDto>(color);
                await ColorService.CreateColorAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Color '{color.Name}' created successfully");
            }

            showSidebar = false;
            selectedColor = null;

            await LoadAllColorsWithRetryAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("Duplicate"))
        {
            Logger.LogWarning(ex, "Duplicate color detected");
            NotificationService.Notify(NotificationSeverity.Error, "Duplicate Error",
                $"A color with name or code '{color.Name}' already exists.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving color");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save color: {ex.Message}");
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
        StateHasChanged();
    }

    protected void OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size))
        {
            ItemsPerPage = size;
            CurrentPage = 1;
            StateHasChanged();
        }
    }

    protected void OnColorSelectionChanged(ColorResponseDto color, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedColors.Contains(color))
                SelectedColors.Add(color);
        }
        else
        {
            SelectedColors.Remove(color);
        }
        StateHasChanged();
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedColors.Count != PaginatedColors.Count;

        if (selectAll)
        {
            foreach (var color in PaginatedColors)
            {
                if (!SelectedColors.Contains(color))
                    SelectedColors.Add(color);
            }
        }
        else
        {
            foreach (var color in PaginatedColors)
            {
                SelectedColors.Remove(color);
            }
        }
        StateHasChanged();
    }

    protected async Task DeleteColor(ColorResponseDto color)
    {
        if (color.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{color.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {color.Name}...");
            var success = await ColorService.DeleteColorAsync(color.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted",
                    $"'{color.Name}' has been deleted successfully");
                await LoadAllColorsWithRetryAsync();
            }
            else
            {
                throw new InvalidOperationException("Delete operation failed");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed for color: {ColorName}", color.Name);
            NotificationService.Notify(NotificationSeverity.Error, "Delete Failed",
                $"Failed to delete '{color.Name}'. Please try again.");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedColors.Any())
        {
            NotificationService.Notify(NotificationSeverity.Info, "No Selection",
                "Please select at least one color to delete.");
            return;
        }

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
            $"Delete {SelectedColors.Count} color(s)? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();
        var successful = new List<string>();

        try
        {
            LoaderService.Show($"Deleting {SelectedColors.Count} colors...");

            foreach (var color in SelectedColors.ToList())
            {
                try
                {
                    var success = await ColorService.DeleteColorAsync(color.Id);
                    if (success)
                    {
                        successful.Add(color.Name);
                    }
                    else
                    {
                        failed.Add(color.Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to delete color: {ColorName}", color.Name);
                    failed.Add(color.Name);
                }
            }

            SelectedColors.Clear();

            if (successful.Any())
            {
                NotificationService.Notify(NotificationSeverity.Success, "Bulk Delete Complete",
                    $"Successfully deleted {successful.Count} color(s). {(failed.Any() ? $"Failed to delete {failed.Count} color(s)." : "")}");
            }

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed to delete: {string.Join(", ", failed)}");
            }

            await LoadAllColorsWithRetryAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Bulk delete error");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                "An error occurred during bulk delete operation. Please try again.");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task RefreshData()
    {
        await LoadAllColorsWithRetryAsync();
    }
}