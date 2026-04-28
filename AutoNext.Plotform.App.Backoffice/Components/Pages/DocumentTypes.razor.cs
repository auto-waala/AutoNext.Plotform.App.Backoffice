using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages;

public class DocumentTypesBase : ComponentBase
{
    [Inject] protected IDocumentTypeService DocumentTypeService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected LoaderService LoaderService { get; set; } = default!;
    [Inject] protected ILogger<DocumentTypesBase> Logger { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMapper Mapper { get; set; } = default!;

    protected List<DocumentTypeResponseDto> AllDocumentTypes { get; set; } = new();
    protected List<DocumentTypeResponseDto> SelectedDocumentTypes { get; set; } = new();

    protected int ItemsPerPage { get; set; } = 10;
    protected int CurrentPage { get; set; } = 1;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; } = false;

    protected bool showSidebar = false;

    protected DocumentType? selectedDocumentType = null;

    // Filter properties
    protected string SearchTerm { get; set; } = string.Empty;
    protected string? CategoryFilter { get; set; }
    protected bool? RequiredFilter { get; set; }
    protected bool? VerifiableFilter { get; set; }
    protected bool? ActiveFilter { get; set; }

    protected List<string> AvailableCategories { get; set; } = new();

    protected int ActiveCount => AllDocumentTypes.Count(dt => dt.IsActive);
    protected int InactiveCount => AllDocumentTypes.Count(dt => !dt.IsActive);
    protected int RequiredCount => AllDocumentTypes.Count(dt => dt.IsRequired);
    protected int VerifiableCount => AllDocumentTypes.Count(dt => dt.IsVerifiable);
    protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

    protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

    protected List<DocumentTypeResponseDto> FilteredDocumentTypes => AllDocumentTypes
        .Where(dt => string.IsNullOrEmpty(SearchTerm) ||
                     dt.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                     dt.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
        .Where(dt => string.IsNullOrEmpty(CategoryFilter) || dt.Category == CategoryFilter)
        .Where(dt => RequiredFilter == null || dt.IsRequired == RequiredFilter)
        .Where(dt => VerifiableFilter == null || dt.IsVerifiable == VerifiableFilter)
        .Where(dt => ActiveFilter == null || dt.IsActive == ActiveFilter)
        .ToList();

    protected List<DocumentTypeResponseDto> PaginatedDocumentTypes => FilteredDocumentTypes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage)
        .ToList();

    protected bool AllPageSelected => PaginatedDocumentTypes.Any() && PaginatedDocumentTypes.All(dt => SelectedDocumentTypes.Contains(dt));

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("DocumentTypes page initialized");
        await LoadAllDocumentTypesAsync();
        await LoadCategoriesAsync();
    }

    protected async Task LoadAllDocumentTypesAsync()
    {
        try
        {
            LoaderService.Show("Loading document types...");
            IsLoading = true;

            var documentTypes = await DocumentTypeService.GetAllDocumentTypesAsync();

            AllDocumentTypes = documentTypes?.ToList() ?? new List<DocumentTypeResponseDto>();
            TotalCount = FilteredDocumentTypes.Count;
            CurrentPage = 1;
            SelectedDocumentTypes.Clear();

            Logger.LogInformation("Loaded {Count} document types", AllDocumentTypes.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading document types");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load document types.");
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
            var categories = await DocumentTypeService.GetDistinctCategoriesAsync();
            AvailableCategories = categories?.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading categories");
        }
    }

    protected void ApplyFilters()
    {
        CurrentPage = 1;
        TotalCount = FilteredDocumentTypes.Count;
        SelectedDocumentTypes.Clear();
        StateHasChanged();
    }

    protected void ClearFilters()
    {
        SearchTerm = string.Empty;
        CategoryFilter = null;
        RequiredFilter = null;
        VerifiableFilter = null;
        ActiveFilter = null;
        ApplyFilters();
    }

    protected void OpenAddDocumentTypeSidebar()
    {
        selectedDocumentType = new DocumentType
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            IsRequired = false,
            IsVerifiable = false,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        showSidebar = true;
    }

    protected void OpenEditDocumentTypeSidebar(DocumentTypeResponseDto documentType)
    {
        selectedDocumentType = Mapper.Map<DocumentType>(documentType);
        // Convert List<string> to comma-separated string for the form
        if (documentType.ApplicableVehicleTypes != null && documentType.ApplicableVehicleTypes.Any())
        {
            selectedDocumentType.ApplicableVehicleTypes = string.Join(",", documentType.ApplicableVehicleTypes);
        }
        showSidebar = true;
    }

    protected async Task OnDocumentTypeSave(DocumentType documentType)
    {
        try
        {
            LoaderService.Show(documentType.Id != Guid.Empty ? "Updating document type..." : "Creating document type...");

            // Convert comma-separated vehicle types to List<string>
            List<string> vehicleTypes = null;
            if (!string.IsNullOrWhiteSpace(documentType.ApplicableVehicleTypes))
            {
                vehicleTypes = documentType.ApplicableVehicleTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .ToList();
            }

            if (documentType.Id != Guid.Empty && documentType.Id != Guid.Empty)
            {
                var updateDto = new DocumentTypeUpdateDto
                {
                    Name = documentType.Name,
                    Code = documentType.Code,
                    Category = documentType.Category,
                    IsRequired = documentType.IsRequired,
                    IsVerifiable = documentType.IsVerifiable,
                    ExpiryMonths = documentType.ExpiryMonths,
                    ApplicableVehicleTypes = vehicleTypes,
                    DisplayOrder = documentType.DisplayOrder,
                    IsActive = documentType.IsActive
                };

                var updated = await DocumentTypeService.UpdateDocumentTypeAsync(documentType.Id.Value, updateDto);

                if (updated != null)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Document type '{documentType.Name}' updated successfully");
                }
            }
            else
            {
                var createDto = new DocumentTypeCreateDto
                {
                    Name = documentType.Name,
                    Code = documentType.Code,
                    Category = documentType.Category,
                    IsRequired = documentType.IsRequired,
                    IsVerifiable = documentType.IsVerifiable,
                    ExpiryMonths = documentType.ExpiryMonths,
                    ApplicableVehicleTypes = vehicleTypes,
                    DisplayOrder = documentType.DisplayOrder
                };

                await DocumentTypeService.CreateDocumentTypeAsync(createDto);

                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"Document type '{documentType.Name}' created successfully");
            }

            showSidebar = false;
            selectedDocumentType = null;

            await LoadAllDocumentTypesAsync();
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving document type");
            NotificationService.Notify(NotificationSeverity.Error, "Error",
                $"Failed to save document type: {ex.Message}");
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

    protected void OnDocumentTypeSelectionChanged(DocumentTypeResponseDto documentType, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedDocumentTypes.Contains(documentType))
                SelectedDocumentTypes.Add(documentType);
        }
        else
        {
            SelectedDocumentTypes.Remove(documentType);
        }
    }

    protected void ToggleAllSelection()
    {
        bool selectAll = SelectedDocumentTypes.Count != PaginatedDocumentTypes.Count;

        if (selectAll)
        {
            foreach (var documentType in PaginatedDocumentTypes)
            {
                if (!SelectedDocumentTypes.Contains(documentType))
                    SelectedDocumentTypes.Add(documentType);
            }
        }
        else
        {
            foreach (var documentType in PaginatedDocumentTypes)
            {
                SelectedDocumentTypes.Remove(documentType);
            }
        }
    }

    protected async Task ToggleStatus(DocumentTypeResponseDto documentType)
    {
        try
        {
            LoaderService.Show($"Updating {documentType.Name} status...");
            var success = await DocumentTypeService.ToggleDocumentTypeStatusAsync(documentType.Id, !documentType.IsActive);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    $"{documentType.Name} is now {(documentType.IsActive ? "inactive" : "active")}");
                await LoadAllDocumentTypesAsync();
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

    protected async Task DeleteDocumentType(DocumentTypeResponseDto documentType)
    {
        if (documentType.Id == Guid.Empty) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{documentType.Name}'? This action cannot be undone.");
        if (!confirmed) return;

        try
        {
            LoaderService.Show($"Deleting {documentType.Name}...");
            var success = await DocumentTypeService.DeleteDocumentTypeAsync(documentType.Id);

            if (success)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{documentType.Name} deleted");
                await LoadAllDocumentTypesAsync();
                await LoadCategoriesAsync();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {documentType.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete failed");
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {documentType.Name}");
        }
        finally
        {
            LoaderService.Hide();
        }
    }

    protected async Task BulkDelete()
    {
        if (!SelectedDocumentTypes.Any()) return;

        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedDocumentTypes.Count} document types? This action cannot be undone.");
        if (!confirmed) return;

        var failed = new List<string>();

        try
        {
            LoaderService.Show("Bulk deleting...");

            foreach (var documentType in SelectedDocumentTypes.ToList())
            {
                try
                {
                    var success = await DocumentTypeService.DeleteDocumentTypeAsync(documentType.Id);
                    if (!success)
                    {
                        failed.Add(documentType.Name);
                    }
                }
                catch
                {
                    failed.Add(documentType.Name);
                }
            }

            SelectedDocumentTypes.Clear();

            if (failed.Any())
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                    $"Failed: {string.Join(", ", failed)}");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success",
                    "All selected document types deleted");
            }

            await LoadAllDocumentTypesAsync();
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

    protected string GetRequiredBadgeClass(bool isRequired)
    {
        return isRequired ? "bg-warning text-dark" : "bg-light text-dark";
    }

    protected string GetVerifiableBadgeClass(bool isVerifiable)
    {
        return isVerifiable ? "bg-info text-dark" : "bg-light text-dark";
    }
}