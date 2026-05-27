using AutoNext.Plotform.App.Backoffice.Integrations.Blob;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Text.RegularExpressions;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    [Authorize]
    public class FeaturedVehicleEditBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected IFeaturedVehicleService FeaturedVehicleService { get; set; } = default!;
        [Inject] protected IBlobService BlobService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected ILogger<FeaturedVehicleEditBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;

        protected FeaturedVehicleResponseDto? Vehicle { get; set; }
        protected FeaturedVehicleRequestDto EditModel { get; set; } = new();
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
        protected string SpecsSection { get; set; } = "specs";
        protected string ImagesSection { get; set; } = "images";
        protected string FeaturesSection { get; set; } = "features";
        protected string FeaturedSection { get; set; } = "featured";
        protected string VideosSection { get; set; } = "videos";
        protected string ShortsSection { get; set; } = "shorts";
        protected string ProsConsSection { get; set; } = "proscons";
        protected string SellerSection { get; set; } = "seller";
        protected string BadgesSection { get; set; } = "badges";
        protected string TestDriveSection { get; set; } = "testdrive";
        protected string Video { get; set; } = "video";
        protected string Short { get; set; } = "short";

        // Image upload
        protected List<IBrowserFile> SelectedImageFiles { get; set; } = new();
        protected bool IsUploadingImages { get; set; } = false;
        protected string ImageUploadProgress { get; set; } = string.Empty;

        // Video management
        protected VideoDto NewVideo { get; set; } = new();
        protected VideoDto NewShort { get; set; } = new();
        protected bool IsAddingVideo { get; set; } = false;
        protected bool IsAddingShort { get; set; } = false;

        // Video Player properties
        protected string SelectedVideoUrl { get; set; } = string.Empty;
        protected string SelectedShortUrl { get; set; } = string.Empty;
        protected bool ShowVideoModal { get; set; } = false;
        protected bool ShowShortModal { get; set; } = false;
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

                Vehicle = await FeaturedVehicleService.GetByIdAsync(VehicleId);

                if (Vehicle != null)
                {
                    MapVehicleToEditModel();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading featured vehicle for editing: {VehicleId}", VehicleId);
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

            EditModel = new FeaturedVehicleRequestDto
            {
                Title = Vehicle.Title,
                Descriptions = Vehicle.Descriptions,
                Slug = Vehicle.Slug,
                MetaTitle = Vehicle.MetaTitle,
                MetaDescription = Vehicle.MetaDescription,
                BrandName = Vehicle.BrandName,
                ModelName = Vehicle.ModelName,
                ModelSlug = Vehicle.ModelSlug,
                VehicleType = Vehicle.VehicleType,
                BodyType = Vehicle.BodyType,
                Price = Vehicle.Price ?? new PriceInfoDto(),
                PriceRangeFrom = Vehicle.PriceRangeFrom,
                PriceRangeTo = Vehicle.PriceRangeTo,
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
                Shorts = Vehicle.Shorts?.Select(shortVideo => new VideoDto
                {
                    FileUrl = shortVideo.FileUrl,
                    ThumbnailUrl = shortVideo.ThumbnailUrl,
                    Duration = shortVideo.Duration
                }).ToList() ?? new List<VideoDto>(),
                Variants = Vehicle.Variants,
                KeySpecifications = Vehicle.KeySpecifications ?? new KeySpecificationsDto(),
                TopFeatures = Vehicle.TopFeatures,
                StandOutFeatures = Vehicle.StandOutFeatures,
                Pros = Vehicle.Pros,
                Cons = Vehicle.Cons,
                Tags = Vehicle.Tags,
                Seller = Vehicle.Seller ?? new SellerInfoDto(),
                Location = Vehicle.Location ?? new LocationInfoDto(),
                Condition = Vehicle.Condition ?? new VehicleConditionDto(),
                ListingDetails = Vehicle.ListingDetails ?? new ListingDetailsDto(),
                Badges = Vehicle.Badges,
                Highlight = Vehicle.Highlight,
                TestDrive = Vehicle.TestDrive ?? new TestDriveInfoDto(),
                Priority = Vehicle.Priority,
                IsActive = Vehicle.IsActive,
                StartDate = Vehicle.StartDate,
                EndDate = Vehicle.EndDate
            };
        }

        private void SyncVehicleFromEditModel(string section)
        {
            if (Vehicle == null) return;

            switch (section)
            {
                case "basic":
                    Vehicle.Title = EditModel.Title;
                    Vehicle.Descriptions = EditModel.Descriptions;
                    Vehicle.Slug = EditModel.Slug;
                    Vehicle.MetaTitle = EditModel.MetaTitle;
                    Vehicle.MetaDescription = EditModel.MetaDescription;
                    Vehicle.BrandName = EditModel.BrandName;
                    Vehicle.ModelName = EditModel.ModelName;
                    Vehicle.ModelSlug = EditModel.ModelSlug;
                    Vehicle.VehicleType = EditModel.VehicleType;
                    Vehicle.BodyType = EditModel.BodyType;
                    break;
                case "pricing":
                    Vehicle.Price = EditModel.Price;
                    Vehicle.PriceRangeFrom = EditModel.PriceRangeFrom;
                    Vehicle.PriceRangeTo = EditModel.PriceRangeTo;
                    break;
                case "specs":
                    Vehicle.KeySpecifications = EditModel.KeySpecifications;
                    break;
                case "features":
                    Vehicle.TopFeatures = EditModel.TopFeatures;
                    Vehicle.StandOutFeatures = EditModel.StandOutFeatures;
                    break;
                case "featured":
                    Vehicle.Priority = EditModel.Priority;
                    Vehicle.IsActive = EditModel.IsActive;
                    Vehicle.StartDate = EditModel.StartDate;
                    Vehicle.EndDate = EditModel.EndDate;
                    break;
                case "proscons":
                    Vehicle.Pros = EditModel.Pros;
                    Vehicle.Cons = EditModel.Cons;
                    break;
                case "seller":
                    Vehicle.Seller = EditModel.Seller;
                    Vehicle.Location = EditModel.Location;
                    break;
                case "badges":
                    Vehicle.Badges = EditModel.Badges;
                    Vehicle.Highlight = EditModel.Highlight;
                    break;
                case "testdrive":
                    Vehicle.TestDrive = EditModel.TestDrive;
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
                case "shorts":
                    Vehicle.Shorts = EditModel.Shorts.Select(s => new VideoDto
                    {
                        FileUrl = s.FileUrl,
                        ThumbnailUrl = s.ThumbnailUrl,
                        Duration = s.Duration
                    }).ToList();
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
                        EditModel.ModelSlug = Vehicle.ModelSlug;
                        EditModel.Slug = Vehicle.Slug;
                        EditModel.Title = Vehicle.Title;
                        EditModel.Descriptions = Vehicle.Descriptions;
                        EditModel.MetaTitle = Vehicle.MetaTitle;
                        EditModel.MetaDescription = Vehicle.MetaDescription;
                        break;
                    case "pricing":
                        EditModel.Price = Vehicle.Price ?? new PriceInfoDto();
                        EditModel.PriceRangeFrom = Vehicle.PriceRangeFrom;
                        EditModel.PriceRangeTo = Vehicle.PriceRangeTo;
                        break;
                    case "specs":
                        EditModel.KeySpecifications = Vehicle.KeySpecifications ?? new KeySpecificationsDto();
                        break;
                    case "features":
                        EditModel.TopFeatures = Vehicle.TopFeatures;
                        EditModel.StandOutFeatures = Vehicle.StandOutFeatures;
                        break;
                    case "featured":
                        EditModel.Priority = Vehicle.Priority;
                        EditModel.IsActive = Vehicle.IsActive;
                        EditModel.StartDate = Vehicle.StartDate;
                        EditModel.EndDate = Vehicle.EndDate;
                        break;
                    case "proscons":
                        EditModel.Pros = Vehicle.Pros;
                        EditModel.Cons = Vehicle.Cons;
                        break;
                    case "seller":
                        EditModel.Seller = Vehicle.Seller ?? new SellerInfoDto();
                        EditModel.Location = Vehicle.Location ?? new LocationInfoDto();
                        break;
                    case "badges":
                        EditModel.Badges = Vehicle.Badges;
                        EditModel.Highlight = Vehicle.Highlight;
                        break;
                    case "testdrive":
                        EditModel.TestDrive = Vehicle.TestDrive ?? new TestDriveInfoDto();
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
                    case "shorts":
                        EditModel.Shorts = Vehicle.Shorts?.Select(shortVideo => new VideoDto
                        {
                            FileUrl = shortVideo.FileUrl,
                            ThumbnailUrl = shortVideo.ThumbnailUrl,
                            Duration = shortVideo.Duration
                        }).ToList() ?? new List<VideoDto>();
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

                var updatedVehicle = await FeaturedVehicleService.UpdateAsync(VehicleId, EditModel);

                if (updatedVehicle != null)
                {
                    Vehicle = updatedVehicle;
                    MapVehicleToEditModel();
                    ChangedSections.Clear();

                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "Featured vehicle updated successfully!");

                    Navigation.NavigateTo($"/featured-vehicles/{VehicleId}");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error",
                        "Failed to update featured vehicle.");
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
                var partialUpdate = new FeaturedVehicleRequestDto();

                switch (section)
                {
                    case "images":
                        partialUpdate.Images = EditModel.Images;
                        break;
                    case "videos":
                        partialUpdate.Videos = EditModel.Videos;
                        break;
                    case "shorts":
                        partialUpdate.Shorts = EditModel.Shorts;
                        break;
                    case "basic":
                        partialUpdate.BrandName = EditModel.BrandName;
                        partialUpdate.ModelName = EditModel.ModelName;
                        partialUpdate.VehicleType = EditModel.VehicleType;
                        partialUpdate.BodyType = EditModel.BodyType;
                        partialUpdate.ModelSlug = EditModel.ModelSlug;
                        partialUpdate.Slug = EditModel.Slug;
                        partialUpdate.Title = EditModel.Title;
                        partialUpdate.Descriptions = EditModel.Descriptions;
                        partialUpdate.MetaTitle = EditModel.MetaTitle;
                        partialUpdate.MetaDescription = EditModel.MetaDescription;
                        break;
                    case "pricing":
                        partialUpdate.Price = EditModel.Price;
                        partialUpdate.PriceRangeFrom = EditModel.PriceRangeFrom;
                        partialUpdate.PriceRangeTo = EditModel.PriceRangeTo;
                        break;
                    case "specs":
                        partialUpdate.KeySpecifications = EditModel.KeySpecifications;
                        break;
                    case "features":
                        partialUpdate.TopFeatures = EditModel.TopFeatures;
                        partialUpdate.StandOutFeatures = EditModel.StandOutFeatures;
                        break;
                    case "featured":
                        partialUpdate.Priority = EditModel.Priority;
                        partialUpdate.IsActive = EditModel.IsActive;
                        partialUpdate.StartDate = EditModel.StartDate;
                        partialUpdate.EndDate = EditModel.EndDate;
                        break;
                    case "proscons":
                        partialUpdate.Pros = EditModel.Pros;
                        partialUpdate.Cons = EditModel.Cons;
                        break;
                    case "seller":
                        partialUpdate.Seller = EditModel.Seller;
                        partialUpdate.Location = EditModel.Location;
                        break;
                    case "badges":
                        partialUpdate.Badges = EditModel.Badges;
                        partialUpdate.Highlight = EditModel.Highlight;
                        break;
                    case "testdrive":
                        partialUpdate.TestDrive = EditModel.TestDrive;
                        break;
                }

                var updatedVehicle = await FeaturedVehicleService.UpdateAsync(VehicleId, partialUpdate);

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

        // Shorts Management Methods
        protected void CancelShort()
        {
            NewShort = new VideoDto();
            IsAddingShort = false;
            StateHasChanged();
        }

        protected async Task AddShort()
        {
            if (string.IsNullOrWhiteSpace(NewShort.FileUrl))
                return;

            EditModel.Shorts.Add(new VideoDto
            {
                FileUrl = NewShort.FileUrl,
                ThumbnailUrl = NewShort.ThumbnailUrl,
                Duration = NewShort.Duration
            });

            NewShort = new VideoDto();
            IsAddingShort = false;

            try
            {
                await SaveSectionToDatabase("shorts");
                SyncVehicleFromEditModel("shorts");
                ChangedSections.Remove(ShortsSection);

                NotificationService.Notify(NotificationSeverity.Success, "Short Added",
                    "Short saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving short to database");
                ChangedSections.Add(ShortsSection);
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Short added but not saved to database. Please save manually.");
            }

            StateHasChanged();
        }

        protected async Task RemoveShort(int index)
        {
            if (index < 0 || index >= EditModel.Shorts.Count)
                return;

            EditModel.Shorts.RemoveAt(index);

            try
            {
                await SaveSectionToDatabase("shorts");
                SyncVehicleFromEditModel("shorts");
                ChangedSections.Remove(ShortsSection);

                NotificationService.Notify(NotificationSeverity.Success, "Short Removed",
                    "Short removed and saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving short removal to database");
                ChangedSections.Add(ShortsSection);
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Short removed but not saved to database. Please save manually.");
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

        protected void PlayShort(string url, string title)
        {
            SelectedShortUrl = url;
            VideoTitle = title;
            ShowShortModal = true;
            StateHasChanged();
        }

        protected void CloseVideoModal()
        {
            ShowVideoModal = false;
            SelectedVideoUrl = string.Empty;
            StateHasChanged();
        }

        protected void CloseShortModal()
        {
            ShowShortModal = false;
            SelectedShortUrl = string.Empty;
            StateHasChanged();
        }

        protected string GetYouTubeEmbedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            var videoId = ExtractYouTubeVideoId(url);
            if (string.IsNullOrEmpty(videoId)) return string.Empty;

            // Common parameters to improve playback and remove permissions errors
            var parameters = new List<string>
            {
                "autoplay=1",
                "rel=0",           // Don't show related videos
                "modestbranding=1", // Minimal YouTube branding
                "controls=1",       // Show controls
                "showinfo=0",       // Don't show video info
                "iv_load_policy=3", // Don't show annotations
                "enablejsapi=0",    // Disable JS API to prevent permission errors
                "origin=" + Uri.EscapeDataString(Navigation.BaseUri), // Set origin for security
                "widget_referrer=" + Uri.EscapeDataString(Navigation.BaseUri) // Set referrer
            };

            // Check if it's a short
            if (url.Contains("/shorts/"))
            {
                return $"https://www.youtube.com/embed/{videoId}?{string.Join("&", parameters)}";
            }

            return $"https://www.youtube.com/embed/{videoId}?{string.Join("&", parameters)}";
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

        protected void OnVideoUrlChanged(string url)
        {
            NewVideo.FileUrl = url;
            NewVideo.ThumbnailUrl = ExtractYouTubeThumbnail(url);
            StateHasChanged();
        }

        protected void OnShortUrlChanged(string url)
        {
            NewShort.FileUrl = url;
            NewShort.ThumbnailUrl = ExtractYouTubeThumbnail(url);
            StateHasChanged();
        }

        // Helper Methods
        protected string FormatPrice(decimal price)
        {
            if (price >= 10_000_000) return $"₹{price / 10_000_000:F2} Cr";
            if (price >= 100_000) return $"₹{price / 100_000:F2} L";
            return $"₹{price:N0}";
        }

        protected string GetPriorityColor(int priority)
        {
            if (priority >= 8) return "#dc3545";
            if (priority >= 5) return "#fd7e14";
            if (priority >= 3) return "#ffc107";
            return "#6c757d";
        }

        protected void GoBack()
        {
            if (HasChanges)
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Unsaved Changes",
                    "You have unsaved changes. Please save before leaving.");
            }
            else
            {
                Navigation.NavigateTo($"/featured-vehicles");
            }
        }

        // Feature management methods
        protected void AddTopFeature()
        {
            EditModel.TopFeatures.Add(new FeatureItemDto { Feature = string.Empty });
            if (!ChangedSections.Contains(FeaturesSection)) ChangedSections.Add(FeaturesSection);
            StateHasChanged();
        }

        protected void RemoveTopFeature(int index)
        {
            if (index >= 0 && index < EditModel.TopFeatures.Count)
            {
                EditModel.TopFeatures.RemoveAt(index);
                if (!ChangedSections.Contains(FeaturesSection)) ChangedSections.Add(FeaturesSection);
                StateHasChanged();
            }
        }

        protected void AddStandOutFeature()
        {
            EditModel.StandOutFeatures.Add(new FeatureItemDto { Feature = string.Empty });
            if (!ChangedSections.Contains(FeaturesSection)) ChangedSections.Add(FeaturesSection);
            StateHasChanged();
        }

        protected void RemoveStandOutFeature(int index)
        {
            if (index >= 0 && index < EditModel.StandOutFeatures.Count)
            {
                EditModel.StandOutFeatures.RemoveAt(index);
                if (!ChangedSections.Contains(FeaturesSection)) ChangedSections.Add(FeaturesSection);
                StateHasChanged();
            }
        }

        protected void AddPro()
        {
            EditModel.Pros.Add(new ProConItemDto { Pro = string.Empty });
            if (!ChangedSections.Contains(ProsConsSection)) ChangedSections.Add(ProsConsSection);
            StateHasChanged();
        }

        protected void RemovePro(int index)
        {
            if (index >= 0 && index < EditModel.Pros.Count)
            {
                EditModel.Pros.RemoveAt(index);
                if (!ChangedSections.Contains(ProsConsSection)) ChangedSections.Add(ProsConsSection);
                StateHasChanged();
            }
        }

        protected void AddCon()
        {
            EditModel.Cons.Add(new ProConItemDto { Con = string.Empty });
            if (!ChangedSections.Contains(ProsConsSection)) ChangedSections.Add(ProsConsSection);
            StateHasChanged();
        }

        protected void RemoveCon(int index)
        {
            if (index >= 0 && index < EditModel.Cons.Count)
            {
                EditModel.Cons.RemoveAt(index);
                if (!ChangedSections.Contains(ProsConsSection)) ChangedSections.Add(ProsConsSection);
                StateHasChanged();
            }
        }
    }
}