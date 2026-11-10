# Gizlilik / Privacy

*Son güncelleme / Last updated: 2026-11-01*

## Türkçe

GameTranslator, Sefflex tarafından yayınlanan açık kaynaklı bir uygulamadır. Hiçbir kişisel veriyi toplamaz,
saklamak üzere kendi sunucularımıza göndermez; bizim bir sunucumuz yoktur. Reklam, analiz ya da izleme
kodu içermez.

**Bilgisayarda kalanlar**

- PS5'ten gelen oyun görüntüsü yalnız altyazıyı okumak için bellekte işlenir; kaydedilmez, hiçbir yere
  gönderilmez.
- Okunan altyazılar ve çevirileri bilgisayarda üretilir ve yalnız ev ağınızdaki TV'ye gönderilir (çevrimiçi çeviriyi
  açarsanız İngilizce metin DeepL'e de gider; aşağıya bakın).
- PlayStation Network hesap kimliği (sayısal), PS5 eşleme anahtarı ve TV eşleme anahtarı bilgisayarda
  saklanır: Mac'te Anahtar Zinciri'nde, Windows'ta yalnız sizin kullanıcı hesabınızın açabileceği
  şifreli bir dosyada (DPAPI).

**Dışarıyla kurulan bağlantılar**

- **Sony:** PlayStation Network girişi doğrudan Sony'nin sayfasında yapılır; şifrenizi GameTranslator görmez.
  Girişten sonra Sony'den yalnız hesap kimliği alınır, oturum anahtarı saklanmaz.
- **Hugging Face (yalnız Windows):** ilk açılışta çeviri modeli `huggingface.co` adresinden bir kez indirilir.
- **Apple (yalnız Mac):** çeviri dilleri macOS tarafından indirilir ve yönetilir.
- **DeepL (yalnız siz açarsanız):** ayarlarda kendi DeepL anahtarınızı girip çevrimiçi çeviriyi açarsanız okunan
  İngilizce altyazı metni çeviri için DeepL'e (`api.deepl.com` ya da `api-free.deepl.com`) gönderilir. Görüntü,
  ses ve hesap bilgisi gönderilmez. DeepL'in kendi gizlilik koşulları geçerlidir. Anahtar Mac'te Anahtar
  Zinciri'nde, Windows'ta DPAPI ile şifreli dosyada saklanır. Açmazsanız DeepL'e hiçbir şey gönderilmez.
- **Ev ağı:** PS5 ve LG TV'yi bulmak ve onlarla eşleşmek için yalnız yerel ağ kullanılır.

TV uygulaması altyazıları yalnız ev ağındaki GameTranslator bilgisayarından alır ve internete bağlanmaz.

Sorularınız için: https://github.com/Sefflex/GameTranslate/issues

## English

GameTranslator is open-source software published by Sefflex. It does not collect personal data and does not
send any data to servers of ours (we do not operate any). It contains no advertising, analytics or
tracking code.

**Stays on your computer:** the game picture received from the PS5 is processed in memory only to read
subtitles and is never stored or uploaded. Subtitles and translations are produced on your computer and
sent only to the TV on your home network. Your numeric PlayStation Network account ID and the PS5 and TV
pairing keys are stored locally (Keychain on Mac; a file encrypted for your Windows user account with
DPAPI on Windows).

**External connections:** PlayStation Network sign-in happens on Sony's own page, and GameTranslator never sees
your password; afterwards only the account ID is requested from Sony, and no session token is kept. On
Windows, the translation model is downloaded once from `huggingface.co`. On Mac, translation languages are
downloaded and managed by macOS. If you enter your own DeepL key in settings and turn on online
translation, the English subtitle text that was read is sent to DeepL (`api.deepl.com` or
`api-free.deepl.com`) to be translated; no picture, audio or account data is sent, and DeepL's own privacy
terms apply. The key is stored in the Keychain on Mac and in the DPAPI-encrypted file on Windows. Nothing is
sent to DeepL unless you turn this on. PS5 and TV discovery and pairing use your local network only.

The TV app receives subtitles only from the GameTranslator computer on your home network and does not connect
to the internet.

Contact: https://github.com/Sefflex/GameTranslate/issues
