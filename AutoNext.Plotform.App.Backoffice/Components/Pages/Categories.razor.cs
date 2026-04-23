using AutoMapper;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class CategoriesBase : ComponentBase
    {
        [Inject] protected ICategoryService CategoryService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected LoaderService LoaderService { get; set; } = default!;
        [Inject] protected ILogger<CategoriesBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected IMapper Mapper { get; set; } = default!;

        protected List<CategoryResponseDto> AllCategories { get; set; } = new();
        protected List<CategoryResponseDto> SelectedCategories { get; set; } = new();
        protected int ItemsPerPage { get; set; } = 10;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        protected bool showSidebar = false;
        protected Category? selectedCategory = null;

        protected int ActiveCount => AllCategories.Count(c => c.IsActive);
        protected int InactiveCount => AllCategories.Count(c => !c.IsActive);
        protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

        protected IEnumerable<int> PageSizeOptions = new[] { 10, 20, 50, 100 };

        protected List<CategoryResponseDto> PaginatedCategories => AllCategories
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected bool AllPageSelected => PaginatedCategories.Any() && PaginatedCategories.All(c => SelectedCategories.Contains(c));

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("Categories page initialized");
            await LoadAllCategoriesAsync();
        }

        protected async Task LoadAllCategoriesAsync()
        {
            try
            {
                LoaderService.Show("Loading categories...");
                IsLoading = true;

                var categories = await CategoryService.GetAllCategoriesAsync();
                AllCategories = categories?.ToList() ?? new List<CategoryResponseDto>();
                TotalCount = AllCategories.Count;
                CurrentPage = 1;
                SelectedCategories.Clear();

                Logger.LogInformation("Loaded {Count} categories", TotalCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading categories");
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load categories.");
            }
            finally
            {
                LoaderService.Hide();
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void OpenAddCategorySidebar()
        {
            selectedCategory = new Category
            {
                Id = Guid.NewGuid(),
                IsActive = true,
                DisplayOrder = 0,
                Metadata = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow
            };
            showSidebar = true;
        }

        protected void OpenEditCategorySidebar(CategoryResponseDto category)
        {
            selectedCategory = Mapper.Map<Category>(category);
            showSidebar = true;
        }

        protected async Task OnCategorySave(Category category)
        {
            try
            {
                LoaderService.Show(category.Id != Guid.Empty ? "Updating category..." : "Creating category...");

                // Clean data
                category.Metadata = category.Metadata?
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Generate slug if needed
                if (string.IsNullOrWhiteSpace(category.Slug) && !string.IsNullOrWhiteSpace(category.Name))
                {
                    category.Slug = GenerateSlug(category.Name);
                }

                if (category.Id != Guid.Empty)
                {
                    var updateDto = Mapper.Map<CategoryUpdateDto>(category);
                    var updated = await CategoryService.UpdateCategoryAsync(category.Id.Value, updateDto);

                    if (updated != null)
                    {
                        NotificationService.Notify(NotificationSeverity.Success, "Success",
                            $"Category '{category.Name}' updated successfully");
                    }
                }
                else
                {
                    var createDto = Mapper.Map<CategoryCreateDto>(category);
                    await CategoryService.CreateCategoryAsync(createDto);

                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        $"Category '{category.Name}' created successfully");
                }

                showSidebar = false;
                selectedCategory = null;
                await LoadAllCategoriesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving category");
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to save category: {ex.Message}");
            }
            finally
            {
                LoaderService.Hide();
            }
        }

        protected void OnCategorySelectionChanged(CategoryResponseDto category, bool isChecked)
        {
            if (isChecked)
            {
                if (!SelectedCategories.Contains(category))
                    SelectedCategories.Add(category);
            }
            else
            {
                SelectedCategories.Remove(category);
            }
        }

        protected void ToggleAllSelection()
        {
            bool selectAll = SelectedCategories.Count != PaginatedCategories.Count;

            if (selectAll)
            {
                foreach (var category in PaginatedCategories)
                {
                    if (!SelectedCategories.Contains(category))
                        SelectedCategories.Add(category);
                }
            }
            else
            {
                foreach (var category in PaginatedCategories)
                {
                    SelectedCategories.Remove(category);
                }
            }
        }

        protected async Task DeleteCategory(CategoryResponseDto category)
        {
            if (category.Id == Guid.Empty) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete '{category.Name}'?");
            if (!confirmed) return;

            try
            {
                LoaderService.Show($"Deleting {category.Name}...");
                await CategoryService.DeleteCategoryAsync(category.Id);

                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"{category.Name} deleted");
                await LoadAllCategoriesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete failed");
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to delete {category.Name}");
            }
            finally
            {
                LoaderService.Hide();
            }
        }

        protected async Task BulkDelete()
        {
            if (!SelectedCategories.Any()) return;

            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Delete {SelectedCategories.Count} categories?");
            if (!confirmed) return;

            var failed = new List<string>();

            try
            {
                LoaderService.Show("Bulk deleting...");

                foreach (var category in SelectedCategories.ToList())
                {
                    try
                    {
                        await CategoryService.DeleteCategoryAsync(category.Id);
                    }
                    catch
                    {
                        failed.Add(category.Name);
                    }
                }

                SelectedCategories.Clear();

                if (failed.Any())
                {
                    NotificationService.Notify(NotificationSeverity.Warning, "Partial Success",
                        $"Failed: {string.Join(", ", failed)}");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "All selected categories deleted");
                }

                await LoadAllCategoriesAsync();
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

        private string GenerateSlug(string name)
        {
            return name.ToLower()
                       .Replace(" ", "-")
                       .Replace("&", "and")
                       .Replace("'", "")
                       .Replace("\"", "")
                       .Replace(".", "")
                       .Replace(",", "")
                       .Replace("?", "")
                       .Replace("!", "")
                       .Replace("/", "-")
                       .Replace("\\", "-");
        }
    }
}