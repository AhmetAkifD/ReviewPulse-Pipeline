using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Windows;

namespace ReviewHarvester.Scrapers
{
    public class Selenium : IReviewScraper
    {
        public async Task<int> ScrapeAsync(string url, List<int> allowedStars, Action<Review> onReviewFound, Action<string> onStatusUpdate, CancellationToken token, int delayMs)
        {
            int count = 0;

            await Task.Run(() =>
            {
                var options = new ChromeOptions();
                options.AddArgument("--headless=new");
                options.AddArgument("--window-size=1920,1080");
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

                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                        // 1. DÖNGÜ: Her bir yıldız için ayrı tarama
                        foreach (int currentStar in allowedStars)
                        {
                            if (token.IsCancellationRequested) break;

                            onStatusUpdate?.Invoke($"Hepsiburada: {currentStar} Yıldız için hazırlanıyor...");

                            // URL baştan yüklenip önceki filtreler temizleniyor
                            driver.Navigate().GoToUrl(baseUrl);
                            Thread.Sleep(4000);

                            // 2. JS AJANI: Filtre Kutusunu Nokta Atışı Bul ve Tıkla
                            string clickFilterScript = $@"
                                var targetStar = '{currentStar}';
                                // İçinde sayı yazan span'ı bul (Örn: '1', '2')
                                var allSpans = document.querySelectorAll('span[class*=""hermes-RateBox-module""]');
                                for(var i=0; i<allSpans.length; i++) {{
                                    if(allSpans[i].innerText.trim() === targetStar) {{
                                        // Rakamı bulduk, tıklanabilir ana kutu onun parent'ıdır!
                                        var clickableDiv = allSpans[i].parentElement;
                                        clickableDiv.scrollIntoView({{block: 'center'}});
                                        clickableDiv.click();
                                        return true;
                                    }}
                                }}
                                return false;
                            ";

                            bool filterClicked = (bool)js.ExecuteScript(clickFilterScript);

                            if (!filterClicked) continue; // O puanda yorum yoksa atla

                            // React'ın sunucudan yeni yorumları çekip DOM'u değiştirmesini bekle
                            Thread.Sleep(4000);

                            int page = 1;
                            bool hasMoreReviews = true;
                            HashSet<string> seenReviews = new HashSet<string>();
                            int noChangeCounter = 0;

                            while (hasMoreReviews)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    onStatusUpdate?.Invoke("İşlem durduruldu.");
                                    break;
                                }

                                onStatusUpdate?.Invoke($"Hepsiburada: {currentStar} Yıldız | Sayfa {page} taranıyor... (Toplanan: {count})");

                                // AJAX tetiklenmesi için kaydır
                                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                                Thread.Sleep(1500);

                                // 3. YENİLENMİŞ, KUSURSUZ EXTRACTION SCRIPT (Veri Koparıcı)
                                string extractionScript = @"
                                    var results = [];
                                    
                                    // Bütün kartları bul
                                    var allCards = document.querySelectorAll('div[class*=""hermes-ReviewCard-module""]');
                                    var mainCards = [];
                                    
                                    // Hepsiburada 'hermes-ReviewCard-module' class'ını iç içe kullanır. Sadece EN DIŞTAKİ kartları seçmeliyiz.
                                    for(var i=0; i<allCards.length; i++) {
                                        var el = allCards[i];
                                        // Eğer içinde Rate pointer varsa, bu geçerli bir yorum kartıdır
                                        if(el.querySelector('div[class*=""hermes-RatingPointer-module""]')) {
                                            var isInner = false;
                                            var p = el.parentElement;
                                            while(p && p !== document.body) {
                                                if(p.className && typeof p.className === 'string' && p.className.includes('hermes-ReviewCard-module')) {
                                                    isInner = true; break;
                                                }
                                                p = p.parentElement;
                                            }
                                            if(!isInner) mainCards.push(el);
                                        }
                                    }

                                    for(var i=0; i<mainCards.length; i++) {
                                        var c = mainCards[i];

                                        // 1. TEXT (METİN) ALMA: CSS boşluklarına aldırış etmeden oku
                                        var text = '';
                                        var spans = c.querySelectorAll('span');
                                        for(var j=0; j<spans.length; j++) {
                                            var css = spans[j].getAttribute('style') || '';
                                            if(css.replace(/\s/g, '').includes('text-align:start')) {
                                                text = spans[j].innerText.trim();
                                                break;
                                            }
                                        }
                                        
                                        // CSS ile bulamadıysak, en dıştaki kartın içindeki ilk yazıyı (span) al
                                        if(!text) {
                                            var firstSpan = c.querySelector('span');
                                            if(firstSpan) text = firstSpan.innerText.trim();
                                        }

                                        if(text.length < 5) continue;

                                        // 2. YILDIZ ALMA: Sadece içi dolu olan (class='star') div'leri say
                                        var star = 5;
                                        var ratingDiv = c.querySelector('div[class*=""hermes-RatingPointer-module""]');
                                        if(ratingDiv) {
                                            var starCount = 0;
                                            var innerDivs = ratingDiv.querySelectorAll('div');
                                            for(var k=0; k<innerDivs.length; k++) {
                                                if(innerDivs[k].className === 'star') {
                                                    starCount++;
                                                }
                                            }
                                            if(starCount > 0) star = starCount;
                                        }

                                        results.push({ text: text, star: star, user: 'Anonim' });
                                    }
                                    return JSON.stringify(results);
                                ";

                                string jsonResult = (string)js.ExecuteScript(extractionScript);
                                var scrapedData = JsonConvert.DeserializeObject<List<dynamic>>(jsonResult);

                                bool addedNewReviewInThisPage = false;

                                if (scrapedData != null)
                                {
                                    foreach (var item in scrapedData)
                                    {
                                        string reviewText = item.text.ToString().Replace('\n', ' ').Trim();
                                        int star = (int)item.star;

                                        // Yıldız filtresi teyidi: Ajan yanlış sayfadaysa o yorumları reddet
                                        if (star != currentStar) continue;

                                        if (string.IsNullOrWhiteSpace(reviewText) || seenReviews.Contains(reviewText)) continue;

                                        seenReviews.Add(reviewText);
                                        addedNewReviewInThisPage = true;

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

                                // 4. SONRAKİ SAYFAYA GEÇİŞ (JS Ajanı ile Nokta Atışı)
                                if (addedNewReviewInThisPage)
                                {
                                    noChangeCounter = 0;
                                    string clickNextPageScript = $@"
                                        var nextPageStr = '{page + 1}';
                                        var pageHolders = document.querySelectorAll('li[class*=""hermes-PageHolder-module""]');
                                        for(var i=0; i<pageHolders.length; i++) {{
                                            var span = pageHolders[i].querySelector('span');
                                            if(span && span.innerText.trim() === nextPageStr) {{
                                                pageHolders[i].scrollIntoView({{block: 'center'}});
                                                pageHolders[i].click();
                                                return true;
                                            }}
                                        }}
                                        return false;
                                    ";

                                    bool clickedNext = (bool)js.ExecuteScript(clickNextPageScript);

                                    if (clickedNext)
                                    {
                                        page++;
                                        Thread.Sleep(3000); // Sayfa geçişinde yeni verilerin akmasını bekle
                                    }
                                    else
                                    {
                                        hasMoreReviews = false;
                                    }
                                }
                                else
                                {
                                    noChangeCounter++;
                                    if (noChangeCounter >= 3) hasMoreReviews = false;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Hepsiburada Selenium Hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
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