# GameTranslator - Windows Manuel Kurulum ve Sorun Giderme Rehberi

Bu rehber, GameTranslator Windows uygulamasının kurulumunu, PlayStation 5 ve Akıllı TV (Samsung, LG, Android TV, PC/OBS) eşleştirmesini adım adım anlatmaktadır.

---

## 🚀 1. Windows Kurulumu

1. [GitHub Releases](https://github.com/Sefflex/GameTranslator/releases) sayfasından **`GameTranslator-Windows-x64.zip`** dosyasını indirin.
2. İndirdiğiniz zip dosyasına **sağ tıklayın** ve **"Tümünü Ayıkla..." (Extract All)** seçeneğine tıklayın.
   > ⚠️ **Önemli:** Uygulamayı zip'in içinden doğrudan çalıştırmayın! Mutlaka bir klasöre çıkarın.
3. Klasörün içindeki **`GameTranslator.exe`** dosyasına çift tıklayarak uygulamayı başlatın.
   > Windows SmartScreen uyarısı çıkarsa: *"Ek Bilgi"* -> *"Yine de çalıştır"* butonuna tıklayın.

---

## 🎮 2. PlayStation 5 Eşleştirmesi

### Adım 1: PS5 Ayarları
1. PS5 konsolunuzu açın.
2. **Ayarlar ➔ Sistem ➔ Uzaktan Oynatma (Remote Play)** menüsüne gidin ve **"Uzaktan Oynatmayı Etkinleştir"** seçeneğini açık konuma getirin.
3. Aynı menüde yer alan **"Cihazı Eşleştir"** seçeneğine tıklayın. Ekranda **8 haneli bir kod** ve 300 saniyelik bir geri sayım görünecektir.

### Adım 2: GameTranslator'da PSN Girişi
1. GameTranslator kurulum sihirbazında **"Sony Giriş Sayfasını Aç"** butonuna tıklayın.
2. Tarayıcınızda açılan resmi Sony giriş ekranından PSN hesabınızla giriş yapın.
3. Giriş tamamlandığında tarayıcınız `remoteplay.dl.playstation.net/remoteplay/redirect?code=...` sayfasına yönlenecektir (sayfa beyaz/boş görünebilir, bu normaldir).
4. Tarayıcınızın adres çubuğundaki bu bağlantının **tamamını kopyalayın** ve GameTranslator'daki alana yapıştırıp **"Doğrula"**ya basın.

### Adım 3: 8 Haneli Kodu Girme
1. PS5 ekranında görünen **8 haneli kodu** GameTranslator'a yazın.
2. Konsol IP adresi otomatik taranır (bulunamazsa PS5 Ayarlar ➔ Ağ ➔ Bağlantı Durumu'ndan IP adresini elle yazabilirsiniz).
3. **"PS5 ile Eşleştir"** butonuna tıklayın. Yeşil onay işaretini görünce ana ekrana geçebilirsiniz.

---

## 📺 3. Akıllı TV Kurulumu (Samsung, LG, Android TV, OBS)

GameTranslator, çevirileri televizyonunuza veya bilgisayar ekranınıza sıfır gecikmeyle yansıtır.

### 🔷 Seçenek A: PC & OBS / Tarayıcı (En Kolay)
- Herhangi bir TV uygulaması yüklemek istemiyorsanız ya da yayında kullanacaksanız:
- OBS Studio'da yeni bir **"Tarayıcı Kaynağı" (Browser Source)** ekleyin:
  - **URL:** `http://localhost:8787/obs`
  - **Genişlik:** 1920, **Yükseklik:** 1080

---

### 🔷 Seçenek B: Samsung Smart TV (Tizen)
1. Uygulama klasöründeki `tv-app/gametranslator-samsung_1.0.0.wgt` dosyasını kullanın (veya GitHub Releases sayfasından indirin).
2. **Apps2Samsung ile Kurulum:**
   - Bilgisayarınızda **Apps2Samsung** programını açın.
   - TV'nizin IP adresini girin.
   - `GameTranslator-Samsung-Tizen-v1.0.0.wgt` dosyasını seçip **Install** butonuna basın.
3. TV'nizde GameTranslator uygulamasını açın.
4. GameTranslator Windows uygulamasında TV IP'nizi girip kaydedin.

---

### 🔷 Seçenek C: LG Smart TV (webOS)
1. Bilgisayarınıza [webOS Dev Manager](https://github.com/webosbrew/dev-manager-desktop) indirin.
2. LG TV'nizin Developer Mode uygulamasını açın ve bilgisayarınızı bağlayın.
3. `tv-app/com.gametranslator.tv_1.0.0_all.ipk` dosyasını webOS Dev Manager üzerinden TV'nize kurun.
4. TV'de uygulamayı açın ve Windows programından IP'nizi kaydedin.

---

### 🔷 Seçenek D: Android TV / Google TV
1. `tv-app/gametranslator-androidtv_1.0.0.apk` dosyasını USB bellek veya "Send Files to TV" uygulamasıyla televizyonunuza yükleyin.
2. Uygulamayı TV'nizde başlatın.
3. Windows uygulamasında TV IP adresinizi girin.

---

## 🛠️ 4. Sık Karşılaşılan Sorunlar ve Çözümleri

### ❓ Soru: "Sony giriş sayfasını açtığımda 4161 hatası veriyor"
**Çözüm:** v1.0.0 güncellemesi ile resmi Sony Remote Play OAuth protokolü güncellenmiştir. Artık doğrudan resmi kimlik doğrulama ekranı açılmaktadır ve 4161 hatası giderilmiştir.

### ❓ Soru: "Unable to load DLL 'gametranslator_native' hatası alıyorum"
**Çözüm:** v1.0.0 sürümünde bu denetim korumalı hale getirilmiştir. Yerel C++ DLL bulunmasa bile Windows saf UDP ağ soketleriyle eşleşme sorunsuz tamamlanır ve program çökmez.

### ❓ Soru: "PS5 eşleşmesi yaptım ama ana ekranda Eşleşmedi yazıyor"
**Çözüm:** Güncel sürümde konsol kaydı yapıldığı anda durum çubuğu yeşil renge döner ve `Eşleşti (192.168.x.x)` şeklinde IP adresiyle birlikte görüntülenir.

### ❓ Soru: "Samsung WGT Apps2Samsung'ta hata veriyor"
**Çözüm:** Önceki pakette eksik olan HTML/JS motor dosyaları v1.0.0 tam sürümünde pakete dahil edilmiştir. Güncel `GameTranslator-Samsung-Tizen-v1.0.0.wgt` dosyasıyla kurulum tek tıkla tamamlanır.
