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
public class VehicleModelsBase : ComponentBase
{
    [Inject] protected IVehicleModelService VehicleModelService { get; set; } = default!;
    [Inject] protected IBrandService VehicleBrandService { get; set; } = default!;
    [Inject] protected IVehicleTypeService VehicleTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<VehicleModelsBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<VehicleModelResponseDto> AllModels { get; set; } = new();
    protected List<VehicleModelResponseDto> SelectedModels { get; set; } = new();

    protected Dictionary<Guid, string> AvailableBrands { get; set; } = new();
    protected Dictionary<Guid, string> AvailableVehicleTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;
    protected VehicleModel? selectedModel = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? BrandFilter { get; set; }
    protected string? VehicleTypeFilter { get; set; }
    protected bool? ActiveFilter { get; set; }
    protected bool? CurrentModelFilter { get; set; }

    protected int ActiveCount => AllModels.Count(m => m.IsActive);
    protected int CurrentModelCount => AllModels.Count(m => m.IsCurrentModel);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<VehicleModelResponseDto> FilteredModels => AllModels
        .Where(m => string.IsNullOrEmpty(SearchTerm) ||
                     m.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     m.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     m.Slug.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(m => string.IsNullOrEmpty(BrandFilter) || m.BrandId.ToString() == BrandFilter)
        .Where(m => string.IsNullOrEmpty(VehicleTypeFilter) || m.VehicleTypeId.ToString() == VehicleTypeFilter)
        .Where(m => ActiveFilter == null || m.IsActive == ActiveFilter)
        .Where(m => CurrentModelFilter == null || m.IsCurrentModel == CurrentModelFilter)
        .OrderBy(m => m.DisplayOrder)
        .ToList();

    protected List<VehicleModelResponseDto> PaginatedModels => FilteredModels
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedModels.Any() && PaginatedModels.All(m => SelectedModels.Contains(m));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("VehicleModels page initialized");
        await LoadAllDataAsync();
    }

    protected async Task LoadAllDataAsync()
    {
        await Task.WhenAll(
            LoadAllModelsAsync(),
            LoadBrandsAsync(),
            LoadVehicleTypesAsync()
        );
    }

    protected async Task LoadAllModelsAsync()
    {
        try
        {
            LoaderService.Show("Loading vehicle models...");
            IsLoading = true;

            var models = await VehicleModelService.GetAllAsync();
            AllModels = models?.ToList() ?? new List<VehicleModelResponseDto>();
            TotalCount = FilteredModels.Count;
            CurrentPage = 1;
            SelectedModels.Clear();

            Logger.LogInformation("Loaded {Count} vehicle models", AllModels.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading vehicle models");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle models.");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task LoadBrandsAsync()
    {
        try
        {
            var brands = await VehicleBrandService.GetAllBrandsAsync();
            AvailableBrands = brands?.ToDictionary(b => b.Id, b => b.Name) ?? new Dictionary<Guid, string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading brands for filter");
        }
    }

    protected async Task LoadVehicleTypesAsync()
    {
        try
        {
            var types = await VehicleTypeService.GetAllAsync();
            AvailableVehicleTypes = types?.ToDictionary(t => t.Id, t => t.Name) ?? new Dictionary<Guid, string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading vehicle types for filter");
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredModels.Count;
        SelectedModels.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        BrandFilter = null;
        VehicleTypeFilter = null;
        ActiveFilter = null;
        CurrentModelFilter = null;
        ApplyFilters();
    }

    protected void OpenAddModelSidebar()
    {
        selectedModel = new VehicleModel
        {
            Id = Guid.NewGuid(),
            BrandId = AvailableBrands.Keys.FirstOrDefault(),
            VehicleTypeId = AvailableVehicleTypes.Keys.FirstOrDefault(),
            Name = string.Empty,
            Code = string.Empty,
            Slug = string.Empty,
            IsActive = true,
            IsCurrentModel = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditModelSidebar(VehicleModelResponseDto model)
    {
        selectedModel = Mapper.Map<VehicleModel>(model);
        showSidebar = true;
    }

    protected async Task OnModelSave(VehicleModel model)
    {
        try
        {
            LoaderService.Show(model.Id != null ? "Updating model..." : "Creating model...");

            if (model.Id.HasValue && model.Id != Guid.Empty)
            {
                var updateDto = Mapper.Map<VehicleModelUpdateDto>(model);
                var updated = await VehicleModelService.UpdateAsync(model.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Model '{model.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = Mapper.Map<VehicleModelCreateDto>(model);
                await VehicleModelService.CreateAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Model '{model.Name}' created successfully");
            }

            showSidebar = false;
            selectedModel = null;

            await LoadAllModelsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving vehicle model");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save model: {ex.Message}");
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

    protected void OnModelSelectionChanged(VehicleModelResponseDto model, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedModels.Contains(model))
                SelectedModels.Add(model);
        }
        else
        {
            SelectedModels.Remove(model);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedModels.Count != PaginatedModels.Count;

        if (selectAll)
        {
            foreach (var model in PaginatedModels)
            {
                if (!SelectedModels.Contains(model))
                    SelectedModels.Add(model);
            }
        }
        else
        {
            foreach (var model in PaginatedModels)
            {
                SelectedModels.Remove(model);
            }
        }
    }

    protected async Task ToggleStatus(VehicleModelResponseDto model)
    {
        try
        {
            // Note: Service doesn't have ToggleStatus, so we update via full update
            LoaderService.Show($"Updating {model.Name} status...");

            var updateModel = Mapper.Map<VehicleModel>(model);
            updateModel.IsActive = !model.IsActive;

            var updateDto = Mapper.Map<VehicleModelUpdateDto>(updateModel);
            var success = await VehicleModelService.UpdateAsync(model.Id, updateDto);

            if (success != null)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{model.Name} is now {(model.IsActive ? "inactive" : "active")}");
                await LoadAllModelsAsync();
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

    protected async Task DeleteModel(VehicleModelResponseDto model)
    {
        if (model.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{model.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {model.Name}...");
            var success = await VehicleModelService.DeleteAsync(model.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{model.Name} deleted");
                await LoadAllModelsAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {model.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {model.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedModels.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedModels.Count} models? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var model in SelectedModels.ToList())
            {
                try
                {
                    var success = await VehicleModelService.DeleteAsync(model.Id);
                    if (!success)
                    {
                        failed.Add(model.Name);
                    }
                }
                catch
                {
                    failed.Add(model.Name);
                }
            }

            SelectedModels.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected models deleted");
            }

            await LoadAllModelsAsync();
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