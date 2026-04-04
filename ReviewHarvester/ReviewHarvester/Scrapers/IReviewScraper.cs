using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReviewHarvester.Scrapers
{
    public interface IReviewScraper
    {
        // UI (Arayüz) ile bağlantıyı koparmamak için Action (Callback) fonksiyonları kullanıyoruz.
        // Böylece motorlar arayüzü bilmeden arayüze veri gönderebilecek.
        Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate);
    }
}