using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Playwright;
using System.Text;
using System.Text.RegularExpressions;

namespace WPFPriceScraper
{
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
            var incehesapUrunler = await IncehesapKategoridekiTumUrunlerAsync(IncehesapKategoriler[kategori], kategori);
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

            var filtreli = tumUrunler.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(aranan)) ||
                (u.Site != null && u.Site.ToLower().Contains(aranan)) ||
                (u.Price != null && u.Price.ToLower().Contains(aranan))
            ).ToList();

            SonucGrid.ItemsSource = filtreli;
        }

        private void SiralamaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

        // Çift tıkla ürünü tarayıcıda aç
        private void SonucGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SonucGrid.SelectedItem is Product seciliUrun)
            {
                if (!string.IsNullOrWhiteSpace(seciliUrun.Url))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = seciliUrun.Url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Bağlantı açılamadı: {ex.Message}");
                    }
                }
            }
        }

        // --- İtopya: Kategorideki Tüm Ürünler ---
        public async Task<List<Product>> ItopyaKategoridekiTumUrunlerAsync(string kategoriUrl)
        {
            var products = new List<Product>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(kategoriUrl);

            int previousHeight = 0;
            while (true)
            {
                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.WaitForTimeoutAsync(1500);

                int currentHeight = await page.EvaluateAsync<int>("() => document.body.scrollHeight");
                if (currentHeight == previousHeight)
                    break;
                previousHeight = currentHeight;
            }

            var cards = await page.QuerySelectorAllAsync(".product");
            foreach (var card in cards)
            {
                try
                {
                    var name = await card.QuerySelectorAsync("a.title");
                    var price = await card.QuerySelectorAsync("span.product-price strong");
                    var urlElem = await card.QuerySelectorAsync("a.title");

                    if (name == null || price == null || urlElem == null)
                        continue;

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
        public async Task<List<Product>> IncehesapKategoridekiTumUrunlerAsync(string kategoriUrl, string seciliKategori)
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

            // Seçili kategori ve ürün kategori adlarını normalize eden fonksiyon
            string Normalize(string txt)
            {
                return txt.ToLower()
                    .Replace("ı", "i")
                    .Replace("ö", "o")
                    .Replace("ü", "u")
                    .Replace("ş", "s")
                    .Replace("ç", "c")
                    .Replace("ğ", "g");
            }
            string kategoriFilter = Normalize(seciliKategori);

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
                            var dataProduct = await card.GetAttributeAsync("data-product");
                            if (string.IsNullOrEmpty(dataProduct)) continue;

                            var productInfo = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataProduct);

                            if (!productInfo.ContainsKey("category") || productInfo["category"] == null)
                                continue;

                            var kategori = Normalize(productInfo["category"].ToString().Trim());

                            // Anahtar kategoriler: "ekran karti", "anakart", "islemci", "ram", "ssd"
                            if (!kategori.Contains(kategoriFilter))
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
            return products;
        }

        // Fiyatı sayısala çeviren yardımcı fonksiyon
        public decimal FiyatiSayisalYap(string fiyat)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fiyat)) return 0;

                var temiz = new string(fiyat.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());

                if (temiz.Contains(",") && temiz.Contains("."))
                {
                    int sonVirgul = temiz.LastIndexOf(',');
                    string left = temiz.Substring(0, sonVirgul).Replace(".", "").Replace(",", "");
                    string right = temiz.Substring(sonVirgul + 1);
                    temiz = left + "." + right;
                }
                else if (temiz.Contains(","))
                {
                    temiz = temiz.Replace(".", "");
                    temiz = temiz.Replace(",", ".");
                }
                else if (temiz.Contains("."))
                {
                    int sonNokta = temiz.LastIndexOf('.');
                    if (temiz.Length - sonNokta == 3)
                    {
                        string left = temiz.Substring(0, sonNokta).Replace(".", "");
                        string right = temiz.Substring(sonNokta + 1);
                        temiz = left + "." + right;
                    }
                    else
                    {
                        temiz = temiz.Replace(".", "");
                    }
                }
                return decimal.Parse(temiz, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
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
}