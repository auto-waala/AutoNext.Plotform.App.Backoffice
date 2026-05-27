using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Blob;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using AutoNext.Plotform.App.Backoffice.Models.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Radzen;
using System.IO;
using System.Text.RegularExpressions;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages.Listings
{
    [Authorize]
    public class PremiumVehicleDetailsBase : ComponentBase
    {
        [Parameter] public string VehicleId { get; set; } = string.Empty;

        [Inject] protected IPremiumVehicleService PremiumVehicleService { get; set; } = default!;
        [Inject] protected IBlobService BlobService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected ILogger<PremiumVehicleDetailsBase> Logger { get; set; } = default!;
        [Inject] protected NotificationService NotificationService { get; set; } = default!;
        [Inject] protected LoaderService LoaderService { get; set; } = default!;

        protected PremiumVehicleResponseDto? Vehicle { get; set; }
        protected PremiumVehicleRequestDto EditModel { get; set; } = new();
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
        protected string PremiumSection { get; set; } = "premium";
        protected string VideosSection { get; set; } = "videos";
        protected string ShortsSection { get; set; } = "shorts";
        protected string ProsConsSection { get; set; } = "proscons";
        protected string SellerSection { get; set; } = "seller";
        protected string BadgesSection { get; set; } = "badges";
        protected string TestDriveSection { get; set; } = "testdrive";
        protected string ConditionSection { get; set; } = "condition";
        protected string ListingSection { get; set; } = "listing";
        protected string TagsSection { get; set; } = "tags";
        protected string VariantsSection { get; set; } = "variants";
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

        // Tag management
        protected string NewTagName { get; set; } = string.Empty;
        protected bool IsAddingTag { get; set; } = false;

        // Variant management
        protected VariantDetailDto NewVariant { get; set; } = new();
        protected bool IsAddingVariant { get; set; } = false;

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
                LoaderService.Show();
                StateHasChanged();

                Vehicle = await PremiumVehicleService.GetByIdAsync(VehicleId);

                if (Vehicle != null)
                {
                    MapVehicleToEditModel();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading premium vehicle for editing: {VehicleId}", VehicleId);
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load vehicle details.");
            }
            finally
            {
                IsLoading = false;
                LoaderService.Hide();
                StateHasChanged();
            }
        }

        private void MapVehicleToEditModel()
        {
            if (Vehicle == null) return;

            EditModel = new PremiumVehicleRequestDto
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
                Price = Vehicle.Price ?? new PremiumVehiclePriceInfoDto(),
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
                Variants = Vehicle.Variants?.Select(v => new VariantDetailDto
                {
                    FuelType = v.FuelType,
                    Transmission = v.Transmission,
                    Mileage = v.Mileage,
                    VariantName = v.VariantName
                }).ToList() ?? new List<VariantDetailDto>(),
                KeySpecifications = Vehicle.KeySpecifications ?? new PremiumVehicleKeySpecificationsDto(),
                TopFeatures = Vehicle.TopFeatures?.Select(f => new PremiumVehicleFeatureItemDto
                {
                    Feature = f.Feature,
                    Icon = f.Icon
                }).ToList() ?? new List<PremiumVehicleFeatureItemDto>(),
                StandOutFeatures = Vehicle.StandOutFeatures?.Select(f => new PremiumVehicleFeatureItemDto
                {
                    Feature = f.Feature,
                    Icon = f.Icon
                }).ToList() ?? new List<PremiumVehicleFeatureItemDto>(),
                Pros = Vehicle.Pros?.Select(p => new PremiumVehicleProConItemDto
                {
                    Pro = p.Pro
                }).ToList() ?? new List<PremiumVehicleProConItemDto>(),
                Cons = Vehicle.Cons?.Select(c => new PremiumVehicleProConItemDto
                {
                    Con = c.Con
                }).ToList() ?? new List<PremiumVehicleProConItemDto>(),
                Tags = Vehicle.Tags?.Select(t => new PremiumVehicleTagItemDto
                {
                    TagName = t.TagName
                }).ToList() ?? new List<PremiumVehicleTagItemDto>(),
                Seller = Vehicle.Seller ?? new SellerInfoDto(),
                Location = Vehicle.Location ?? new PremiumVehicleLocationInfoDto(),
                Condition = Vehicle.Condition ?? new PremiumVehicleConditionDto(),
                ListingDetails = Vehicle.ListingDetails ?? new PremiumVehicleListingDetailsDto(),
                Badges = Vehicle.Badges,
                Highlight = Vehicle.Highlight,
                TestDrive = Vehicle.TestDrive ?? new PremiumVehicleTestDriveInfoDto(),
                Priority = Vehicle.Priority,
                IsActive = Vehicle.IsActive,
                StartDate = Vehicle.StartDate,
                EndDate = Vehicle.EndDate
            };
        }

        private void CopyAllDataToPartialUpdate(PremiumVehicleRequestDto target)
        {
            if (target == null) return;

            // Basic Information
            target.Title = EditModel.Title;
            target.Descriptions = EditModel.Descriptions;
            target.Slug = EditModel.Slug;
            target.MetaTitle = EditModel.MetaTitle;
            target.MetaDescription = EditModel.MetaDescription;
            target.BrandName = EditModel.BrandName;
            target.ModelName = EditModel.ModelName;
            target.ModelSlug = EditModel.ModelSlug;
            target.VehicleType = EditModel.VehicleType;
            target.BodyType = EditModel.BodyType;

            // Pricing
            target.Price = EditModel.Price != null ? new PremiumVehiclePriceInfoDto
            {
                Amount = EditModel.Price.Amount,
                Currency = EditModel.Price.Currency,
                Negotiable = EditModel.Price.Negotiable,
                OnRoadPrice = EditModel.Price.OnRoadPrice,
            } : new PremiumVehiclePriceInfoDto();

            target.PriceRangeFrom = EditModel.PriceRangeFrom;
            target.PriceRangeTo = EditModel.PriceRangeTo;

            // Media
            target.Images = EditModel.Images?.Select(img => new ImageDto
            {
                FileId = img.FileId,
                FileUrl = img.FileUrl,
                IsPrimary = img.IsPrimary
            }).ToList() ?? new List<ImageDto>();

            target.Videos = EditModel.Videos?.Select(vid => new VideoDto
            {
                FileUrl = vid.FileUrl,
                ThumbnailUrl = vid.ThumbnailUrl,
                Duration = vid.Duration
            }).ToList() ?? new List<VideoDto>();

            target.Shorts = EditModel.Shorts?.Select(shortVideo => new VideoDto
            {
                FileUrl = shortVideo.FileUrl,
                ThumbnailUrl = shortVideo.ThumbnailUrl,
                Duration = shortVideo.Duration
            }).ToList() ?? new List<VideoDto>();

            // Variants
            target.Variants = EditModel.Variants?.Select(v => new VariantDetailDto
            {
                VariantName = v.VariantName,
                FuelType = v.FuelType,
                Transmission = v.Transmission,
                Mileage = v.Mileage,
                EngineCc = v.EngineCc,
                Emi = v.Emi,
                ExShowRoomPrice = v.ExShowRoomPrice,
                OnRoadPrice = v.OnRoadPrice,
                OnRoadPriceValue = v.OnRoadPriceValue,
                Tag = v.Tag,
                VariantId = v.VariantId,
                VariantShortName = v.VariantShortName,
                VariantSlug = v.VariantSlug,
                IsRecentLaunch = v.IsRecentLaunch,
                IsTopSelling = v.IsTopSelling,
                PriceBreakup = v.PriceBreakup
            }).ToList() ?? new List<VariantDetailDto>();

            // Specifications
            target.KeySpecifications = EditModel.KeySpecifications != null ? new PremiumVehicleKeySpecificationsDto
            {
                Engine = EditModel.KeySpecifications.Engine,
                Transmission = EditModel.KeySpecifications.Transmission,
                FuelType = EditModel.KeySpecifications.FuelType,
                Mileage = EditModel.KeySpecifications.Mileage,
                YearOfManufacture = EditModel.KeySpecifications.YearOfManufacture,
                MaxPower = EditModel.KeySpecifications.MaxPower,
                MaxTorque = EditModel.KeySpecifications.MaxTorque,
                SeatingCapacity = EditModel.KeySpecifications.SeatingCapacity,
            } : new PremiumVehicleKeySpecificationsDto();

            // Features
            target.TopFeatures = EditModel.TopFeatures?.Select(f => new PremiumVehicleFeatureItemDto
            {
                Feature = f.Feature,
                Icon = f.Icon
            }).ToList() ?? new List<PremiumVehicleFeatureItemDto>();

            target.StandOutFeatures = EditModel.StandOutFeatures?.Select(f => new PremiumVehicleFeatureItemDto
            {
                Feature = f.Feature,
                Icon = f.Icon
            }).ToList() ?? new List<PremiumVehicleFeatureItemDto>();

            // Pros & Cons
            target.Pros = EditModel.Pros?.Select(p => new PremiumVehicleProConItemDto
            {
                Pro = p.Pro
            }).ToList() ?? new List<PremiumVehicleProConItemDto>();

            target.Cons = EditModel.Cons?.Select(c => new PremiumVehicleProConItemDto
            {
                Con = c.Con
            }).ToList() ?? new List<PremiumVehicleProConItemDto>();

            // Tags
            target.Tags = EditModel.Tags?.Select(t => new PremiumVehicleTagItemDto
            {
                TagName = t.TagName
            }).ToList() ?? new List<PremiumVehicleTagItemDto>();

            // Seller & Location
            target.Seller = EditModel.Seller != null ? new SellerInfoDto
            {
                UserId = EditModel.Seller.UserId,
                Name = EditModel.Seller.Name,
                Phone = EditModel.Seller.Phone,
                Email = EditModel.Seller.Email,
                SellerType = EditModel.Seller.SellerType,
                DealerId = EditModel.Seller.DealerId,
                StoreId = EditModel.Seller.StoreId,
                ChatEnabled = EditModel.Seller.ChatEnabled,
                CallEnabled = EditModel.Seller.CallEnabled,
                IsVerified = EditModel.Seller.IsVerified,
                Location = EditModel.Seller.Location
            } : new SellerInfoDto();

            target.Location = EditModel.Location != null ? new PremiumVehicleLocationInfoDto
            {
                City = EditModel.Location.City,
                State = EditModel.Location.State,
                Pincode = EditModel.Location.Pincode,
                Latitude = EditModel.Location.Latitude,
                Longitude = EditModel.Location.Longitude,
            } : new PremiumVehicleLocationInfoDto();

            // Condition
            target.Condition = EditModel.Condition != null ? new PremiumVehicleConditionDto
            {
                IsNew = EditModel.Condition.IsNew,
                OwnerCount = EditModel.Condition.OwnerCount,
                KMDriven = EditModel.Condition.KMDriven,
                Accidental = EditModel.Condition.Accidental,
                ServiceHistoryAvailable = EditModel.Condition.ServiceHistoryAvailable,
            } : new PremiumVehicleConditionDto();

            // Listing Details
            target.ListingDetails = EditModel.ListingDetails != null ? new PremiumVehicleListingDetailsDto
            {
                IsAvailable = EditModel.ListingDetails.IsAvailable,
                IsSold = EditModel.ListingDetails.IsSold,
                PostedDate = EditModel.ListingDetails.PostedDate,
                ExpiryDate = EditModel.ListingDetails.ExpiryDate,
                IsVerified = EditModel.ListingDetails.IsVerified,
                VerifiedBy = EditModel.ListingDetails.VerifiedBy,
                VerificationDate = EditModel.ListingDetails.VerificationDate,
            } : new PremiumVehicleListingDetailsDto();

            // Badges & Highlight
            target.Badges = EditModel.Badges?.ToList() ?? new List<string>();
            target.Highlight = EditModel.Highlight;

            // Test Drive
            target.TestDrive = EditModel.TestDrive != null ? new PremiumVehicleTestDriveInfoDto
            {
                Available = EditModel.TestDrive.Available,
                BookingAmount = EditModel.TestDrive.BookingAmount,
                BookingUrl = EditModel.TestDrive.BookingUrl,
            } : new PremiumVehicleTestDriveInfoDto();

            // Premium Settings
            target.Priority = EditModel.Priority;
            target.IsActive = EditModel.IsActive;
            target.StartDate = EditModel.StartDate;
            target.EndDate = EditModel.EndDate;
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
                case "premium":
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
                case "condition":
                    Vehicle.Condition = EditModel.Condition;
                    break;
                case "listing":
                    Vehicle.ListingDetails = EditModel.ListingDetails;
                    break;
                case "tags":
                    Vehicle.Tags = EditModel.Tags;
                    break;
                case "variants":
                    Vehicle.Variants = EditModel.Variants;
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
                        EditModel.Price = Vehicle.Price ?? new PremiumVehiclePriceInfoDto();
                        EditModel.PriceRangeFrom = Vehicle.PriceRangeFrom;
                        EditModel.PriceRangeTo = Vehicle.PriceRangeTo;
                        break;
                    case "specs":
                        EditModel.KeySpecifications = Vehicle.KeySpecifications ?? new PremiumVehicleKeySpecificationsDto();
                        break;
                    case "features":
                        EditModel.TopFeatures = Vehicle.TopFeatures;
                        EditModel.StandOutFeatures = Vehicle.StandOutFeatures;
                        break;
                    case "premium":
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
                        EditModel.Location = Vehicle.Location ?? new PremiumVehicleLocationInfoDto();
                        break;
                    case "badges":
                        EditModel.Badges = Vehicle.Badges;
                        EditModel.Highlight = Vehicle.Highlight;
                        break;
                    case "testdrive":
                        EditModel.TestDrive = Vehicle.TestDrive ?? new PremiumVehicleTestDriveInfoDto();
                        break;
                    case "condition":
                        EditModel.Condition = Vehicle.Condition ?? new PremiumVehicleConditionDto();
                        break;
                    case "listing":
                        EditModel.ListingDetails = Vehicle.ListingDetails ?? new PremiumVehicleListingDetailsDto();
                        break;
                    case "tags":
                        EditModel.Tags = Vehicle.Tags;
                        break;
                    case "variants":
                        EditModel.Variants = Vehicle.Variants;
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
                LoaderService.Show();
                StateHasChanged();

                await SaveSectionToDatabase(section);
                SyncVehicleFromEditModel(section);

                EditingSections.Remove(section);
                ChangedSections.Remove(section);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Saved",
                    $"{section} section saved successfully.");
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
                Logger.LogError(ex, "Error saving section: {Section}", section);
                NotificationService.Notify(NotificationSeverity.Error, "Error",
                    $"Failed to save {section} section: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
            }
        }

        protected async Task SaveSectionToDatabase(string section)
        {
            try
            {
                // Create a complete DTO with all current data
                var completeUpdate = new PremiumVehicleRequestDto();
                CopyAllDataToPartialUpdate(completeUpdate);

                // Send the complete DTO to the API
                var updatedVehicle = await PremiumVehicleService.UpdateAsync(VehicleId, completeUpdate);

                if (updatedVehicle != null)
                {
                    Vehicle = updatedVehicle;

                    // Refresh the EditModel with the updated data from server
                    MapVehicleToEditModel();

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
                LoaderService.Show();
                StateHasChanged();

                // Create complete DTO for full update
                var completeUpdate = new PremiumVehicleRequestDto();
                CopyAllDataToPartialUpdate(completeUpdate);

                var updatedVehicle = await PremiumVehicleService.UpdateAsync(VehicleId, completeUpdate);

                if (updatedVehicle != null)
                {
                    Vehicle = updatedVehicle;
                    MapVehicleToEditModel();
                    ChangedSections.Clear();

                    LoaderService.Hide();
                    NotificationService.Notify(NotificationSeverity.Success, "Success",
                        "Premium vehicle updated successfully!");

                    Navigation.NavigateTo($"/premium-vehicles/{VehicleId}");
                }
                else
                {
                    LoaderService.Hide();
                    NotificationService.Notify(NotificationSeverity.Error, "Error",
                        "Failed to update premium vehicle.");
                }
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
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
                LoaderService.Show();
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

                    LoaderService.Hide();
                    NotificationService.Notify(NotificationSeverity.Success, "Upload Complete",
                        $"{EditModel.Images.Count} image(s) uploaded and saved successfully.");
                }
                catch (Exception dbEx)
                {
                    LoaderService.Hide();
                    Logger.LogError(dbEx, "Error saving images to database");
                    NotificationService.Notify(NotificationSeverity.Error, "Save Failed",
                        "Images uploaded but failed to save to database. Please save manually.");
                    ChangedSections.Add(ImagesSection);
                }
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
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
                LoaderService.Show();

                if (index < 0 || index >= EditModel.Images.Count)
                    return;

                var image = EditModel.Images[index];

                if (!string.IsNullOrWhiteSpace(image.FileId) && Guid.TryParse(image.FileId, out var fileId))
                {
                    var deleted = await BlobService.DeleteAsync(fileId);
                    if (!deleted)
                    {
                        LoaderService.Hide();
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

                    LoaderService.Hide();
                    NotificationService.Notify(NotificationSeverity.Success, "Image Removed",
                        "Image deleted and saved successfully.");
                }
                catch (Exception dbEx)
                {
                    LoaderService.Hide();
                    Logger.LogError(dbEx, "Error saving image removal to database");
                    NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                        "Image removed but changes not saved to database. Please save manually.");
                    ChangedSections.Add(ImagesSection);
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
                Logger.LogError(ex, "Error removing image");
                NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", ex.Message);
            }
        }

        protected async Task SetPrimaryImage(int index)
        {
            if (index < 0 || index >= EditModel.Images.Count) return;

            try
            {
                LoaderService.Show();

                foreach (var img in EditModel.Images)
                    img.IsPrimary = false;

                EditModel.Images[index].IsPrimary = true;

                await SaveSectionToDatabase("images");
                SyncVehicleFromEditModel("images");
                ChangedSections.Remove(ImagesSection);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Primary Image Updated",
                    "Primary image saved successfully.");
            }
            catch (Exception dbEx)
            {
                LoaderService.Hide();
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
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Validation", "Please enter a valid YouTube URL.");
                return;
            }

            try
            {
                LoaderService.Show();

                EditModel.Videos.Add(new VideoDto
                {
                    FileUrl = NewVideo.FileUrl,
                    ThumbnailUrl = NewVideo.ThumbnailUrl,
                    Duration = NewVideo.Duration
                });

                NewVideo = new VideoDto();
                IsAddingVideo = false;

                await SaveSectionToDatabase("videos");
                SyncVehicleFromEditModel("videos");
                ChangedSections.Remove(VideosSection);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Video Added",
                    "Video saved successfully.");
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
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

            try
            {
                LoaderService.Show();

                EditModel.Videos.RemoveAt(index);

                await SaveSectionToDatabase("videos");
                SyncVehicleFromEditModel("videos");
                ChangedSections.Remove(VideosSection);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Video Removed",
                    "Video removed and saved successfully.");
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
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
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Validation", "Please enter a valid YouTube Shorts URL.");
                return;
            }

            try
            {
                LoaderService.Show();

                EditModel.Shorts.Add(new VideoDto
                {
                    FileUrl = NewShort.FileUrl,
                    ThumbnailUrl = NewShort.ThumbnailUrl,
                    Duration = NewShort.Duration
                });

                NewShort = new VideoDto();
                IsAddingShort = false;

                await SaveSectionToDatabase("shorts");
                SyncVehicleFromEditModel("shorts");
                ChangedSections.Remove(ShortsSection);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Short Added",
                    "Short saved successfully.");
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
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

            try
            {
                LoaderService.Show();

                EditModel.Shorts.RemoveAt(index);

                await SaveSectionToDatabase("shorts");
                SyncVehicleFromEditModel("shorts");
                ChangedSections.Remove(ShortsSection);

                LoaderService.Hide();
                NotificationService.Notify(NotificationSeverity.Success, "Short Removed",
                    "Short removed and saved successfully.");
            }
            catch (Exception ex)
            {
                LoaderService.Hide();
                Logger.LogError(ex, "Error saving short removal to database");
                ChangedSections.Add(ShortsSection);
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Short removed but not saved to database. Please save manually.");
            }

            StateHasChanged();
        }

        // Tag Management Methods
        protected void CancelAddTag()
        {
            NewTagName = string.Empty;
            IsAddingTag = false;
            StateHasChanged();
        }

        protected void AddTagToList()
        {
            if (string.IsNullOrWhiteSpace(NewTagName))
                return;

            if (!EditModel.Tags.Any(t => t.TagName.Equals(NewTagName, StringComparison.OrdinalIgnoreCase)))
            {
                EditModel.Tags.Add(new PremiumVehicleTagItemDto { TagName = NewTagName.Trim() });
                if (!ChangedSections.Contains(TagsSection)) ChangedSections.Add(TagsSection);
            }

            NewTagName = string.Empty;
            IsAddingTag = false;
            StateHasChanged();
        }

        protected async Task RemoveTag(int index)
        {
            if (index < 0 || index >= EditModel.Tags.Count)
                return;

            EditModel.Tags.RemoveAt(index);
            if (!ChangedSections.Contains(TagsSection)) ChangedSections.Add(TagsSection);

            try
            {
                await SaveSectionToDatabase("tags");
                SyncVehicleFromEditModel("tags");
                ChangedSections.Remove(TagsSection);

                NotificationService.Notify(NotificationSeverity.Success, "Tag Removed",
                    "Tag removed and saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving tag removal to database");
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Tag removed but not saved to database. Please save manually.");
            }

            StateHasChanged();
        }

        // Variant Management Methods
        protected void CancelAddVariant()
        {
            NewVariant = new VariantDetailDto();
            IsAddingVariant = false;
            StateHasChanged();
        }

        protected void AddVariantToList()
        {
            if (string.IsNullOrWhiteSpace(NewVariant.VariantName))
                return;

            EditModel.Variants.Add(new VariantDetailDto
            {
                VariantName = NewVariant.VariantName,
                FuelType = NewVariant.FuelType,
                Transmission = NewVariant.Transmission,
                Mileage = NewVariant.Mileage
            });

            NewVariant = new VariantDetailDto();
            IsAddingVariant = false;
            if (!ChangedSections.Contains(VariantsSection)) ChangedSections.Add(VariantsSection);
            StateHasChanged();
        }

        protected async Task RemoveVariant(int index)
        {
            if (index < 0 || index >= EditModel.Variants.Count)
                return;

            EditModel.Variants.RemoveAt(index);
            if (!ChangedSections.Contains(VariantsSection)) ChangedSections.Add(VariantsSection);

            try
            {
                await SaveSectionToDatabase("variants");
                SyncVehicleFromEditModel("variants");
                ChangedSections.Remove(VariantsSection);

                NotificationService.Notify(NotificationSeverity.Success, "Variant Removed",
                    "Variant removed and saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving variant removal to database");
                NotificationService.Notify(NotificationSeverity.Warning, "Save Failed",
                    "Variant removed but not saved to database. Please save manually.");
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

            var parameters = new List<string>
            {
                "autoplay=1",
                "rel=0",
                "modestbranding=1",
                "controls=1",
                "showinfo=0",
                "iv_load_policy=3",
                "enablejsapi=0",
                "origin=" + Uri.EscapeDataString(Navigation.BaseUri),
                "widget_referrer=" + Uri.EscapeDataString(Navigation.BaseUri)
            };

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

        protected string FormatNumber(long number)
        {
            if (number >= 1_000_000) return $"{number / 1_000_000:F1}M";
            if (number >= 1_000) return $"{number / 1_000:F1}K";
            return number.ToString();
        }

        protected string GetPriorityColor(int priority)
        {
            if (priority >= 8) return "#dc3545";
            if (priority >= 5) return "#fd7e14";
            if (priority >= 3) return "#ffc107";
            return "#6c757d";
        }

        protected string GetStatusBadgeClass(bool isActive, bool isExpired)
        {
            if (!isActive) return "bg-secondary";
            if (isExpired) return "bg-danger";
            return "bg-success";
        }

        protected string GetStatusText(bool isActive, bool isExpired)
        {
            if (!isActive) return "Inactive";
            if (isExpired) return "Expired";
            return "Active";
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
                Navigation.NavigateTo("/premium-vehicles");
            }
        }

        // Feature management methods
        protected void AddTopFeature()
        {
            EditModel.TopFeatures.Add(new PremiumVehicleFeatureItemDto { Feature = string.Empty });
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
            EditModel.StandOutFeatures.Add(new PremiumVehicleFeatureItemDto { Feature = string.Empty });
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
            EditModel.Pros.Add(new PremiumVehicleProConItemDto { Pro = string.Empty });
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
            EditModel.Cons.Add(new PremiumVehicleProConItemDto { Con = string.Empty });
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

        protected void AddBadgeFromInput(string badgesInput)
        {
            if (!string.IsNullOrWhiteSpace(badgesInput))
            {
                var newBadges = badgesInput.Split(',')
                    .Select(b => b.Trim())
                    .Where(b => !string.IsNullOrEmpty(b))
                    .ToList();

                foreach (var badge in newBadges)
                {
                    if (!EditModel.Badges.Contains(badge))
                    {
                        EditModel.Badges.Add(badge);
                    }
                }

                if (!ChangedSections.Contains(BadgesSection)) ChangedSections.Add(BadgesSection);
                StateHasChanged();
            }
        }

        protected void RemoveBadge(int index)
        {
            if (index >= 0 && index < EditModel.Badges.Count)
            {
                EditModel.Badges.RemoveAt(index);
                if (!ChangedSections.Contains(BadgesSection)) ChangedSections.Add(BadgesSection);
                StateHasChanged();
            }
        }
    }
}