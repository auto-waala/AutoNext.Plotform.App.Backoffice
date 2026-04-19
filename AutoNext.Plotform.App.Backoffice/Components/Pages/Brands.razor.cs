using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class BrandsBase : ComponentBase
    {
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected List<Brand> AllBrands { get; set; } = new();

        protected int ItemsPerPage { get; set; } = 25;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        protected int ActiveCount => AllBrands.Count(b => b.IsActive);
        protected int InactiveCount => AllBrands.Count(b => !b.IsActive);
        protected int TotalPages => (int)Math.Ceiling((double)TotalCount / ItemsPerPage);

        protected List<Brand> PaginatedBrands => AllBrands
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        protected IEnumerable<int> PageSizeOptions = new[] { 10, 25, 50, 100 };
        protected List<Brand> SelectedBrands { get; set; } = new();

        protected override async Task OnInitializedAsync()
        {
            await LoadAllBrandsAsync();
        }

        protected async Task LoadAllBrandsAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var brands = await BrandService.GetAllBrandsAsync();
                AllBrands = brands?.ToList() ?? new List<Brand>();
                TotalCount = AllBrands.Count;
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading brands: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void GoToPage(int page)
        {
            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            StateHasChanged();
        }

        protected async Task OnPageSizeChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var size))
            {
                ItemsPerPage = size;
                CurrentPage = 1;
                StateHasChanged();
            }
        }

        protected void OnBrandSelectionChanged(Brand brand)
        {
            if (brand.IsSelected)
            {
                if (!SelectedBrands.Contains(brand))
                    SelectedBrands.Add(brand);
            }
            else
            {
                SelectedBrands.Remove(brand);
            }
            StateHasChanged();
        }

        protected void ToggleAllSelection()
        {
            var allSelected = SelectedBrands.Count == PaginatedBrands.Count && PaginatedBrands.Any();

            foreach (var brand in PaginatedBrands)
            {
                if (allSelected)
                {
                    brand.IsSelected = false;
                    SelectedBrands.Remove(brand);
                }
                else
                {
                    if (!SelectedBrands.Contains(brand))
                    {
                        brand.IsSelected = true;
                        SelectedBrands.Add(brand);
                    }
                }
            }
            StateHasChanged();
        }

        protected void CreateNewBrand() => Navigation.NavigateTo("/brands/create");

        protected void EditBrand(Brand brand)
        {
            if (brand?.Id is not null)
                Navigation.NavigateTo($"/brands/edit/{brand.Id}");
        }

        protected async Task DeleteBrand(Brand brand)
        {
            if (brand?.Id is null) return;

            bool confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", $"Delete brand '{brand.Name}'? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                await BrandService.DeleteBrandAsync(brand.Id.Value);
                await LoadAllBrandsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting brand: {ex.Message}");
            }
        }

        protected async Task BulkDelete()
        {
            if (!SelectedBrands.Any()) return;

            bool confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", $"Delete {SelectedBrands.Count} selected brand(s)? This cannot be undone.");
            if (!confirmed) return;

            try
            {
                foreach (var brand in SelectedBrands.ToList())
                    if (brand?.Id is not null)
                        await BrandService.DeleteBrandAsync(brand.Id.Value);

                SelectedBrands.Clear();
                await LoadAllBrandsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during bulk delete: {ex.Message}");
            }
        }
    }
}