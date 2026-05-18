
namespace AutoNext.Plotform.App.Backoffice.Models.Common
{
    public class UsedVehiclesSearchCriteria
    {
        public string? BrandName { get; set; }
        public string? ModelName { get; set; }
        public string? VehicleType { get; set; }
        public string? BodyType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public int? MaxKmDriven { get; set; }
        public string? FuelType { get; set; }
        public string? Transmission { get; set; }
        public int? MinOwnerCount { get; set; }
        public int? MaxOwnerCount { get; set; }
        public bool? IsAccidental { get; set; }
        public bool? IsVerified { get; set; }
        public string? SellerType { get; set; }
        public double? MinRating { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
