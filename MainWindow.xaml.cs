using Microsoft.Playwright;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Dictionary<string, string> GamingGenKategoriler = new Dictionary<string, string>
        {
            { "İşlemci", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/islemci/" },
            { "Ekran Kartı", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ekran-karti/" },
            { "Anakart", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/anakart/" },
            { "RAM", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ram-bellek/" },
            { "SSD", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ssd/" }
        };

        private List<Product> tumUrunler = new List<Product>();
        private const string CACHE_FOLDER = "cache";
        private const string CACHE_FILE = "products_cache.json";
        public CacheManager cacheManager;

        public MainWindow()
        {
            InitializeComponent();
            cacheManager = new CacheManager(CACHE_FOLDER, CACHE_FILE);
        }

        private static BrowserTypeLaunchOptions GetBrowserOptions()
        {
            return new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
            "--disable-dev-shm-usage",
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-web-security",
            "--disable-features=IsolateOrigins,site-per-process"
                },
                Timeout = 120_000
            };
        }

        // Scraper interface
        public interface ISiteScraper
        {
            string SiteName { get; }
            Task<List<Product>> FetchProductsAsync(string kategoriUrl, string kategoriName);
        }

        // --- Itopya Scraper ---
        public class ItopyaScraper : ISiteScraper
        {
            public string SiteName => "İtopya";

            public async Task<List<Product>> FetchProductsAsync(string kategoriUrl, string kategoriName)
            {
                var products = new List<Product>();
                try
                {
                    using var playwright = await Playwright.CreateAsync();
                    var browserOptions = GetBrowserOptions();
                    await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                    });
                    var page = await context.NewPageAsync();
                    try
                    {
                        await page.GotoAsync(kategoriUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 120000
                        });
                    }
                    catch
                    {
                        await Task.Delay(5000);
                        await page.GotoAsync(kategoriUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.Load,
                            Timeout = 120000
                        });
                    }
                    await Task.Delay(5000);

                    // Scroll
                    int previousHeight = 0, scrollAttempt = 0, maxScrollAttempts = 10;
                    while (scrollAttempt < maxScrollAttempts)
                    {
                        await page.EvaluateAsync(@"window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });");
                        await Task.Delay(2000);
                        int currentHeight = await page.EvaluateAsync<int>("() => document.body.scrollHeight");
                        if (currentHeight == previousHeight) { await Task.Delay(3000); break; }
                        previousHeight = currentHeight; scrollAttempt++;
                    }

                    await page.WaitForSelectorAsync(".product", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 60000 });
                    var cards = await page.QuerySelectorAllAsync(".product");
                    foreach (var card in cards)
                    {
                        try
                        {
                            var name = await card.QuerySelectorAsync("a.title");
                            var price = await card.QuerySelectorAsync("span.product-price strong");
                            var urlElem = await card.QuerySelectorAsync("a.title");
                            if (name == null || price == null || urlElem == null) continue;
                            string isim = await name.InnerTextAsync();
                            string fiyat = await price.InnerTextAsync();
                            string? url = await urlElem.GetAttributeAsync("href");
                            if (string.IsNullOrWhiteSpace(url)) continue;
                            if (!url.StartsWith("http")) url = "https://www.itopya.com" + url;
                            products.Add(new Product
                            {
                                Name = isim.Trim(),
                                Price = fiyat.Trim(),
                                PriceValue = ParsePrice(fiyat),
                                Url = url,
                                Site = "İtopya",
                                LastUpdated = DateTime.Now
                            });
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İtopya ürünleri çekilirken hata oluştu: {ex.Message}");
                }
                return products;
            }
        }

        // --- İncehesap Scraper ---
        public class IncehesapScraper : ISiteScraper
        {
            public string SiteName => "İncehesap";
            public async Task<List<Product>> FetchProductsAsync(string kategoriUrl, string kategoriName)
            {
                var products = new List<Product>();
                var urlSet = new HashSet<string>();
                try
                {
                    using var playwright = await Playwright.CreateAsync();
                    var browserOptions = GetBrowserOptions();
                    await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                    });
                    var page = await context.NewPageAsync();
                    await page.GotoAsync(kategoriUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                    await Task.Delay(2000);

                    int toplamSayfa = 1;
                    try
                    {
                        var sayfaLinkleri = await page.QuerySelectorAllAsync("nav[aria-label='Pagination'] a");
                        foreach (var link in sayfaLinkleri)
                        {
                            var text = await link.InnerTextAsync();
                            if (int.TryParse(text.Trim(), out int n) && n > toplamSayfa)
                                toplamSayfa = n;
                        }
                    }
                    catch { toplamSayfa = 1; }

                    string Normalize(string txt) => txt.ToLower()
                        .Replace("ı", "i").Replace("ö", "o").Replace("ü", "u").Replace("ş", "s").Replace("ç", "c").Replace("ğ", "g");
                    string kategoriFilter = Normalize(kategoriName);

                    for (int s = 1; s <= toplamSayfa; s++)
                    {
                        try
                        {
                            string pageUrl = kategoriUrl;
                            if (!kategoriUrl.EndsWith("/")) pageUrl += "/";
                            if (s > 1)
                            {
                                pageUrl = kategoriUrl.TrimEnd('/') + $"/sayfa-{s}/";
                                await page.GotoAsync(pageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                                await Task.Delay(2000);
                            }
                            await page.WaitForSelectorAsync("a.product", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
                            var allCards = await page.QuerySelectorAllAsync("a.product");
                            foreach (var card in allCards)
                            {
                                try
                                {
                                    var dataProduct = await card.GetAttributeAsync("data-product");
                                    if (string.IsNullOrEmpty(dataProduct)) continue;
                                    var productInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(dataProduct);
                                    if (productInfo == null || !productInfo.ContainsKey("category") || productInfo["category"] == null)
                                        continue;
                                    var kategoriStr = productInfo["category"]?.ToString();
                                    if (string.IsNullOrWhiteSpace(kategoriStr)) continue;
                                    var kategori = Normalize(kategoriStr.Trim());
                                    if (!kategori.Contains(kategoriFilter)) continue;
                                    var nameElem = await card.QuerySelectorAsync("div[itemprop='name']");
                                    var priceElem = await card.QuerySelectorAsync("span[itemprop='price']");
                                    string isim = nameElem != null ? (await nameElem.InnerTextAsync())?.Trim() ?? "" : "";
                                    string fiyat = priceElem != null ? (await priceElem.InnerTextAsync())?.Trim() ?? "" : "";
                                    if (string.IsNullOrWhiteSpace(isim) || string.IsNullOrWhiteSpace(fiyat)) continue;
                                    var url = await card.GetAttributeAsync("href");
                                    if (string.IsNullOrWhiteSpace(url)) continue;
                                    if (!url.StartsWith("http")) url = "https://www.incehesap.com" + url;
                                    if (urlSet.Contains(url)) continue;
                                    urlSet.Add(url);
                                    products.Add(new Product
                                    {
                                        Name = isim,
                                        Price = fiyat,
                                        PriceValue = ParsePrice(fiyat),
                                        Url = url,
                                        Site = "İncehesap",
                                        LastUpdated = DateTime.Now
                                    });
                                }
                                catch { continue; }
                            }
                        }
                        catch { continue; }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İncehesap ürünleri çekilirken hata oluştu: {ex.Message}");
                }
                return products;
            }
        }

        // --- GamingGen Scraper ---
        public class GamingGenScraper : ISiteScraper
        {
            public string SiteName => "Gaming.gen.tr";
            public async Task<List<Product>> FetchProductsAsync(string kategoriUrl, string kategoriName)
            {
                var products = new List<Product>();
                var urlSet = new HashSet<string>();
                try
                {
                    using var playwright = await Playwright.CreateAsync();
                    var browserOptions = GetBrowserOptions();
                    await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                    });
                    var page = await context.NewPageAsync();
                    await page.GotoAsync(kategoriUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                    await Task.Delay(2000);

                    int toplamSayfa = 1;
                    try
                    {
                        var sayfaLinkleri = await page.QuerySelectorAllAsync("ul.page-numbers li a.page-numbers, ul.page-numbers li span.page-numbers");
                        foreach (var link in sayfaLinkleri)
                        {
                            var text = await link.InnerTextAsync();
                            if (int.TryParse(text.Trim(), out int n) && n > toplamSayfa)
                                toplamSayfa = n;
                        }
                    }
                    catch { toplamSayfa = 1; }

                    for (int s = 1; s <= toplamSayfa; s++)
                    {
                        try
                        {
                            string pageUrl = kategoriUrl.TrimEnd('/');
                            if (s > 1)
                            {
                                pageUrl += $"/page/{s}/";
                                await page.GotoAsync(pageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                                await Task.Delay(2000);
                            }
                            await page.WaitForSelectorAsync("ul.products li.product", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
                            var cards = await page.QuerySelectorAllAsync("li.product");
                            if (cards == null) continue;
                            foreach (var card in cards)
                            {
                                try
                                {
                                    var aTag = await card.QuerySelectorAsync("a.woocommerce-LoopProduct-link");
                                    string urunUrl = aTag != null ? await aTag.GetAttributeAsync("href") : null;
                                    if (string.IsNullOrWhiteSpace(urunUrl)) continue;
                                    if (!urunUrl.StartsWith("http")) urunUrl = "https://www.gaming.gen.tr" + urunUrl;
                                    if (urlSet.Contains(urunUrl)) continue;
                                    urlSet.Add(urunUrl);
                                    var titleElem = await card.QuerySelectorAsync("h2.woocommerce-loop-product__title");
                                    string urunAdi = titleElem != null ? await titleElem.InnerTextAsync() : "";
                                    if (string.IsNullOrWhiteSpace(urunAdi)) continue;
                                    var priceElem = await card.QuerySelectorAsync("span.price > span.woocommerce-Price-amount");
                                    string fiyat = "";
                                    if (priceElem != null)
                                    {
                                        var bdi = await priceElem.QuerySelectorAsync("bdi");
                                        if (bdi != null)
                                            fiyat = await bdi.InnerTextAsync();
                                    }
                                    if (string.IsNullOrWhiteSpace(fiyat)) continue;
                                    products.Add(new Product
                                    {
                                        Name = urunAdi.Trim(),
                                        Price = fiyat.Trim(),
                                        PriceValue = ParsePrice(fiyat),
                                        Url = urunUrl,
                                        Site = "Gaming.gen.tr",
                                        LastUpdated = DateTime.Now
                                    });
                                }
                                catch { continue; }
                            }
                        }
                        catch { continue; }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gaming.gen.tr ürünleri çekilirken hata oluştu: {ex.Message}");
                }
                return products;
            }
        }

        // ---------- YENİ MODÜLER SCRAPER KULLANIMI ----------
        private async Task<List<Product>> TumSitelerdenUrunCek(string kategori, string kategoriAdi)
        {
            var scrapers = new List<ISiteScraper>();
            if (CheckItopya.IsChecked == true) scrapers.Add(new ItopyaScraper());
            if (CheckIncehesap.IsChecked == true) scrapers.Add(new IncehesapScraper());
            if (CheckGamingGen.IsChecked == true) scrapers.Add(new GamingGenScraper());

            var urunler = new List<Product>();
            int siteSayisi = scrapers.Count;
            int currentStep = 0;

            foreach (var scraper in scrapers)
            {
                ProgressStepText.Text = $"{scraper.SiteName} ürünleri çekiliyor...";
                string url = scraper.SiteName switch
                {
                    "İtopya" => ItopyaKategoriler[kategori],
                    "İncehesap" => IncehesapKategoriler[kategori],
                    "Gaming.gen.tr" => GamingGenKategoriler[kategori],
                    _ => null
                };
                if (url != null)
                {
                    var siteProducts = await scraper.FetchProductsAsync(url, kategori);
                    urunler.AddRange(siteProducts);
                }
                UpdateProgress(ref currentStep, siteSayisi);
            }
            return urunler;
        }

        // --------- GUNCELLE BUTTON KODUNDA KULLANIMI ----------
        private async void GuncelleBtn_Click(object sender, RoutedEventArgs e)
        {
            string? kategori = (KategoriBox.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(kategori)) return;

            SonucGrid.ItemsSource = null;
            GuncelleBtn.IsEnabled = false;
            GosterBtn.IsEnabled = false;
            SetupProgressUI();

            var urunler = await TumSitelerdenUrunCek(kategori, kategori);

            string cacheKey = GenerateCacheKey(kategori);
            await cacheManager.SaveAsync(cacheKey, urunler);

            tumUrunler = urunler;
            ApplySorting();
            SonucGrid.ItemsSource = tumUrunler;

            await CleanupUI();
            GuncelleBtn.IsEnabled = true;
            GosterBtn.IsEnabled = true;
        }

        private async void GosterBtn_Click(object sender, RoutedEventArgs e)
        {
            string? kategori = (KategoriBox.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(kategori)) return;

            string cacheKey = GenerateCacheKey(kategori);
            (bool exists, List<Product>? cachedProducts) = await cacheManager.GetFromCacheAsync(cacheKey);
            if (!exists || cachedProducts == null)
            {
                MessageBox.Show("Bu kategori için önbelleğe alınmış veri bulunamadı.\nLütfen 'Güncelle' butonunu kullanarak verileri çekin.",
                    "Veri Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            tumUrunler = cachedProducts;
            ApplySorting();
            SonucGrid.ItemsSource = tumUrunler;
        }

        // ARAMA
        private void AramaBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tumUrunler == null || tumUrunler.Count == 0) return;
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

        // SIRALAMA
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

        // ÇİFT TIKLA ÜRÜNÜ AÇ
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

        // --------- DIGER (KISA OLAN) YARDIMCILAR DEGISEMEDI, AYNI KALDI ---------

        private string GenerateCacheKey(string kategori)
        {
            var sites = new StringBuilder(kategori);
            if (CheckItopya.IsChecked == true) sites.Append("_itopya");
            if (CheckIncehesap.IsChecked == true) sites.Append("_incehesap");
            if (CheckGamingGen.IsChecked == true) sites.Append("_gaminggen");
            return sites.ToString();
        }

        private void SetupProgressUI()
        {
            ProgressBar1.Visibility = Visibility.Visible;
            ProgressPercentText.Visibility = Visibility.Visible;
            ProgressStepText.Visibility = Visibility.Visible;
            ProgressBar1.Value = 0;
            ProgressPercentText.Text = "0%";
            ProgressStepText.Text = "Ürünler çekiliyor...";
        }

        private void ApplySorting()
        {
            if (tumUrunler == null || !tumUrunler.Any()) return;
            tumUrunler = SiralamaBox.SelectedIndex == 0
                ? tumUrunler.OrderBy(x => x.PriceValue).ToList()
                : tumUrunler.OrderByDescending(x => x.PriceValue).ToList();
        }

        private async Task CleanupUI()
        {
            ProgressStepText.Text = "Tamamlandı!";
            await Task.Delay(1200);
            ProgressBar1.Visibility = Visibility.Collapsed;
            ProgressPercentText.Visibility = Visibility.Collapsed;
            ProgressStepText.Visibility = Visibility.Collapsed;
        }

        private void UpdateProgress(ref int currentStep, int totalSteps)
        {
            currentStep++;
            ProgressBar1.Value = (100 * currentStep) / totalSteps;
            ProgressPercentText.Text = $"{ProgressBar1.Value}%";
        }

        // ------------------- YARDIMCI PARSEPRICE -----------------------
        public static decimal ParsePrice(string rawPrice)
        {
            if (string.IsNullOrWhiteSpace(rawPrice))
                return 0;
            var clean = new string(rawPrice.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
            if (clean.Contains(",") && clean.Contains("."))
            {
                if (clean.LastIndexOf(',') > clean.LastIndexOf('.'))
                {
                    clean = clean.Replace(".", "").Replace(",", ".");
                }
                else
                {
                    clean = clean.Replace(",", "");
                }
            }
            else if (clean.Contains(","))
            {
                clean = clean.Replace(".", "").Replace(",", ".");
            }
            else if (clean.Count(c => c == '.') == 1 && clean.Length - clean.LastIndexOf('.') <= 3)
            {
                // Do nothing
            }
            else
            {
                clean = clean.Replace(".", "");
            }
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        // ... diğer kodların değişmeden devamı (CacheManager, Product vs) ...
    }

    public class CacheManager
    {
        private readonly string cacheFolder;
        private readonly string cacheFile;
        private readonly JsonSerializerOptions jsonOptions;
        public CacheManager(string folder, string file)
        {
            cacheFolder = folder;
            cacheFile = file;
            jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        public async Task SaveAsync(string key, List<Product> products)
        {
            var path = Path.Combine(cacheFolder, cacheFile);
            var cacheData = await LoadCacheDataAsync();
            cacheData.Products[key] = products;
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cacheData, jsonOptions));
        }

        public async Task<(bool exists, List<Product>? products)> GetFromCacheAsync(string key)
        {
            try
            {
                var path = Path.Combine(cacheFolder, cacheFile);
                if (!File.Exists(path))
                    return (false, null);

                var cacheData = await LoadCacheDataAsync();
                if (cacheData.Products.TryGetValue(key, out var products))
                {
                    // Check if cache is older than 24 hours
                    if (products.Any() && DateTime.Now.Subtract(products[0].LastUpdated).TotalHours > 24)
                        return (false, null);
                        
                    return (true, products);
                }
                return (false, null);
            }
            catch (Exception)
            {
                return (false, null);
            }
        }

        private async Task<CacheData> LoadCacheDataAsync()
        {
            var path = Path.Combine(cacheFolder, cacheFile);
            if (!File.Exists(path)) return new CacheData();
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<CacheData>(json, jsonOptions) ?? new CacheData();
        }
    }

    public class CacheData
    {
        public Dictionary<string, List<Product>> Products { get; set; } = new();
    }

    public class Product : INotifyPropertyChanged
    {
        private string name;
        private string price;
        private decimal priceValue;
        private string url;
        private string site;
        private DateTime lastUpdated;

        public string Name
        {
            get => name;
            set { if (name != value) { name = value; OnPropertyChanged(nameof(Name)); } }
        }
        public string Price
        {
            get => price;
            set { if (price != value) { price = value; OnPropertyChanged(nameof(Price)); } }
        }
        public decimal PriceValue
        {
            get => priceValue;
            set { if (priceValue != value) { priceValue = value; OnPropertyChanged(nameof(PriceValue)); } }
        }
        public string Url
        {
            get => url;
            set { if (url != value) { url = value; OnPropertyChanged(nameof(Url)); } }
        }
        public string Site
        {
            get => site;
            set { if (site != value) { site = value; OnPropertyChanged(nameof(Site)); } }
        }
        public DateTime LastUpdated
        {
            get => lastUpdated;
            set { if (lastUpdated != value) { lastUpdated = value; OnPropertyChanged(nameof(LastUpdated)); } }
        }

        // (Opsiyonel) Karşılaştırma listesi için ek alan
        private bool isInComparison;
        public bool IsInComparison
        {
            get => isInComparison;
            set { if (isInComparison != value) { isInComparison = value; OnPropertyChanged(nameof(IsInComparison)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
