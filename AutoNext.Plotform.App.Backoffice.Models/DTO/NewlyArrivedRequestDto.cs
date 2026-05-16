namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class NewlyArrivedRequestDto
    {
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public string ArrivalPeriod { get; set; } = "weekly";
        public EmiDto? Emi { get; set; }
        public List<ImageDto>? Images { get; set; }
        public List<VideoDto>? Videos { get; set; }
        public List<VariantDetailDto>? Variants { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public string PageTitle { get; set; } = string.Empty;
        public string DescriptionText { get; set; } = string.Empty;
    }

    public class EmiDto
    {
        public int Emi { get; set; }
        public int Months { get; set; }
        public double InterestRate { get; set; }
        public string DisplayValue { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LoanUrl { get; set; } = string.Empty;
        public string CarVariantId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string DownPayment { get; set; } = string.Empty;
        public string VariantSlug { get; set; } = string.Empty;
        public bool ApplicableForloEMI { get; set; } = false;
        public string Title { get; set; } = string.Empty;
        public bool ShowEmiPopup { get; set; } = true;
        public bool DefaultCity { get; set; } = false;
        public string DisplayText { get; set; } = string.Empty;
        public string EmailIdRequired { get; set; } = "0";
        public string SubTitle { get; set; } = string.Empty;
        public string Img { get; set; } = string.Empty;
        public string EmiText { get; set; } = string.Empty;
        public int ModelId { get; set; }
    }

    public class ImageDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }

    public class NewlyArrivedResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelSlug { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;
        public string PriceRange { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public DateTime ArrivalDate { get; set; }
        public string ArrivalPeriod { get; set; } = string.Empty;
        public bool Featured { get; set; }
        public EmiDto? Emi { get; set; }
        public List<ImageDto>? Images { get; set; }
        public string? ThumbnailImage { get; set; }
        public List<VideoDto>? Videos { get; set; }
        public List<VariantDetailDto>? Variants { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public string PageTitle { get; set; } = string.Empty;
        public string DescriptionText { get; set; } = string.Empty;
    }

    public class VideoDto
    {
        public string FileUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
    }

    public class VariantDetailDto
    {
        public string VariantId { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string VariantShortName { get; set; } = string.Empty;
        public string VariantSlug { get; set; } = string.Empty;
        public string ExShowRoomPrice { get; set; } = string.Empty;
        public string OnRoadPrice { get; set; } = string.Empty;
        public decimal OnRoadPriceValue { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public string Mileage { get; set; } = string.Empty;
        public string EngineCc { get; set; } = string.Empty;
        public string Emi { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty; // "Base Model", "Top Model"
        public bool IsRecentLaunch { get; set; }
        public bool IsTopSelling { get; set; }
        public PriceBreakupDto? PriceBreakup { get; set; }
    }

    public class PriceBreakupDto
    {
        public string ExShowRoom { get; set; } = string.Empty;
        public string Rto { get; set; } = string.Empty;
        public string Insurance { get; set; } = string.Empty;
        public OthersChargesDto? Others { get; set; }
        public OptionalAccessoriesDto? OptionalAccessories { get; set; }
        public string OnRoadPrice { get; set; } = string.Empty;
        public decimal OnRoadPriceValue { get; set; }
    }

    public class OptionalAccessoriesDto
    {
        public string TotalAccessoriesInRs { get; set; } = string.Empty;
        public decimal TotalAccessories { get; set; }
        public List<ChargeDto> List { get; set; } = new();
    }

    public class OthersChargesDto
    {
        public string TotalOtherChargesInRsFormat { get; set; } = string.Empty;
        public decimal TotalOtherCharges { get; set; }
        public List<ChargeDto> List { get; set; } = new();
    }

    public class ChargeDto
    {
        public string Price { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Key { get; set; } = string.Empty;
    }
}
