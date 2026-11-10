# 🎮 GameTranslator v1.0 Pro — Sefflex Edition
### Konsol Oyunları İçin Gerçek Zamanlı, Sıfır Gecikmeli Türkçe Altyazı Sistemi

[![Platform: macOS](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-blue?style=flat-square&logo=apple)](https://github.com/Sefflex/GameTranslate)
[![Target: Smart TV](https://img.shields.io/badge/TV-TCL%20Android%20TV%20%7C%20LG%20webOS%20%7C%20Samsung%20Tizen-green?style=flat-square&logo=android)](https://github.com/Sefflex/GameTranslate)
[![Zero-Lag](https://img.shields.io/badge/Gecikme-0%20ms%20(Doğrudan%20HDMI)-brightgreen?style=flat-square)](#-temel-mantık-3-cümlede-nasıl-çalışıyor)
[![License: AGPL-3.0](https://img.shields.io/badge/Lisans-AGPL--3.0-red?style=flat-square)](LICENSE)

![GameTranslator Native UI](docs/screenshots/gametranslator-ui.jpg)

**GameTranslator**, başta **GTA 6** olmak üzere Türkçe dil desteği bulunmayan PlayStation 5 oyunlarını, televizyonunuzda **sıfır gecikmeyle (0 input lag)**, **4K 120 FPS / HDR görüntü kalitesi zerre bozulmadan** Türkçe altyazıyla oynamanızı sağlayan açık kaynaklı sistemdir.

---

## 🧠 Temel Mantık: 3 Cümlede Nasıl Çalışıyor?

1. **Görüntü TV'de Kalır (0 Lag):** PS5 konsolunuz televizyonunuza doğrudan **HDMI kablosuyla** bağlıdır. Oyunun görüntüsü ve sesi televizyondan doğrudan akar; 4K, 120Hz, HDR kaliteniz aynen korunur, elinizdeki DualSense kumanda sıfır gecikmeyle çalışır.
2. **Bilgisayar Arkada Sessizce Dinler:** Bilgisayarınız (Mac veya Windows PC) evinizdeki yerel ağ üzerinden PS5'e Remote Play ile bağlanır. Kumandanız TV'den kopmasın diye konsola **ücretsiz açtığımız 2. bir hesapla** bağlanır! Bilgisayar sadece görüntüyü okur (OCR) ve İngilizce konuşmaları Türkçeye çevirir.
3. **Televizyona Sadece Altyazı Gider:** Bilgisayar, çevrilen Türkçe metni televizyondaki GameTranslator uygulamasına (TCL/Android TV, LG webOS veya Samsung Tizen) yollar. Televizyon, HDMI'daki oyunun üzerine şeffaf bir katmanla Türkçe altyazıyı orijinal yazının üstüne basar!

---

## 🖥️ Ortak Yerel Masaüstü Tasarımı (Mac & PC)

GameTranslator, hem **macOS (Apple SwiftUI)** hem de **Windows (WPF / .NET 10)** platformlarında aynı sade, şık ve modern koyu arayüzü sunar. Tarayıcı veya web sunucusu gerektirmez.

* **Sol Panel (Çeviri Merkezi / Translation Hub):**
  * **▶ Çeviriyi Başlat / ■ Çeviriyi Durdur:** Canlı sistemi tek tıkla başlatan ve durduran yüksek kontrastlı birincil buton.
  * **Connection Status:** PS5 konsolunun ve TV bağlantısının durumunu anlık renkli noktalarla (yeşil/turuncu/gri) bildirir.
  * **Son Altyazı:** Oyunda konuşulan son altyazıyı anlık olarak gösterir ve tek tıkla panoya kopyalama imkanı sunar.

* **Sağ Panel (Entegre Ayarlar Paneli):**
  * **Üst Sekmeler (Segmented Control):** `[ 🖥️ Görüntü & Dil ]`, `[ 🔊 Çeviri & Ses ]`, `[ 🔗 Bağlantı & Sistem ]`.
  * **Hassas Kaydırıcılar:** Yazı boyutu (*Kök ➔ Büyük*) ve Arka plan opaklığı (*Yok ➔ Tam*).
  * **Altyazı Yeri:** İngilizce altyazının üstünü örten dinamik takip veya ekranda sabit konumlandırma.
  * **Hızlı Seçenekler:** `Kalın yazı`, `Türkçe Dublaj` ve tek tıkla `Test altyazısı gönder`.
  * **Altyazı Alanı:** 16:9 ekran önizleme kutusu üzerinden altyazı alanını 4 eksende (*Üst, Alt, Sol, Sağ*) ayarlama ve `Alanı sıfırla`.
  * **DeepL API:** Gelişmiş çevrimiçi motor için API anahtarı girme ve anında deneme.
  * **Kısayol & OBS:** Ekranı anında çevirme kısayolu (`^⌘⌥T`) ve yayıncılar için OBS Browser Source overlay bağlantısı (`http://localhost:8787/obs`).

---

## 🎯 Platform Kurulum Rehberi

---

### 1. 🍏 Mac (macOS Yerel Uygulaması)
> Apple Silicon (M1, M2, M3, M4) Neural Engine NPU çipini ve Vision OCR donanımını kullanarak **0 ms gecikmeyle** çevrimdışı çalışır.

1. `GameTranslator` klasöründeki **`GameTranslator.app`** uygulamasına çift tıklayın (veya `GameTranslator.command` çalıştırın).
2. Karşınıza yukarıdaki macOS masaüstü arayüzü gelecektir.
3. Mavi **`▶ Çeviriyi Başlat`** butonuna basmanız yeterlidir.

---

### 2. 🪟 PC (Windows Yerel Masaüstü Uygulaması)
> C# / .NET 10, WPF ve DirectML / Windows Media OCR altyapısıyla çalışan yerel Windows uygulamasıdır.

1. Bilgisayarınızda **[.NET 10 SDK](https://dotnet.microsoft.com/download)** kurulu olduğundan emin olun.
2. `GameTranslator` klasöründeki **`GameTranslator.bat`** dosyasına çift tıklayın.
   *(Geliştiriciler için doğrudan komut: `dotnet run --project windows\src\GameTranslator.App\GameTranslator.App.csproj`)*
3. Karşınıza donanım hızlandırmalı Windows masaüstü arayüzü gelecektir.
4. **`▶ Çeviriyi Başlat`** butonuna tıklayın.

---

### 3. 📺 Akıllı TV (Smart TV Companion Uygulamaları)
> TV'niz HDMI'daki oyun görüntüsünü oynatırken, üzerine şeffaf bir OSD penceresi açarak Türkçe altyazıyı basar.

* **TCL / Google TV / Android TV (Sony, Philips, Vestel vb.):**
  - `tv/packages/gametranslator-androidtv_1.0.0.apk` dosyasını USB bellek veya Wi-Fi (`adb install`) ile televizyonunuza kurun.
  - Uygulamayı açıp PS5'in bağlı olduğu HDMI portunu seçin.
* **LG TV (webOS):**
  - TV'de Developer Mode'u açın.
  - `tv/packages/com.gametranslator.tv_1.0.0_all.ipk` dosyasını `ares-install` veya webOS Dev Manager ile kurun.
* **Samsung TV (Tizen):**
  - TV'de Geliştirici Modunu (1-2-3-4-5) açın.
  - `tv/packages/gametranslator-samsung_1.0.0.wgt` dosyasını Tizen CLI (`sdb install`) ile yükleyin.

---

## 🎮 PlayStation 5 Ayarı (Kumanda Neden Kopmaz?)

PlayStation normalde Remote Play açılınca elinizdeki kumandayı televizyondan koparır. Bunu aşmak son derece basittir:
1. PS5'te `Ayarlar` ➔ `Sistem` ➔ `Uzaktan Oynatma (Remote Play)` ➔ **Etkinleştir** yapın.
2. PS5'inizde **ücretsiz ikinci bir kullanıcı / PSN hesabı** ekleyin (örneğin: `oyunceviri@gmail.com`).
3. **Nasıl Oynayacaksınız?**
   - TV karşısında **kendi ana hesabınızı** açıp kumandanızı elinize alın.
   - Bilgisayar ise arka planda bu **ikinci hesapla** konsola bağlanır.
   - Böylece DualSense kumandanız TV'den **asla kopmaz**!

---

## 🧠 Hangi Çeviri Modeli Ne İşe Yarar?

| Model | Nasıl Çalışır? | Ne Zaman Tercih Edilmeli? |
| :--- | :--- | :--- |
| **1. Apple Yerel (Önerilen)** | Mac'inizin Neural Engine (NPU) yapay zeka çipini kullanır. | **0 ms gecikme sağlar.** Tamamen internetsiz, çevrimdışı, kotasız ve ücretsizdir. |
| **2. Google Gemini 2.5 Flash** | Google Gemini API (AI Studio). | GTA 6 gibi oyunlardaki **sokak argosu, küfürler, şakalar ve hikaye bağlamını** en zeki ve en doğal Türkçeyle çeviren yapay zeka modelidir. |
| **3. DeepL API** | DeepL Free / Pro API. | Dünyanın en bilinen profesyonel çeviri motorudur. Kusursuz dilbilgisi için tercih edilir. |
| **4. Yerel Derin Öğrenme** | Bilgisayarınızın CPU/GPU'sunda yerel çalışan MarianMT / DirectML sinir ağı modeli. | İnternetsiz yerel yapay sinir ağı çevirisi. |

---

## 📁 Proje Klasör Rehberi

```text
GameTranslate/
├── mac/                            # 🍏 macOS Yerel Uygulaması (SwiftUI, Apple Vision OCR, NPU)
│   ├── GameTranslator.xcodeproj    # Xcode Projesi
│   └── GameTranslator/             # Swift Kaynak Kodları (UI, Pipeline, Dubbing)
│
├── windows/                        # 🪟 Windows Yerel Uygulaması (.NET 10, C#, WPF, DirectML)
│   ├── GameTranslator.Windows.slnx # Visual Studio Çözümü
│   └── src/GameTranslator.App/     # Windows WPF Arayüzü & OCR Motoru
│
├── tv-android/                     # 📺 Android TV / TCL Yerel TvView Projesi (Java/Kotlin)
├── tv-webos/                       # 📺 LG webOS HTML5 Şeffaf Altyazı Katmanı
├── tv-tizen/                       # 📺 Samsung Tizen TV Katmanı
│
├── core/                           # ⚙️ Çekirdek Swift Kütüphanesi (Ağ, OCR, Altyazı Senkronu)
│
└── GameTranslator/                 # 🚀 Çift Tıklamayla Çalışan Dağıtım Paketi
    ├── GameTranslator.app          # macOS Yerel Uygulama Paketi
    ├── GameTranslator.command      # Mac çift tık başlatıcısı
    ├── GameTranslator.bat          # Windows çift tık başlatıcısı
    ├── KULLANIM_KILAVUZU.txt       # Hızlı Türkçe başlangıç rehberi
    └── tv/
        ├── gametranslator-androidtv_1.0.0.apk  # Android TV / TCL paketi
        ├── com.gametranslator.tv_1.0.0_all.ipk # LG webOS paketi
        └── gametranslator-samsung_1.0.0.wgt   # Samsung Tizen paketi
```

---

## 👨‍💻 Geliştiriciler İçin: Kodları Derleme

```bash
# Mac'te Core kütüphanesini derleme:
export DEVELOPER_DIR="/Library/Developer/CommandLineTools"
swift build -c release --package-path core

# macOS Yerel Uygulamasını Açma:
open mac/GameTranslator.xcodeproj

# Windows Projesini Derleme ve Çalıştırma:
dotnet run --project windows\src\GameTranslator.App\GameTranslator.App.csproj

# Android TV APK Derleme:
cd tv-android && ./gradlew assembleRelease
```

---

## ⚖️ Lisans & Telif Hakkı

Bu proje **[GNU AGPL-3.0](LICENSE)** lisansı altında açık kaynak olarak sunulmaktadır.
Geliştirici: **Sefflex** — Telif Hakkı © 2026 GameTranslator Pro. Tüm hakları saklıdır.
Çekirdek ağ ve video yakalama altyapısında açık kaynaklı [chiaki-ng](https://github.com/chiaki-ng/chiaki-ng) projesinin temellerinden yararlanılmıştır.
