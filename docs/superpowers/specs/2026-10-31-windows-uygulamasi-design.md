# GameTranslator Windows uygulaması — tasarım

Üst tasarım: `2026-10-31-oyun-altyazi-uygulamasi-design.md`. Görünüm: `docs/TASARIM.md` + `design/tokens.json`.

## Amaç

Mac uygulamasının yaptığı her şeyi Windows 10/11 (x64) bilgisayarda aynı görünüm ve aynı kurulum
akışıyla yapmak: ikinci PSN hesabıyla Remote Play'e bağlanır, altyazı bölgesini okur, Türkçeye çevirir,
LG TV uygulamasına gönderir. Kullanıcı bir zip/kurulum dosyası indirip çalıştırır; komut satırı yok.

Hedef dışı: ARM Windows, Windows 7/8, ses, kol girdisi, mağaza (Microsoft Store) yayını bu aşamada.

## Davranış eşitliği

Mac sürümündeki kurallar aynen geçerlidir ve aynı testlerle korunur:
kararlılık (noktalamayla biten satır ilk karede, yarım satır iki karede; 4 boş karede temizleme),
çevirilerin sıra numarasıyla gösterimi (yeni bir çeviri gösterildiyse eskisi atlanır, ekrandan kalkmış
satır okunma süresi kadar gösterilir), saniyede en çok 15 kare, boş bölgede şerit + büyütme yedek okuması,
TV'yi açık tutma (TVKeeper), PS5'i uyandırma, kopunca 2–30 sn aralıklarla yeniden bağlanma,
PS5 kayıt red nedenlerinin (yanlış hesap) açık mesajla gösterilmesi.

## Mimari (kararlar)

```
windows/
  GameTranslator.Windows.sln
  src/GameTranslator.Core           net10.0 — platformdan bağımsız mantık (Mac'teki core + AppModel karşılığı)
  src/GameTranslator.Translation    net10.0 — OPUS-MT en→tr, ONNX Runtime ile cihazda çeviri
  src/GameTranslator.RemotePlay     net10.0 — yerel kütüphaneye P/Invoke + PSN hesap kimliği
  src/GameTranslator.App            net10.0-windows10.0.19041.0, WPF — arayüz, Windows OCR, WebView2 girişi
  tests/*                      xUnit; Core ve Translation testleri Mac'te de koşar
  native/                      gametranslator_native.c (libchiaki + FFmpeg H.264 çözücü üstünde düz C arayüzü)
.github/workflows/windows.yml  yerel DLL'i MSYS2 ile derler, testleri koşar, uygulamayı paketler
```

- **Arayüz: WPF (.NET 10).** WinUI 3 yerine WPF: Mac'te derlenebilir (EnableWindowsTargeting), kurulum
  gerektiren çalışma zamanı yok, görünüm tamamen tokens.json'dan geldiği için Fluent bileşenlerine
  ihtiyaç yok. Yazı tipi Segoe UI Variable (yoksa Segoe UI).
- **Remote Play: libchiaki + ince C katmanı.** C#'tan libchiaki yapılarına doğrudan erişmek kırılgan
  olduğu için `gametranslator_native.dll` düz bir arayüz sunar: PS5 arama, uyandırma, kayıt (red nedeniyle),
  oturum başlat/durdur; oturum, H.264'ü FFmpeg ile çözüp BGRA kareyi en çok 15 fps geri çağırır.
  MSYS2 mingw64 ile statik derlenir (chiaki-ng'nin kendi Windows derlemesiyle aynı araç zinciri).
  FFmpeg yalnız h264 çözücü + swscale ile, LGPL olarak kaynaktan derlenir.
- **PS5 arama:** hedef adresler C#'ta bağlı IPv4 ağlarından hesaplanır (Mac'teki /22 kuralı) ve
  genel yayın da eklenir.
- **Yazı tanıma: Windows.Media.Ocr.** Önce oyun dili (en-US), yoksa kullanıcının yüklü Latin alfabeli
  OCR dili (ör. tr-TR) kullanılır. Hiçbiri yoksa arayüz İngilizce OCR'nin nasıl ekleneceğini söyler.
  Kısa satırlar için Mac'teki şerit + büyütme yedek okuması aynı.
- **Çeviri: Helsinki-NLP opus-mt-tc-big-en-tr (CC BY 4.0), ONNX int8** (onnx-community dönüşümü, sabit sürüm).
  Satırlar cümle cümle çevrilir (bütün olarak çevrilince model cümle atlıyordu); büyük harfli konuşmacı adları korunur. Kodlayıcı/kod çözücü ONNX
  Runtime ile, SentencePiece belirteçleme Microsoft.ML.Tokenizers ile, açgözlü kod çözme (KV önbellekli).
  Model (~534 MB) ilk kurulumda sihirbazdaki "Çeviri modelini indir" adımıyla indirilir ve
  `%LOCALAPPDATA%\GameTranslator\models` altına konur; özet (SHA-256) doğrulanır. Çeviri önbelleği Mac'teki gibi.
  Diğer oyun dilleri (ja/de/fr/es) için aynı aileden modeller sonradan eklenebilir; ilk sürüm en→tr.
- **PSN girişi: WebView2** (Windows 10/11'de yüklü gelir). Yönlendirme adresi yakalanır, token saklanmaz.
- **Saklama:** hesap kimliği, PS5 anahtarları ve TV anahtarı DPAPI (CurrentUser) ile şifrelenmiş dosyada;
  ayarlar JSON dosyasında (`%LOCALAPPDATA%\GameTranslator`).
- **Metinler:** Mac'in `Localizable.xcstrings` dosyası uygulamaya gömülür ve doğrudan okunur; iki
  uygulamanın metinleri tek kaynaktan gelir. Windows'a özgü yeni metinler de bu dosyaya eklenir.
- **Tasarım değerleri:** `design/tokens.json` gömülür, açık/koyu tema Windows ayarını izler; kontrast
  testleri burada da koşar.
- **Güvenlik duvarı:** Altyazı sunucusu (8787) ve PS5 bağlantısı için ilk açılışta Windows'un ağ izni
  penceresi çıkar; sihirbazda bunun onaylanması gerektiği söylenir.
- **Uyku:** Çeviri sürerken `SetThreadExecutionState` ile uyku engellenir; uyanınca bağlantılar yenilenir.

## Doğrulama

- Core ve Translation testleri Mac'te `dotnet test` ile; App ve yerel DLL GitHub Actions Windows
  makinesinde derlenir, tüm testler orada da koşar, uygulama zip'i yapıt olarak yüklenir.
- Gerçek uçtan uca deneme (PS5 + TV) bir Windows bilgisayarda kullanıcıyla yapılır.

## Kararları bana ait olanlar

WPF seçimi, OPUS-MT modeli ve indirme yeri, MSYS2 + statik DLL, OCR dili sırası, DPAPI ile saklama.
