# Oyun Altyazı Çevirmeni — Tasarım

Tarih: 2026-10-31 · Durum: kullanıcı "işe koyul" dedi · Ad: **GameTranslator** · GitHub: `Sefflex/gametranslator` (bireysel hesap; mağaza hesapları sonra)

Ertelenen kararlar: mağaza/geliştirici hesaplarının
kimin adına açılacağı, GitHub'da kimin yayınlayacağı, gizlilik metni. Test cihazları: LG 55UK7550 ve bir
Samsung TV mevcut (model sonra).

## 1. Amaç ve neden

Türkçe altyazısı olmayan PS5 oyunlarında, oyunun İngilizce altyazısını anlık okuyup **Türkçesini TV'de
oyun görüntüsünün üstünde** göstermek. Oyuncu her zamanki gibi TV'de, kendi koluyla oynar; çeviri arka
planda ayrı bir cihazda (önce bilgisayar, sonra telefon) yapılır.

Benzer bir ücretli uygulama var (Console Translator, iOS, aylık abonelik, çeviriler internet üzerinden
gidiyor). Bu proje **ücretsiz, açık kaynak, internet gerektirmeyen ve kurulumu herkesin yapabileceği**
bir alternatif olacak.

## 2. Hedefler, hedef olmayanlar, başarısızlık

**Hedefler**
- Kurulumu teknik bilgisi olmayan biri yardımsız yapabilmeli (kullanıcının ana önceliği).
- Uygulama arayüzü temiz ve sade; Türkçe ve İngilizce.
- Masaüstü uygulaması, iOS ve Android uygulamaları; LG (webOS) ve Samsung (Tizen) TV alıcıları.
- Açık kaynak olarak GitHub'da yayın. Mobil uygulamalar ücretsiz; içine reklam konabilir (karar sonra).
- Okuma ve çeviri tamamen cihaz üstünde; oyun görüntüsü ya da metni hiçbir sunucuya gitmez.

**Hedef olmayanlar**
- Oyunu telefonda/bilgisayarda oynatmak (oyun hep TV'de oynanır; görüntü sadece okumak için çözülür).
- Sesten çeviri (altyazısı hiç olmayan oyunlar) — ilk sürümde yok.
- PS4, Xbox, Switch — ilk sürümde yok.
- Bulut sunucusu, hesap sistemi, abonelik.

**Başarısızlık sayılacaklar** (çalışsa bile)
- Kullanıcının komut satırı, geliştirici modu, IP adresi yazma, adres kopyalayıp yapıştırma gibi
  adımlarla uğraşması.
- Uygulamanın oyuncunun kolunu düşürmesi ya da oyunu bölmesi.
- Altyazının oyundaki altyazıdan belirgin geç gelmesi (hedef: oyundaki altyazı belirdikten sonra ~1,5 sn
  içinde TV'de).

## 3. Doğrulanmış olanlar (prototipten)

Prototip `~/Projeler/ps5-altyazi` altında; ayrıntılar `BENIOKU.md`'de.

- LG 55UK7550 (webOS 4.4): web uygulaması HDMI'ı `<video><source type="service/webos-external"
  src="ext://hdmi:N">` ile tam ekran gösterip üstüne HTML altyazı basabiliyor. Sayfa arka planı şeffaf
  olmalı.
- PS5'in girişi TV'nin giriş listesinden otomatik bulunuyor (`luna://com.webos.service.eim/getAllInputStatus`,
  `spdSourceDeviceInfo: "GAME"`).
- Mac'te Vision OCR + Apple Translation (cihaz üstü) ile okuma 0,1–0,6 sn, çeviri 0,7–1 sn. Altyazı iki
  kare üst üste aynı okununca gönderiliyor; ekrandan kalkınca TV'deki çeviri de kalkıyor; zaten Türkçe olan
  metin (PS5 menüleri) atlanıyor.
- **Aynı PSN hesabıyla Remote Play olmuyor**: oturum açılınca PS5 oyuncunun kolunu kapatıyor; kol geri
  bağlanınca Remote Play oturumu kapanıyor (resmî uygulamada da chiaki-ng'de de aynı).
- chiaki-ng ile PS5 kaydı Mac'te çalıştı.

**Henüz doğrulanmamış (ilk iş, adım 0):** Kullanıcı kendi PS5'inde iki hesapla dener.
- *Geçer* (kararım): oyuncunun kolu bağlı kalır, oyun duraklamaz, TV'de kalıcı bir uyarı/seçim ekranı
  kalmaz, Remote Play görüntüsü engelsiz akar. Kısa süreli bir "uzaktan kullanıcı bağlandı" bildirimi kabul.
- *Kalır*: kol düşer, oyun durur, aktif kullanıcı değiştirmek gerekir ya da TV'de kalıcı bir ekran çıkar.
  Bu durumda durulur ve tasarım yeniden konuşulur.
- Aynı testte **TV tarafı da ölçülür**: HDMI uygulama içinde gösterilirken tuş gecikmesi hissediliyor mu,
  HDR / oyun modu kapanıyor mu, ses geliyor mu. Kullanıcı normal HDMI girişine göre belirgin bir fark
  hissederse bu da durma sebebi (kararım; ölçüt kullanıcının oyuncu olarak hissi).

## 4. Kullanıcı deneyimi

**İlk kurulum (sihirbaz, bir kez)**
1. Hoş geldin + ne gerektiği (PS5, aynı Wi-Fi, LG/Samsung TV, ücretsiz ikinci PSN hesabı).
2. **İkinci PSN hesabı**: neden gerektiği tek cümleyle; nasıl açılır ve PS5'e kullanıcı olarak eklenir,
   o kullanıcı için Uzaktan Oynatma nasıl açılır — ekran görüntüleriyle.
3. **PSN girişi** uygulamanın içinde açılan giriş penceresiyle; giriş bitince hesap kimliği kendiliğinden
   alınır (adres kopyalama yok).
4. **PS5'i bul**: aynı ağda otomatik bulunur; PS5 ekranındaki 8 haneli kodu girme adımı.
5. **TV'yi bağla**: TV mağazadan kurulmuş uygulamayı açar; uygulama TV'yi ağda bulup eşleşir
   (bkz. §5.4). Kullanıcı en fazla TV'de bir "izin ver" onayı verir.
6. **Deneme**: TV'de örnek bir Türkçe altyazı çıkar, "Görüyor musun?" onayı.

**Oyun sırasında (tek ekran)**
- Büyük bir "Çeviriyi başlat / durdur".
- Durum: PS5 bağlı · TV bağlı · son okunan İngilizce ve Türkçesi.
- Ayarlar (az ve basit): altyazının okunduğu bölge (görsel bir çerçeveyle ayarlanır, varsayılan alt bant),
  TV'de yazı boyutu ve konumu, kaynak dil (varsayılan İngilizce).

**TV uygulaması**
- Açılınca PS5 girişini otomatik bulur ve tam ekran gösterir; üstte sadece altyazı katmanı.
- Eşleşmemişse ekranda kısa bir "Telefondaki/bilgisayardaki uygulamayı açın" mesajı.

## 5. Mimari

```
PS5 ──HDMI──────────────▶ TV uygulaması (HDMI tam ekran + altyazı katmanı)
 │                                   ▲
 └─Remote Play (ikinci hesap)        │ yerel ağ
            ▼                         │
   Uygulama (masaüstü / iOS / Android)
   Remote Play çekirdeği → görüntü çözme → bölge kırp → OCR → kararlılık → çeviri → TV'ye gönder
```

### 5.1 Remote Play çekirdeği
- chiaki-ng'nin C kütüphanesi (`libchiaki`): keşif, kayıt, oturum, görüntü akışı. Arayüzü kullanılmaz.
- Lisans: AGPL-3.0 + OpenSSL istisnası (§7).
- Oturumdan kol girdisi gönderilmez; ses kullanılmaz (mümkünse ses akışı kapatılır).
- Bağlantıyı kurup tutmak, koparsa yeniden bağlanmak uygulamanın işi.

### 5.2 Okuma hattı (platformdan bağımsız mantık, platforma özel motorlar)
- Kare hızı ~4/sn; kullanıcının seçtiği bölge kırpılır (varsayılan dikey %62–97, yatay %14–86).
- Kararlılık, tekrar engelleme, "zaten hedef dilde" atlama ve "altyazı kalktı" mantığı prototipteki gibi.
- Motorlar: macOS/iOS'ta Vision + Apple Translation; Android'de ML Kit Text Recognition + ML Kit Translate.
  Hepsi cihaz üstü, ücretsiz. Dil paketi yoksa uygulama indirtir.

### 5.3 Platformlar
- **Masaüstü (ilk ürün): macOS, SwiftUI.** Prototip parçaları zaten Swift'te. Windows sonraya (kararım).
- **iOS: Swift/SwiftUI. Android: Kotlin.** Kullanıcının diğer projelerindeki gibi her platform ayrı,
  yerel uygulama; Remote Play çekirdeği ortak C kodu (iOS'ta xcframework, Android'de NDK).
- iOS'ta arka planda çalışma güvenilir değil: oyun boyunca uygulama önde, ekran kararmış/kilitsiz
  tutulur ve kullanıcıya şarja takması söylenir. Android'de ön plan servisiyle çalışır.

### 5.4 Uygulama ↔ TV iletişimi (kararım; uygulayıcı daha iyisini bulursa değiştirebilir)
- TV web uygulamaları sunucu açamaz; bu yüzden **TV, uygulamaya bağlanır** (prototipteki gibi
  Server-Sent Events). Uygulama küçük bir yerel HTTP sunucusu çalıştırır.
- TV'nin uygulamanın adresini öğrenmesi için önerilen yol: uygulama TV'yi ağda bulur (SSDP) ve TV'nin
  kendi uzaktan kumanda arayüzüyle (LG: SSAP WebSocket, port 3000/3001; Samsung: uzaktan kumanda
  WebSocket, port 8001/8002) **TV uygulamamızı kendi adresiyle parametre olarak başlatır**. TV ilk seferde
  bir "izin ver" sorar. Bu aynı zamanda TV kendi HDMI girişine geçtiğinde (SimpLink/CEC) uygulamamızı
  yeniden öne getirmeyi de sağlar.
- Yedek yol: TV ekranında kısa bir eşleme kodu; uygulama yerel ağı tarayıp kodu gösteren TV'yi bulur.
- İletişim sadece yerel ağda; internet gerekmez.

### 5.5 TV uygulamaları
- **LG webOS**: mevcut prototip temel alınır (ES5, eski webOS'lar için). Hedef en az webOS 4.x+.
- **Samsung Tizen**: aynı HTML/JS; HDMI gösterimi resmî TVWindow API ile (`tizen.tvwindow`, kaynak HDMI,
  pencere uygulamanın arkasında). Belgeli ama henüz denenmedi.
- Ortak kod tek yerde, platform farkı ince bir katmanda.

## 6. Diller
- Arayüz: Türkçe ve İngilizce (sistem diline göre, ayarlardan değiştirilebilir). TV uygulaması da iki dilli.
- Çeviri: kaynak varsayılan İngilizce, hedef varsayılan Türkçe; motorların desteklediği diğer diller
  ayarlardan seçilebilir (ek iş gerektirmiyorsa).

## 7. Lisans, mağazalar, marka

- **Lisans: AGPL-3.0** (chiaki-ng'den gelen zorunluluk). Tüm kaynak GitHub'da; her sürümün tam kaynağı
  erişilebilir; chiaki-ng'ye uygun atıf. chiaki-ng bakımcısı "ücret almak serbest ama tam kaynak şart"
  diyor; reklam da AGPL ile uyumlu.
- **Ayrıca izin gerekmiyor** (kullanıcının kararı): proje baştan açık kaynak ve AGPL olduğu için chiaki-ng
  kodunu kullanmak serbest. Şartlar: tüm kaynağı aynı lisansla yayınlamak, chiaki-ng'nin telif ve lisans
  bildirimlerini korumak, uygulamada "Hakkında" ekranında atıf. Kendi yazdığımız kod (arayüz, okuma hattı,
  TV uygulamaları, eşleme) bizimdir; chiaki-ng'den alınan ya da ona bakılarak çevrilen kısımlar onlarındır
  ve AGPL kalır — bunu gizlemeye çalışılmaz (chiaki-ng bakımcısı, kaynağını gizleyen bir App Store
  uygulamasına karşı Apple'a DMCA bildirimi göndermiş).
- **Apple App Store**: GPL/AGPL ile App Store koşulları arasında bilinen bir tartışma var; chiaki-ng
  bakımcısının tutumu "ücret serbest, tam kaynak ve bildirimler şart". Kaynak tamamen açık olacağı için
  risk kabul edilir (kararım). Mac sürümü ilk aşamada GitHub'dan imzalı/notarize edilmiş indirme olarak da
  dağıtılır.
- **Google Play**: GPL/AGPL uygulamalar kabul ediliyor; risk düşük.
- **LG Content Store / Samsung Apps**: ana hedef mağazada yayın. HDMI'ı uygulama içinde gösteren bir
  uygulamanın kabul edilip edilmeyeceği bilinmiyor; benzer ücretli uygulamanın LG'de alıcı uygulaması var.
  Kabul edilmezse mağaza dışı kurulum adım adım anlatılır (kullanıcı kararı).
- **Marka**: Uygulama adında "PlayStation" ya da "PS5" geçmez; açıklamada "PS5 ile çalışır" denir. Sony
  ile ilişkisi olmadığı belirtilir.
- **Reklam**: sadece mobilde, olursa rahatsız etmeyen bir yerde (kurulum/ayar ekranları; oyun sırasında
  TV'de asla). Reklam SDK'sı dışında veri toplanmaz; gizlilik metni ve gerekli onaylar eklenir.

## 8. Sıra

0. **İkinci hesap ve TV gecikmesi testi** (§3). Başarısızsa dur.
1. **Masaüstü (macOS) uygulaması**: libchiaki entegrasyonu, kurulum sihirbazı, okuma hattı, LG eşleme.
   GitHub'da **beta** olarak açık kaynak yayın; bu aşamada TV uygulaması geliştirici modu ile kurulur ve
   bunun için adım adım bir rehber yazılır (kurulumu otomatikleştiren araç yapılmaz). Kararım.
   - **LG mağaza başvurusu bu adımla paralel başlar** (onay uzun sürer); TV uygulamasının kalıcı kimliği
     (app id) ve adı bu adımın başında sabitlenir, çünkü eşleme (§5.4) buna bağlı.
   - "Herkes için" duyurulan ilk sürüm, LG mağaza onayını bekler. Onay çıkmazsa mağaza dışı kurulum rehberi
     sadeleştirilir (kullanıcının kararı: "öncelik mağaza, olmazsa tarif ederiz").
2. **Samsung Tizen TV uygulaması** (önce HDMI gösterimi denenir; olmazsa Samsung desteği ertelenir) ve
   Samsung mağaza başvurusu.
3. **Android uygulaması**.
4. **iOS uygulaması**.

Minimum sürümler (kararım, uygulayıcı genişletebilir): macOS ve iOS için cihaz üstü çevirinin arayüzsüz
kullanılabildiği 26; Android için ML Kit'in desteklediği en eski sürüm.

## 9. Uygulayıcıya bırakılanlar
- Kod düzeni, depo yapısı, derleme sistemi, test yaklaşımı.
- libchiaki'nin hangi sürümünün/commit'inin alınacağı ve nasıl derleneceği.
- Görsel tasarım ayrıntıları (renk, tipografi), çeviri dosyası formatı.
- Eşleme mekanizmasının ayrıntıları (§5.4'teki önerinin uygulanabilirliğine göre).
