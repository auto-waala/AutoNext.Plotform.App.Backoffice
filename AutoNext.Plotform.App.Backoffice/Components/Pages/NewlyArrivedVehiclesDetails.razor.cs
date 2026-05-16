using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class NewlyArrivedVehiclesDetailsBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected INewlyArrivedService NewlyArrivedService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected ILogger<NewlyArrivedVehiclesDetailsBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        protected NewlyArrivedResponseDto? Vehicle { get; set; }
        protected NewlyArrivedRequestDto EditModel { get; set; } = new();
        protected bool IsLoading { get; set; } = true;
        protected bool IsSaving { get; set; } = false;
        protected bool ShowSaveConfirmation { get; set; } = false;

        // Track editing state per section
        protected HashSet<string> EditingSections { get; set; } = new();
        protected HashSet<string> ChangedSections { get; set; } = new();
        protected bool HasChanges => ChangedSections.Any();

        // Section identifiers
        protected string Basic { get; set; } = "basic";
        protected string Pricing { get; set; } = "pricing";
        protected string Images { get; set; } = "images";
        protected string Videos { get; set; } = "videos";
        protected string ArrivalSettings { get; set; } = "arrivalsettings";
        protected string Variants { get; set; } = "variants";
        protected string Seo { get; set; } = "seo";

        // Image upload
        protected List<IBrowserFile> SelectedImageFiles { get; set; } = new();
        protected bool IsUploadingImages { get; set; } = false;
        protected string ImageUploadProgress { get; set; } = string.Empty;

        // Video management
        protected VideoDto NewVideo { get; set; } = new();
        protected bool IsAddingVideo { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadVehicleDetails();
        }

        protected async Task LoadVehicleDetails()
        {
            try
            {
                IsLoading = true;
                StateHasChanged();

                Vehicle = await NewlyArrivedService.GetByIdAsync(VehicleId);

                if (Vehicle != null)
                {
                    // Initialize EditModel from Vehicle
                    EditModel = new NewlyArrivedRequestDto
                    {
                        BrandName = Vehicle.BrandName,
                        ModelName = Vehicle.ModelName,
                        VehicleType = Vehicle.VehicleType,
                        BodyType = Vehicle.BodyType,
                        MinPrice = Vehicle.MinPrice,
                        MaxPrice = Vehicle.MaxPrice,
                        ArrivalPeriod = Vehicle.ArrivalPeriod,
                        Emi = Vehicle.Emi ?? new EmiDto(),
                        Images = Vehicle.Images?.Select(img => new ImageDto 
                        { 
                            FileId = img.FileId, 
                            FileUrl = img.FileUrl, 
                            IsPrimary = img.IsPrimary 
                        }).ToList() ?? new List<ImageDto>(),
                        Videos = Vehicle.Videos?.Select(vid => new VideoDto 
                        { 
                            FileUrl = vid.FileUrl, 
                            ThumbnailUrl = vid.ThumbnailUrl, 
                            Duration = vid.Duration 
                        }).ToList() ?? new List<VideoDto>(),
                        Variants = Vehicle.Variants?.Select(v => new VariantDetailDto
                        {
                            VariantId = v.VariantId,
                            VariantName = v.VariantName,
                            VariantShortName = v.VariantShortName,
                            VariantSlug = v.VariantSlug,
                            ExShowRoomPrice = v.ExShowRoomPrice,
                            OnRoadPrice = v.OnRoadPrice,
                            OnRoadPriceValue = v.OnRoadPriceValue,
                            FuelType = v.FuelType,
                            Transmission = v.Transmission,
                            Mileage = v.Mileage,
                            EngineCc = v.EngineCc,
                            Emi = v.Emi,
                            Tag = v.Tag,
                            IsRecentLaunch = v.IsRecentLaunch,
                            IsTopSelling = v.IsTopSelling,
                            PriceBreakup = v.PriceBreakup
                        }).ToList() ?? new List<VariantDetailDto>(),
                        Rating = Vehicle.Rating,
                        ReviewCount = Vehicle.ReviewCount,
                        PageTitle = Vehicle.PageTitle,
                        DescriptionText = Vehicle.DescriptionText
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading vehicle for editing: {VehicleId}", VehicleId);
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle details.");
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        protected void ToggleSectionEdit(string section)
        {
            if (EditingSections.Contains(section))
                EditingSections.Remove(section);
            else
                EditingSections.Add(section);
            StateHasChanged();
        }

        protected void CancelSectionEdit(string section)
        {
            EditingSections.Remove(section);
            // Reload original values from Vehicle
            if (Vehicle != null)
            {
                switch (section)
                {
                    case "basic":
                        EditModel.BrandName = Vehicle.BrandName;
                        EditModel.ModelName = Vehicle.ModelName;
                        EditModel.VehicleType = Vehicle.VehicleType;
                        EditModel.BodyType = Vehicle.BodyType;
                        EditModel.PageTitle = Vehicle.PageTitle;
                        EditModel.DescriptionText = Vehicle.DescriptionText;
                        break;
                    case "pricing":
                        EditModel.MinPrice = Vehicle.MinPrice;
                        EditModel.MaxPrice = Vehicle.MaxPrice;
                        EditModel.Emi = Vehicle.Emi ?? new EmiDto();
                        break;
                    case "images":
                        EditModel.Images = Vehicle.Images?.Select(img => new ImageDto 
                        { 
                            FileId = img.FileId, 
                            FileUrl = img.FileUrl, 
                            IsPrimary = img.IsPrimary 
                        }).ToList() ?? new List<ImageDto>();
                        SelectedImageFiles.Clear();
                        break;
                    case "videos":
                        EditModel.Videos = Vehicle.Videos?.Select(vid => new VideoDto 
                        { 
                            FileUrl = vid.FileUrl, 
                            ThumbnailUrl = vid.ThumbnailUrl, 
                            Duration = vid.Duration 
                        }).ToList() ?? new List<VideoDto>();
                        break;
                    case "arrivalsettings":
                        EditModel.ArrivalPeriod = Vehicle.ArrivalPeriod;
                        EditModel.Rating = Vehicle.Rating;
                        EditModel.ReviewCount = Vehicle.ReviewCount;
                        break;
                    case "variants":
                        EditModel.Variants = Vehicle.Variants?.Select(v => new VariantDetailDto
                        {
                            VariantId = v.VariantId,
                            VariantName = v.VariantName,
                            VariantShortName = v.VariantShortName,
                            VariantSlug = v.VariantSlug,
                            ExShowRoomPrice = v.ExShowRoomPrice,
                            OnRoadPrice = v.OnRoadPrice,
                            OnRoadPriceValue = v.OnRoadPriceValue,
                            FuelType = v.FuelType,
                            Transmission = v.Transmission,
                            Mileage = v.Mileage,
                            EngineCc = v.EngineCc,
                            Emi = v.Emi,
                            Tag = v.Tag,
                            IsRecentLaunch = v.IsRecentLaunch,
                            IsTopSelling = v.IsTopSelling,
                            PriceBreakup = v.PriceBreakup
                        }).ToList() ?? new List<VariantDetailDto>();
                        break;
                    case "seo":
                        EditModel.PageTitle = Vehicle.PageTitle;
                        EditModel.DescriptionText = Vehicle.DescriptionText;
                        break;
                }
            }
            StateHasChanged();
        }

        protected async Task SaveSection(string section)
        {
            try
            {
                if (!ChangedSections.Contains(section))
                    ChangedSections.Add(section);

                EditingSections.Remove(section);

                NotificationService.Notify(NotificationSeverity.Success, "Section Updated",
                    $"Changes saved to {section} section. Click 'Save All Changes' to persist to server.");

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving section: {Section}", section);
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to save {section} section.");
            }
        }

        protected void SaveAllChanges()
        {
            if (HasChanges)
            {
                ShowSaveConfirmation = true;
                StateHasChanged();
            }
        }

        protected void CloseSaveConfirmation()
        {
            ShowSaveConfirmation = false;
            StateHasChanged();
        }

        protected async Task ConfirmSaveAll()
        {
            try
            {
                IsSaving = true;
                StateHasChanged();

                var updatedVehicle = await NewlyArrivedService.UpdateAsync(VehicleId, EditModel);

                if (updatedVehicle != null)
                {
                    Vehicle = updatedVehicle;
                    ChangedSections.Clear();

                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "Vehicle updated successfully!");

                    Navigation.NavigateTo($"/newly-arrived-vehicles/{VehicleId}");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error",
                        "Failed to update vehicle.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving all changes for vehicle: {VehicleId}", VehicleId);
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to save changes: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                ShowSaveConfirmation = false;
                StateHasChanged();
            }
        }

        // Image Management Methods
        protected async Task HandleImageFilesSelected(InputFileChangeEventArgs e)
        {
            SelectedImageFiles.Clear();
            foreach (var file in e.GetMultipleFiles(10))
            {
                SelectedImageFiles.Add(file);
            }
            StateHasChanged();
        }

        protected async Task UploadImages()
        {
            if (!SelectedImageFiles.Any()) return;

            try
            {
                IsUploadingImages = true;
                ImageUploadProgress = "Uploading images...";
                StateHasChanged();

                foreach (var file in SelectedImageFiles)
                {
                    var imageUrl = await SimulateImageUpload(file);
                    
                    var newImage = new ImageDto
                    {
                        FileId = Guid.NewGuid().ToString(),
                        FileUrl = imageUrl,
                        IsPrimary = !EditModel.Images.Any()
                    };
                    
                    EditModel.Images.Add(newImage);
                }

                SelectedImageFiles.Clear();
                if (!ChangedSections.Contains(Images))
                    ChangedSections.Add(Images);

                NotificationService.Notify(NotificationSeverity.Success, "Upload Complete",
                    $"{EditModel.Images.Count} images uploaded successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error uploading images");
                NotificationService.Notify(NotificationSeverity.Error, "Upload Failed", ex.Message);
            }
            finally
            {
                IsUploadingImages = false;
                ImageUploadProgress = string.Empty;
                StateHasChanged();
            }
        }

        protected async Task<string> SimulateImageUpload(IBrowserFile file)
        {
            await Task.Delay(500);
            return $"https://via.placeholder.com/400x300?text={Uri.EscapeDataString(file.Name)}";
        }

        protected void SetPrimaryImage(int index)
        {
            if (index < 0 || index >= EditModel.Images.Count) return;

            foreach (var img in EditModel.Images)
                img.IsPrimary = false;
            
            EditModel.Images[index].IsPrimary = true;
            
            if (!ChangedSections.Contains(Images))
                ChangedSections.Add(Images);
            
            StateHasChanged();
        }

        protected void RemoveImage(int index)
        {
            if (index >= 0 && index < EditModel.Images.Count)
            {
                EditModel.Images.RemoveAt(index);
                if (!ChangedSections.Contains(Images))
                    ChangedSections.Add(Images);
                StateHasChanged();
            }
        }

        // Video Management Methods

        protected void CancelVideo()
        {
            NewVideo = new VideoDto();
            IsAddingVideo = false;
            StateHasChanged();
        }
        protected void AddVideo()
        {
            if (!string.IsNullOrWhiteSpace(NewVideo.FileUrl))
            {
                EditModel.Videos.Add(new VideoDto
                {
                    FileUrl = NewVideo.FileUrl,
                    ThumbnailUrl = NewVideo.ThumbnailUrl,
                    Duration = NewVideo.Duration
                });
                
                NewVideo = new VideoDto();
                IsAddingVideo = false;
                
                if (!ChangedSections.Contains(Videos))
                    ChangedSections.Add(Videos);
                
                StateHasChanged();
            }
        }

        protected void RemoveVideo(int index)
        {
            if (index >= 0 && index < EditModel.Videos.Count)
            {
                EditModel.Videos.RemoveAt(index);
                if (!ChangedSections.Contains(Videos))
                    ChangedSections.Add(Videos);
                StateHasChanged();
            }
        }

        protected void OnVideoUrlChanged(string url)
        {
            NewVideo.ThumbnailUrl = ExtractYouTubeThumbnail(url);
            StateHasChanged();
        }

        protected string ExtractYouTubeThumbnail(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            
            var videoId = ExtractYouTubeVideoId(url);
            if (!string.IsNullOrEmpty(videoId))
            {
                return $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
            }
            return string.Empty;
        }

        protected string ExtractYouTubeVideoId(string url)
        {
            var regex = new System.Text.RegularExpressions.Regex(
                @"(?:youtube\.com\/watch\?v=|youtu\.be\/)([^&\n?#]+)");
            var match = regex.Match(url);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Variant Management Methods
        protected void AddVariant()
        {
            EditModel.Variants.Add(new VariantDetailDto
            {
                VariantId = Guid.NewGuid().ToString(),
                VariantName = string.Empty,
                VariantShortName = string.Empty,
                ExShowRoomPrice = string.Empty,
                OnRoadPrice = string.Empty,
                FuelType = string.Empty,
                Transmission = string.Empty
            });
            
            if (!ChangedSections.Contains(Variants))
                ChangedSections.Add(Variants);
            
            StateHasChanged();
        }

        protected void RemoveVariant(int index)
        {
            if (index >= 0 && index < EditModel.Variants.Count)
            {
                EditModel.Variants.RemoveAt(index);
                if (!ChangedSections.Contains(Variants))
                    ChangedSections.Add(Variants);
                StateHasChanged();
            }
        }

        // Helper Methods
        protected string FormatPrice(decimal price)
        {
            if (price >= 10_000_000) return $"₹{price / 10_000_000:F2} Cr";
            if (price >= 100_000) return $"₹{price / 100_000:F2} L";
            return $"₹{price:N0}";
        }

        protected string GetArrivalPeriodBadgeClass(string period) => period?.ToLower() switch
        {
            "weekly" => "bg-success",
            "monthly" => "bg-primary",
            "yearly" => "bg-warning text-dark",
            _ => "bg-secondary"
        };

        protected void GoBack()
        {
            if (HasChanges)
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Unsaved Changes",
                    "You have unsaved changes. Please save before leaving.");
            }
            else
            {
                Navigation.NavigateTo($"/newly-arrived-vehicles/{VehicleId}");
            }
        }
    }
}