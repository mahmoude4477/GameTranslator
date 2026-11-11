# 🏛️ GameTranslator v1.0 Pro — Detaylı Sistem Mimarisi & Geliştirici Kılavuzu

Bu belge, **GameTranslator** ekosisteminin düşük seviyeli (low-level) çalışma prensiplerini, donanım hızlandırma boru hatlarını, ağ protokollerini ve altyazı kararlılık algoritmalarını detaylandırmak üzere geliştiriciler için hazırlanmıştır.

---

## 1. Veri Akışı ve Donanım Boru Hattı (Data Pipeline)

```
[ PlayStation 5 ]
       │
       ├──> (HDMI 2.1) ──────────────────────────────────────────> [ TV Ekranı ] (4K 120Hz HDR)
       │                                                                  ▲
       └──> (UDP Takion 9295)                                             │
                  │                                                       │
                  ▼                                                       │
     [ Chiaki Akış Yakalayıcı ]                                           │
                  │ (Ham H.264/HEVC NAL Paketleri)                        │
                  ▼                                                       │
     [ VideoToolbox / Direct3D ] (Donanım Çözücü)                         │
                  │ (CVPixelBufferRef / 1080p 60 FPS)                     │
                  ▼                                                       │
     [ RegionCropper & Signature ]                                        │
                  │ (Piksel İmza Değişimi Var mı?)                        │
                  ├──[Hayır]──> (Kareyi Atla - 0% CPU)                   │
                  │                                                       │
                  └──[Evet]───> [ Apple Vision / DirectML OCR ]           │
                                       │ (Ham Metin + Koordinat Kutusu)   │
                                       ▼                                  │
                                [ Gürültü & Buton Filtresi ]              │
                                       │ (L2, R2, X, Çöp Karakter Temizliği)
                                       ▼                                  │
                                [ SubtitleStabilizer ]                    │
                                       │ (Titreme Engelleme / 6 Boş Kare) │
                                       ▼                                  │
                                [ Model Yönlendirici ]                    │
                                  ├─ Apple NPU (0ms)                      │
                                  ├─ Gemini 2.5 Flash                     │
                                  ├─ DeepL API                            │
                                  └─ Yerel MarianMT                       │
                                       │                                  │
                                       ▼                                  │
                                [ SubtitleServer (8787) ]                 │
                                       │                                  │
                                       └───> (HTTP SSE / Yerel Ağ) ───────┘
                                                (Altyazı JSON Paketi)
```

---

## 2. Düşük Seviyeli Bileşenler

### 2.1. Video Alımı ve Donanım Çözücü (`remoteplay/`)
1. **Soket Bağlantısı:** PS5 Remote Play protokolü, kontrol ve video için UDP portları 9295-9304'ü kullanır. `chiaki-ng` tabanlı oturum yöneticisi konsolla el sıkışır (handshake).
2. **Kare Yakalama:** Gelen H.264 video paketleri doğrudan macOS'in **Apple VideoToolbox** API'sine (`VTDecompressionSession`) beslenir.
3. **Sıfır Kopya (Zero-Copy):** Çözülen her video karesi bellekte `CVPixelBuffer` olarak tutulur; RAM üzerinde ek kopyalama yapılmadan doğrudan Metal grafik dokusuna (texture) dönüştürülür.

### 2.2. OCR Optimizasyonu ve İmza Doğrulama (`RegionCropper.swift`)
Saniyede 60 kare üzerinde OCR çalıştırmak işlemciyi boğar. GameTranslator bunu iki aşamalı bir filtreyle çözer:
1. **Bölge Kırpma (Bounding Box):** Yalnızca altyazıların göründüğü alt bölge kırpılır (varsayılan: Y ekseninde %70 - %95 arası).
2. **Algısal İmza (Perceptual Signature):** Kırpılan bölgenin piksel luma ortalaması ve kenar gradyanları taranır. Eğer sahne değişmemişse veya altyazı kutusunda yeni piksel hareketi yoksa OCR çağrılmaz (`scheduler.plan -> .reuse`).

### 2.3. Kararlılık ve Titreme Önleme Algoritması (`SubtitleStabilizer.swift`)
Oyunlardaki hızlı kamera hareketleri ve yarı saydam yazı tipleri OCR'da harf titremelerine yol açar. `SubtitleStabilizer`:
* **Boş Kare Eşiği:** Altyazı ekrandan aniden kaybolsa dahi hemen silinmez; en az 6 ardışık boş kare (`emptyFramesToClear: 6`, yaklaşık 300 ms) beklenir.
* **Metin Benzerlik Eşiği:** Aynı cümlenin küçük harf farklılıkları ("Trust" vs "Trust.") yeni bir çeviri isteği tetiklemez.
* **Ekran Süresi Hesaplayıcı:**
  $$\text{Süre (ms)} = \min(8000, 2000 + (\text{Karakter Sayısı} \times 60))$$
  Bu formül sayesinde uzun replikler TV ekranında oyuncunun okuyabileceği kadar kalır.

---

## 3. Çoklu Çeviri Motoru Mimarisi (`Translator.swift`)

| Motor | Uygulama Katmanı | Gecikme | Kullanım Senaryosu |
|---|---|---|---|
| **Apple Yerel (Neural Engine)** | `macOS NaturalLanguage / Translation` | **< 5 ms** | Standart diyaloglar, sıfır internet, sınırsız kullanım |
| **Google Gemini 2.5 Flash** | `Generative AI REST API` | **~150 ms** | Sokak argosu, GTA 6 suç jargonu, kültürel uyarlamalar |
| **DeepL Pro/Free** | `DeepL API v2` | **~120 ms** | Edebi ve kusursuz gramer gerektiren RPG/hikaye oyunları |
| **Yerel Derin Öğrenme** | `ONNX Runtime / MarianMT` | **~30 ms** | Windows veya Apple dışı platformlarda yerel sinir ağı |

---

## 4. Altyazı Sunucusu ve Ağ Protokolü (`SubtitleServer.swift`)

Sunucu, pure Swift ile `Network.framework` kullanılarak yazılmıştır:
* **Asenkron TCP Listener:** `NWListener` ile tek bir port (8787) üzerinde HTTP ve SSE istekleri karşılanır.
* **Keep-Alive:** TV'nin Wi-Fi kopmalarını fark etmek için 10 saniyelik TCP keep-alive ve 15 saniyelik SSE ping akışı devrededir.

### Olay Formatları (Server-Sent Events)

#### `subtitle` Olayı
```json
event: subtitle
data: {
  "text": "muhafız",
  "source": "Guard",
  "duration": 4000,
  "at": 1710000000000,
  "box": {
    "left": 0.35,
    "top": 0.82,
    "right": 0.65,
    "bottom": 0.88
  }
}
```

#### `style` Olayı
```json
event: style
data: {
  "fontSize": 46,
  "color": "#FACC15",
  "bottomPercent": 12,
  "background": 72,
  "placement": "cover",
  "bold": true
}
```

---

## 5. TV Katmanı ve Ekran Bindirme (`tv-android/`)

Android TV üzerinde altyazı bindirme mantığı şu şekildedir:

1. **Giriş Görüntüsü (`TvView`):**
   Android TV'nin yerel TV servisinden gelen HDMI 1 veya HDMI 2 girişi en alt katmanda donanım seviyesinde tam ekran çizilir.
2. **Şeffaf Görünüm (`WebView`):**
   `TvView` üzerinde `Color.TRANSPARENT` arka planlı ve `LAYER_TYPE_HARDWARE` modunda bir `WebView` oluşturulur.
3. **1920 CSS Viewport Eşleme:**
   TCL ve diğer Android TV'lerin WebView çözünürlük ölçekleme hatalarını önlemek için `fitScalePercent()` formülü kullanılır:
   $$\text{Scale} = \max\left(1, \text{round}\left(\frac{\text{Ekran Genişliği} \times 100}{1920}\right)\right)$$
   Böylece altyazı boyutları ve kutu koordinatları her TV panelinde piksel-piksel kusursuz oturur.

---

## 6. Geliştirici Katkı Prensipleri

* **Sıfır Bağımlılık (Zero External Deps):** Çekirdek sunucu ve motor harici ağır paketler (Electron, Node, React) barındırmaz; işletim sisteminin yerel yeteneklerini kullanır.
* **Geriye Dönük Uyumluluk:** Yeni eklenen stil veya model parametreleri eski TV sürümlerini bozmayacak şekilde `Codable` varsayılanlarıyla korunur.
