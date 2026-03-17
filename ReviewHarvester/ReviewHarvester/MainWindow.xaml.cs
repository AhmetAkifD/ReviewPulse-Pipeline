using CsvHelper;
using HtmlAgilityPack;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Threading;
using OpenQA.Selenium.Support.UI;

namespace ReviewHarvester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Review> HarvestedReviews { get; set; } = new ObservableCollection<Review>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this; // LstReviews verileri görebilsin diye EKLENDİ
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUrl.Text) || !Uri.IsWellFormedUriString(TxtUrl.Text, UriKind.Absolute))
            {
                MessageBox.Show("Lütfen geçerli bir URL girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string url = TxtUrl.Text;

            HarvestedReviews.Clear();
            BtnStart.IsEnabled = false;
            BtnSave.IsEnabled = false;
            PrgStatus.IsIndeterminate = true;
            TxtStatus.Text = "Veriler toplanıyor, lütfen bekleyin...";

            try
            {
                int count = await StartScraping(url); // await eklendi

                if (count > 0)
                    TxtStatus.Text = $"Başarılı! {count} adet yorum toplandı.";
                else
                    TxtStatus.Text = "Hiç yorum bulunamadı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Hata oluştu.";
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnSave.IsEnabled = true;
                PrgStatus.IsIndeterminate = false;
            }
        }

        private async Task<int> StartScraping(string url)
        {
            int count = 0;

            // Arka planda çalışması için Task.Run içine alıyoruz ki UI donmasın
            await Task.Run(() =>
            {
                var options = new ChromeOptions();

                // 1. Tarayıcıyı görünmez yapmak için bunu kullanırız. 
                // EĞER hala engel yersen, bu satırı silip tarayıcının ekranda açılmasını sağla.
                options.AddArgument("--headless=new");

                // 2. Bot olduğumuzu gizleyen "Ninja" ayarları
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                // HIZLANDIRICI (NİTRO) AYARLAR: Resimleri ve CSS'i yüklemeyi tamamen reddet!
                options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);
                options.AddUserProfilePreference("profile.default_content_setting_values.stylesheet", 2);
                options.AddArgument("--disable-gpu"); // Grafik işlemciyi yormaya gerek yok

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    try
                    {
                        // URL'yi temizleyip yorumlar formatına getiriyoruz
                        string baseUrl = url.Split('?')[0];
                        if (!baseUrl.EndsWith("-yorumlari"))
                        {
                            baseUrl += "-yorumlari";
                        }

                        int page = 1;
                        bool hasMoreReviews = true;

                        // Botun Hafızası: Çektiğimiz yorumları buraya kaydedip mükerrer (kopya) veriyi engelleyeceğiz
                        HashSet<string> seenReviews = new HashSet<string>();

                        // Akıllı Bekleme objemizi oluşturuyoruz (Maksimum 10 saniye bekler, veri gelirse anında devam eder)
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                        while (hasMoreReviews)
                        {
                            // Botumuz linki zorla değiştirip Enter'a basıyor
                            string pageUrl = $"{baseUrl}?sayfa={page}";
                            driver.Navigate().GoToUrl(pageUrl);

                            Application.Current.Dispatcher.Invoke(() => TxtStatus.Text = $"Hasat: Sayfa {page} taranıyor...");

                            // YENİ HALİ: Sadece aradığımız JSON script'i DOM'a düşene kadar bekle, düştüğü an fırla!
                            try
                            {
                                wait.Until(d =>
                                {
                                    var scripts = d.FindElements(By.XPath("//script[@type='application/ld+json']"));
                                    // LINQ kullanarak içlerinden herhangi birinde "reviewBody" var mı diye bakıyoruz
                                    return scripts.Any(s => s.GetAttribute("innerHTML").Contains("reviewBody"));
                                });
                            }
                            catch (WebDriverTimeoutException)
                            {
                                // 10 saniye boyunca JSON gelmediyse sayfa boş demektir, döngüyü kır
                                hasMoreReviews = false;
                                continue;
                            }

                            var scriptNodes = driver.FindElements(By.XPath("//script[@type='application/ld+json']"));
                            bool addedNewReviewInThisPage = false; // Bu sayfada yeni bir şey bulduk mu?

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
                                            if (string.IsNullOrWhiteSpace(reviewText)) continue;

                                            // HAFIZA KONTROLÜ: Bu yorumu daha önce çektik mi?
                                            if (seenReviews.Contains(reviewText)) continue;

                                            // Yeni yorumsa hafızaya ve listeye ekle
                                            seenReviews.Add(reviewText);
                                            addedNewReviewInThisPage = true;

                                            int star = review["reviewRating"]?["ratingValue"] != null ? (int)review["reviewRating"]["ratingValue"] : 5;

                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                HarvestedReviews.Add(new Review
                                                {
                                                    User = "Anonim",
                                                    Comment = reviewText,
                                                    Rating = star,
                                                    Source = "Hepsiburada"
                                                });
                                            });
                                            count++;
                                        }
                                    }
                                }
                                catch (Exception) { continue; }
                            }

                            // Eğer bu sayfada hiç "yeni" yorum bulamadıysak (demek ki son sayfaya geldik veya site başa sardı)
                            if (!addedNewReviewInThisPage)
                            {
                                hasMoreReviews = false;
                                Application.Current.Dispatcher.Invoke(() => TxtStatus.Text = $"Hasat kusursuz bitti! Toplam {count} benzersiz yorum çekildi.");
                            }
                            else
                            {
                                page++; // Yeni yorumlar bulduysak diğer sayfaya geç
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Tarayıcı hatası: " + ex.Message));
                    }
                    finally
                    {
                        // İşlem bitince arkada açık Chrome kalmasın diye kesinlikle kapatıyoruz
                        driver.Quit();
                    }
                }
            });

            return count;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (HarvestedReviews.Count == 0)
            {
                MessageBox.Show("Kaydedilecek veri bulunamadı. Önce hasat yapmalısınız!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = "reviews_data.csv"
            };

            try
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(HarvestedReviews);
                    }
                    MessageBox.Show("Veriler başarıyla CSV formatına dönüştürüldü!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (IOException)
            {
                MessageBox.Show("Dosya başka bir program (örn: Excel) tarafından kullanılıyor. Lütfen kapatıp tekrar deneyin.", "Dosya Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCsvManager_Click(object sender, RoutedEventArgs e)
        {
            CsvManagerWindow csvWindow = new CsvManagerWindow();
            csvWindow.ShowDialog(); // Yeni pencereyi aç
        }
    }
}