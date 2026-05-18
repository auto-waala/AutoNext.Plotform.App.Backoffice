using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Models.Listings
{
    public class PremiumVehicleRequestDto
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

        public PremiumVehiclePriceInfoDto Price { get; set; } = new();
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        public List<ImageDto> Images { get; set; } = new();
        public List<VideoDto> Videos { get; set; } = new();
        public List<VideoDto> Shorts { get; set; } = new();

        public List<VariantDetailDto> Variants { get; set; } = new();
        public PremiumVehicleKeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<PremiumVehicleFeatureItemDto> TopFeatures { get; set; } = new();
        public List<PremiumVehicleFeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<PremiumVehicleProConItemDto> Pros { get; set; } = new();
        public List<PremiumVehicleProConItemDto> Cons { get; set; } = new();
        public List<PremiumVehicleTagItemDto> Tags { get; set; } = new();

        public List<PremiumVehicleUserRatingDto> UserRatings { get; set; } = new();

        public SellerInfoDto Seller { get; set; } = new();
        public PremiumVehicleLocationInfoDto Location { get; set; } = new();
        public PremiumVehicleConditionDto Condition { get; set; } = new();
        public PremiumVehicleListingDetailsDto ListingDetails { get; set; } = new();

        public List<string> Badges { get; set; } = new();
        public string Highlight { get; set; } = string.Empty;
        public PremiumVehicleTestDriveInfoDto TestDrive { get; set; } = new();

        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
    }

    public class PremiumVehicleResponseDto
    {
        public string Id { get; set; } = string.Empty;
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

        public PremiumVehiclePriceInfoDto Price { get; set; } = new();
        public string PriceRange { get; set; } = string.Empty;
        public string PriceRangeFrom { get; set; } = string.Empty;
        public string PriceRangeTo { get; set; } = string.Empty;

        public List<ImageDto> Images { get; set; } = new();
        public List<VideoDto> Videos { get; set; } = new();
        public List<VideoDto> Shorts { get; set; } = new();
        public string ThumbnailImage { get; set; } = string.Empty;
        public string ThumbnailWebp { get; set; } = string.Empty;

        public List<VariantDetailDto> Variants { get; set; } = new();
        public PremiumVehicleKeySpecificationsDto KeySpecifications { get; set; } = new();
        public List<PremiumVehicleFeatureItemDto> TopFeatures { get; set; } = new();
        public List<PremiumVehicleFeatureItemDto> StandOutFeatures { get; set; } = new();
        public List<PremiumVehicleProConItemDto> Pros { get; set; } = new();
        public List<PremiumVehicleProConItemDto> Cons { get; set; } = new();
        public List<PremiumVehicleTagItemDto> Tags { get; set; } = new();

        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public List<PremiumVehicleUserRatingDto> UserRatings { get; set; } = new();

        public SellerInfoDto Seller { get; set; } = new();
        public PremiumVehicleLocationInfoDto Location { get; set; } = new();
        public PremiumVehicleConditionDto Condition { get; set; } = new();
        public PremiumVehicleListingDetailsDto ListingDetails { get; set; } = new();

        public PremiumVehicleEngagementMetricsDto Engagement { get; set; } = new();
        public PremiumVehicleShareUrlsDto ShareUrls { get; set; } = new();

        public List<string> Badges { get; set; } = new();
        public string Highlight { get; set; } = string.Empty;
        public PremiumVehicleTestDriveInfoDto TestDrive { get; set; } = new();

        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public bool IsPremium { get; set; } = true;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsExpired => EndDate.HasValue && EndDate.Value < DateTime.UtcNow;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PremiumVehicleListResponseDto
    {
        public List<PremiumVehicleResponseDto> PremiumVehicles { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class PremiumVehicleFilterDto
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
        public bool? IsAvailable { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "Priority";
        public string SortOrder { get; set; } = "Desc";
    }

    public class UpdatePremiumVehiclePriorityDto
    {
        public string VehicleId { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    public class BulkUpdatePremiumVehiclesDto
    {
        public List<string> VehicleIds { get; set; } = new();
        public bool IsActive { get; set; }
        public int? Priority { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class PremiumVehicleSummaryDto
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
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ActivatePremiumVehicleRequest
    {
        public DateTime? EndDate { get; set; }
    }

    public class PremiumVehiclePriceInfoDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public bool Negotiable { get; set; } = true;
        public decimal OnRoadPrice { get; set; }

        public string FormattedPrice => $"{Currency} {Amount:N0}";
        public string FormattedOnRoadPrice => $"{Currency} {OnRoadPrice:N0}";
    }

    public class PremiumVehicleKeySpecificationsDto
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

    public class PremiumVehicleFeatureItemDto
    {
        public string Feature { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }

    public class PremiumVehicleProConItemDto
    {
        public string Pro { get; set; } = string.Empty;
        public string Con { get; set; } = string.Empty;
    }

    public class PremiumVehicleTagItemDto
    {
        public string TagName { get; set; } = string.Empty;
    }

    public class PremiumVehicleUserRatingDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PremiumVehicleLocationInfoDto
    {
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;

        public string FullAddress => $"{City}, {State} - {Pincode}";
    }

    public class PremiumVehicleConditionDto
    {
        public bool IsNew { get; set; } = true;
        public int OwnerCount { get; set; }
        public int KMDriven { get; set; }
        public bool Accidental { get; set; }
        public bool ServiceHistoryAvailable { get; set; }

        public string ConditionStatus => IsNew ? "New" : $"{OwnerCount} Owner(s)";
    }

    public class PremiumVehicleListingDetailsDto
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

    public class PremiumVehicleEngagementMetricsDto
    {
        public long Views { get; set; }
        public long Likes { get; set; }
        public long Shares { get; set; }
        public long Enquiries { get; set; }

        public double EngagementRate => Views > 0 ? (double)(Likes + Shares + Enquiries) / Views * 100 : 0;
    }

    public class PremiumVehicleShareUrlsDto
    {
        public string Facebook { get; set; } = string.Empty;
        public string Twitter { get; set; } = string.Empty;
        public string WhatsApp { get; set; } = string.Empty;
        public string LinkedIn { get; set; } = string.Empty;
    }

    public class PremiumVehicleTestDriveInfoDto
    {
        public bool Available { get; set; }
        public decimal BookingAmount { get; set; }
        public string? BookingUrl { get; set; }
    }
}
