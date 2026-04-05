using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ReviewHarvester.Scrapers
{
    public class TrendyolScraper : IReviewScraper
    {
        public async Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate, CancellationToken token)
        {
            int count = 0;
            onStatusUpdate?.Invoke("Trendyol Klasik Selenium Motoru başlatıldı...");

            await Task.Run(() =>
            {
                var options = new ChromeOptions();

                options.AddArgument("--headless=new");

                // Headless modun en büyük açığını kapatıyoruz: Çözünürlüğü insan gibi yapıyoruz
                options.AddArgument("--window-size=1920,1080");

                // Tarayıcının dilini Türkiye/Türkçe olarak ayarlıyoruz (Cloudflare bazen dil eksikliğinden anlar)
                options.AddArgument("--lang=tr-TR");

                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
                options.AddUserProfilePreference("profile.default_content_setting_values.stylesheet", 2);

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    try
                    {
                        // Linki doğrudan Yorumlar sekmesine yönlendiriyoruz
                        string baseUrl = url.Split('?')[0];
                        if (!baseUrl.EndsWith("/yorumlar"))
                        {
                            baseUrl += "/yorumlar";
                        }

                        onStatusUpdate?.Invoke("Sayfa açılıyor... (Lütfen tarayıcıya müdahale etmeyin)");
                        driver.Navigate().GoToUrl(baseUrl);

                        // Sayfanın tamamen yüklenmesi ve güvenlik testlerinin geçmesi için bekle
                        Thread.Sleep(4000);

                        HashSet<string> seenReviews = new HashSet<string>();
                        int previousCount = 0;
                        int noChangeCounter = 0;

                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                        while (true)
                        {
                            if (token.IsCancellationRequested)
                            {
                                onStatusUpdate?.Invoke("İşlem kullanıcı tarafından durduruldu.");
                                break; // Döngüyü kır ve işlemi bitir
                            }
                            onStatusUpdate?.Invoke($"Aşağı kaydırılıyor... (Toplanan: {count})");

                            // TRUVA ATI 2.0: Sayfadaki yorumları ve yıldızları salise hızında toplayan JS Ajanı
                            // TRUVA ATI 2.1: Gelişmiş ve Saldırgan JS Ajanı
                            // TRUVA ATI 3.0: "Aşağıdan Yukarı" (Bottom-Up) Taraması
                            // TRUVA ATI 3.1: Anti-Parazit Kalkanlı "Bottom-Up" Taraması
                            // TRUVA ATI 4.0: Doğrudan Hedefe Kilitli (Target-Locked) Ajan
                            string extractionScript = @"
                                var results = [];
                                
                                // Senin bulduğun o sihirli kelimeyi (class adını) arıyoruz!
                                // 'class*=' kullanıyoruz ki yanına 'review-comment-123' gibi rastgele kodlar ekleseler bile yakalayalım.
                                var comments = document.querySelectorAll('[class*=""review-comment""]');
                                
                                for(var i=0; i<comments.length; i++){
                                    var c = comments[i];
                                    var text = c.innerText.trim();
                                    
                                    // Çok kısa veya boş yazıları geç
                                    if(text.length < 5) continue;
                                    
                                    // Trendyol'un Yapay Zeka özetini her ihtimale karşı yine engelliyoruz
                                    if(text.includes('Yapay Zeka') || text.includes('Değerlendirme Özeti')) continue;
                                    
                                    // Metni bulduk! Şimdi tek yapmamız gereken bağlı olduğu karttaki yıldızları bulmak.
                                    var parent = c.parentElement;
                                    var starDiv = null;
                                    var attempts = 0;
                                    
                                    // En fazla 6 kademe yukarı çıkıp yıldız kutusunu arıyoruz
                                    while(parent && attempts < 6){
                                        starDiv = parent.querySelector('.star-w, [class*=""star""], [class*=""rating""]');
                                        if(starDiv) break;
                                        parent = parent.parentElement;
                                        attempts++;
                                    }
                                    
                                    // Yıldızı bulduysak genişliğinden puanı hesapla (Bulamazsa varsayılan 5)
                                    var star = 5; 
                                    if(starDiv) {
                                        var fullStar = starDiv.querySelector('.full, .fill, [style*=""width""]');
                                        if(fullStar) {
                                            var w = fullStar.getAttribute('style') || '';
                                            if(w.includes('100')) star = 5;
                                            else if(w.includes('80')) star = 4;
                                            else if(w.includes('60')) star = 3;
                                            else if(w.includes('40')) star = 2;
                                            else if(w.includes('20')) star = 1;
                                        }
                                    }
                                    
                                    results.push({ text: text, star: star });
                                }
                                
                                // Selenium sayfayı aşağı kaydırdıkça aynı yorumları tekrar okumasın diye mükerrerleri siliyoruz
                                var uniqueResults = [];
                                var seen = new Set();
                                for(var j=0; j<results.length; j++){
                                    if(!seen.has(results[j].text)){
                                        seen.add(results[j].text);
                                        uniqueResults.push(results[j]);
                                    }
                                }
                                
                                return JSON.stringify(uniqueResults);
                            ";

                            // JS ajanını çalıştır ve veriyi al
                            string jsonResult = (string)js.ExecuteScript(extractionScript);

                            // Gelen JSON verisini C# listesine çevir
                            var scrapedData = JsonConvert.DeserializeObject<List<dynamic>>(jsonResult);

                            if (scrapedData != null)
                            {
                                foreach (var item in scrapedData)
                                {
                                    string reviewText = item.text.ToString().Replace('\n', ' ');
                                    int star = (int)item.star;

                                    if (seenReviews.Contains(reviewText)) continue;
                                    if (!allowedStars.Contains(star)) continue;

                                    seenReviews.Add(reviewText);

                                    onReviewFound?.Invoke(new Review
                                    {
                                        User = "Anonim",
                                        Comment = reviewText,
                                        Rating = star,
                                        Source = "Trendyol"
                                    });
                                    count++;
                                }
                            }

                            // Sayfayı en aşağı kaydır (Lazy loading tetiklensin)
                            js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");

                            // Trendyol'un yeni yorumları yüklemesi için bekle
                            Thread.Sleep(2500);

                            // Eğer yeni yorum gelmiyorsa döngüyü kırma mantığı
                            if (seenReviews.Count == previousCount)
                            {
                                noChangeCounter++;
                                if (noChangeCounter >= 3) // 3 kez kaydırdık ve yeni yorum gelmediyse işimiz bitti demektir
                                    break;
                            }
                            else
                            {
                                noChangeCounter = 0; // Yeni yorum geldiyse sayacı sıfırla
                            }

                            previousCount = seenReviews.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Selenium Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                    finally
                    {
                        // Arka planda tarayıcı açık kalmasın
                        driver.Quit();
                    }
                }
            });

            return count;
        }
    }
}