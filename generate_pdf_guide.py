
import os, subprocess

html_content = '''<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<title>GameTranslator TV Uygulaması Kurulum Rehberi</title>
<style>
  @page {
    size: A4;
    margin: 14mm 14mm 14mm 14mm;
  }
  * {
    box-sizing: border-box;
    margin: 0;
    padding: 0;
  }
  body {
    font-family: -apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text", "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    color: #1f2937;
    background-color: #ffffff;
    line-height: 1.45;
    font-size: 12.5px;
  }

  .page-break {
    page-break-before: always;
    break-before: page;
  }

  /* Header */
  .header {
    border-bottom: 2px solid #0A84FF;
    padding-bottom: 12px;
    margin-bottom: 16px;
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
  }
  .title-group h1 {
    font-size: 22px;
    font-weight: 800;
    color: #0F172A;
    letter-spacing: -0.5px;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .title-group .subtitle {
    font-size: 12.5px;
    color: #0A84FF;
    font-weight: 600;
    margin-top: 3px;
  }
  .badge-repo {
    background: #0F172A;
    color: #38BDF8;
    padding: 4px 10px;
    border-radius: 6px;
    font-size: 10px;
    font-family: ui-monospace, SFMono-Regular, monospace;
    font-weight: 600;
  }

  /* Intro Box */
  .intro-box {
    background: #F0F7FF;
    border-left: 4px solid #0A84FF;
    padding: 10px 14px;
    border-radius: 0 8px 8px 0;
    margin-bottom: 16px;
    font-size: 11.5px;
    color: #1E3A8A;
  }
  .intro-box strong {
    color: #1D4ED8;
  }

  /* Section */
  .section {
    margin-bottom: 18px;
    break-inside: avoid;
  }
  .section-title {
    font-size: 14.5px;
    font-weight: 700;
    color: #0F172A;
    margin-bottom: 10px;
    display: flex;
    align-items: center;
    gap: 7px;
    border-bottom: 1px solid #E2E8F0;
    padding-bottom: 5px;
  }
  .section-title span.num {
    background: #0A84FF;
    color: white;
    width: 22px;
    height: 22px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 700;
  }

  /* Table */
  table {
    width: 100%;
    border-collapse: collapse;
    margin: 8px 0 14px 0;
    font-size: 11px;
    border-radius: 8px;
    overflow: hidden;
    border: 1px solid #E2E8F0;
  }
  th {
    background: #F1F5F9;
    color: #334155;
    font-weight: 700;
    text-align: left;
    padding: 8px 10px;
    border-bottom: 1px solid #CBD5E1;
  }
  td {
    padding: 7px 10px;
    border-bottom: 1px solid #E2E8F0;
    color: #334155;
  }
  tr:last-child td {
    border-bottom: none;
  }
  tr:nth-child(even) {
    background: #F8FAFC;
  }
  .status-yes {
    color: #059669;
    font-weight: 700;
  }
  .status-no {
    color: #DC2626;
    font-weight: 600;
  }

  /* Steps List */
  .step-card {
    background: #F8FAFC;
    border: 1px solid #E2E8F0;
    border-radius: 8px;
    padding: 11px 14px;
    margin-bottom: 10px;
    break-inside: avoid;
  }
  .step-card h3 {
    font-size: 12.5px;
    font-weight: 700;
    color: #0F172A;
    margin-bottom: 6px;
    display: flex;
    align-items: center;
    gap: 6px;
  }
  .step-card ol, .step-card ul {
    margin-left: 20px;
    color: #374151;
    font-size: 11.5px;
  }
  .step-card li {
    margin-bottom: 4px;
  }
  .step-card li:last-child {
    margin-bottom: 0;
  }

  /* Callouts & Badges */
  .tip-box {
    background: #ECFDF5;
    border: 1px solid #A7F3D0;
    border-left: 4px solid #10B981;
    padding: 8px 12px;
    border-radius: 0 6px 6px 0;
    font-size: 11px;
    color: #065F46;
    margin: 8px 0;
  }
  .warn-box {
    background: #FFFBEB;
    border: 1px solid #FDE68A;
    border-left: 4px solid #F59E0B;
    padding: 8px 12px;
    border-radius: 0 6px 6px 0;
    font-size: 11px;
    color: #92400E;
    margin: 8px 0;
  }
  code {
    background: #F1F5F9;
    color: #0F172A;
    padding: 1px 5px;
    border-radius: 4px;
    font-family: ui-monospace, SFMono-Regular, monospace;
    font-size: 10.5px;
    border: 1px solid #E2E8F0;
  }

  /* Two Column */
  .two-cols {
    display: flex;
    gap: 12px;
    margin-bottom: 10px;
  }
  .two-cols > div {
    flex: 1;
  }

  /* Footer */
  .footer {
    margin-top: 20px;
    border-top: 1px solid #E2E8F0;
    padding-top: 10px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 10px;
    color: #64748B;
  }
</style>
</head>
<body>

  <!-- SAYFA 1 -->
  <div class="header">
    <div class="title-group">
      <h1>🎮 GameTranslator TV Uygulaması Kurulum Rehberi</h1>
      <div class="subtitle">Konsol Oyunları İçin Gerçek Zamanlı Türkçe Altyazı Sistemi — Sefflex Edition</div>
    </div>
    <div class="badge-repo">github.com/Sefflex/GameTranslator</div>
  </div>

  <div class="intro-box">
    <strong>Önemli Bilgi:</strong> Televizyonunuz için harici veya karmaşık dosyalar aramanıza gerek yoktur. Bilgisayarınızdaki <strong>GameTranslator</strong> uygulaması, televizyonunuza tek tıkla ağ üzerinden bağlanır ve uygulamayı kendisi kurar. Sizin yapmanız gereken tek şey, televizyonunuzda bir defaya mahsus geliştirici modunu açmaktır. Geliştirici modu tamamen ücretsizdir, televizyona hiçbir zarar vermez ve garanti bozmaz.<br>
    <strong>Temel Kural:</strong> Bilgisayarınız ve Televizyonunuz <u>aynı ev ağında (aynı Wi-Fi veya modem)</u> bulunmalıdır.
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">1</span>
      <span>Bilgisayar Uygulamasını İndirin & Başlatın</span>
    </div>
    <div class="step-card">
      <ul>
        <li><strong>🍏 Mac (Apple Silicon M1 / M2 / M3 / M4):</strong> Proje klasöründeki <code>GameTranslator.command</code> dosyasına çift tıklayın. Sistem motorları otomatik hazırlar ve uygulamayı doğrudan açar.</li>
        <li><strong>🪟 Windows PC (Windows 10 / 11):</strong> Proje klasöründeki <code>GameTranslator.bat</code> dosyasına çift tıklayın (.NET 10 / WPF masaüstü arayüzü başlar). Bağımsız tek tıkla EXE üretmek isterseniz <code>Derle-Windows.bat</code> dosyasını çalıştırabilirsiniz.</li>
        <li><strong>🔗 Resmi Açık Kaynak Deposu:</strong> <a href="https://github.com/Sefflex/GameTranslator" style="color:#0A84FF; text-decoration:none; font-weight:600;">https://github.com/Sefflex/GameTranslator</a></li>
      </ul>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">2</span>
      <span>Televizyonunuz Destekleniyor mu? (TV Uyumluluk Tablosu)</span>
    </div>
    <table>
      <thead>
        <tr>
          <th>Televizyon Modeli / İşletim Sistemi</th>
          <th>Destek Durumu</th>
          <th>Kurulum Yöntemi</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><strong>Android TV / Google TV</strong><br><small>TCL, Philips, Sony, Xiaomi, Vestel, Beko, Grundig vb.</small></td>
          <td class="status-yes">✓ Tam Uyumlu</td>
          <td>GameTranslator'dan Tek Tıkla (ADB) veya USB'den APK</td>
        </tr>
        <tr>
          <td><strong>LG Smart TV</strong><br><small>webOS 4.0 ve üstü (2018 ve sonrası tüm OLED, QNED, NanoCell)</small></td>
          <td class="status-yes">✓ Tam Uyumlu</td>
          <td>LG Developer Mode uygulaması ile IPK paketi</td>
        </tr>
        <tr>
          <td><strong>Samsung Smart TV</strong><br><small>Tizen OS (2019 ve sonrası tüm QLED, Crystal UHD modeller)</small></td>
          <td class="status-yes">✓ Tam Uyumlu</td>
          <td>Kumandadan 1-2-3-4-5 Geliştirici Modu ile WGT paketi</td>
        </tr>
        <tr>
          <td><strong>Linux Tabanlı TV'ler / Diğerleri</strong><br><small>Vestel/Beko eski Linux, Philips Saphi, Titan OS vb.</small></td>
          <td class="status-no">✕ Desteklenmiyor</td>
          <td>HDMI üstü katman açılamaz (Mac/PC veya Mobil ekrandan izlenir)</td>
        </tr>
      </tbody>
    </table>
    <div class="tip-box">
      <strong>TV'm Android mi?</strong> Televizyonunuzun ana menüsünde <em>Google Play Store</em> veya <em>Google TV</em> yazısı varsa Android tabanlıdır. Yalnızca üreticinin kendi mağazası varsa genellikle Linux veya özel sistemdir.
    </div>
  </div>

  <!-- SAYFA 2 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📱 Android TV & Google TV Kurulumu</h1>
      <div class="subtitle">TCL, Philips, Sony, Xiaomi, Vestel ve Diğer Android / Google TV Modelleri</div>
    </div>
    <div class="badge-repo">TCL / Android TV</div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">1</span>
      <span>Televizyonda Hata Ayıklama (ADB) Modunu Açın</span>
    </div>
    <div class="step-card">
      <ol>
        <li>TV kumandanızdan <strong>Ayarlar ➔ Cihaz Tercihleri ➔ Hakkında</strong> yolunu izleyin (Google TV modellerinde: <strong>Ayarlar ➔ Sistem ➔ Hakkında</strong>).</li>
        <li>Açılan menüde <strong>Yapı Numarası (Build Number)</strong> seçeneğini bulun ve kumandanın <strong>Tamam (OK)</strong> tuşuna arka arkaya <strong>7 kez</strong> basın. Ekranda <em>"Artık bir geliştiricisiniz!"</em> uyarısı belirecektir.</li>
        <li>Bir önceki menüye dönüp <strong>Geliştirici Seçenekleri</strong> bölümüne girin.</li>
        <li><strong>"USB Hata Ayıklama"</strong> özelliğini <strong>AÇIK</strong> yapın. Menüde varsa <strong>"Ağ Üzerinden Hata Ayıklama"</strong> seçeneğini de aktif hale getirin.</li>
      </ol>
      <div class="warn-box">
        <em>Not:</em> Menü başlıkları televizyonunuzun markasına ve arayüz sürümüne göre çok küçük farklılıklar gösterebilir.
      </div>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">2</span>
      <span>GameTranslator TV Uygulamasını Kurun</span>
    </div>
    <div class="two-cols">
      <div class="step-card">
        <h3>🟢 Yöntem A: Tek Tıkla Otomatik Kur (Önerilen)</h3>
        <ol>
          <li>Bilgisayarınızda <strong>GameTranslator</strong> uygulamasını açın ve <strong>Adım 4 (Akıllı TV)</strong> ekranına gelin.</li>
          <li>TV IP Adresi alanına televizyonunuzun yerel IP'sini yazın (örn: <code>192.168.1.108</code>) veya <em>Ağda Tara</em> butonuna basın.</li>
          <li>Mavi <strong>"TV'ye Otomatik Kur"</strong> butonuna tıklayın.</li>
          <li>TV ekranında <em>"USB hata ayıklamaya izin verilsin mi?"</em> sorusu çıkar. <strong>"Bu bilgisayardan her zaman izin ver"</strong> kutusunu işaretleyip <strong>İzin Ver</strong>'e basın.</li>
          <li>Uygulama televizyonunuza otomatik yüklenir ve açılır!</li>
        </ol>
      </div>

      <div class="step-card">
        <h3>💾 Yöntem B: USB Bellek ile Kurulum</h3>
        <ol>
          <li>Proje klasöründeki <code>tv/packages/gametranslator-androidtv_1.0.0.apk</code> dosyasını bir USB flash belleğe kopyalayın.</li>
          <li>USB belleği televizyonunuza takın.</li>
          <li>TV'deki herhangi bir Dosya Yöneticisi (File Commander vb.) uygulamasından APK dosyasına tıklayıp yüklemeyi tamamlayın.</li>
          <li>Yüklenen <strong>GameTranslator</strong> uygulamasını televizyonunuzda açın.</li>
        </ol>
      </div>
    </div>
    <div class="tip-box">
      <strong>Görüntü Kontrolü:</strong> TV'de GameTranslator açıldığında oyunun HDMI görüntüsü arka planda görünür. Eğer arka plan siyah kalırsa kumandadan <strong>Tamam (OK)</strong> tuşuna basarak paneli açın ve kumandanın sol/sağ oklarıyla PS5'inizin bağlı olduğu HDMI portunu (örn: HDMI 1) seçin.
    </div>
  </div>

  <!-- SAYFA 3 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📺 LG Smart TV Kurulumu (webOS)</h1>
      <div class="subtitle">webOS 4.0+ (OLED, QNED, NanoCell, UHD Modelleri)</div>
    </div>
    <div class="badge-repo">LG webOS</div>
  </div>

  <div class="section">
    <div class="step-card">
      <h3>1. Ücretsiz LG Geliştirici Hesabı Açın</h3>
      <ol>
        <li>Bilgisayarınızın tarayıcısından <code>webostv.developer.lge.com</code> adresine gidin.</li>
        <li>Sağ üstten <strong>SIGN IN</strong>, ardından <strong>Create Account</strong> seçeneğine tıklayarak ücretsiz bir geliştirici hesabı oluşturun.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>2. TV'de Developer Mode Uygulamasını Başlatın</h3>
      <ol>
        <li>Televizyonunuzun internete bağlı olduğundan emin olun.</li>
        <li>TV'deki <strong>LG Content Store (Uygulama Mağazası)</strong>'nı açın, arama kısmına <strong>"Developer Mode"</strong> yazarak resmi uygulamayı TV'nize yükleyin.</li>
        <li>Developer Mode uygulamasını açın; açtığınız LG hesabının e-posta ve şifresiyle giriş yapın.</li>
        <li><strong>Dev Mode Status</strong> ayarını <strong>ON (Açık)</strong> yapın. Televizyon otomatik olarak yeniden başlayacaktır.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>3. GameTranslator TV Uygulamasını Kurun</h3>
      <ol>
        <li>TV açılınca <strong>Developer Mode</strong> uygulamasını tekrar açın ve <strong>Key Server</strong> seçeneğini <strong>ON</strong> yapın. Uygulamayı ekranda açık bırakın.</li>
        <li>TV ekranının sol altında 6 karakterli bir <strong>Passphrase</strong> kodu belirecektir.</li>
        <li>Bilgisayarınızda GameTranslator uygulamasını açın, LG TV sekmesinde TV'nizi seçip bu Passphrase kodunu girin ve <strong>"TV'ye Kur"</strong> butonuna tıklayın.</li>
        <li>GameTranslator <code>tv/packages/com.gametranslator.tv_1.0.0_all.ipk</code> paketini TV'nize yükler ve çalıştırır.</li>
      </ol>
      <div class="tip-box">
        <em>Süre Notu:</em> LG geliştirici modu 1000 saat açık kalır. GameTranslator uygulamanızı her kullandığınızda bu süreyi otomatik olarak baştan başlatır.
      </div>
    </div>
  </div>

  <!-- SAYFA 4 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📺 Samsung Smart TV Kurulumu (Tizen)</h1>
      <div class="subtitle">Tizen OS 2019 ve Sonrası Modeller</div>
    </div>
    <div class="badge-repo">Samsung Tizen</div>
  </div>

  <div class="section">
    <div class="step-card">
      <h3>1. TV'de Geliştirici Modunu Açın</h3>
      <ol>
        <li>Televizyonunuzda <strong>Uygulamalar (Apps)</strong> ekranına girin.</li>
        <li>Kumandanızın rakam tuşlarıyla sırasıyla <strong>1 - 2 - 3 - 4 - 5</strong> tuşlarına basın (kumandada rakam yoksa ekran klavyesiyle yazın).</li>
        <li>Ekrana <strong>Developer Mode</strong> penceresi açılacaktır. <strong>Developer Mode'u ON</strong> konumuna getirin.</li>
        <li><strong>Host PC IP</strong> alanına bilgisayarınızın yerel IP adresini yazıp <strong>OK</strong> tuşuna basın (Bilgisayarınızın IP adresi GameTranslator uygulamasında TV adımında açıkça gösterilir).</li>
        <li>Kumandanın Güç (Kapatma) tuşunu ekranda <strong>Samsung logosu çıkana kadar basılı tutarak</strong> televizyonu yeniden başlatın. Açıldığında Uygulamalar ekranında üstte <em>"Develop Mode"</em> yazısını göreceksiniz.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>2. GameTranslator ile TV Paketini Yükleyin</h3>
      <ol>
        <li>Bilgisayarınızda GameTranslator'ın TV adımında Samsung TV'nizi seçip <strong>Bağlan</strong> butonuna basın.</li>
        <li>TV ekranında bağlantı izin penceresi çıkarsa kumandayla <strong>İzin Ver</strong>'e basın.</li>
        <li>GameTranslator TV'nize özel <code>gametranslator-samsung_1.0.0.wgt</code> paketini kurar ve başlatır.</li>
      </ol>
      <div class="warn-box">
        <strong>Sorun Giderme:</strong> Eğer TV bilgisayarı kabul etmezse Host PC IP adresinizin bilgisayarınızın şu anki IP'siyle uyuştuğunu kontrol edip TV'yi kumandanın güç tuşuyla yeniden başlatın.
      </div>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">✨</span>
      <span>Kurulduğunu Nasıl Anlarım & Akıllı Özellikler</span>
    </div>
    <div class="step-card">
      <ul>
        <li><strong>Tanı ve Durum Paneli:</strong> TV'de GameTranslator açıkken kumandanızın <strong>Tamam (OK)</strong> tuşuna basarsanız ekranın altında bilgi paneli açılır. Burada seçili <strong>HDMI portu</strong>, bilgisayarınızın IP adresi ve <strong>Bağlantı: bağlı (ok)</strong> ibaresi görünür. Paneli kapatmak için tekrar <strong>Tamam (OK)</strong> tuşuna basabilirsiniz.</li>
        <li><strong>İngilizce Altyazının Üstünü Otomatik Örtme:</strong> GameTranslator'da <em>"İngilizce altyazının üstünü örtsün"</em> seçeneğini açtığınızda, Vision OCR altyazının boyutunu ve koordinatlarını milimetrik hesaplayarak orijinal İngilizce yazıyı kapatan akıllı bir siyah kutu oluşturur ve Türkçe çeviriyi tam üzerine yerleştirir.</li>
        <li><strong>Bilgisayar Oyun Sesini Kapatma (Mute):</strong> Oyunun sesi zaten HDMI ile televizyonunuzdan geldiği için, GameTranslator arayüzünden tek tıkla <strong>"PC Sesi: Sessiz (Mute)"</strong> modunu açarak bilgisayar hoparlörünü sessize alabilirsiniz.</li>
      </ul>
    </div>
  </div>

  <div class="footer">
    <div>🎮 GameTranslator v1.0 Pro — Sefflex Edition | Tamamen Ücretsiz & Açık Kaynak</div>
    <div>Destek & Katkı: <strong>github.com/Sefflex/GameTranslator</strong></div>
  </div>

</body>
</html>
'''

html_path = '/tmp/gametranslator_kurulum_rehberi.html'
with open(html_path, 'w', encoding='utf-8') as f:
    f.write(html_content)

chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
pdf_repo = os.path.abspath('GameTranslator_TV_Kurulum_Rehberi.pdf')
pdf_artifact = '/Users/sefflex/.gemini/antigravity/brain/d24ae91f-b40c-43d3-b017-0a5746c34d96/GameTranslator_TV_Kurulum_Rehberi.pdf'

cmd = [chrome, '--headless=new', '--disable-gpu', '--no-pdf-header-footer', f'--print-to-pdf={pdf_repo}', html_path]
res = subprocess.run(cmd, capture_output=True, text=True)
print('Chrome returncode:', res.returncode)

import shutil
shutil.copyfile(pdf_repo, pdf_artifact)
print('PDF generated successfully at:', pdf_repo)
print('Size:', os.path.getsize(pdf_repo))
