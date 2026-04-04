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
                return new Selenium(); // Ağır ama mecburi Selenium motoru
            }
            else if (lowerUrl.Contains("amazon.com") || lowerUrl.Contains("amazon.com.tr"))
            {
                // return new AmazonScraper(); // Gelecekte aktif edeceğiz
                throw new NotImplementedException("Amazon motoru yapım aşamasında!");
            }
            else if (lowerUrl.Contains("trendyol.com"))
            {
                // return new TrendyolScraper(); // Gelecekte aktif edeceğiz
                throw new NotImplementedException("Trendyol motoru yapım aşamasında!");
            }
            else
            {
                throw new NotSupportedException("Bu e-ticaret sitesi henüz desteklenmiyor.");
            }
        }
    }
}