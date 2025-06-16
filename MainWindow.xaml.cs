using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using Microsoft.Playwright;

namespace WPFPriceScraper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<string, string> ItopyaKategoriler = new Dictionary<string, string>
        {
            { "İşlemci", "https://www.itopya.com/islemci_k8" },
            { "Ekran Kartı", "https://www.itopya.com/ekran-karti_k11" },
            { "Anakart", "https://www.itopya.com/anakart_k9" },
            { "RAM", "https://www.itopya.com/ram_k10" },
            { "SSD", "https://www.itopya.com/ssd_k20" }
        };
        Dictionary<string, string> IncehesapKategoriler = new Dictionary<string, string>
        {
            { "İşlemci", "https://www.incehesap.com/islemci-fiyatlari/" },
            { "Ekran Kartı", "https://www.incehesap.com/ekran-karti-fiyatlari/" },
            { "Anakart", "https://www.incehesap.com/anakart-fiyatlari/" },
            { "RAM", "https://www.incehesap.com/ram-fiyatlari/" },
            { "SSD", "https://www.incehesap.com/ssd-harddisk-fiyatlari/" }
        };

        private List<Product> tumUrunler = new List<Product>();
        private async void UrunleriCekBtn_Click(object sender, RoutedEventArgs e)
        {
            string? kategori = (KategoriBox.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(kategori)) return;

            SonucGrid.ItemsSource = null;
            UrunleriCekBtn.IsEnabled = false;

            ProgressBar1.Visibility = Visibility.Visible;
            ProgressPercentText.Visibility = Visibility.Visible;
            ProgressStepText.Visibility = Visibility.Visible;
            ProgressBar1.Value = 0;
            ProgressPercentText.Text = "0%";
            ProgressStepText.Text = "Ürünler çekiliyor...";

            var urunler = new List<Product>();
            int totalSteps = 2;
            int currentStep = 0;

            // 1. Adım: İtopya Ürünleri
            ProgressStepText.Text = "İtopya ürünleri çekiliyor...";
            var itopyaUrunler = await ItopyaKategoridekiTumUrunlerAsync(ItopyaKategoriler[kategori]);
            urunler.AddRange(itopyaUrunler);

            currentStep++;
            ProgressBar1.Value = (100 * currentStep) / totalSteps;
            ProgressPercentText.Text = $"{ProgressBar1.Value}%";

            // 2. Adım: İncehesap Ürünleri
            ProgressStepText.Text = "İncehesap ürünleri çekiliyor...";
            var incehesapUrunler = await IncehesapKategoridekiTumUrunlerAsync(IncehesapKategoriler[kategori]);
            urunler.AddRange(incehesapUrunler);

            currentStep++;
            ProgressBar1.Value = (100 * currentStep) / totalSteps;
            ProgressPercentText.Text = $"{ProgressBar1.Value}%";

            // Sıralama
            if (SiralamaBox.SelectedIndex == 0)
                urunler = urunler.OrderBy(x => x.PriceValue).ToList();
            else
                urunler = urunler.OrderByDescending(x => x.PriceValue).ToList();

            tumUrunler = urunler;
            SonucGrid.ItemsSource = urunler;

            ProgressStepText.Text = "Tamamlandı!";
            await Task.Delay(1200);

            ProgressBar1.Visibility = Visibility.Collapsed;
            ProgressPercentText.Visibility = Visibility.Collapsed;
            ProgressStepText.Visibility = Visibility.Collapsed;
            UrunleriCekBtn.IsEnabled = true;
        }

        private void AraBtn_Click(object sender, RoutedEventArgs e)
        {
            string aranan = (AramaBox.Text ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(aranan))
            {
                SonucGrid.ItemsSource = tumUrunler;
                return;
            }

            var filtreli = tumUrunler
                .Where(u => u.Name.ToLower().Contains(aranan))
                .ToList();

            SonucGrid.ItemsSource = filtreli;
        }

        private void AramaBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tumUrunler == null || tumUrunler.Count == 0)
                return;

            string aranan = AramaBox.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(aranan))
            {
                SonucGrid.ItemsSource = tumUrunler;
                return;
            }

            // Arama (Ad, Fiyat, Site'de arama yapabilirsin)
            var filtreli = tumUrunler.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(aranan)) ||
                (u.Site != null && u.Site.ToLower().Contains(aranan)) ||
                (u.Price != null && u.Price.ToLower().Contains(aranan))
            ).ToList();

            SonucGrid.ItemsSource = filtreli;
        }

        private void SiralamaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Eğer grid null veya kaynak yoksa veya hiç ürün yoksa fonksiyonu bitir
            if (SonucGrid == null || SonucGrid.ItemsSource == null) return;

            if (SonucGrid.ItemsSource is List<Product> urunler && urunler.Count > 0)
            {
                if (SiralamaBox.SelectedIndex == 0)
                    urunler = urunler.OrderBy(x => x.PriceValue).ToList();
                else
                    urunler = urunler.OrderByDescending(x => x.PriceValue).ToList();

                SonucGrid.ItemsSource = urunler;
            }
        }

        private string NormalizeUrunAdi(string urunAdi)
        {
            // Sadece harf ve rakam bırak, boşlukları tek boşluğa indir
            var temiz = Regex.Replace(urunAdi.ToUpperInvariant(), @"[^\w\d ]", " ");
            temiz = Regex.Replace(temiz, @"\s+", " ").Trim();

            // Marka: ilk kelime (ASUS, MSI, CORSAIR vs)
            var kelimeler = temiz.Split(' ');
            string marka = kelimeler.Length > 0 ? kelimeler[0] : "";
            // Model kodu/anahtar: ilk sayıdan sonraki 1-2 kelime ve varsa rakamlar (örn: "5600X", "16GB", "B550", "980 PRO")
            var kod = Regex.Match(temiz, @"\d{3,6}[A-Z]*").Value;
            // Kapasite/variant: GB/TB/MHz gibi anahtar kelimeler
            var kapasite = Regex.Match(temiz, @"(\d+\s?(GB|TB|MHZ))").Value;

            return $"{marka} {kod} {kapasite}".Trim();
        }

        private List<Product> BenzerUrunleriBul(Product seciliUrun, List<Product> tumUrunler)
        {
            string anahtar = NormalizeUrunAdi(seciliUrun.Name);

            return tumUrunler
                .Where(x =>
                    !string.IsNullOrWhiteSpace(NormalizeUrunAdi(x.Name)) &&
                    NormalizeUrunAdi(x.Name) == anahtar)
                .ToList();
        }


        // Çift tıklama ile link aç
        private void SonucGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SonucGrid.SelectedItem is Product seciliUrun)
            {
                var benzerUrunler = BenzerUrunleriBul(seciliUrun, tumUrunler);

                if (benzerUrunler.Count <= 1)
                {
                    MessageBox.Show("Bu ürün sadece bir sitede bulunuyor.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var fiyatlar = benzerUrunler.Select(x => x.PriceValue).OrderBy(x => x).ToList();
                var min = fiyatlar.First();
                var max = fiyatlar.Last();
                var fark = max - min;
                var yuzde = min > 0 ? fark / min * 100 : 0;

                string mesaj = $"\"{seciliUrun.Name}\"\n\n"
                             + $"{string.Join("\n", benzerUrunler.Select(x => $"{x.Site}: {x.Price}"))}\n\n"
                             + $"Fiyat farkı: {fark:N2} TL (%{yuzde:0})";

                MessageBox.Show(mesaj, "Ürün Fiyat Karşılaştırması", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Ürün ekleme metodunu güncelleyelim
        private bool TryAddProduct(List<Product> products, string name, string url, string price, string site)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(price))
                return false;

            if (products.Any(p => p.Name == name && p.Price == price))
                return false;

            products.Add(new Product
            {
                Name = name,
                Url = url,
                Price = price,
                PriceValue = FiyatiSayisalYap(price), // EKLENDİ
                Site = site
            });

            return true;
        }

        // --- İtopya: Kategorideki Tüm Ürünler ---
        public async Task<List<Product>> ItopyaKategoridekiTumUrunlerAsync(string kategoriUrl)
        {
            var products = new List<Product>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(kategoriUrl);

            // Sayfayı aşağıya kaydırıp ürünlerin yüklenmesini bekle
            int previousHeight = 0;
            while (true)
            {
                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.WaitForTimeoutAsync(1500); // Yüklenmesi için zaman ver

                int currentHeight = await page.EvaluateAsync<int>("() => document.body.scrollHeight");
                if (currentHeight == previousHeight)
                    break; // Daha fazla yükleme yoksa çık
                previousHeight = currentHeight;
            }

            // Ürünleri çek
            var cards = await page.QuerySelectorAllAsync(".product");
            foreach (var card in cards)
            {
                try
                {
                    var name = await card.QuerySelectorAsync("a.title");
                    var price = await card.QuerySelectorAsync("span.product-price strong");
                    var urlElem = await card.QuerySelectorAsync("a.title");

                    if (name == null || price == null || urlElem == null)
                        continue; // bu üründe eksik bilgi var, atla!

                    string isim = await name.InnerTextAsync();
                    string fiyat = await price.InnerTextAsync();
                    string? url = await urlElem.GetAttributeAsync("href");
                    if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("http"))
                        url = "https://www.itopya.com" + url;

                    products.Add(new Product
                    {
                        Name = isim,
                        Price = fiyat,
                        PriceValue = FiyatiSayisalYap(fiyat),
                        Url = url ?? "",
                        Site = "İtopya"
                    });
                }
                catch { }
            }
            return products;
        }

        // --- İncehesap: Kategorideki Tüm Ürünler ---
        public async Task<List<Product>> IncehesapKategoridekiTumUrunlerAsync(string kategoriUrl)
        {
            var products = new List<Product>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(kategoriUrl);

            int toplamSayfa = 1;
            try
            {
                var sayfaLinkleri = await page.QuerySelectorAllAsync("nav[aria-label='Pagination'] a");
                foreach (var link in sayfaLinkleri)
                {
                    var text = await link.InnerTextAsync();
                    if (int.TryParse(text.Trim(), out int n))
                        if (n > toplamSayfa)
                            toplamSayfa = n;
                }
            }
            catch { toplamSayfa = 1; }

            HashSet<string> urlSet = new HashSet<string>();

            for (int s = 1; s <= toplamSayfa; s++)
            {
                string pageUrl = kategoriUrl;
                if (!kategoriUrl.EndsWith("/")) pageUrl += "/";
                if (s > 1)
                    pageUrl = kategoriUrl.TrimEnd('/') + $"/sayfa-{s}/";

                await page.GotoAsync(pageUrl);
                await page.WaitForSelectorAsync("a.product");

                try
                {
                    var allCards = await page.QuerySelectorAllAsync("a.product");
                    foreach (var card in allCards)
                    {
                        try
                        {
                            // data-product JSON'unu oku
                            var dataProduct = await card.GetAttributeAsync("data-product");
                            if (string.IsNullOrEmpty(dataProduct)) continue;

                            var productInfo = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataProduct);

                            if (!productInfo.ContainsKey("category") || productInfo["category"] == null)
                                continue;

                            var kategori = productInfo["category"].ToString().Trim().ToLower();
                            if (kategori != "işlemci")
                                continue;

                            var nameElem = await card.QuerySelectorAsync("div[itemprop='name']");
                            var priceElem = await card.QuerySelectorAsync("span[itemprop='price']");
                            string isim = (await nameElem?.InnerTextAsync())?.Trim() ?? "";
                            string fiyat = (await priceElem?.InnerTextAsync())?.Trim() ?? "";
                            string url = await card.GetAttributeAsync("href");
                            if (!url.StartsWith("http"))
                                url = "https://www.incehesap.com" + url;
                            if (urlSet.Contains(url)) continue;
                            urlSet.Add(url);

                            products.Add(new Product
                            {
                                Name = isim,
                                Price = fiyat,
                                PriceValue = FiyatiSayisalYap(fiyat),
                                Url = url,
                                Site = "İncehesap"
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return products; // FONKSİYONUN EN SONUNDA MUTLAKA BU OLMALI!
        }

        // Fiyatı sayısala çeviren yardımcı fonksiyon
        public decimal FiyatiSayisalYap(string fiyat)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fiyat)) return 0;

                // Sadece rakam, nokta ve virgül bırak, geri kalanı sil
                var temiz = new string(fiyat.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());

                // Eğer fiyat hem nokta hem virgül içeriyorsa, 
                // SON VİRGÜL ondalık ayracı (Türk usulü)
                // Sadece nokta varsa (ve virgül yoksa) ondalık olarak da kullanabiliriz
                // Sadece rakam varsa direkt parse edelim

                // 1. Nokta ve virgül birlikte mi var?
                if (temiz.Contains(",") && temiz.Contains("."))
                {
                    // Son virgülün konumunu bul
                    int sonVirgul = temiz.LastIndexOf(',');
                    string left = temiz.Substring(0, sonVirgul).Replace(".", "").Replace(",", "");
                    string right = temiz.Substring(sonVirgul + 1);
                    temiz = left + "." + right;
                }
                else if (temiz.Contains(","))
                {
                    // Sadece virgül varsa: ondalık olarak kullan
                    temiz = temiz.Replace(".", ""); // binlik varsa sil
                    temiz = temiz.Replace(",", "."); // virgül ondalık olsun
                }
                else if (temiz.Contains("."))
                {
                    // Sadece nokta varsa: ya binliktir ya ondalıktır
                    // Uzunluğu kontrol et, 3'ten büyükse binliktir (Türk usulü)
                    int sonNokta = temiz.LastIndexOf('.');
                    if (temiz.Length - sonNokta == 3)
                    {
                        // Son nokta ondalık
                        string left = temiz.Substring(0, sonNokta).Replace(".", "");
                        string right = temiz.Substring(sonNokta + 1);
                        temiz = left + "." + right;
                    }
                    else
                    {
                        // Tüm noktalar binliktir, sil
                        temiz = temiz.Replace(".", "");
                    }
                }
                // Sadece rakam varsa, direkt parse edilir

                return decimal.Parse(temiz, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }
    }
}

public class Product
{
    public required string Name { get; set; }
    public required string Price { get; set; }
    public required decimal PriceValue { get; set; }
    public required string Url { get; set; }
    public required string Site { get; set; }
}