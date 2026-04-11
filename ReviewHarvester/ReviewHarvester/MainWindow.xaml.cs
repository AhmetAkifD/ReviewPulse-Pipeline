using CsvHelper;
using HtmlAgilityPack; // HTML parsing için (Gelecekte gerekebilir)
using Microsoft.Win32;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using ReviewHarvester.Scrapers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq; // LINQ işlemleri için kritik
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReviewHarvester
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Review> HarvestedReviews { get; set; } = new ObservableCollection<Review>();
        private List<string> _targetUrls = new List<string>();
        private CancellationTokenSource _cts;

        // Tema takibi için değişken
        private bool _isDarkTheme = true;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        // ==========================================
        // 1. SIDEBAR MENÜ VE NAVİGASYON (SPA MANTIĞI)
        // ==========================================
        private void BtnNavScraper_Click(object sender, RoutedEventArgs e)
        {
            PageScraper.Visibility = Visibility.Visible;
            PageCsvManager.Visibility = Visibility.Collapsed;

            BtnNavScraper.Foreground = (Brush)Application.Current.Resources["SecondaryBrush"];
            BtnNavCsv.Foreground = (Brush)Application.Current.Resources["TextBrush"];
        }

        private void BtnNavCsv_Click(object sender, RoutedEventArgs e)
        {
            PageScraper.Visibility = Visibility.Collapsed;
            PageCsvManager.Visibility = Visibility.Visible;

            BtnNavCsv.Foreground = (Brush)Application.Current.Resources["SecondaryBrush"];
            BtnNavScraper.Foreground = (Brush)Application.Current.Resources["TextBrush"];
        }

        // ==========================================
        // 2. TEMA SİSTEMİ
        // ==========================================
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;

            string themeFile = _isDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";

            var oldTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

            if (oldTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldTheme);
            }

            ResourceDictionary newTheme = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeFile}", UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Add(newTheme);

            if (_isDarkTheme)
            {
                TxtThemeIcon.Text = "🌙";
            }
            else
            {
                TxtThemeIcon.Text = "☀";
            }
        }

        // ==========================================
        // 3. FİLTRELEME HIZLI SEÇİM BUTONLARI
        // ==========================================
        private void BtnSelectNegative_Click(object sender, RoutedEventArgs e)
        {
            Chk1.IsChecked = Chk2.IsChecked = Chk3.IsChecked = true;
            Chk4.IsChecked = Chk5.IsChecked = false;
        }

        private void BtnSelectPositive_Click(object sender, RoutedEventArgs e)
        {
            Chk1.IsChecked = Chk2.IsChecked = Chk3.IsChecked = false;
            Chk4.IsChecked = Chk5.IsChecked = true;
        }

        // ==========================================
        // 4. HASAT BAŞLATMA VE ARAYÜZ KONTROLÜ
        // ==========================================
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
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

            // --- ARAYÜZÜ KİLİTLE (UI LOCKDOWN) - TEKRARLARDAN TEMİZLENDİ ---
            BtnStart.IsEnabled = false;
            BtnSave.IsEnabled = false;
            BtnSaveJson.IsEnabled = false;
            BtnLoadTxt.IsEnabled = false;
            TxtUrl.IsEnabled = false;
            BtnThemeToggle.IsEnabled = false;
            BtnNavScraper.IsEnabled = false;
            BtnNavCsv.IsEnabled = false;
            RdbNormal.IsEnabled = false;
            RdbHuman.IsEnabled = false;

            _cts = new CancellationTokenSource();
            BtnStop.IsEnabled = true; // SADECE DURDUR BUTONU AKTİF!
                                      // ---------------------------------------------------------------

            int totalCollectedCount = 0;

            // Kullanıcının seçtiği hızı hesapla
            int delayMs = 2000; // Varsayılan (Normal)
            Application.Current.Dispatcher.Invoke(() => {
                if (RdbHuman.IsChecked == true)
                {
                    delayMs = 4000;
                }
            });

            // UI'daki CheckBox'ları oku
            List<int> allowedStars = new List<int>();
            if (Chk1.IsChecked == true) allowedStars.Add(1);
            if (Chk2.IsChecked == true) allowedStars.Add(2);
            if (Chk3.IsChecked == true) allowedStars.Add(3);
            if (Chk4.IsChecked == true) allowedStars.Add(4);
            if (Chk5.IsChecked == true) allowedStars.Add(5);

            if (allowedStars.Count == 0) allowedStars = new List<int> { 1, 2, 3, 4, 5 };
            allowedStars = allowedStars.OrderByDescending(x => x).ToList();

            try
            {
                for (int i = 0; i < _targetUrls.Count; i++)
                {
                    string currentUrl = _targetUrls[i];
                    if (!Uri.IsWellFormedUriString(currentUrl, UriKind.Absolute)) continue;

                    TxtUrl.Text = currentUrl;
                    PrgStatus.IsIndeterminate = true;

                    // 1. DOĞRU MOTORU (SCRAPER) AL
                    IReviewScraper scraper = ScraperFactory.GetScraper(currentUrl);

                    // 2. HASADI BAŞLAT
                    int countFromThisUrl = await scraper.ScrapeAsync(currentUrl, allowedStars,
                        review =>
                        {
                            // İŞTE KRİTİK DÜZELTME: İki işlemi de süslü parantez içine aldık!
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                HarvestedReviews.Add(review);
                                LstReviews.ScrollIntoView(review);
                            });
                        },
                        statusMessage =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                TxtStatus.Text = $"{statusMessage} Toplanan: {totalCollectedCount}";
                            });
                        },
                        _cts.Token,
                        delayMs
                    );

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
                // --- ARAYÜZ KİLİDİNİ AÇ ---
                BtnStart.IsEnabled = true;
                BtnSave.IsEnabled = true;
                BtnSaveJson.IsEnabled = true;
                BtnLoadTxt.IsEnabled = true;
                TxtUrl.IsEnabled = true;
                BtnThemeToggle.IsEnabled = true;
                BtnNavScraper.IsEnabled = true;
                BtnNavCsv.IsEnabled = true;
                RdbNormal.IsEnabled = true;
                RdbHuman.IsEnabled = true;

                PrgStatus.IsIndeterminate = false;
                _targetUrls.Clear();

                BtnStop.IsEnabled = false;
                _cts?.Dispose();
            }
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
                _targetUrls = File.ReadAllLines(openFileDialog.FileName)
                                  .Where(line => !string.IsNullOrWhiteSpace(line))
                                  .Select(line => line.Trim())
                                  .ToList();

                if (_targetUrls.Count > 0)
                {
                    BtnStart_Click(null, null);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (HarvestedReviews.Count == 0)
            {
                MessageBox.Show("Kaydedilecek veri bulunamadı. Önce hasat yapmalısınız!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // --- 1. AKILLI İSİMLENDİRME MOTORU ---
            // Hangi yıldızların seçili olduğunu buluyoruz
            List<string> selectedStars = new List<string>();
            if (Chk1.IsChecked == true) selectedStars.Add("1");
            if (Chk2.IsChecked == true) selectedStars.Add("2");
            if (Chk3.IsChecked == true) selectedStars.Add("3");
            if (Chk4.IsChecked == true) selectedStars.Add("4");
            if (Chk5.IsChecked == true) selectedStars.Add("5");

            // Eğer hepsi seçiliyse "Tumu", değilse aralarına tire koy (Örn: 1-2-3)
            string starsText = selectedStars.Count == 5 ? "Tumu" : string.Join("-", selectedStars);

            // Günün tarihini alıyoruz (Karışmaması için YılAyGün formatında)
            string dateText = DateTime.Now.ToString("yyyyMMdd");

            // Örnek Çıktı: 1540_Yorum_1-2-3_Yildiz_20260403.csv
            string smartFileName = $"{HarvestedReviews.Count}_Yorum_{starsText}_Yildiz_{dateText}.csv";
            // ---------------------------------------

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = smartFileName // Akıllı ismimizi varsayılan olarak atıyoruz!
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
                    MessageBox.Show($"Veriler başarıyla kaydedildi!\n\nDosya: {saveFileDialog.SafeFileName}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (IOException)
            {
                MessageBox.Show("Dosya başka bir program (örn: Excel) tarafından kullanılıyor. Lütfen kapatıp tekrar deneyin.", "Dosya Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 5. CSV YÖNETİCİSİ (BİRLEŞTİRME MOTORU)
        // ==========================================
        private void BtnMergeCsv_Click(object sender, RoutedEventArgs e)
        {
            // 1. Dosya Seçimi
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Dosyaları (*.csv)|*.csv",
                Title = "Birleştirilecek CSV Dosyalarını Seçin",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string[] selectedFiles = openFileDialog.FileNames;

                if (selectedFiles.Length < 2)
                {
                    MessageBox.Show("Birleştirme işlemi için en az 2 adet CSV dosyası seçmelisiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // --- AKILLI İSİMLENDİRME İÇİN ÖN SAYIM ---
                int totalRows = 0;
                foreach (var file in selectedFiles)
                {
                    // Dosyadaki satır sayısını al ve başlık satırını (-1) çıkar
                    int lineCount = File.ReadLines(file).Count();
                    totalRows += (lineCount > 0) ? (lineCount - 1) : 0;
                }

                string dateText = DateTime.Now.ToString("yyyyMMdd");
                // Örnek: Master_5400_Yorum_Birlesik_20260403.csv
                string smartMasterName = $"Master_{totalRows}_Yorum_Birlesik_{dateText}.csv";
                // ----------------------------------------

                // 2. Kayıt Dosyası Hazırlığı
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Dosyası (*.csv)|*.csv",
                    Title = "Birleştirilmiş Master Dosyayı Kaydet",
                    FileName = smartMasterName // Akıllı ismimizi buraya verdik
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                        {
                            for (int i = 0; i < selectedFiles.Length; i++)
                            {
                                string[] lines = File.ReadAllLines(selectedFiles[i]);
                                int startLine = (i == 0) ? 0 : 1;

                                for (int j = startLine; j < lines.Length; j++)
                                {
                                    writer.WriteLine(lines[j]);
                                }
                            }
                        }

                        MessageBox.Show($"{selectedFiles.Length} adet dosya birleştirildi!\nToplam Veri: {totalRows} yorum.",
                                        "İşlem Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel(); // İptal sinyalini ateşle!
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Durduruluyor... Lütfen mevcut sayfanın bitmesini bekleyin.";
        }

        private void BtnSaveJson_Click(object sender, RoutedEventArgs e)
        {
            if (HarvestedReviews.Count == 0)
            {
                MessageBox.Show("Kaydedilecek veri yok. Önce hasadı başlatın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- AKILLI İSİMLENDİRME MOTORU (JSON İÇİN) ---
            List<string> selectedStars = new List<string>();
            if (Chk1.IsChecked == true) selectedStars.Add("1");
            if (Chk2.IsChecked == true) selectedStars.Add("2");
            if (Chk3.IsChecked == true) selectedStars.Add("3");
            if (Chk4.IsChecked == true) selectedStars.Add("4");
            if (Chk5.IsChecked == true) selectedStars.Add("5");

            string starsText = selectedStars.Count == 5 ? "Tumu" : string.Join("-", selectedStars);
            string dateText = DateTime.Now.ToString("yyyyMMdd");

            // Örnek Çıktı: 1540_Yorum_1-2-3_Yildiz_20260405.json
            string smartFileName = $"{HarvestedReviews.Count}_Yorum_{starsText}_Yildiz_{dateText}.json";
            // ----------------------------------------------

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = smartFileName,
                DefaultExt = ".json",
                Filter = "JSON Dosyaları (*.json)|*.json"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string jsonOutput = JsonConvert.SerializeObject(HarvestedReviews, Formatting.Indented);
                    System.IO.File.WriteAllText(dlg.FileName, jsonOutput);

                    MessageBox.Show($"JSON dosyası başarıyla kaydedildi!\n\nDosya: {dlg.SafeFileName}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kayıt sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}