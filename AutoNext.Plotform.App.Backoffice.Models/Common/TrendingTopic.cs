namespace AutoNext.Plotform.App.Backoffice.Models.Common
{
    public class TrendingTopic
    {
        public string Topic { get; set; } = "";
        public string Volume { get; set; } = "";
        public int PercentChange { get; set; }
        public string TrendDirection { get; set; } = "";
        public string TrendIcon { get; set; } = "";
    }
}
