using System;

namespace ReviewHarvester.Scrapers
{
    public static class ScraperFactory
    {
        public static IReviewScraper GetScraper(string url)
        {
            string lowerUrl = url.ToLower();

            // Link Hepsiburada ise Ağır Makineyi (Selenium) çağır
            if (lowerUrl.Contains("hepsiburada.com"))
            {
                return new Selenium(); // Senin değiştirdiğin isme göre güncelledim
            }
            // Link Trendyol ise Yeni Hızlı Motoru (HttpClient) çağır
            else if (lowerUrl.Contains("trendyol.com"))
            {
                return new TrendyolScraper();
            }
            // Diğer siteler için
            else if (lowerUrl.Contains("amazon.com") || lowerUrl.Contains("amazon.com.tr"))
            {
                throw new NotImplementedException("Amazon motoru yapım aşamasında!");
            }
            else
            {
                throw new NotSupportedException("Bu e-ticaret sitesi henüz desteklenmiyor.");
            }
        }
    }
}