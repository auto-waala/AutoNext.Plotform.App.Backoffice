
namespace AutoNext.Plotform.App.Backoffice.Models.Common
{
    // ----- Dashboard Models -----
    public class VehicleBrief
    {
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string Type { get; set; } = "";
        public int Year { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = "";
        public int AddedDays { get; set; }
        public int Priority { get; set; } = 0;
        public int Views { get; set; }
        public int Likes { get; set; }
        public bool IsActive { get; set; } = true;
        public string Engine { get; set; } = "";
        public string Transmission { get; set; } = "";
        public double Rating { get; set; }
        public string Mileage { get; set; } = "0";
        public int DaysLeft { get; set; }
        public string EndDateString { get; set; } = "";
    }
}
