using CsvHelper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ReviewHarvester
{
    /// <summary>
    /// Interaction logic for CsvManagerWindow.xaml
    /// </summary>
    public partial class CsvManagerWindow : Window
    {
        private List<Review> _mergedReviews = new List<Review>();

        public CsvManagerWindow()
        {
            InitializeComponent();
        }

        private void BtnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV dosyaları (*.csv)|*.csv",
                Multiselect = true, // Birden fazla dosya seçimine izin ver
                Title = "Birleştirilecek CSV Dosyalarını Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _mergedReviews.Clear();

                foreach (string filePath in openFileDialog.FileNames)
                {
                    try
                    {
                        using (var reader = new StreamReader(filePath))
                        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                        {
                            var records = csv.GetRecords<Review>().ToList();
                            _mergedReviews.AddRange(records);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"{System.IO.Path.GetFileName(filePath)} okunurken hata: {ex.Message}");
                    }
                }

                // Önizleme için DataGrid'e bağla
                DgdPreview.ItemsSource = null;
                DgdPreview.ItemsSource = _mergedReviews;

                TxtInfo.Text = $"{openFileDialog.FileNames.Length} dosya okundu. Toplam {_mergedReviews.Count} yorum.";
                BtnMergeCsv.IsEnabled = _mergedReviews.Count > 0;
            }
        }

        private void BtnMergeCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = "master_reviews_dataset.csv",
                Title = "Birleştirilmiş Veri Setini Kaydet"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(_mergedReviews);
                }
                MessageBox.Show("Tüm CSV dosyaları başarıyla tek bir dosyada birleştirildi!", "Hasat Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
