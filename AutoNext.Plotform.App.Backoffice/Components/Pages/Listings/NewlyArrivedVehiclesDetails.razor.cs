using AutoNext.Plotform.App.Backoffice.Integrations.Blob;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Text.RegularExpressions;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages.Listings
{
    [Authorize]
    public class NewlyArrivedVehiclesDetailsBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected INewlyArrivedService NewlyArrivedService { get; set; } = default!;
        [Inject] protected IBlobService BlobService { get; set; } = default!;
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
        protected string BasicSection { get; set; } = "basic";
        protected string PricingSection { get; set; } = "pricing";
        protected string ImagesSection { get; set; } = "images";
        protected string VideosSection { get; set; } = "videos";
        protected string ArrivalSettingsSection { get; set; } = "arrivalsettings";
        protected string VariantsSection { get; set; } = "variants";
        protected string SeoSection { get; set; } = "seo";
        protected string Video { get; set; } = "video";
        protected string Short { get; set; } = "short";

        // Image upload
        protected List<IBrowserFile> SelectedImageFiles { get; set; } = new();
        protected bool IsUploadingImages { get; set; } = false;
        protected string ImageUploadProgress { get; set; } = string.Empty;

        // Video management
        protected VideoDto NewVideo { get; set; } = new();
        protected bool IsAddingVideo { get; set; } = false;

        // Video Player properties
        protected string SelectedVideoUrl { get; set; } = string.Empty;
        protected bool ShowVideoModal { get; set; } = false;
        protected string VideoTitle { get; set; } = string.Empty;

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
                    MapVehicleToEditModel();
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

        private void MapVehicleToEditModel()
        {
            if (Vehicle == null) return;

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

        private void SyncVehicleFromEditModel(string section)
        {
            if (Vehicle == null) return;

            switch (section)
            {
                case "basic":
                    Vehicle.BrandName = EditModel.BrandName;
                    Vehicle.ModelName = EditModel.ModelName;
                    Vehicle.VehicleType = EditModel.VehicleType;
                    Vehicle.BodyType = EditModel.BodyType;
                    Vehicle.PageTitle = EditModel.PageTitle;
                    Vehicle.DescriptionText = EditModel.DescriptionText;
                    break;
                case "pricing":
                    Vehicle.MinPrice = EditModel.MinPrice;
                    Vehicle.MaxPrice = EditModel.MaxPrice;
                    Vehicle.Emi = EditModel.Emi;
                    break;
                case "images":
                    Vehicle.Images = EditModel.Images.Select(img => new ImageDto
                    {
                        FileId = img.FileId,
                        FileUrl = img.FileUrl,
                        IsPrimary = img.IsPrimary
                    }).ToList();
                    break;
                case "videos":
                    Vehicle.Videos = EditModel.Videos.Select(v => new VideoDto
                    {
                        FileUrl = v.FileUrl,
                        ThumbnailUrl = v.ThumbnailUrl,
                        Duration = v.Duration
                    }).ToList();
                    break;
                case "arrivalsettings":
                    Vehicle.ArrivalPeriod = EditModel.ArrivalPeriod;
                    Vehicle.Rating = EditModel.Rating;
                    Vehicle.ReviewCount = EditModel.ReviewCount;
                    break;
                case "variants":
                    Vehicle.Variants = EditModel.Variants.Select(v => new VariantDetailDto
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
                    }).ToList();
                    break;
                case "seo":
                    Vehicle.PageTitle = EditModel.PageTitle;
                    Vehicle.DescriptionText = EditModel.DescriptionText;
                    break;
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
                IsSaving = true;
                StateHasChanged();

                await SaveSectionToDatabase(section);
                SyncVehicleFromEditModel(section);

                EditingSections.Remove(section);
                ChangedSections.Remove(section);

                NotificationService.Notify(NotificationSeverity.Success, "Saved",
                    $"{section} section saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving section: {Section}", section);
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to save {section} section.");
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
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
                    MapVehicleToEditModel();
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

        private async Task<IFormFile> ConvertToFormFile(IBrowserFile browserFile)
        {
            var memoryStream = new MemoryStream();
            await browserFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024).CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return new FormFile(memoryStream, 0, memoryStream.Length, browserFile.Name, browserFile.Name)
            {
                Headers = new HeaderDictionary(),
                ContentType = browserFile.ContentType
            };
        }

        protected async Task SaveSectionToDatabase(string section)
        {
            try
            {
                var partialUpdate = new NewlyArrivedRequestDto();

                switch (section)
                {
                    case "images":
                        partialUpdate.Images = EditModel.Images;
                        break;
                    case "videos":
                        partialUpdate.Videos = EditModel.Videos;
                        break;
                    case "basic":
                        partialUpdate.BrandName = EditModel.BrandName;
                        partialUpdate.ModelName = EditModel.ModelName;
                        partialUpdate.VehicleType = EditModel.VehicleType;
                        partialUpdate.BodyType = EditModel.BodyType;
                        partialUpdate.PageTitle = EditModel.PageTitle;
                        partialUpdate.DescriptionText = EditModel.DescriptionText;
                        break;
                    case "pricing":
                        partialUpdate.MinPrice = EditModel.MinPrice;
                        partialUpdate.MaxPrice = EditModel.MaxPrice;
                        partialUpdate.Emi = EditModel.Emi;
                        break;
                    case "arrivalsettings":
                        partialUpdate.ArrivalPeriod = EditModel.ArrivalPeriod;
                        partialUpdate.Rating = EditModel.Rating;
                        partialUpdate.ReviewCount = EditModel.ReviewCount;
                        break;
                    case "variants":
                        partialUpdate.Variants = EditModel.Variants;
                        break;
                    case "seo":
                        partialUpdate.PageTitle = EditModel.PageTitle;
                        partialUpdate.DescriptionText = EditModel.DescriptionText;
                        break;
                }

                var updatedVehicle = await NewlyArrivedService.UpdateAsync(VehicleId, partialUpdate);

                if (updatedVehicle != null)
                {
                    Vehicle = updatedVehicle;

                    if (section == "images")
                    {
                        EditModel.Images = updatedVehicle.Images?.Select(img => new ImageDto
                        {
                            FileId = img.FileId.ToString(),
                            FileUrl = img.FileUrl,
                            IsPrimary = img.IsPrimary
                        }).ToList() ?? new List<ImageDto>();
                    }

                    Logger.LogInformation("Section {Section} saved to database successfully", section);
                }
                else
                {
                    throw new Exception($"Server returned null for section {section} update.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving section {Section} to database", section);
                throw;
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
            if (!SelectedImageFiles.Any())
                return;

            try
            {
                IsUploadingImages = true;
                ImageUploadProgress = "Uploading images...";
                StateHasChanged();

                foreach (var file in SelectedImageFiles)
                {
                    ImageUploadProgress = $"Uploading {file.Name}...";
                    StateHasChanged();

                    var formFile = await ConvertToFormFile(file);
                    var uploadResponse = await BlobService.UploadAsync(formFile);

                    if (uploadResponse == null || string.IsNullOrEmpty(uploadResponse.FileId.ToString()))
                    {
                        Logger.LogWarning("Blob upload failed for file: {FileName}", file.Name);
                        continue;
                    }

                    var newImage = new ImageDto
                    {
                        FileId = uploadResponse.FileId.ToString(),
                        FileUrl = uploadResponse.FileUrl,
                        IsPrimary = !EditModel.Images.Any()
                    };

                    EditModel.Images.Add(newImage);
                    Logger.LogInformation("Image uploaded successfully. FileId: {FileId}", uploadResponse.FileId);
                }

                SelectedImageFiles.Clear();

                try
                {
                    ImageUploadProgress = "Saving to database...";
                    StateHasChanged();

                    await SaveSectionToDatabase("images");
                    SyncVehicleFromEditModel("images");
                    ChangedSections.Remove(ImagesSection);

                    NotificationService.Notify(NotificationSeverity.Success, "Upload Complete",
                        $"{EditModel.Images.Count} image(s) uploaded and saved successfully.");
                }
                catch (Exception dbEx)
                {
                    Logger.LogError(dbEx, "Error saving images to database");
                    NotificationService.Notify(NotificationSeverity.Error, "Save Failed",
                        "Images uploaded but failed to save to database. Please save manually.");
                    ChangedSections.Add(ImagesSection);
                }
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

        protected async Task RemoveImage(int index)
        {
            try
            {
                if (index < 0 || index >= EditModel.Images.Count)
                    return;

                var image = EditModel.Images[index];

                if (!string.IsNullOrWhiteSpace(image.FileId) && Guid.TryParse(image.FileId, out var fileId))
                {
                    var deleted = await BlobService.DeleteAsync(fileId);
                    if (!deleted)
                    {
                        NotificationService.Notify(NotificationSeverity.Error, "Delete Failed",
                            "Failed to delete image from blob storage.");
                        return;
                    }
                    Logger.LogInformation("Image deleted from blob storage. FileId: {FileId}", fileId);
                }

                EditModel.Images.RemoveAt(index);

                if (EditModel.Images.Any() && !EditModel.Images.Any(x => x.IsPrimary))
                    EditModel.Images.First().IsPrimary = true;

                try
                {
                    await SaveSectionToDatabase("images");
                    SyncVehicleFromEditModel("images");
                    ChangedSections.Remove(ImagesSection);

                    NotificationService.Notify(NotificationSeverity.Success, "Image Removed",
                        "Image deleted and saved successfully.");
                }
                catch (Exception dbEx)
                {
                    Logger.LogError(dbEx, "Error saving image removal to database");
                    NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                        "Image removed but changes not saved to database. Please save manually.");
                    ChangedSections.Add(ImagesSection);
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error removing image");
                NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
            }
        }

        protected async Task SetPrimaryImage(int index)
        {
            if (index < 0 || index >= EditModel.Images.Count) return;

            foreach (var img in EditModel.Images)
                img.IsPrimary = false;

            EditModel.Images[index].IsPrimary = true;

            try
            {
                await SaveSectionToDatabase("images");
                SyncVehicleFromEditModel("images");
                ChangedSections.Remove(ImagesSection);

                NotificationService.Notify(NotificationSeverity.Success, "Primary Image Updated",
                    "Primary image saved successfully.");
            }
            catch (Exception dbEx)
            {
                Logger.LogError(dbEx, "Error saving primary image to database");
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Primary image updated but not saved to database. Please save manually.");
                ChangedSections.Add(ImagesSection);
            }

            StateHasChanged();
        }

        // Video Management Methods
        protected void CancelVideo()
        {
            NewVideo = new VideoDto();
            IsAddingVideo = false;
            StateHasChanged();
        }

        protected async Task AddVideo()
        {
            if (string.IsNullOrWhiteSpace(NewVideo.FileUrl))
                return;

            EditModel.Videos.Add(new VideoDto
            {
                FileUrl = NewVideo.FileUrl,
                ThumbnailUrl = NewVideo.ThumbnailUrl,
                Duration = NewVideo.Duration
            });

            NewVideo = new VideoDto();
            IsAddingVideo = false;

            try
            {
                await SaveSectionToDatabase("videos");
                SyncVehicleFromEditModel("videos");
                ChangedSections.Remove(VideosSection);

                NotificationService.Notify(NotificationSeverity.Success, "Video Added",
                    "Video saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving video to database");
                ChangedSections.Add(VideosSection);
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Video added but not saved to database. Please save manually.");
            }

            StateHasChanged();
        }

        protected async Task RemoveVideo(int index)
        {
            if (index < 0 || index >= EditModel.Videos.Count)
                return;

            EditModel.Videos.RemoveAt(index);

            try
            {
                await SaveSectionToDatabase("videos");
                SyncVehicleFromEditModel("videos");
                ChangedSections.Remove(VideosSection);

                NotificationService.Notify(NotificationSeverity.Success, "Video Removed",
                    "Video removed and saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving video removal to database");
                ChangedSections.Add(VideosSection);
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Video removed but not saved to database. Please save manually.");
            }

            StateHasChanged();
        }

        // Video Player Methods
        protected void PlayVideo(string url, string title)
        {
            SelectedVideoUrl = url;
            VideoTitle = title;
            ShowVideoModal = true;
            StateHasChanged();
        }

        protected void CloseVideoModal()
        {
            ShowVideoModal = false;
            SelectedVideoUrl = string.Empty;
            StateHasChanged();
        }

        protected string GetYouTubeEmbedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            var videoId = ExtractYouTubeVideoId(url);
            if (string.IsNullOrEmpty(videoId)) return string.Empty;

            var parameters = new List<string>
            {
                "autoplay=1",
                "rel=0",
                "modestbranding=1",
                "controls=1",
                "showinfo=0",
                "iv_load_policy=3",
                "enablejsapi=0"
            };

            return $"https://www.youtube-nocookie.com/embed/{videoId}?{string.Join("&", parameters)}";
        }

        protected void OnVideoUrlChanged(string url)
        {
            NewVideo.FileUrl = url;
            NewVideo.ThumbnailUrl = ExtractYouTubeThumbnail(url);
            StateHasChanged();
        }

        protected string ExtractYouTubeThumbnail(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            var videoId = ExtractYouTubeVideoId(url);
            return !string.IsNullOrEmpty(videoId)
                ? $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg"
                : string.Empty;
        }

        protected string ExtractYouTubeVideoId(string url)
        {
            var regex = new Regex(@"(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/shorts\/)([^&\n?#]+)");
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

            if (!ChangedSections.Contains(VariantsSection))
                ChangedSections.Add(VariantsSection);

            StateHasChanged();
        }

        protected void RemoveVariant(int index)
        {
            if (index >= 0 && index < EditModel.Variants.Count)
            {
                EditModel.Variants.RemoveAt(index);
                if (!ChangedSections.Contains(VariantsSection))
                    ChangedSections.Add(VariantsSection);
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

        protected string FormatPriceString(string price)
        {
            if (string.IsNullOrEmpty(price)) return "₹0";
            if (decimal.TryParse(price.Replace("₹", "").Replace(",", ""), out decimal value))
            {
                return FormatPrice(value);
            }
            return price;
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