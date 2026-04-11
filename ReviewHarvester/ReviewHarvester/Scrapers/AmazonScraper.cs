using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace ReviewHarvester.Scrapers
{
    public class AmazonScraper : IReviewScraper
    {
        public async Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate, CancellationToken token, int delayMs)
        {
            int count = 0;
            onStatusUpdate?.Invoke("Amazon Motoru (Kalıcı Profil) başlatıldı...");

            string domain = "amazon.com.tr";
            Match domainMatch = Regex.Match(url, @"amazon\.([a-z\.]+)");
            if (domainMatch.Success) domain = domainMatch.Groups[1].Value;

            string asin = "";
            Match asinMatch = Regex.Match(url, @"(?:dp|product-reviews|d)\/([A-Z0-9]{10})");
            if (asinMatch.Success)
            {
                asin = asinMatch.Groups[1].Value;
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Linkten Amazon ASIN Kodu (B0...) bulunamadı! Lütfen geçerli bir Amazon ürün linki girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
                return 0;
            }

            await Task.Run(() =>
            {
                var options = new ChromeOptions();

                options.AddArgument("--headless=new");
                string profilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AmazonProfile");
                options.AddArgument($"--user-data-dir={profilePath}");
                options.AddArgument("--window-size=1920,1080");
                options.AddArgument("--lang=tr-TR");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    try
                    {
                        // HER BİR YILDIZ İÇİN AYRI AYRI TARAMA YAP (Trendyol'daki mantığın aynısı)
                        foreach (int currentStar in allowedStars)
                        {
                            if (token.IsCancellationRequested) break;

                            // Amazon'un URL filtre kelimeleri
                            string starText = "all_stars";
                            if (currentStar == 1) starText = "one_star";
                            else if (currentStar == 2) starText = "two_star";
                            else if (currentStar == 3) starText = "three_star";
                            else if (currentStar == 4) starText = "four_star";
                            else if (currentStar == 5) starText = "five_star";

                            int page = 1;
                            bool hasMore = true;
                            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                            while (hasMore)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    onStatusUpdate?.Invoke("İşlem durduruldu.");
                                    break;
                                }

                                // SİHİRLİ URL HACK: Tıklamaya gerek yok, Amazon'a doğrudan "Bana bu puanı ve bu sayfayı ver" diyoruz.
                                string pageUrl = $"https://www.amazon.{domain}/product-reviews/{asin}/ref=cm_cr_arp_d_viewopt_sr?ie=UTF8&filterByStar={starText}&reviewerType=all_reviews&pageNumber={page}";
                                onStatusUpdate?.Invoke($"Amazon: {currentStar} Yıldız, Sayfa {page} taranıyor... (Toplanan: {count})");

                                driver.Navigate().GoToUrl(pageUrl);
                                Thread.Sleep(delayMs);

                                // Güvenlik Duvarı Kontrolü
                                if (driver.Url.Contains("signin") || driver.Url.Contains("ap/signin") || driver.Title.Contains("Robot"))
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        MessageBox.Show("Amazon güvenlik duvarı aktif!\n\nLütfen açılan tarayıcı ekranından Amazon hesabınıza giriş yapın (veya Captcha çözün).\n\nGiriş işlemini başarıyla tamamladıktan sonra BU KUTUYA 'Tamam' diyerek devam edin.", "Kullanıcı Müdahalesi Gerekiyor", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    });

                                    driver.Navigate().GoToUrl(pageUrl);
                                    Thread.Sleep(4000);
                                }

                                // YABANCI YORUM FİLTRELİ JS AJANI
                                string extractionScript = $@"
                                    var results = [];
                                    var reviews = document.querySelectorAll('[data-hook=""review""], .review, .a-section.review');
                                    
                                    for(var i=0; i<reviews.length; i++){{
                                        var r = reviews[i];
                                        
                                        // 1. YABANCI YORUM FİLTRESİ (Tarih ve Lokasyon kontrolü)
                                        var dateElem = r.querySelector('[data-hook=""review-date""]');
                                        if(dateElem) {{
                                            var dateText = dateElem.innerText.toLowerCase();
                                            // Yorumun yapıldığı lokasyon 'Türkiye' değilse bu yorumu çöpe at ve sonrakine geç!
                                            if(!dateText.includes('türkiye')) continue;
                                        }}
                                        
                                        // 2. Metni Al
                                        var textElem = r.querySelector('[data-hook=""review-body""]') || r.querySelector('.review-text') || r.querySelector('.review-text-content');
                                        if(!textElem) continue;
                                        
                                        var text = textElem.innerText.trim();
                                        text = text.replace('Daha fazlasını oku', '').replace('Read more', '').trim();
                                        if(text.length < 5) continue;
                                        
                                        // 3. Kullanıcı Adını Al
                                        var userElem = r.querySelector('.a-profile-name');
                                        var user = userElem ? userElem.innerText.trim() : 'Anonim';
                                        
                                        // Puanı arayüzden bildiğimiz için URL'den alıyoruz
                                        var star = {currentStar};
                                        
                                        results.push({{ text: text, star: star, user: user }});
                                    }}
                                    return JSON.stringify(results);
                                ";

                                string jsonResult = (string)js.ExecuteScript(extractionScript);
                                var scrapedData = JsonConvert.DeserializeObject<List<dynamic>>(jsonResult);

                                if (scrapedData == null || scrapedData.Count == 0)
                                {
                                    // Sayfada hiç (yerli) yorum kalmadıysa sıradaki yıldıza geç
                                    hasMore = false;
                                    break;
                                }

                                foreach (var item in scrapedData)
                                {
                                    string reviewText = item.text.ToString().Replace('\n', ' ');
                                    int star = (int)item.star;
                                    string userName = item.user.ToString();

                                    onReviewFound?.Invoke(new Review
                                    {
                                        User = userName,
                                        Comment = reviewText,
                                        Rating = star,
                                        Source = "Amazon"
                                    });
                                    count++;
                                }

                                // Amazon'da 'Sonraki Sayfa' butonu aktif mi kontrolü
                                // JS ajanına diyoruz ki: "nextBtn null değilse VE a-disabled class'ı yoksa true dön, aksi halde false dön."
                                bool hasNextPage = (bool)js.ExecuteScript("var nextBtn = document.querySelector('li.a-last'); return (nextBtn !== null) && (!nextBtn.classList.contains('a-disabled'));");
                                if (!hasNextPage)
                                {
                                    hasMore = false; // Bu yıldızdaki tüm sayfalar bitti
                                }
                                else
                                {
                                    page++; // Sonraki sayfaya geç
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Amazon Selenium Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
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