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
        private List<string> _targetUrls = new List<string>();
        private bool _isDarkTheme = true; // Varsayılan olarak karanlık başlıyoruz

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this; // LstReviews verileri görebilsin diye EKLENDİ
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // Eğer TXT yüklenmemişse ama Textbox'a elle bir link girilmişse, onu listeye ekle
            if (_targetUrls.Count == 0 && !string.IsNullOrWhiteSpace(TxtUrl.Text))
            {
                _targetUrls.Add(TxtUrl.Text.Trim());
            }

            if (_targetUrls.Count == 0)
            {
                MessageBox.Show("Lütfen bir URL girin veya TXT dosyası seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HarvestedReviews.Clear();
            BtnStart.IsEnabled = false;
            BtnSave.IsEnabled = false;
            BtnLoadTxt.IsEnabled = false;

            int totalCollectedCount = 0;

            try
            {
                for (int i = 0; i < _targetUrls.Count; i++)
                {
                    string currentUrl = _targetUrls[i];
                    if (!Uri.IsWellFormedUriString(currentUrl, UriKind.Absolute)) continue;

                    // İŞTE BURASI: Sıradaki linki Textbox'a yapıştırıyoruz!
                    TxtUrl.Text = currentUrl;

                    TxtStatus.Text = $"Görev {i + 1}/{_targetUrls.Count} işleniyor... Toplanan: {totalCollectedCount}";
                    PrgStatus.IsIndeterminate = true;

                    int countFromThisUrl = await Task.Run(() => StartScraping(currentUrl));
                    totalCollectedCount += countFromThisUrl;
                }

                TxtStatus.Text = $"Tüm görevler bitti! Toplam {totalCollectedCount} yorum hasat edildi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Otomasyon sırasında hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Görev iptal edildi.";
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnSave.IsEnabled = true;
                BtnLoadTxt.IsEnabled = true;
                PrgStatus.IsIndeterminate = false;

                // İşlem bitince listeyi temizle ki, sonradan elle yeni bir link girildiğinde eskilere tekrar girmesin
                _targetUrls.Clear();
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

                                            if (seenReviews.Contains(reviewText)) continue;

                                            seenReviews.Add(reviewText);
                                            addedNewReviewInThisPage = true;

                                            // Puanı alıyoruz
                                            int star = review["reviewRating"]?["ratingValue"] != null ? (int)review["reviewRating"]["ratingValue"] : 5;

                                            // İŞTE YENİ FİLTREMİZ: Eğer puan 3'ten büyükse (4 veya 5 ise) bu yorumu atla ve listeye ekleme!
                                            if (star > 3) continue;

                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                HarvestedReviews.Add(new Review
                                                {
                                                    User = "Anonim",
                                                    Comment = reviewText,
                                                    Rating = star, // Sadece 1, 2 ve 3 yıldızlar buraya ulaşabilecek
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

        private void BtnLoadTxt_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Metin Dosyaları (*.txt)|*.txt",
                Title = "Linklerin Bulunduğu Dosyayı Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Dosyadaki linkleri temizleyip listeye alıyoruz
                _targetUrls = File.ReadAllLines(openFileDialog.FileName)
                                  .Where(line => !string.IsNullOrWhiteSpace(line))
                                  .Select(line => line.Trim())
                                  .ToList();

                if (_targetUrls.Count > 0)
                {
                    // Kullanıcının isteği: Dosya seçilir seçilmez sistemi otomatik başlat!
                    // Butona basılmış gibi Start metodunu tetikliyoruz.
                    BtnStart_Click(null, null);
                }
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme; // Durumu tersine çevir

            // 1. Yeni tema dosyasının adını belirle
            string themeFile = _isDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";

            // 2. Uygulama kaynaklarındaki (MergedDictionaries) eski temayı bul ve sil
            var oldTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

            if (oldTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldTheme);
            }

            // 3. Yeni temayı ekle
            ResourceDictionary newTheme = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeFile}", UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Add(newTheme);

            // 4. Butonun üzerindeki yazıyı ve ikonu güncelle
            if (_isDarkTheme)
            {
                TxtThemeIcon.Text = "🌙";
            }
            else
            {
                TxtThemeIcon.Text = "☀";
            }
        }

        // Sidebar - Hasat Merkezi Butonu Tıklanınca
        private void BtnNavScraper_Click(object sender, RoutedEventArgs e)
        {
            PageScraper.Visibility = Visibility.Visible;
            PageCsvManager.Visibility = Visibility.Collapsed;

            // Aktif sayfa hissiyatı için buton renklerini ayarlayabiliriz
            BtnNavScraper.Foreground = (Brush)Application.Current.Resources["SecondaryBrush"];
            BtnNavCsv.Foreground = (Brush)Application.Current.Resources["TextBrush"];
        }

        // Sidebar - CSV Yöneticisi Butonu Tıklanınca
        private void BtnNavCsv_Click(object sender, RoutedEventArgs e)
        {
            PageScraper.Visibility = Visibility.Collapsed;
            PageCsvManager.Visibility = Visibility.Visible;

            // Aktif sayfa hissiyatı
            BtnNavCsv.Foreground = (Brush)Application.Current.Resources["SecondaryBrush"];
            BtnNavScraper.Foreground = (Brush)Application.Current.Resources["TextBrush"];
        }

        private void BtnMergeCsv_Click(object sender, RoutedEventArgs e)
        {
            // 1. Birleştirilecek dosyaları seçtirme (Multiselect aktif!)
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Dosyaları (*.csv)|*.csv",
                Title = "Birleştirilecek CSV Dosyalarını Seçin",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string[] selectedFiles = openFileDialog.FileNames;

                // Kullanıcı yanlışlıkla tek dosya seçtiyse uyar
                if (selectedFiles.Length < 2)
                {
                    MessageBox.Show("Birleştirme işlemi için en az 2 adet CSV dosyası seçmelisiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Birleştirilmiş yeni dosyanın nereye kaydedileceğini sorma
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Dosyası (*.csv)|*.csv",
                    Title = "Birleştirilmiş Master Dosyayı Kaydet",
                    FileName = "Master_Reviews_Dataset.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        // Dosyaları okuyup yeni dosyaya yazma işlemi
                        using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                        {
                            for (int i = 0; i < selectedFiles.Length; i++)
                            {
                                string[] lines = File.ReadAllLines(selectedFiles[i]);

                                // KRİTİK NOKTA: İlk dosya değilse, ilk satırı (Başlıklar: User,Rating,Comment) atla ki araya karışmasın
                                int startLine = (i == 0) ? 0 : 1;

                                for (int j = startLine; j < lines.Length; j++)
                                {
                                    writer.WriteLine(lines[j]);
                                }
                            }
                        }

                        MessageBox.Show($"{selectedFiles.Length} adet CSV dosyası başarıyla tek bir dosyada birleştirildi!\n\nKonum: {saveFileDialog.FileName}",
                                        "İşlem Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Dosyalar birleştirilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}