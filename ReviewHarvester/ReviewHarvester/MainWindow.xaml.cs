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

namespace ReviewHarvester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        public ObservableCollection<Review> HarvestedReviews { get; set; } = new ObservableCollection<Review>();

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // 1. Temel Girdi Kontrolleri
            if (string.IsNullOrWhiteSpace(TxtUrl.Text) || !Uri.IsWellFormedUriString(TxtUrl.Text, UriKind.Absolute))
            {
                MessageBox.Show("Lütfen geçerli bir URL girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtXPath.Text))
            {
                MessageBox.Show("XPath alanı boş bırakılamaz!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Hazırlık
            string url = TxtUrl.Text;
            string xpath = TxtXPath.Text;

            HarvestedReviews.Clear(); // Yeni hasat öncesi listeyi temizle
            BtnStart.IsEnabled = false;
            BtnSave.IsEnabled = false;
            PrgStatus.IsIndeterminate = true;
            TxtStatus.Text = "Veriler toplanıyor, lütfen bekleyin...";

            try
            {
                int count = await Task.Run(() => StartScraping(url));

                if (count > 0)
                    TxtStatus.Text = $"Başarılı! {count} adet yorum toplandı.";
                else
                    TxtStatus.Text = "Eşleşme bulunamadı. XPath'i kontrol edin.";
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
                //options.AddArgument("--headless=new");

                // 2. Bot olduğumuzu gizleyen "Ninja" ayarları
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    try
                    {
                        // Sayfayı aç (Sitenin normal açıldığını gördük, yani 403 engeli yok)
                        driver.Navigate().GoToUrl(url);
                        Application.Current.Dispatcher.Invoke(() => TxtStatus.Text = "Sayfa yüklendi, SEO verileri (JSON) aranıyor...");

                        // Sitenin arka planda verileri yerleştirmesi için kısa bir bekleme
                        Thread.Sleep(3000);

                        // Yeni Python projesindeki taktiği kullanıyoruz: Gizli JSON script'ini bul
                        var scriptNodes = driver.FindElements(By.XPath("//script[@type='application/ld+json']"));

                        foreach (var node in scriptNodes)
                        {
                            // Script etiketinin içindeki metni (JSON) alıyoruz
                            string jsonText = node.GetAttribute("innerHTML");

                            // Eğer içinde 'reviewBody' yoksa bu başka bir SEO verisidir, atla
                            if (string.IsNullOrEmpty(jsonText) || !jsonText.Contains("reviewBody")) continue;

                            try
                            {
                                // Newtonsoft.Json ile veriyi parçala
                                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonText);

                                // Python kodunda verinin liste (array) olduğu kontrol edilmiş, biz de aynısını yapıyoruz
                                if (data is Newtonsoft.Json.Linq.JArray)
                                {
                                    foreach (var review in data)
                                    {
                                        // reviewBody anahtarıyla yorum metnini çekiyoruz
                                        string reviewText = review["reviewBody"]?.ToString();
                                        if (string.IsNullOrWhiteSpace(reviewText)) continue;

                                        // reviewRating altından yıldız puanını alıyoruz
                                        int star = review["reviewRating"]?["ratingValue"] != null ? (int)review["reviewRating"]["ratingValue"] : 5;

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            HarvestedReviews.Add(new Review
                                            {
                                                User = "Hepsiburada Kullanıcısı",
                                                Comment = reviewText.Replace('\n', ' ').Trim(), // Satır atlamalarını temizle
                                                Rating = star,
                                                Source = "Hepsiburada"
                                            });
                                        });
                                        count++;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // JSON dönüştürme hatası olursa diğer script'e geç
                                continue;
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
    }
}