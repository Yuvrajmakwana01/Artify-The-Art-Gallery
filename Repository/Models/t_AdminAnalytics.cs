using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class AdminAnalyticsViewModel
    {
        public string SelectedPeriod { get; set; } = "month";
        public string DateRangeText { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int ActiveBuyers { get; set; }
        public decimal TotalArtistPayout { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal RevenueChangePercentage { get; set; }
        public decimal AverageOrderValueChangePercentage { get; set; }
        public decimal ActiveBuyersChangePercentage { get; set; }
        public decimal PayoutChangePercentage { get; set; }
        public List<AdminRevenuePointViewModel> RevenueGrowth { get; set; } = [];
        public List<AdminUserActivityPointViewModel> UserActivity { get; set; } = [];
        public List<AdminTopArtworkViewModel> TopPerformingArtworks { get; set; } = [];

        public string TotalRevenueText => TotalRevenue.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        public string AverageOrderValueText => AverageOrderValue.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        public string PayoutVsCommissionText => $"{PayoutRatioRounded}% / {CommissionRatioRounded}%";

        public int PayoutRatioRounded
        {
            get
            {
                var total = TotalArtistPayout + TotalCommission;
                if (total <= 0) return 0;
                return (int)Math.Round((TotalArtistPayout / total) * 100m, MidpointRounding.AwayFromZero);
            }
        }

        public int CommissionRatioRounded => Math.Max(0, 100 - PayoutRatioRounded);
    }

    public class AdminRevenuePointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public class AdminUserActivityPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int NewUsers { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class AdminTopArtworkViewModel
    {
        public string ArtworkTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
        public string RevenueText { get; set; } = string.Empty;
        public string ArtworkTone { get; set; } = "peach";
    }

}