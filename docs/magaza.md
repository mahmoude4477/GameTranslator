# TV mağazalarında yayın

Yayıncı: Sefflex (kurumsal satıcı hesabı). Uygulama adı her yerde **GameTranslator**. Ad, simge ve ekran
görüntülerinde PlayStation/PS5 adı ya da logosu kullanılmaz. Açıklamada en fazla "HDMI ile bağlı oyun
konsolları için", sonunda da Sony ile bağlantısızlık notu yer alır.

## Ortak gereksinimler

- Gizlilik politikası: https://github.com/Sefflex/GameTranslate/blob/main/PRIVACY.md
- Destek: https://github.com/Sefflex/GameTranslate/issues
- **Demo modu:** test eden kişide PS5 ve bilgisayar yok. Bekleme ekranındaki "Demoyu izle" (Tamam) ya da
  Mavi tuş örnek altyazıları oynatır. Bu durum test notunda ve arayüz anlatımında (pptx) açıkça yazılır.
- Kumanda: yön tuşları, Tamam ve Geri çalışır, seçili öğe belli olur. Geri tuşu giriş ekranında TV'nin
  ana ekranına döner.
- Kaynak kodda şifre, anahtar ya da token yoktur.

## LG Content Store (Seller Lounge)

1. seller.lgappstv.com üzerinde Sefflex adına **kurumsal satıcı** kaydı açılır (LG hesabıyla).
2. Gönderimden önce Seller Lounge Q&A'dan yazılı soru sorulur: HDMI görüntüsü belgelenmemiş
   `ext://hdmi` kaynağıyla gösteriliyor ve LG bunun için resmi API sunmuyor. Bu yöntemin mağaza
   uygulamasında kabul edilip edilmediği sorulur. Gerekirse webOS Partner Zone'a başvurulur.
   Soru metni: `store/lg/question.md`.
3. Olumlu cevap gelirse gönderimde şunlar verilir (görseller ve metinler `store/lg/` altında,
   Self-Checklist şablonu Seller Lounge'dan alınır):
   - IPK (`release.yml` üretir)
   - Simge: 400×400
   - Ekran görüntüleri: 1 ana + 2–5 ek, 1280×720
   - Açılış arka planı: 1920×1080
   - Açıklama: 50–4000 karakter
   - Yaş derecesi, kategori ve ülke seçimi
   - Self-Checklist (xlsx)
   - UX Scenario (pptx)
4. İnceleme genelde 1–3 ay sürer.

Mağaza onayı gelmezse kurulum geliştirici moduyla yapılır. Bu modun oturumu en çok 1000 saat sürer ve
süre dolmadan uzatılmalıdır, yoksa kurulan uygulama silinir. Bu uzatmayı Mac ve Windows
uygulamalarının kendiliğinden yapması planlanıyor (henüz yok).

## Samsung (TV Seller Office, Tizen)

1. Seller Office'e Samsung hesabıyla üye olunur. Bu hesap ilk aşamada yalnız ABD'de yayın izni verir.
2. Türkiye için **Partner Seller** olmak gerekir:
   - Samsung (merkez ya da Samsung Türkiye) ile sözleşme imzalanır.
   - Membership › Partnership menüsünden bölgenin içerik yöneticisinin e-postasıyla talep açılır. Bu
     e-posta bilinmiyorsa 1:1 Q&A'dan istenir.
   - En uzun süren adım budur; hemen başlanmalıdır.
3. Tizen uygulaması `tizen.tvwindow` ile HDMI görüntüsünü HTML katmanının arkasında gösterir (`BEHIND`).
   Bu API belgelidir ve yetki seviyesi public'tir. Gönderimden önce QN70F üzerinde geliştirici moduyla
   denenir.
4. Gönderimde şunlar verilir:
   - .wgt paketi (author ve distributor imzalı; author sertifikası yedeklenir)
   - Simge: 1920×1080
   - Tam 4 ekran görüntüsü: 1920×1080, her biri en fazla 500 kB
   - App UI Description (pptx)
   - Her dil için başlık ve açıklama
5. İnceleme genelde 1–4 ay sürer.
