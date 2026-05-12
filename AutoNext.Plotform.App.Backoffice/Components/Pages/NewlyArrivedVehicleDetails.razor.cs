using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class NewlyArrivedVehicleDetailsBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected INewlyArrivedService NewlyArrivedService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<NewlyArrivedVehicleDetailsBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        protected NewlyArrivedResponseDto? Vehicle { get; set; }
        protected bool IsLoading { get; set; } = true;
        protected string ActiveTab { get; set; } = "specs";
        protected string SecondTab { get; set; } = "standout";

        protected List<VehicleColor> Colors { get; set; } = new();

        protected string SelectedFuelType { get; set; } = "all";
        protected List<string> FuelTypes { get; set; } = new();

        // FIX: Computed property is fine, but adding null-safety guard
        protected List<VariantDetailDto> FilteredVariants => SelectedFuelType == "all"
            ? Vehicle?.Variants?.ToList() ?? new List<VariantDetailDto>()
            : Vehicle?.Variants?.Where(v => v.FuelType == SelectedFuelType).ToList() ?? new List<VariantDetailDto>();

        protected bool ShowImageViewer { get; set; }
        protected string CurrentImageUrl { get; set; } = string.Empty;

        protected bool ShowVideoPlayer { get; set; }
        protected VideoDto? CurrentVideo { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadVehicleDetails();
        }

        protected async Task LoadVehicleDetails()
        {
            try
            {
                IsLoading = true;
                // FIX: Removed redundant StateHasChanged() here — OnInitializedAsync
                // triggers a render automatically; calling it early causes a double render.

                Vehicle = await NewlyArrivedService.GetByIdAsync(VehicleId);

                if (Vehicle != null)
                {
                    InitializeColors();
                    InitializeFuelTypes();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading vehicle details for ID: {VehicleId}", VehicleId);
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle details.");
            }
            finally
            {
                IsLoading = false;
                // FIX: Single StateHasChanged() call here is sufficient
                StateHasChanged();
            }
        }

        protected void InitializeColors()
        {
            Colors = new List<VehicleColor>
            {
                new() { Name = "Andaman Adventure Yellow", Code = "#FFD700" },
                new() { Name = "Oberon Black",             Code = "#1a1a1a" },
                new() { Name = "Sea Green",                Code = "#2E8B57" },
                new() { Name = "Tectonic Blue",            Code = "#1E3A8A" },
                new() { Name = "Pure White",               Code = "#FFFFFF" },
                new() { Name = "Lunar Grey",               Code = "#808080" }
            };
        }

        protected void InitializeFuelTypes()
        {
            if (Vehicle?.Variants != null)
            {
                // FIX: Added .OrderBy for consistent button ordering in the UI
                FuelTypes = Vehicle.Variants
                    .Select(v => v.FuelType)
                    .Distinct()
                    .OrderBy(f => f)
                    .ToList();
            }
        }

        protected string GetMainImage()
        {
            if (Vehicle?.Images?.Any() == true)
            {
                var primary = Vehicle.Images.FirstOrDefault(i => i.IsPrimary);
                return primary?.Url ?? Vehicle.Images.First().Url;
            }
            return "https://placehold.co/800x400?text=No+Image";
        }

        protected string GetEngineSpecs()
        {
            if (Vehicle?.Variants?.Any() == true)
            {
                var engineCc = Vehicle.Variants.First().EngineCc;
                return string.IsNullOrEmpty(engineCc) ? "1497 cc - 1498 cc" : engineCc;
            }
            return "1497 cc - 1498 cc";
        }

        protected string GetPowerSpecs() => "105 - 158 bhp";
        protected string GetTorqueSpecs() => "145 Nm - 280 Nm";
        protected string GetGroundClearance() => "205 mm";

        protected string FormatPrice(decimal price)
        {
            if (price >= 10_000_000) return $"₹{price / 10_000_000:F2} Cr";
            if (price >= 100_000) return $"₹{price / 100_000:F2} L";
            return $"₹{price:N0}";
        }

        protected string FormatVariantPrice(decimal price)
        {
            if (price >= 10_000_000) return $"₹{price / 10_000_000:F2} Cr*";
            if (price >= 100_000) return $"₹{price / 100_000:F2} L*";
            return $"₹{price:N0}*";
        }

        // FIX: Parameter changed from string (double-quoted in Razor) to char
        // so the Razor template can call SetActiveTab('specs') with single quotes
        protected void SetActiveTab(char tab) => SetActiveTab(tab.ToString());
        protected void SetActiveTab(string tab)
        {
            ActiveTab = tab;
            StateHasChanged();
        }

        protected void FilterVariantsByFuel(string fuelType)
        {
            SelectedFuelType = fuelType;
            StateHasChanged();
        }

        protected void SelectColor(VehicleColor color)
        {
            NotificationService.Notify(NotificationSeverity.Info, "Color Selected", $"{color.Name} selected");
        }

        protected void OpenImageViewer()
        {
            CurrentImageUrl = GetMainImage();
            ShowImageViewer = true;
            StateHasChanged();
        }

        protected void CloseImageViewer()
        {
            ShowImageViewer = false;
            StateHasChanged();
        }

        protected void PlayVideo(VideoDto video)
        {
            CurrentVideo = video;
            ShowVideoPlayer = true;
            StateHasChanged();
        }

        protected void CloseVideoPlayer()
        {
            ShowVideoPlayer = false;
            CurrentVideo = null;
            StateHasChanged();
        }

        protected string GetYouTubeEmbedUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            var videoId = ExtractYouTubeVideoId(url);
            // FIX: Guard against empty videoId to avoid a broken iframe src
            return string.IsNullOrEmpty(videoId)
                ? string.Empty
                : $"https://www.youtube.com/embed/{videoId}?autoplay=1";
        }

        protected string ExtractYouTubeVideoId(string url)
        {
            var regex = new System.Text.RegularExpressions.Regex(
                @"(?:youtube\.com\/watch\?v=|youtu\.be\/)([^&\n?#]+)");
            var match = regex.Match(url);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        protected void GetOnRoadPrice() =>
            NotificationService.Notify(NotificationSeverity.Info, "On-Road Price", "Calculating on-road price...");

        protected void ViewMayOffers() =>
            NotificationService.Notify(NotificationSeverity.Success, "May Offers", "Viewing festive offers...");

        protected void RateVehicle() =>
            NotificationService.Notify(NotificationSeverity.Info, "Rate & Win", "Opening rating dialog...");

        protected void ReadMore() =>
            NotificationService.Notify(NotificationSeverity.Info, "Description", "Loading full description...");

        protected void ReadCompleteReview() =>
            NotificationService.Notify(NotificationSeverity.Info, "Expert Review", "Loading complete review...");

        protected void ViewVariantDetails(VariantDetailDto variant) =>
            NotificationService.Notify(NotificationSeverity.Info, "Variant Details", $"Viewing {variant.VariantName} details...");

        protected void GoBack() =>
            Navigation.NavigateTo("/newly-arrived-vehicles");
    }

    public class VehicleColor
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}