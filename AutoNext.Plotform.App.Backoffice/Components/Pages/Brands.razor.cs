using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Core;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class BrandsBase : ComponentBase
    {
        [Inject] protected IBrandService BrandService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected IEnumerable<Brand> Brands { get; set; } = new List<Brand>();
        protected IEnumerable<Brand> brands => Brands;

        protected int ItemsPerPage { get; set; } = 25;
        protected int CurrentPage { get; set; } = 1;
        protected int TotalCount { get; set; }
        protected bool IsLoading { get; set; } = false;

        protected IEnumerable<int> PageSizeOptions = new[] { 10, 25, 50, 100 };

        // Required by razor: SelectedBrands for bulk delete
        protected IList<Brand> SelectedBrands { get; set; } = new List<Brand>();

        protected override async Task OnInitializedAsync()
        {
            await LoadBrandsAsync();
        }

        protected async Task LoadBrandsAsync()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                var allBrands = await BrandService.GetAllBrandsAsync();
                Brands = allBrands ?? new List<Brand>();
                TotalCount = Brands.Count();
            }
            catch (Exception)
            {
                // Log exception here if you have a logger injected
                throw; // preserves original stack trace
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void CreateNewBrand()
        {
            Navigation.NavigateTo("/brands/create");
        }

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
                await LoadBrandsAsync();
            }
            catch (Exception)
            {
                throw;
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
                {
                    if (brand?.Id is not null)
                        await BrandService.DeleteBrandAsync(brand.Id.Value);
                }

                SelectedBrands.Clear();
                await LoadBrandsAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public string GetFlagEmoji(string country)
        {
            return country switch
            {
                "USA" => "🇺🇸",
                "UK" => "🇬🇧",
                "Germany" => "🇩🇪",
                "France" => "🇫🇷",
                "India" => "🇮🇳",
                "China" => "🇨🇳",
                _ => "🌍"
            };
        }

        public async Task ItemsPerPageChanged(int itemsPerPage)
        {
            ItemsPerPage = itemsPerPage;
            CurrentPage = 1;
            await LoadBrandsAsync();
        }

        public async Task CurrentPageChanged(int currentPage)
        {
            CurrentPage = currentPage;
            await LoadBrandsAsync();
        }
    }
}