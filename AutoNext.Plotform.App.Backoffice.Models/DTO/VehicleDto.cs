
namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class VehicleDto
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public string Id { get; set; } = string.Empty;
        public string? VIN { get; set; }
        public string? ChassisNumber { get; set; }
        public string? EngineNumber { get; set; }

        // ── Listing info ──────────────────────────────────────────────────────
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Locality { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
        public List<string> Benefits { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Views { get; set; }

        // ── Flat fields (backward compat) ─────────────────────────────────────
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Kilometers { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;

        // ── Structured price ──────────────────────────────────────────────────
        public VehiclePriceDto Price { get; set; } = new();

        // ── Specifications ────────────────────────────────────────────────────
        public VehicleSpecificationsDto? Specifications { get; set; }

        // ── Seller ────────────────────────────────────────────────────────────
        public SellerInfoDto? Seller { get; set; }

        // ── Legacy flat seller fields (backward compat) ───────────────────────
        public string SellerId { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;

        // ── Inspection ────────────────────────────────────────────────────────
        public InspectionReportDto? Inspection { get; set; }

        // ── Audit ─────────────────────────────────────────────────────────────
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public DateTime ModifiedOn { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Nested response DTOs
    // ─────────────────────────────────────────────────────────────────────────────

    public class VehiclePriceDto
    {
        public decimal Raw { get; set; }
        public string Display { get; set; } = string.Empty;  // "₹8.50 Lakh"
        public string Currency { get; set; } = "INR";
    }

    public class VehicleSpecificationsDto
    {
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public int Year { get; set; }
        public int? EngineCC { get; set; }
        public double? MileageKmpl { get; set; }
        public int? SeatingCapacity { get; set; }
        public int OwnershipCount { get; set; }
    }

    public class SellerInfoDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SellerType { get; set; } = string.Empty; // Dealer / Individual
        public string? DealerId { get; set; }
        public string? StoreId { get; set; }
        public string? Location { get; set; }
        public bool ChatEnabled { get; set; }
        public bool CallEnabled { get; set; }
        public bool IsVerified { get; set; }
    }

    public class InspectionReportDto
    {
        public string ReportUrl { get; set; } = string.Empty;
        public int InspectionPoints { get; set; }
        public DateTime InspectedOn { get; set; }
        public bool IsAvailable { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Request DTOs
    // ─────────────────────────────────────────────────────────────────────────────

    public class CreateVehicleRequest
    {
        // ── Listing info ──────────────────────────────────────────────────────
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty; // Car, Bike, Truck, EV, Bicycle
        public string BodyType { get; set; } = string.Empty; // Hatchback, Sedan, SUV...
        public string Color { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Locality { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
        public List<string> Benefits { get; set; } = new();

        // ── Flat vehicle fields ───────────────────────────────────────────────
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal Price { get; set; }
        public int Kilometers { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;

        // ── Identity numbers (optional) ───────────────────────────────────────
        public string? VIN { get; set; }
        public string? ChassisNumber { get; set; }
        public string? EngineNumber { get; set; }

        // ── Specifications ────────────────────────────────────────────────────
        public CreateSpecificationsRequest? Specifications { get; set; }

        // ── Seller ────────────────────────────────────────────────────────────
        public CreateSellerRequest Seller { get; set; } = new();
    }

    public class CreateSpecificationsRequest
    {
        public string Variant { get; set; } = string.Empty;
        public int? EngineCC { get; set; }
        public double? MileageKmpl { get; set; }
        public int? SeatingCapacity { get; set; }
        public int OwnershipCount { get; set; } = 1;
    }

    public class CreateSellerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SellerType { get; set; } = "Individual"; // Dealer / Individual
        public string? DealerId { get; set; }
        public string? StoreId { get; set; }
        public string? Location { get; set; }
        public bool ChatEnabled { get; set; } = true;
        public bool CallEnabled { get; set; } = true;
    }

    // ─────────────────────────────────────────────────────────────────────────────

    public class UpdateVehicleRequest
    {
        // ── Editable listing fields ───────────────────────────────────────────
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? City { get; set; }
        public string? Locality { get; set; }
        public List<string>? Images { get; set; }
        public List<string>? Benefits { get; set; }
        public string? Status { get; set; } // active, sold, expired, draft

        // ── Price ─────────────────────────────────────────────────────────────
        public decimal? Price { get; set; }

        // ── Mileage update (km driven increases over time) ────────────────────
        public int? Kilometers { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    public class VehicleSearchRequest
    {
        // ── Vehicle filters ───────────────────────────────────────────────────
        public string? VehicleType { get; set; }
        public string? BodyType { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public string? FuelType { get; set; }
        public string? Transmission { get; set; }
        public string? Color { get; set; }

        // ── Price range ───────────────────────────────────────────────────────
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // ── Mileage range ─────────────────────────────────────────────────────
        public int? MinKilometers { get; set; }
        public int? MaxKilometers { get; set; }

        // ── Location ──────────────────────────────────────────────────────────
        public string? City { get; set; }
        public string? Locality { get; set; }

        // ── Seller filter ─────────────────────────────────────────────────────
        public string? SellerType { get; set; } // Dealer / Individual

        // ── Sorting ───────────────────────────────────────────────────────────
        public string SortBy { get; set; } = "created_on";  // created_on, price, kilometers, year
        public string SortOrder { get; set; } = "desc";        // asc / desc

        // ── Pagination ────────────────────────────────────────────────────────
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
