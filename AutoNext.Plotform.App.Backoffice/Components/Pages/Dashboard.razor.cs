using AutoNext.Plotform.App.Backoffice.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AutoNext.Plotform.App.Backoffice.Components.Pages
{
    [Authorize]
    public class DashboardBase : ComponentBase
    {
        // Dashboard Properties
        protected int TotalVehicles { get; set; }
        protected int ActiveListings { get; set; }
        protected int FeaturedCount { get; set; }
        protected int PremiumCount { get; set; }
        protected int TotalSellers { get; set; }
        protected int UsedVehiclesCount { get; set; }
        protected int NewThisMonth { get; set; }
        protected string AvgPriority { get; set; } = "0";
        protected string CurrentDate { get; set; } = "";
        protected int ActivePercentage => TotalVehicles > 0 ? (ActiveListings * 100 / TotalVehicles) : 0;

        // Section data collections
        protected List<VehicleBrief> NewArrivals = new();
        protected List<VehicleBrief> FeaturedVehicles = new();
        protected List<VehicleBrief> PremiumVehicles = new();
        protected List<VehicleBrief> UsedVehicles = new();
        protected List<VehicleBrief> ActiveVehiclesList = new();
        protected List<SellerInfo> TopSellers = new();

        // Metrics
        protected int BuyerInquiries = 0;
        protected int TestDriveRequests = 0;
        protected int BuyerConversion = 32;
        protected int TotalViews = 0;
        protected int TotalLikes = 0;
        protected int TotalShares = 0;
        protected int TotalLeads = 0;

        // NEW: Audience & Social Media Metrics
        protected int FacebookEngagement = 0;
        protected int InstagramClicks = 0;
        protected int TwitterClicks = 0;
        protected int YoutubeViews = 0;
        protected int FacebookTrend = 12;
        protected int InstagramTrend = 8;
        protected int TwitterTrend = 5;
        protected int YoutubeTrend = 18;

        // Traffic Metrics
        protected int TrafficOrganic = 45;
        protected int TrafficSocial = 32;
        protected int TrafficDirect = 23;
        protected int TotalVisits = 0;
        protected int BounceRate = 38;
        protected double AvgSessionTime = 4.2;

        // Post & Engagement Metrics
        protected int TotalPosts = 0;
        protected int TotalSharesSocial = 0;
        protected int TotalReactions = 0;

        // Trending Topics
        protected List<TrendingTopic> TrendingTopics = new();

        protected override void OnInitialized()
        {
            RefreshAllData();
            CurrentDate = DateTime.Now.ToString("dd MMM yyyy");
        }

        protected void RefreshAllData()
        {
            var random = new Random();

            TotalVehicles = 128;
            ActiveListings = 94;
            FeaturedCount = 12;
            PremiumCount = 18;
            TotalSellers = 32;
            UsedVehiclesCount = 47;
            NewThisMonth = 23;
            AvgPriority = "7.2";

            // Vehicle data (same as before)
            NewArrivals = Enumerable.Range(1, 6).Select(i => new VehicleBrief
            {
                Brand = new[] { "Hyundai", "Maruti Suzuki", "Tata", "Honda", "Kia", "Mahindra" }[i % 6],
                Model = new[] { "i20", "Swift", "Nexon", "City", "Seltos", "XUV700" }[i % 6],
                Type = new[] { "Hatchback", "Hatchback", "SUV", "Sedan", "SUV", "SUV" }[i % 6],
                Year = 2024 - (i % 2),
                Price = 700000 + random.Next(500000),
                ImageUrl = "",
                AddedDays = random.Next(1, 25)
            }).ToList();

            FeaturedVehicles = Enumerable.Range(1, 5).Select(i => new VehicleBrief
            {
                Brand = new[] { "Toyota", "Hyundai", "MG", "Skoda", "Volkswagen" }[i - 1],
                Model = new[] { "Fortuner", "Tucson", "Hector", "Kodiaq", "Taigun" }[i - 1],
                Views = random.Next(1200, 8900),
                Likes = random.Next(80, 1200),
                IsActive = i % 2 == 0 ? true : false,
                Priority = 10 - i,
                Price = 1800000 + random.Next(1000000)
            }).ToList();

            PremiumVehicles = Enumerable.Range(1, 4).Select(i => new VehicleBrief
            {
                Brand = new[] { "BMW", "Mercedes", "Audi", "Lexus" }[i - 1],
                Model = new[] { "3 Series", "C-Class", "A6", "ES" }[i - 1],
                Engine = "2.0L Turbo",
                Transmission = "Automatic",
                Price = 5500000 + random.Next(2000000),
                Rating = 4.5 + random.NextDouble() * 0.5,
                ImageUrl = ""
            }).ToList();

            UsedVehicles = Enumerable.Range(1, 5).Select(i => new VehicleBrief
            {
                Brand = new[] { "Honda", "Ford", "Nissan", "Renault", "Chevrolet" }[i - 1],
                Model = new[] { "Civic", "Figo", "Sunny", "Kwid", "Beat" }[i - 1],
                Year = 2019 - (i % 2),
                Mileage = (30000 + random.Next(40000)).ToString(),
                Price = 350000 + random.Next(300000),
                ImageUrl = ""
            }).ToList();

            ActiveVehiclesList = Enumerable.Range(1, 8).Select(i => new VehicleBrief
            {
                Brand = new[] { "Hyundai", "Maruti", "Kia", "Tata", "Mahindra", "MG", "Honda", "Skoda" }[i - 1],
                Model = new[] { "Verna", "Baleno", "Carens", "Harrier", "Scorpio", "Astor", "Amaze", "Slavia" }[i - 1],
                DaysLeft = random.Next(1, 28),
                EndDateString = DateTime.Now.AddDays(random.Next(5, 30)).ToString("dd MMM")
            }).ToList();

            TopSellers = new List<SellerInfo>
            {
                new() { Name = "City Motors", Listings = 24 },
                new() { Name = "AutoHub Delhi", Listings = 18 },
                new() { Name = "Elite Cars", Listings = 12 },
                new() { Name = "Speed Wheels", Listings = 9 }
            };

            BuyerInquiries = 187;
            TestDriveRequests = 64;
            BuyerConversion = random.Next(25, 45);
            TotalViews = 24580;
            TotalLikes = 3420;
            TotalShares = 982;
            TotalLeads = 411;

            // NEW: Audience Metrics Data
            FacebookEngagement = 15420;
            InstagramClicks = 8920;
            TwitterClicks = 3450;
            YoutubeViews = 12780;
            FacebookTrend = random.Next(5, 20);
            InstagramTrend = random.Next(3, 18);
            TwitterTrend = random.Next(-2, 15);
            YoutubeTrend = random.Next(10, 25);

            // Traffic Metrics
            TrafficOrganic = random.Next(40, 55);
            TrafficSocial = random.Next(28, 40);
            TrafficDirect = 100 - (TrafficOrganic + TrafficSocial);
            TotalVisits = 28450;
            BounceRate = random.Next(32, 45);
            AvgSessionTime = Math.Round(3.5 + random.NextDouble() * 2.5, 1);

            // Post Metrics
            TotalPosts = 48;
            TotalSharesSocial = 3240;
            TotalReactions = 18750;

            // Trending Topics
            TrendingTopics = new List<TrendingTopic>
            {
                new() { Topic = "Electric SUVs 2025", Volume = "2.4K", PercentChange = 28, TrendDirection = "up", TrendIcon = "🔥" },
                new() { Topic = "Luxury Sedans", Volume = "1.8K", PercentChange = 15, TrendDirection = "up", TrendIcon = "📈" },
                new() { Topic = "Affordable Hatchbacks", Volume = "3.2K", PercentChange = -5, TrendDirection = "down", TrendIcon = "📉" },
                new() { Topic = "Year End Discounts", Volume = "4.1K", PercentChange = 42, TrendDirection = "up", TrendIcon = "🚀" }
            };

            StateHasChanged();
        }

        protected void RefreshMetrics() => RefreshAllData();

        protected string GetThumb(string url) => string.IsNullOrEmpty(url) ? "https://placehold.co/55x55?text=🚗" : url;

        protected string FormatCurrency(decimal price) => price >= 10000000 ? $"₹{price / 10000000:F1}Cr" : (price >= 100000 ? $"₹{price / 100000:F1}L" : $"₹{price:N0}");
    }

    public class SellerInfo
    {
        public string Name { get; set; } = "";
        public int Listings { get; set; }
    }
   
}