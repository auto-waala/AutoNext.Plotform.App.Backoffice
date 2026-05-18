namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class UsedVehiclesRequestDto
    {
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

        // Price Information
        public PriceInfoDto Price { get; set; } = new();
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        // Media
        public List<VehicleImageDto> Images { get; set; } = new();
        public List<VehicleMediaDto> Videos { get; set; } = new();
        public List<VehicleMediaDto> Shorts { get; set; } = new();

        // Specifications
        public List<VehicleVariantDto> Variants { get; set; } = new();
        public KeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<FeatureItemDto> TopFeatures { get; set; } = new();
        public List<FeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<ProConItemDto> Pros { get; set; } = new();
        public List<ProConItemDto> Cons { get; set; } = new();
        public List<TagItemDto> Tags { get; set; } = new();

        // Ratings
        public List<UserRatingDto> UserRatings { get; set; } = new();
        public double Rating { get; set; }

        // Seller Information
        public SellerInfoDto Seller { get; set; } = new();

        // Location
        public LocationInfoDto Location { get; set; } = new();

        // Vehicle Condition
        public VehicleConditionDto Condition { get; set; } = new();

        // Listing Details
        public ListingDetailsDto ListingDetails { get; set; } = new();

        // Test Drive
        public TestDriveInfoDto TestDrive { get; set; } = new();

        // Sorting & Visibility
        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
    }

    public class UsedVehiclesResponseDto
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

        // Price Information
        public PriceInfoDto Price { get; set; } = new();
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        // Media
        public List<VehicleImageDto> Images { get; set; } = new();
        public List<VehicleMediaDto> Videos { get; set; } = new();
        public List<VehicleMediaDto> Shorts { get; set; } = new();
        public string Thumbnail { get; set; } = string.Empty;
        public string? ThumbnailWebp { get; set; }

        // Specifications
        public List<VehicleVariantDto> Variants { get; set; } = new();
        public KeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<FeatureItemDto> TopFeatures { get; set; } = new();
        public List<FeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<ProConItemDto> Pros { get; set; } = new();
        public List<ProConItemDto> Cons { get; set; } = new();
        public List<TagItemDto> Tags { get; set; } = new();

        // Ratings & Engagement
        public List<UserRatingDto> UserRatings { get; set; } = new();
        public double Rating { get; set; }
        public EngagementMetricsDto Engagement { get; set; } = new();

        // Seller Information
        public SellerInfoDto Seller { get; set; } = new();

        // Location
        public LocationInfoDto Location { get; set; } = new();

        // Vehicle Condition
        public VehicleConditionDto Condition { get; set; } = new();

        // Listing Details
        public ListingDetailsDto ListingDetails { get; set; } = new();

        // Share URLs
        public ShareUrlsDto ShareUrls { get; set; } = new();

        // Test Drive
        public TestDriveInfoDto TestDrive { get; set; } = new();

        // Sorting & Visibility
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }


    public class VehicleImageDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
    }

    public class VehicleMediaDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
    }

    public class VehicleVariantDto
    {
        public string Color { get; set; } = string.Empty;
        public string Engine { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public string Mileage { get; set; } = string.Empty;
        public string YearOfManufacture { get; set; } = string.Empty;
    }
}
