namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class UpcomingVehicleRequestDto
    {
        // Basic Information
        public string Title { get; set; } = string.Empty;
        public string Descriptions { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelSlug { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;

        // Upcoming-specific Information
        public DateTime LaunchDate { get; set; }
        public string LaunchPeriod { get; set; } = string.Empty;
        public bool IsFeatured { get; set; }
        public string ExpectedStatus { get; set; } = "Upcoming";

        // Price Information
        public UpcomingVehiclePriceInfoDto Price { get; set; } = new();
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        // Media
        public List<ImageDto> Images { get; set; } = new();
        public List<VideoDto> Videos { get; set; } = new();
        public List<VideoDto> Shorts { get; set; } = new();

        // Specifications
        public List<VariantDetailDto> Variants { get; set; } = new();
        public UpcomingVehicleKeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<UpcomingVehicleFeatureItemDto> TopFeatures { get; set; } = new();
        public List<UpcomingVehicleFeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<UpcomingVehicleProConItemDto> Pros { get; set; } = new();
        public List<UpcomingVehicleProConItemDto> Cons { get; set; } = new();
        public List<UpcomingVehicleTagItemDto> Tags { get; set; } = new();

        // Ratings & Reviews
        public List<UpcomingVehicleUserRatingDto> UserRatings { get; set; } = new();

        // Seller Information
        public SellerInfoDto Seller { get; set; } = new();

        // Location
        public UpcomingVehicleLocationInfoDto Location { get; set; } = new();

        // Vehicle Condition
        public UpcomingVehicleConditionDto Condition { get; set; } = new();

        // Listing Details
        public UpcomingVehicleListingDetailsDto ListingDetails { get; set; } = new();

        // Display Features
        public List<string> Badges { get; set; } = new();
        public string Highlight { get; set; } = string.Empty;

        // Test Drive
        public UpcomingVehicleTestDriveInfoDto TestDrive { get; set; } = new();

        // Status Fields
        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public bool IsLaunched { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }

        // SEO Content
        public string PageTitle { get; set; } = string.Empty;
        public string DescriptionText { get; set; } = string.Empty;
    }

    public class UpcomingVehicleResponseDto
    {
        public string Id { get; set; } = string.Empty;

        // Basic Information
        public string Title { get; set; } = string.Empty;
        public string Descriptions { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelSlug { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;

        // Upcoming-specific Information
        public DateTime LaunchDate { get; set; }
        public string LaunchPeriod { get; set; } = string.Empty;
        public bool IsFeatured { get; set; }
        public string ExpectedStatus { get; set; } = string.Empty;

        // Price Information
        public UpcomingVehiclePriceInfoDto Price { get; set; } = new();
        public string PriceRange { get; set; } = string.Empty;
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        // Media
        public List<ImageDto> Images { get; set; } = new();
        public List<VideoDto> Videos { get; set; } = new();
        public List<VideoDto> Shorts { get; set; } = new();
        public string ThumbnailImage { get; set; } = string.Empty;
        public string? ThumbnailWebp { get; set; }

        // Specifications
        public List<VariantDetailDto> Variants { get; set; } = new();
        public UpcomingVehicleKeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<UpcomingVehicleFeatureItemDto> TopFeatures { get; set; } = new();
        public List<UpcomingVehicleFeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<UpcomingVehicleProConItemDto> Pros { get; set; } = new();
        public List<UpcomingVehicleProConItemDto> Cons { get; set; } = new();
        public List<UpcomingVehicleTagItemDto> Tags { get; set; } = new();

        // Ratings & Reviews
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public List<UpcomingVehicleUserRatingDto> UserRatings { get; set; } = new();

        // Seller Information
        public SellerInfoDto Seller { get; set; } = new();

        // Location
        public UpcomingVehicleLocationInfoDto Location { get; set; } = new();

        // Vehicle Condition
        public UpcomingVehicleConditionDto Condition { get; set; } = new();

        // Listing Details
        public UpcomingVehicleListingDetailsDto ListingDetails { get; set; } = new();

        // Engagement Metrics
        public UpcomingVehicleEngagementMetricsDto Engagement { get; set; } = new();

        // Share URLs
        public UpcomingVehicleShareUrlsDto ShareUrls { get; set; } = new();

        // Display Features
        public List<string> Badges { get; set; } = new();
        public string? Highlight { get; set; }

        // Test Drive
        public UpcomingVehicleTestDriveInfoDto TestDrive { get; set; } = new();

        // Status Fields
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public bool IsLaunched { get; set; }
        public bool IsUpcoming => !IsLaunched && LaunchDate > DateTime.UtcNow;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // SEO Content
        public string PageTitle { get; set; } = string.Empty;
        public string DescriptionText { get; set; } = string.Empty;

        // Audit
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpcomingVehicleListResponseDto
    {
        public List<UpcomingVehicleResponseDto> UpcomingVehicles { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class UpcomingVehicleFilterDto
    {
        public string? BrandName { get; set; }
        public string? ModelName { get; set; }
        public string? VehicleType { get; set; }
        public string? BodyType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public List<string>? Badges { get; set; }
        public double? MinRating { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public bool? IsActive { get; set; } = true;
        public DateTime? LaunchDateFrom { get; set; }
        public DateTime? LaunchDateTo { get; set; }
        public bool? IsFeatured { get; set; }
        public string? LaunchPeriod { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "LaunchDate";
        public string SortOrder { get; set; } = "Asc";
    }

    public class UpdateUpcomingVehiclePriorityDto
    {
        public string VehicleId { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    public class BulkUpdateUpcomingVehiclesDto
    {
        public List<string> VehicleIds { get; set; } = new();
        public bool IsActive { get; set; }
        public int? Priority { get; set; }
        public DateTime? LaunchDate { get; set; }
        public bool? IsFeatured { get; set; }
    }

    public class UpcomingVehicleSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ThumbnailImage { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string FormattedPrice { get; set; } = string.Empty;
        public List<string> Badges { get; set; } = new();
        public double Rating { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime LaunchDate { get; set; }
        public string LaunchPeriod { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ActivateUpcomingVehicleRequest
    {
        public DateTime? LaunchDate { get; set; }
    }


    // DTO Supporting Classes

    public class UpcomingVehiclePriceInfoDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public bool Negotiable { get; set; } = true;
        public decimal OnRoadPrice { get; set; }

        public string FormattedPrice => $"{Currency} {Amount:N0}";
        public string FormattedOnRoadPrice => $"{Currency} {OnRoadPrice:N0}";
    }

    public class UpcomingVehicleKeySpecificationsDto
    {
        public string Engine { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public string Mileage { get; set; } = string.Empty;
        public string YearOfManufacture { get; set; } = string.Empty;
        public string? MaxPower { get; set; }
        public string? MaxTorque { get; set; }
        public string? SeatingCapacity { get; set; }
    }

    public class UpcomingVehicleFeatureItemDto
    {
        public string Feature { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }

    public class UpcomingVehicleProConItemDto
    {
        public string Pro { get; set; } = string.Empty;
        public string Con { get; set; } = string.Empty;
    }

    public class UpcomingVehicleTagItemDto
    {
        public string TagName { get; set; } = string.Empty;
    }

    public class UpcomingVehicleUserRatingDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UpcomingVehicleLocationInfoDto
    {
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;

        public string FullAddress => $"{City}, {State} - {Pincode}";
    }

    public class UpcomingVehicleConditionDto
    {
        public bool IsNew { get; set; } = true;
        public int OwnerCount { get; set; }
        public int KMDriven { get; set; }
        public bool Accidental { get; set; }
        public bool ServiceHistoryAvailable { get; set; }

        public string ConditionStatus => IsNew ? "New" : $"{OwnerCount} Owner(s)";
    }

    public class UpcomingVehicleListingDetailsDto
    {
        public bool IsAvailable { get; set; } = true;
        public bool IsPremium { get; set; }
        public bool IsSold { get; set; }
        public DateTime PostedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsVerified { get; set; }
        public string VerifiedBy { get; set; } = string.Empty;
        public DateTime? VerificationDate { get; set; }

        public int DaysListed => (DateTime.UtcNow - PostedDate).Days;
    }

    public class UpcomingVehicleEngagementMetricsDto
    {
        public long Views { get; set; }
        public long Likes { get; set; }
        public long Shares { get; set; }
        public long Enquiries { get; set; }

        public double EngagementRate => Views > 0 ? (double)(Likes + Shares + Enquiries) / Views * 100 : 0;
    }

    public class UpcomingVehicleShareUrlsDto
    {
        public string Facebook { get; set; } = string.Empty;
        public string Twitter { get; set; } = string.Empty;
        public string WhatsApp { get; set; } = string.Empty;
        public string LinkedIn { get; set; } = string.Empty;
    }

    public class UpcomingVehicleTestDriveInfoDto
    {
        public bool Available { get; set; }
        public decimal BookingAmount { get; set; }
        public string? BookingUrl { get; set; }
    }
}
