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
        Dictionary<string, string> GamingGenKategoriler = new Dictionary<string, string>
        {
            { "İşlemci", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/islemci/" },
            { "Ekran Kartı", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ekran-karti/" },
            { "Anakart", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/anakart/" },
            { "RAM", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ram-bellek/" },
            { "SSD", "https://www.gaming.gen.tr/kategori/bilgisayar-bilesenleri/ssd/" }
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
            int siteSayisi = (CheckItopya.IsChecked == true ? 1 : 0) +
                             (CheckIncehesap.IsChecked == true ? 1 : 0) +
                             (CheckGamingGen.IsChecked == true ? 1 : 0);
            int currentStep = 0;

            if (siteSayisi == 0)
            {
                ProgressStepText.Text = "Lütfen en az bir site seçiniz.";
                ProgressBar1.Visibility = Visibility.Collapsed;
                ProgressPercentText.Visibility = Visibility.Collapsed;
                UrunleriCekBtn.IsEnabled = true;
                return;
            }

            if (CheckItopya.IsChecked == true)
            {
                ProgressStepText.Text = "İtopya ürünleri çekiliyor...";
                var ItopyaUrunler = await ItopyaKategoridekiTumUrunlerAsync(ItopyaKategoriler[kategori]);
                urunler.AddRange(ItopyaUrunler);

                currentStep++;
                ProgressBar1.Value = (100 * currentStep) / siteSayisi;
                ProgressPercentText.Text = $"{ProgressBar1.Value}%";
            }

            if (CheckIncehesap.IsChecked == true)
            {
                ProgressStepText.Text = "İncehesap ürünleri çekiliyor...";
                var InceHesapUrunler = await IncehesapKategoridekiTumUrunlerAsync(IncehesapKategoriler[kategori], kategori);
                urunler.AddRange(InceHesapUrunler);

                currentStep++;
                ProgressBar1.Value = (100 * currentStep) / siteSayisi;
                ProgressPercentText.Text = $"{ProgressBar1.Value}%";
            }

            if (CheckGamingGen.IsChecked == true)
            {
                ProgressStepText.Text = "GamingGen ürünleri çekiliyor...";
                var GamingGenUrunler = await GamingGenKategoridekiTumUrunlerAsync(GamingGenKategoriler[kategori], kategori);
                urunler.AddRange(GamingGenUrunler);

                currentStep++;
                ProgressBar1.Value = (100 * currentStep) / siteSayisi;
                ProgressPercentText.Text = $"{ProgressBar1.Value}%";
            }

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

            try
            {
                using var playwright = await Playwright.CreateAsync();
                var browserOptions = new BrowserTypeLaunchOptions
                {
                    Headless = false, // Sorun devam ederse false olarak bırakın
                    Args = new[] 
                    {
                        "--disable-dev-shm-usage",
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-web-security",
                        "--disable-features=IsolateOrigins,site-per-process",
                        "--disable-web-security",
                        "--disable-site-isolation-trials"
                    },
                    Timeout = 120000 // 120 saniye
                };

                await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                // Fix for CS0117: 'BrowserNewContextOptions' does not contain a definition for 'Timeout'
                // The 'Timeout' property is not part of 'BrowserNewContextOptions'. Instead, the timeout should be set in the 'PageGotoOptions' or other relevant methods.

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();
                
                // Sayfa yükleme stratejisi
                try 
                {
                    await page.GotoAsync(kategoriUrl, new PageGotoOptions 
                    { 
                        WaitUntil = WaitUntilState.DOMContentLoaded, // NetworkIdle yerine DOMContentLoaded kullan
                        Timeout = 120000 
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Sayfa yüklenirken ilk hata: {ex.Message}");
                    // İkinci deneme
                    await Task.Delay(5000);
                    await page.GotoAsync(kategoriUrl, new PageGotoOptions 
                    { 
                        WaitUntil = WaitUntilState.Load,
                        Timeout = 120000 
                    });
                }

                await Task.Delay(5000); // Daha uzun bekleme süresi

                // Dinamik yüklenen içerik için scroll - daha yavaş ve kontrollü
                try
                {
                    int previousHeight = 0;
                    int scrollAttempt = 0;
                    const int maxScrollAttempts = 10; // Maximum scroll deneme sayısı

                    while (scrollAttempt < maxScrollAttempts)
                    {
                        await page.EvaluateAsync(@"window.scrollTo({
                            top: document.body.scrollHeight,
                            behavior: 'smooth'
                        });");
                        
                        await Task.Delay(2000); // Scroll sonrası daha uzun bekleme

                        int currentHeight = await page.EvaluateAsync<int>("() => document.body.scrollHeight");
                        if (currentHeight == previousHeight)
                        {
                            // Son scroll'dan sonra biraz daha bekle
                            await Task.Delay(3000);
                            break;
                        }
                        previousHeight = currentHeight;
                        scrollAttempt++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Scroll işleminde hata: {ex.Message}");
                }

                // Ürünlerin yüklenmesini bekle - retry mekanizması ile
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        await page.WaitForSelectorAsync(".product", new PageWaitForSelectorOptions 
                        { 
                            State = WaitForSelectorState.Visible,
                            Timeout = 60000 
                        });
                        break;
                    }
                    catch (TimeoutException) when (retry < 2)
                    {
                        Debug.WriteLine($"Ürünler yüklenirken timeout. Deneme: {retry + 1}");
                        await Task.Delay(5000);
                        continue;
                    }
                }

                var cards = await page.QuerySelectorAllAsync(".product");
                foreach (var card in cards)
                {
                    try
                    {
                        // Her ürün için zaman aşımı kontrolü
                        var name = await card.QuerySelectorAsync("a.title");
                        if (name == null) continue;

                        var price = await card.QuerySelectorAsync("span.product-price strong");
                        if (price == null) continue;

                        var urlElem = await card.QuerySelectorAsync("a.title");
                        if (urlElem == null) continue;

                        string isim = await name.InnerTextAsync();
                        string fiyat = await price.InnerTextAsync();
                        string? url = await urlElem.GetAttributeAsync("href");
                        
                        if (string.IsNullOrWhiteSpace(url)) continue;
                        if (!url.StartsWith("http"))
                            url = "https://www.itopya.com" + url;

                        products.Add(new Product
                        {
                            Name = isim.Trim(),
                            Price = fiyat.Trim(),
                            PriceValue = FiyatiSayisalYap(fiyat),
                            Url = url,
                            Site = "İtopya"
                        });

                        await Task.Delay(100); // Her ürün arası kısa bekleme
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ürün işlenirken hata: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İtopya ürünleri çekilirken hata oluştu: {ex.Message}");
            }

            return products;
        }

        // --- İncehesap: Kategorideki Tüm Ürünler ---
        public async Task<List<Product>> IncehesapKategoridekiTumUrunlerAsync(string kategoriUrl, string seciliKategori)
        {
            var products = new List<Product>();
            var urlSet = new HashSet<string>();

            try
            {
                using var playwright = await Playwright.CreateAsync();
                var browserOptions = new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Args = new[] 
                    {
                        "--disable-dev-shm-usage",
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-web-security",
                        "--disable-features=IsolateOrigins,site-per-process"
                    },
                    Timeout = 60000
                };

                await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();
                
                // İlk sayfaya git
                await page.GotoAsync(kategoriUrl, new PageGotoOptions 
                { 
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000 
                });

                await Task.Delay(2000); // Sayfa tam yüklensin

                // Sayfa sayısını bul
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Sayfa sayısı bulunurken hata: {ex.Message}");
                    toplamSayfa = 1;
                }

                string Normalize(string txt) => txt.ToLower()
                    .Replace("ı", "i")
                    .Replace("ö", "o")
                    .Replace("ü", "u")
                    .Replace("ş", "s")
                    .Replace("ç", "c")
                    .Replace("ğ", "g");

                string kategoriFilter = Normalize(seciliKategori);

                // Her sayfa için
                for (int s = 1; s <= toplamSayfa; s++)
                {
                    try
                    {
                        string pageUrl = kategoriUrl;
                        if (!kategoriUrl.EndsWith("/")) pageUrl += "/";
                        if (s > 1)
                        {
                            pageUrl = kategoriUrl.TrimEnd('/') + $"/sayfa-{s}/";
                            await page.GotoAsync(pageUrl, new PageGotoOptions 
                            { 
                                WaitUntil = WaitUntilState.NetworkIdle,
                                Timeout = 60000 
                            });
                            await Task.Delay(2000);
                        }

                        await page.WaitForSelectorAsync("a.product", new PageWaitForSelectorOptions 
                        { 
                            State = WaitForSelectorState.Visible,
                            Timeout = 30000 
                        });

                        var allCards = await page.QuerySelectorAllAsync("a.product");
                        foreach (var card in allCards)
                        {
                            try
                            {
                                var dataProduct = await card.GetAttributeAsync("data-product");
                                if (string.IsNullOrEmpty(dataProduct)) continue;

                                var productInfo = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataProduct);
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
                                if (string.IsNullOrWhiteSpace(isim) || string.IsNullOrWhiteSpace(fiyat))
                                    continue;

                                var url = await card.GetAttributeAsync("href");
                                if (string.IsNullOrWhiteSpace(url)) continue;
                                
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
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ürün işlenirken hata: {ex.Message}");
                                continue;
                            }
                        }

                        await Task.Delay(1000); // Her sayfa sonrası bekle
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Sayfa {s} işlenirken hata: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İncehesap ürünleri çekilirken hata oluştu: {ex.Message}");
            }

            return products;
        }

        // --- GamingGenTR: Kategorideki Tüm Ürünler ---
        public async Task<List<Product>> GamingGenKategoridekiTumUrunlerAsync(string kategoriUrl, string? seciliKategori = null)
        {
            var products = new List<Product>();
            var urlSet = new HashSet<string>();

            try
            {
                using var playwright = await Playwright.CreateAsync();
                var browserOptions = new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Args = new[] 
                    {
                        "--disable-dev-shm-usage",
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-web-security",
                        "--disable-features=IsolateOrigins,site-per-process"
                    },
                    Timeout = 60000
                };

                await using var browser = await playwright.Chromium.LaunchAsync(browserOptions);
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();
                
                // İlk sayfaya git ve yüklenmesini bekle
                await page.GotoAsync(kategoriUrl, new PageGotoOptions 
                { 
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 40000 
                });

                await Task.Delay(2000); // Sayfa tam yüklensin

                // Sayfa sayısını bul
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
                catch (Exception ex)
                {
                    MessageBox.Show($"Sayfa sayısı bulunurken hata: {ex.Message}");
                    toplamSayfa = 1;
                }

                // Her sayfa için
                for (int s = 1; s <= toplamSayfa; s++)
                {
                    try
                    {
                        string pageUrl = kategoriUrl.TrimEnd('/');
                        if (s > 1)
                        {
                            pageUrl += $"/page/{s}/";
                            await page.GotoAsync(pageUrl, new PageGotoOptions 
                            { 
                                WaitUntil = WaitUntilState.NetworkIdle,
                                Timeout = 60000 
                            });
                            await Task.Delay(2000); // Sayfalar arası bekle
                        }

                        // Ürün listesinin yüklenmesini bekle
                        await page.WaitForSelectorAsync("ul.products li.product", new PageWaitForSelectorOptions 
                        { 
                            State = WaitForSelectorState.Visible,
                            Timeout = 30000 
                        });

                        var cards = await page.QuerySelectorAllAsync("li.product");
                        if (cards == null) continue;

                        foreach (var card in cards)
                        {
                            try
                            {
                                var aTag = await card.QuerySelectorAsync("a.woocommerce-LoopProduct-link");
                                string urunUrl = aTag != null ? await aTag.GetAttributeAsync("href") : null;
                                if (string.IsNullOrWhiteSpace(urunUrl)) continue;

                                if (!urunUrl.StartsWith("http"))
                                    urunUrl = "https://www.gaming.gen.tr" + urunUrl;

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
                                    PriceValue = FiyatiSayisalYap(fiyat),
                                    Url = urunUrl,
                                    Site = "Gaming.gen.tr"
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ürün işlenirken hata: {ex.Message}");
                                continue;
                            }
                        }

                        await Task.Delay(1000); // Her ürün grubu sonrası kısa bekle
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Sayfa {s} işlenirken hata: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gaming.gen.tr ürünleri çekilirken hata oluştu: {ex.Message}");
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