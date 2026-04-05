using System;

namespace ReviewHarvester.Scrapers
{
    public static class ScraperFactory
    {
        public static IReviewScraper GetScraper(string url)
        {
            string lowerUrl = url.ToLower();

            if (lowerUrl.Contains("hepsiburada.com"))
            {
                return new Selenium();
            }
            else if (lowerUrl.Contains("trendyol.com"))
            {
                return new TrendyolScraper();
            }
            else if (lowerUrl.Contains("amazon.com") || lowerUrl.Contains("amazon.com.tr"))
            {
                // AMAZON MOTORU AKTİF EDİLDİ
                return new AmazonScraper();
            }
            else
            {
                throw new NotSupportedException("Bu e-ticaret sitesi henüz desteklenmiyor.");
            }
        }
    }
}