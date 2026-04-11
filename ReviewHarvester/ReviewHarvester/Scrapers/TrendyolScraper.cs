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
        public async Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate, CancellationToken token, int delayMs)
        {
            int count = 0;
            onStatusUpdate?.Invoke("Trendyol Klasik Selenium Motoru başlatıldı...");

            await Task.Run(() =>
            {
                var options = new ChromeOptions();

                // options.AddArgument("--headless=new"); // Test aşamasında tıklamaları görmek istersen bunu yoruma alabilirsin

                // Headless modun en büyük açığını kapatıyoruz: Çözünürlüğü insan gibi yapıyoruz
                options.AddArgument("--window-size=1920,1080");

                // Tarayıcının dilini Türkiye/Türkçe olarak ayarlıyoruz
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
                        Thread.Sleep(5000);

                        HashSet<string> seenReviews = new HashSet<string>();
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                        // HER BİR SEÇİLİ YILDIZ İÇİN DÖNGÜ BAŞLIYOR
                        foreach (int currentStar in allowedStars)
                        {
                            if (token.IsCancellationRequested) break;

                            onStatusUpdate?.Invoke($"Trendyol: {currentStar} Yıldız filtresi aranıyor...");

                            try
                            {
                                // AŞAMA 1: Puan Menüsünü Aç (Test-ID ile Keskin Atış)
                                js.ExecuteScript(@"
                                    var puanBtn = document.querySelector('[data-testid=""filter-toggle-rate""]');
                                    if(puanBtn) puanBtn.click();
                                ");
                                Thread.Sleep(1000); // Menünün açılmasını bekle

                                // AŞAMA 2: Temizle Butonu (Güvenlik için önceki seçimleri kaldır)
                                js.ExecuteScript(@"
                                    var btns = document.querySelectorAll('button');
                                    for(var i=0; i<btns.length; i++) {
                                        if(btns[i].innerText && btns[i].innerText.trim() === 'Temizle') {
                                            btns[i].click(); 
                                            break;
                                        }
                                    }
                                ");
                                Thread.Sleep(500);

                                // AŞAMA 3: İlgili Yıldızı Bul ve Seç (Senin getirdiğin html yapısına göre)
                                js.ExecuteScript($@"
                                    // Bütün checkbox kapsayıcılarını al (Gelen istihbarattaki yapı)
                                    var checkboxes = document.querySelectorAll('[data-testid=""checkbox""]');
                                    
                                    for(var i=0; i<checkboxes.length; i++) {{
                                        var el = checkboxes[i];
                                        // Öğenin içindeki yazıyı al (Örn: '1 (2434)')
                                        var text = el.innerText.trim(); 
                                        
                                        // Eğer yazı aradığımız yıldızla başlıyorsa
                                        if(text.startsWith('{currentStar}')) {{
                                            // İçindeki asıl <input type='checkbox'> öğesini bulup tıkla
                                            var input = el.querySelector('input');
                                            if(input) input.click();
                                            else el.click(); // Input yoksa geneline tıkla
                                            
                                            break; // Bulduk ve tıkladık, döngüyü bitir
                                        }}
                                    }}
                                ");
                                Thread.Sleep(500);

                                // AŞAMA 4: Uygula Butonu (Test-ID ile Keskin Atış)
                                js.ExecuteScript(@"
                                    var uygulaBtn = document.querySelector('[data-testid=""filter-apply-button-rate""]');
                                    if(uygulaBtn) uygulaBtn.click();
                                ");

                                // Filtre uygulandıktan sonra yeni yorumların DOM'a (ekrana) düşmesi için bekle
                                Thread.Sleep(4000);
                            }
                            catch (Exception)
                            {
                                // Herhangi bir nedenle menü açılmazsa veya yıldız bulunamazsa atla
                                continue;
                            }

                            // Bu yıldızın kaydırma (scroll) işlemi için sayaçları sıfırlıyoruz
                            int previousCount = seenReviews.Count;
                            int noChangeCounter = 0;

                            // AŞAĞI KAYDIRMA (SCROLL) DÖNGÜSÜ
                            while (true)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    onStatusUpdate?.Invoke("İşlem kullanıcı tarafından durduruldu.");
                                    break;
                                }

                                onStatusUpdate?.Invoke($"Trendyol {currentStar} Yıldız: Aşağı kaydırılıyor... (Toplanan: {count})");

                                // DİKKAT: Başına $ işareti koyduk ki C# değişkeni olan {currentStar}'ı içeri sızdırabilelim.
                                string extractionScript = $@"
                                    var results = [];
                                    
                                    // İstihbarat 6: Doğrudan senin bulduğun review-comment class'ını hedef alıyoruz!
                                    // İç içe span olması fark etmez, querySelectorAll ve innerText onu dümdüz bir yazıya çevirir.
                                    var comments = document.querySelectorAll('.review-comment');
                                    
                                    for(var i = 0; i < comments.length; i++) {{
                                        var text = comments[i].innerText.trim();
                                        
                                        // 5 karakterden kısa boşlukları veya sistemsel özetleri atla
                                        if(text.length < 5) continue;
                                        if(text.includes('Yapay Zeka') || text.includes('Değerlendirme Özeti')) continue;
                                        
                                        // Boy/kilo gibi gereksiz detaylar bu class'ın DıŞıNDA kaldığı için 
                                        // o uzun blacklist filtrelerine artık hiç gerek yok!
                                        
                                        // Puanı zaten arayüzden fiziksel olarak tıkladığımız için C#'tan alıyoruz.
                                        var star = {currentStar};
                                        
                                        results.push({{ text: text, star: star }});
                                    }}
                                    
                                    // Mükerrer (aynı) yorumları ele
                                    var uniqueResults = [];
                                    var seen = new Set();
                                    for(var k = 0; k < results.length; k++) {{
                                        if(!seen.has(results[k].text)) {{
                                            seen.add(results[k].text);
                                            uniqueResults.push(results[k]);
                                        }}
                                    }}
                                    
                                    return JSON.stringify(uniqueResults);
                                ";

                                // JS ajanını çalıştır
                                string jsonResult = (string)js.ExecuteScript(extractionScript);
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

                                // Trendyol'un yeni yorumları yüklemesi için kullanıcının seçtiği süre kadar bekle
                                Thread.Sleep(delayMs);

                                // Döngü kırma mantığı (O yıldızdaki yorumlar bittiyse)
                                if (seenReviews.Count == previousCount)
                                {
                                    noChangeCounter++;
                                    if (noChangeCounter >= 3)
                                        break; // Bu yıldız bitti, foreach döngüsü sıradaki yıldıza geçecek
                                }
                                else
                                {
                                    noChangeCounter = 0;
                                }

                                previousCount = seenReviews.Count;
                            }

                            // Eğer iptal edildiyse foreach döngüsünden de çık
                            if (token.IsCancellationRequested) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Selenium Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
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