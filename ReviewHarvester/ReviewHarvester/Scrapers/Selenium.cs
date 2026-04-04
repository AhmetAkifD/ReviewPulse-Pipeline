using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace ReviewHarvester.Scrapers
{
    public class Selenium : IReviewScraper
    {
        public async Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate, CancellationToken token)
        {
            int count = 0;

            await Task.Run(() =>
            {
                var options = new ChromeOptions();
                options.AddArgument("--headless=new");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
                options.AddUserProfilePreference("profile.default_content_setting_values.stylesheet", 2);
                options.AddArgument("--disable-gpu");

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    try
                    {
                        string baseUrl = url.Split('?')[0];
                        if (!baseUrl.EndsWith("-yorumlari")) baseUrl += "-yorumlari";

                        int page = 1;
                        bool hasMoreReviews = true;
                        HashSet<string> seenReviews = new HashSet<string>();
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                        while (hasMoreReviews)
                        {
                            string pageUrl = $"{baseUrl}?sayfa={page}";
                            driver.Navigate().GoToUrl(pageUrl);

                            // Arayüze mesaj gönderiyoruz
                            onStatusUpdate?.Invoke($"Hepsiburada Hasadı: Sayfa {page} taranıyor...");

                            try
                            {
                                wait.Until(d =>
                                {
                                    var scripts = d.FindElements(By.XPath("//script[@type='application/ld+json']"));
                                    return scripts.Any(s => s.GetAttribute("innerHTML").Contains("reviewBody"));
                                });
                            }
                            catch (WebDriverTimeoutException)
                            {
                                hasMoreReviews = false;
                                continue;
                            }

                            var scriptNodes = driver.FindElements(By.XPath("//script[@type='application/ld+json']"));
                            bool addedNewReviewInThisPage = false;

                            foreach (var node in scriptNodes)
                            {
                                string jsonText = node.GetAttribute("innerHTML");
                                if (string.IsNullOrEmpty(jsonText) || !jsonText.Contains("reviewBody")) continue;

                                try
                                {
                                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonText);
                                    if (data is Newtonsoft.Json.Linq.JArray)
                                    {
                                        foreach (var review in data)
                                        {
                                            string reviewText = review["reviewBody"]?.ToString().Replace('\n', ' ').Trim();
                                            if (string.IsNullOrWhiteSpace(reviewText) || seenReviews.Contains(reviewText)) continue;

                                            int star = review["reviewRating"]?["ratingValue"] != null ? (int)review["reviewRating"]["ratingValue"] : 5;

                                            // Filtre kontrolü
                                            if (!allowedStars.Contains(star)) continue;

                                            seenReviews.Add(reviewText);
                                            addedNewReviewInThisPage = true;

                                            // Bulunan yorumu arayüze anında gönderiyoruz
                                            onReviewFound?.Invoke(new Review
                                            {
                                                User = "Anonim",
                                                Comment = reviewText,
                                                Rating = star,
                                                Source = "Hepsiburada"
                                            });
                                            count++;
                                        }
                                    }
                                }
                                catch (Exception) { continue; }
                            }

                            if (!addedNewReviewInThisPage)
                            {
                                hasMoreReviews = false;
                            }
                            else
                            {
                                page++;
                            }
                        }
                    }
                    finally
                    {
                        driver.Quit();
                    }
                }
            });

            return count;
        }
    }
}