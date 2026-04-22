namespace AutoNext.Plotform.App.Backoffice.Models.DTO
{
    public class CityAreaDto
    {
        public Guid Id { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string? AreaCode { get; set; }
        public string? Pincode { get; set; }
    }
}
