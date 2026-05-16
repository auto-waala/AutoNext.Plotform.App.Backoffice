using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    public class FeaturedVehicleEditBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected IFeaturedVehicleService FeaturedVehicleService { get; set; } = default!;
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
        protected string Seller { get; set; } = "seller";
        protected string Features { get; set; } = "features";
        protected string Featured { get; set; } = "featured";
        protected string Specs { get; set; } = "specs";
        protected string Pricing { get; set; } = "pricing";
        protected string Basic { get; set; } = "basic";
        protected string ProsCons { get; set; } = "proscons";
        protected string Badges { get; set; } = "badges";
        protected string TestDrive { get; set; } = "testdrive";
        protected string Images { get; set; } = "images";
        protected string Videos { get; set; } = "videos";
        protected string Shorts { get; set; } = "shorts";

        // Image upload
        protected List<IBrowserFile> SelectedImageFiles { get; set; } = new();
        protected bool IsUploadingImages { get; set; } = false;
        protected string ImageUploadProgress { get; set; } = string.Empty;

        // Video management
        protected VideoDto NewVideo { get; set; } = new();
        protected VideoDto NewShort { get; set; } = new();
        protected bool IsAddingVideo { get; set; } = false;
        protected bool IsAddingShort { get; set; } = false;

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
                    // Initialize EditModel from Vehicle
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
                // Mark section as changed
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

                // Send entire updated object to API
                var updatedVehicle = await FeaturedVehicleService.UpdateAsync(VehicleId, EditModel);

                if (updatedVehicle != null)
                {
                    // Update local vehicle with response
                    Vehicle = updatedVehicle;
                    ChangedSections.Clear();

                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "Featured vehicle updated successfully!");

                    // Navigate back to details page
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

        // Image Management Methods
        protected async Task HandleImageFilesSelected(InputFileChangeEventArgs e)
        {
            SelectedImageFiles.Clear();
            foreach (var file in e.GetMultipleFiles(10)) // Max 10 images
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
                    // Simulate upload - replace with actual API call
                    var imageUrl = await SimulateImageUpload(file);

                    var newImage = new ImageDto
                    {
                        FileId = Guid.NewGuid().ToString(),
                        FileUrl = imageUrl,
                        IsPrimary = !EditModel.Images.Any() // First image becomes primary
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
            // TODO: Replace with actual file upload API call
            await Task.Delay(500); // Simulate upload delay
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

        protected void OnVideoUrlChanged(string url)
        {
            NewVideo.ThumbnailUrl = ExtractYouTubeThumbnail(url);
            StateHasChanged();
        }

        // Shorts Management Methods
        protected void CancelShort()
        {
            NewShort = new VideoDto();
            IsAddingShort = false;
            StateHasChanged();
        }
        protected void AddShort()
        {
            if (!string.IsNullOrWhiteSpace(NewShort.FileUrl))
            {
                EditModel.Shorts.Add(new VideoDto
                {
                    FileUrl = NewShort.FileUrl,
                    ThumbnailUrl = NewShort.ThumbnailUrl,
                    Duration = NewShort.Duration
                });

                NewShort = new VideoDto();
                IsAddingShort = false;

                if (!ChangedSections.Contains(Shorts))
                    ChangedSections.Add(Shorts);

                StateHasChanged();
            }
        }

        protected void RemoveShort(int index)
        {
            if (index >= 0 && index < EditModel.Shorts.Count)
            {
                EditModel.Shorts.RemoveAt(index);
                if (!ChangedSections.Contains(Shorts))
                    ChangedSections.Add(Shorts);
                StateHasChanged();
            }
        }

        protected void OnShortUrlChanged(string url)
        {
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
                Navigation.NavigateTo($"/featured-vehicles/{VehicleId}");
            }
        }

        // Feature management methods
        protected void AddTopFeature()
        {
            EditModel.TopFeatures.Add(new FeatureItemDto { Feature = string.Empty });
            if (!ChangedSections.Contains("features")) ChangedSections.Add("features");
            StateHasChanged();
        }

        protected void RemoveTopFeature(int index)
        {
            if (index >= 0 && index < EditModel.TopFeatures.Count)
            {
                EditModel.TopFeatures.RemoveAt(index);
                if (!ChangedSections.Contains("features")) ChangedSections.Add("features");
                StateHasChanged();
            }
        }

        protected void AddStandOutFeature()
        {
            EditModel.StandOutFeatures.Add(new FeatureItemDto { Feature = string.Empty });
            if (!ChangedSections.Contains("features")) ChangedSections.Add("features");
            StateHasChanged();
        }

        protected void RemoveStandOutFeature(int index)
        {
            if (index >= 0 && index < EditModel.StandOutFeatures.Count)
            {
                EditModel.StandOutFeatures.RemoveAt(index);
                if (!ChangedSections.Contains("features")) ChangedSections.Add("features");
                StateHasChanged();
            }
        }

        protected void AddPro()
        {
            EditModel.Pros.Add(new ProConItemDto { Pro = string.Empty });
            if (!ChangedSections.Contains("proscons")) ChangedSections.Add("proscons");
            StateHasChanged();
        }

        protected void RemovePro(int index)
        {
            if (index >= 0 && index < EditModel.Pros.Count)
            {
                EditModel.Pros.RemoveAt(index);
                if (!ChangedSections.Contains("proscons")) ChangedSections.Add("proscons");
                StateHasChanged();
            }
        }

        protected void AddCon()
        {
            EditModel.Cons.Add(new ProConItemDto { Con = string.Empty });
            if (!ChangedSections.Contains("proscons")) ChangedSections.Add("proscons");
            StateHasChanged();
        }

        protected void RemoveCon(int index)
        {
            if (index >= 0 && index < EditModel.Cons.Count)
            {
                EditModel.Cons.RemoveAt(index);
                if (!ChangedSections.Contains("proscons")) ChangedSections.Add("proscons");
                StateHasChanged();
            }
        }
    }
}