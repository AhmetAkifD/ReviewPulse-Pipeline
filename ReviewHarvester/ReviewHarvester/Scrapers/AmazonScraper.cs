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
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Linkten Amazon ASIN Kodu (B0...) bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
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

                            string pageUrl = $"https://www.amazon.{domain}/product-reviews/{asin}/ref=cm_cr_arp_d_paging_btm_next_{page}?pageNumber={page}";
                            onStatusUpdate?.Invoke($"Amazon: {page}. sayfa taranıyor... (Toplanan: {count})");

                            driver.Navigate().GoToUrl(pageUrl);
                            Thread.Sleep(delayMs);

                            // SİHİRLİ DOKUNUŞ 2: Giriş veya Captcha Kontrolü
                            if (driver.Url.Contains("signin") || driver.Url.Contains("ap/signin") || driver.Title.Contains("Robot"))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show("Amazon güvenlik duvarı aktif!\n\nLütfen açılan tarayıcı ekranından Amazon hesabınıza giriş yapın (veya Captcha çözün).\n\nGiriş işlemini başarıyla tamamladıktan sonra BU KUTUYA 'Tamam' diyerek devam edin.", "Kullanıcı Müdahalesi Gerekiyor", MessageBoxButton.OK, MessageBoxImage.Warning);
                                });

                                // Kullanıcı 'Tamam'a bastıktan sonra hedef sayfaya tekrar git
                                // (Çünkü login sonrası anasayfaya atmış olabilir)
                                driver.Navigate().GoToUrl(pageUrl);
                                Thread.Sleep(4000);
                            }

                            // 3. AMAZON İÇİN ÖZEL JS AJANI
                            // 3. AMAZON İÇİN ÖZEL VE GENİŞ ÇAPLI JS AJANI
                            string extractionScript = @"
                                var results = [];
                                // Amazon yorum kapsayıcıları (Tüm güncel ihtimaller)
                                var reviews = document.querySelectorAll('[data-hook=""review""], .review, .a-section.review');
                                
                                for(var i=0; i<reviews.length; i++){
                                    var r = reviews[i];
                                    
                                    // Metni Al (Farklı class isimlerinin hepsini deniyoruz)
                                    var textElem = r.querySelector('[data-hook=""review-body""]') || r.querySelector('.review-text') || r.querySelector('.review-text-content');
                                    if(!textElem) continue;
                                    
                                    var text = textElem.innerText.trim();
                                    
                                    // Amazon'un 'Daha fazlasını oku' gibi arayüz yazılarını metinden temizliyoruz
                                    text = text.replace('Daha fazlasını oku', '').replace('Read more', '').trim();
                                    
                                    if(text.length < 5) continue;
                                    
                                    // Puanı Al
                                    var starElem = r.querySelector('[data-hook=""review-star-rating""]') || r.querySelector('[class*=""a-star-""]');
                                    var star = 5; // Bulamazsa 5 varsay
                                    if(starElem) {
                                        var classes = starElem.className;
                                        if(classes.includes('a-star-1')) star = 1;
                                        else if(classes.includes('a-star-2')) star = 2;
                                        else if(classes.includes('a-star-3')) star = 3;
                                        else if(classes.includes('a-star-4')) star = 4;
                                        else if(classes.includes('a-star-5')) star = 5;
                                    }
                                    
                                    // Kullanıcı Adını Al
                                    var userElem = r.querySelector('.a-profile-name');
                                    var user = userElem ? userElem.innerText.trim() : 'Anonim';
                                    
                                    results.push({ text: text, star: star, user: user });
                                }
                                return JSON.stringify(results);
                            ";

                            string jsonResult = (string)js.ExecuteScript(extractionScript);
                            var scrapedData = JsonConvert.DeserializeObject<List<dynamic>>(jsonResult);

                            if (scrapedData == null || scrapedData.Count == 0)
                            {
                                // Eğer gerçekten son sayfadaysa veya başka bir hata varsa çık
                                hasMore = false;
                                break;
                            }

                            foreach (var item in scrapedData)
                            {
                                string reviewText = item.text.ToString().Replace('\n', ' ');
                                int star = (int)item.star;
                                string userName = item.user.ToString();

                                if (!allowedStars.Contains(star)) continue;

                                onReviewFound?.Invoke(new Review
                                {
                                    User = userName,
                                    Comment = reviewText,
                                    Rating = star,
                                    Source = "Amazon"
                                });
                                count++;
                            }

                            bool hasNextPage = (bool)js.ExecuteScript("return document.querySelector('li.a-last a') !== null;");
                            if (!hasNextPage)
                            {
                                hasMore = false;
                            }
                            else
                            {
                                page++;
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