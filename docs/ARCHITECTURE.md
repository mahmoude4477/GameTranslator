# 🏛️ GameTranslator — بنية النظام التفصيلية ودليل المطوّر

أُعدّت هذه الوثيقة للمطوّرين، وتشرح طريقة عمل **GameTranslator** من الداخل: مسار الصورة من PS5 إلى التعرّف على النص (OCR)،
والترجمة إلى العربية، وبروتوكول الشبكة الذي يوصل الترجمة النصية إلى التلفزيون، وخوارزميات تثبيت الترجمة.

المشروع الأصلي: GameTranslator من تأليف Sefflex (AGPL-3.0). هذه النسخة العربية:
<https://github.com/mahmoude4477/GameTranslator>.

---

## 1. تدفق البيانات (Data Pipeline)

```
[ PlayStation 5 ]
       │
       ├──> (HDMI) ──────────────────────────────────────────────> [ TV screen ] (game picture, untouched)
       │                                                                  ▲
       └──> (Remote Play, local network)                                  │
                  │                                                       │
                  ▼                                                       │
     [ Chiaki / chiaki-ng stream ]                                        │
                  │  Mac:     Chiaki window captured every 0.2 s          │
                  │  Windows: gametranslator_native.dll frames,           │
                  │           or Chiaki window captured every 150 ms      │
                  ▼                                                       │
     [ Crop the reading region ]                                          │
                  │  (Windows: region signature changed?)                 │
                  ├──[no]───> reuse the last reading (no OCR)             │
                  │                                                       │
                  └──[yes]──> [ OCR: Apple Vision / Windows.Media.Ocr ]   │
                                       │ (text lines + boxes)             │
                                       ▼                                  │
                                [ Noise & button-prompt filters ]         │
                                       │ (Windows: L2, R2, junk words)    │
                                       ▼                                  │
                                [ SubtitleStabilizer ]                    │
                                       │ (stable line → translate once)   │
                                       ▼                                  │
                                [ Translator (online) ]                   │
                                  ├─ Google Translate (default)           │
                                  └─ DeepL (with the user's key)          │
                                       │                                  │
                                       ▼                                  │
                                [ Subtitle server (8787) ]                │
                                       │                                  │
                                       └───> (HTTP SSE / local network) ──┘
                                                (subtitle JSON, Arabic, right-to-left)
```

صورة اللعبة وصوتها يصلان إلى التلفزيون مباشرة عبر HDMI ولا يمرّان بالكمبيوتر. الكمبيوتر يقرأ نسخة من الصورة عبر
Remote Play فقط، ويرسل إلى التلفزيون نص الترجمة لا غير.

---

## 2. المكوّنات

### 2.1. استقبال الفيديو

- **Windows** (`windows/src/GameTranslator.RemotePlay/`، `windows/native/`): يستخدم `StreamSession` المكتبة الأصلية
  `gametranslator_native.dll`، وهي واجهة C فوق libchiaki مع FFmpeg (مفكّك h264 و swscale) تُبنى من `windows/native/`
  بـ CMake (تحتاج إلى شيفرة chiaki-ng في `third-party/chiaki-ng`، وهي غير مضمّنة في المستودع). إذا لم توجد المكتبة
  (`StreamSession.IsNativeAvailable`) يشغّل التطبيق Chiaki عبر `ChiakiLauncher` ويلتقط نافذته دوريًا
  (`WindowCaptureStreamSession`، `WindowCaptureService`). مسار البناء الجاهز (`.github/workflows/release-windows.yml`)
  لا يبني المكتبة الأصلية، بل يرفق نسخة chiaki-ng المحمولة في المجلد `chiaki/`.
- **Mac** (`mac/NativeApp.swift`): يشغّل Chiaki (من `/Applications/Chiaki.app` أو النسخة المضمّنة في
  `Contents/Helpers`) ويخفي نافذته خلف نافذة GameTranslator، ثم يلتقط صورة النافذة كل 0.2 ثانية
  (`CGWindowListCreateImage`). لذلك يحتاج إلى إذن تسجيل الشاشة.
- **المطابقة والبحث عن PS5**: في Windows `Registration.cs` و`PsnAccount.cs` و`ConsoleDiscovery.cs` (بحث UDP على المنفذ
  9302)؛ ويرافق التطبيقين المساعد `scripts/ps5-register.js` (Node.js).

### 2.2. منطقة القراءة وتوقيعها (`Frame.cs`، `ReadScheduler.cs`)

تشغيل OCR على كل إطار يرهق المعالج. في Windows يُحلّ ذلك بمرحلتين:
1. **قص المنطقة (`RegionCropper.Crop`)**: تُقص المنطقة التي تظهر فيها الترجمة فقط (الافتراضي `CropRegion`: من 62% إلى 97%
   عموديًا، ومن 14% إلى 86% أفقيًا). يختار المستخدم المنطقة من الواجهة.
2. **توقيع المنطقة (`RegionCropper.Signature`)**: نسخة رمادية مصغّرة من المنطقة (64×12 عيّنة). إذا لم يتغير التوقيع عن
   الإطار السابق لا يُستدعى OCR ويُعاد استخدام القراءة الأخيرة (`ReadScheduler.Plan` ← `Reused`)، بحد أقصى 20 إطارًا
   متتاليًا. وفي المنطقة الفارغة تُجرَّب «القراءة العميقة» (تكبير الصورة لالتقاط الأسطر القصيرة) مرة كل 3 قراءات.

في Mac تُقص المنطقة وتُقرأ بـ Apple Vision (`VNRecognizeTextRequest`، المستوى `accurate`) في كل التقاط ما لم تكن قراءة سابقة
ما زالت جارية.

### 2.3. الفلاتر وخوارزمية التثبيت (`Reading.cs`، `OcrNoise.cs`، `SubtitleStabilizer.cs`)

في Windows تمرّ القراءة قبل الترجمة بفلاتر: تلميحات أزرار ذراع التحكم (`WithoutButtonPrompts`، مثل "R2 Swing")، والكلمات
العشوائية التي يختلقها OCR (`WithoutOcrNoise`)، واختيار كتلة الترجمة وحدها من بين القوائم والمؤشرات (`SubtitleBlock`).

ثم يتابع `SubtitleStabilizer` القراءات إطارًا إطارًا:
* **الاستقرار:** يُرسَل السطر إلى الترجمة حين يتكرر في قراءتين متتاليتين (تشابه 0.85)، أو فورًا إذا انتهى بعلامة ترقيم
  (نقطة، تعجب، استفهام، …). بذلك لا يُترجَم السطر وهو يُكتب حرفًا حرفًا (تأثير الآلة الكاتبة).
* **عتبة الإطارات الفارغة:** لا تُمسح الترجمة فور اختفاء النص؛ يُنتظر 6 قراءات فارغة متتالية
  (`emptyFramesToClear: 6`، نحو 0.3 ثانية). والقراءة الأقصر من 3 أحرف تُعدّ فارغة.
* **عتبة التشابه:** إعادة قراءة السطر نفسه بفروق طفيفة ("Trust" و"Trust.") لا تطلب ترجمة جديدة.
* **مدة العرض:** ما دام السطر على شاشة اللعبة يبقى على التلفزيون حتى يُمسح. وإذا اختفى قبل وصول ترجمته تُعرض الترجمة مدة تكفي لقراءتها:
  $$\text{المدة (ms)} = \min(8000,\ 2000 + (\text{عدد الأحرف} \times 60))$$
  (`SubtitlePipeline.ReadingTime`، والقاعدة نفسها في تطبيق التلفزيون).
* **الترتيب:** يحمل كل طلب ترجمة رقمًا تسلسليًا؛ فإذا عادت الترجمات بغير ترتيبها لا تُعرض ترجمة أقدم فوق ترجمة أحدث.

في Mac المنطق أبسط: يُرسَل النص حين يختلف عن آخر نص أُرسل، ويُنسى آخر نص بعد 4 قراءات فارغة.

---

## 3. محركات الترجمة

الترجمة تجري عبر الإنترنت، واللغة الهدف العربية افتراضيًا. لا يوجد نموذج يعمل بلا إنترنت.

| المحرك | الموضع في الشيفرة | متى يُستخدم |
|---|---|---|
| **Google Translate** | Windows: `GameTranslator.Translation/GoogleGtxTranslator.cs` · Mac: `translateGoogleGTX` | الافتراضي؛ لا يحتاج إلى مفتاح |
| **DeepL** | Windows: `GameTranslator.Translation/DeepLTranslator.cs` · Mac: `translateDeepL` | حين يحفظ المستخدم مفتاح DeepL في الإعدادات |

- **Windows:** يقسّم `SubtitleSegmenter` الترجمة النصية إلى جمل ويترك اسم المتكلم ("KRATOS:") وشرطات الحوار كما هي.
  يحتفظ `DeepLTranslator` بذاكرة مؤقتة للترجمات ويضيف السطر السابق سياقًا؛ وإذا تعذّر الوصول إلى DeepL أو انتهت الحصة
  يُترجَم السطر بالمترجم الاحتياطي (Google) ولا يُجرَّب DeepL مدة دقيقة.
- **Mac:** يُجرَّب DeepL أولًا إن وُجد مفتاح، ثم Google. تعرض شاشة الإعدادات «نماذج» أخرى، لكن هذين المحركين وحدهما
  موصولان فعليًا.
- **اللغة الهدف:** في Windows المفتاح `TargetLanguage` في `%LOCALAPPDATA%\GameTranslator\settings.json` (الافتراضي `"ar"`)؛
  وفي Mac القيمة `gt_target_language` (الافتراضي `ar`).
- **التعرّف على النص (OCR)** يجري على الجهاز نفسه: Apple Vision في Mac، و`Windows.Media.Ocr` في Windows
  (`WindowsOcrReader`).

---

## 4. خادم الترجمة النصية وبروتوكول الشبكة

الخادم مكتوب بلا مكتبات خارجية: في Windows `GameTranslator.Core/SubtitleServer.cs` (`TcpListener`)، وفي Mac
`NativeSubtitleServer` داخل `mac/NativeApp.swift` (`NWListener` من `Network.framework`). يستمع على منفذ واحد (8787):

| المسار | الوظيفة |
|---|---|
| `GET /events` | بث Server-Sent Events إلى تطبيق التلفزيون؛ طبقة OBS تتصل بـ `/events?viewer=obs` فلا تُحتسب تلفزيونًا |
| `GET /obs` | صفحة طبقة OBS (`windows/src/GameTranslator.Core/Resources/obs-overlay.html`) |
| `GET /hello` | يردّ بـ `{"app":"gametranslator"}`؛ به يتعرّف تطبيق التلفزيون على الكمبيوتر في الشبكة |

* **Keep-Alive:** لاكتشاف انقطاع Wi-Fi عن التلفزيون يُفعَّل TCP keep-alive (بعد 10 ثوانٍ، كل 5 ثوانٍ، 3 محاولات) ويُرسَل
  سطر `: ping` عبر SSE كل 15 ثانية. ويبدأ البث بـ `retry: 2000` وبحدث `style` الحالي.
* **اتجاه النص:** الأسطر المكتوبة من اليمين إلى اليسار تُحاط بعلامتَي الاتجاه U+202B و U+202C (`Directional` في Windows،
  `directional` في Mac)، كل سطر على حدة؛ فتبقى علامات الترقيم واسم المتكلم في الطرف الصحيح حتى في نسخ تطبيق التلفزيون
  الأقدم. وتطبيق التلفزيون وطبقة OBS يضبطان `dir="rtl"` بنفسيهما عند وجود أحرف عربية.

### صيغ الأحداث (Server-Sent Events)

#### الحدث `subtitle`
```json
event: subtitle
data: {
  "at": 1710000000000,
  "duration": 15000,
  "text": "الحارس",
  "box": {
    "left": 0.35,
    "top": 0.82,
    "right": 0.65,
    "bottom": 0.88
  }
}
```

`box` موضع الترجمة الإنجليزية في صورة اللعبة (نسب من 0 إلى 1) وهو اختياري. يضيف Windows الحقل `"paragraphs": true` للنصوص
المتعددة الأسطر (مثل لوحة المهام)، ويضيف Mac الحقل `"original"` (النص الإنجليزي).

#### الحدث `style`
```json
event: style
data: {
  "background": 72,
  "bold": false,
  "bottomPercent": 7,
  "color": "#FFFFFF",
  "fontSize": 46,
  "placement": "cover"
}
```

`placement` واحد من: `cover` (تغطية الترجمة الإنجليزية)، `above` (فوقها)، `below` (تحتها)، `fixed` (ارتفاع ثابت يحدده
`bottomPercent`).

#### الحدثان `clear` و`screen`
`clear` يمسح الترجمة المعروضة. و`screen` (Windows، ميزة «ترجِم الشاشة») يحمل `items`: كل عنصر فيه `text` وموضعه `box`
وارتفاع السطر `line`، مع `duration`.

---

## 5. طبقة التلفزيون

واجهة التلفزيون تطبيق ويب واحد مشترك في `tv/webos/` (`index.html`، `app.js`، `style.css`، `i18n.js`، `config.js`)،
ويختلف ملف `platform.js` وحده بحسب المنصة:

| المنصة | الملفات | صورة HDMI |
|---|---|---|
| **LG webOS** | `tv/webos/platform.js`، `appinfo.json` | عنصر فيديو مصدره `ext://hdmi:<المنفذ>` |
| **Samsung Tizen** | `tv/tizen/platform.js`، `config.xml` | `tizen.tvwindow` خلف طبقة HTML (`BEHIND`) |
| **Android TV** | `tv/android/platform.js`، `MainActivity.java` | `TvView` تحت `WebView` شفاف |

يتصل `app.js` بالخادم عبر `EventSource`، ويعرض الترجمة بحسب `placement` و`box`، ويضبط اتجاه السطر (`dir`) من اليمين
إلى اليسار للعربية.

منطق العرض في Android TV (`tv/android/app/src/main/java/com/gametranslator/tv/MainActivity.java`):

1. **صورة المنفذ (`TvView`):**
   يُرسم منفذ HDMI القادم من خدمة التلفزيون في Android بملء الشاشة في الطبقة السفلى.
2. **العرض الشفاف (`WebView`):**
   يُنشأ فوق `TvView` عنصر `WebView` بخلفية `Color.TRANSPARENT` وبالنمط `LAYER_TYPE_HARDWARE`، ويحمّل الواجهة المشتركة.
3. **مطابقة عرض 1920 بكسل CSS:**
   لتفادي أخطاء تكبير WebView في تلفزيونات Android تُستخدم الدالة `fitScalePercent()`:
   $$\text{Scale} = \max\left(1, \text{round}\left(\frac{\text{عرض الشاشة} \times 100}{1920}\right)\right)$$
   فتأخذ أحجام الترجمة وإحداثيات المربّع المقاس نفسه على كل شاشة.

**الحزم:** `scripts/package-ipk.py` ينتج حزمة LG ‏(`tv/packages/com.gametranslator.tv_1.0.0_all.ipk`)،
و`scripts/package-tizen.sh` ينتج حزمة Samsung غير الموقّعة (انظر [tv-samsung.md](tv-samsung.md))، ومشروع Gradle في
`tv/android` ينتج ملف APK. النسخ الجاهزة في `tv/packages/`؛ وملف APK الموجود هناك من البناء الأقدم (لم يُعَد بناؤه) ويعرض
الترجمة العربية بشكل صحيح بفضل علامات الاتجاه. خطوات التثبيت للمستخدم: [tv-setup.ar.md](tv-setup.ar.md).

---

## 6. بنية المستودع والبناء

| المسار | المحتوى |
|---|---|
| `mac/NativeApp.swift`، `mac/build.sh`، `mac/Resources/` | تطبيق macOS (ملف Swift واحد؛ Apple Silicon، macOS 14 فما فوق) |
| `windows/src/GameTranslator.App` | واجهة WPF (‎.NET 10) والخدمات الخاصة بـ Windows (OCR، التقاط النافذة، جدار الحماية، الاختصارات) |
| `windows/src/GameTranslator.Shell` | المتحكّم `AppController` وعقود الخدمات |
| `windows/src/GameTranslator.Core` | مسار الترجمة (`SubtitlePipeline`)، خادم الترجمة، التحكم بتلفزيونات LG و Samsung، النصوص (`Resources/Localizable.xcstrings`) |
| `windows/src/GameTranslator.Translation` | Google ‏(GTX)، DeepL، تقسيم الجمل |
| `windows/src/GameTranslator.RemotePlay` | الاتصال بـ PS5 والمطابقة |
| `windows/src/GameTranslator.TvInstall`، `GameTranslator.TvHelper` | تثبيت تطبيق التلفزيون على Samsung و Android TV، وأداة سطر الأوامر المقابلة |
| `windows/tests/`، `windows/native/` | الاختبارات، وواجهة C فوق chiaki |
| `tv/webos`، `tv/tizen`، `tv/android`، `tv/packages` | تطبيق التلفزيون وحزمه الجاهزة |
| `scripts/` | `package-ipk.py`، `package-tizen.sh`، `android-tv-install.py`، `ps5-register.js`، `build-tv-helper.sh` |
| `design/` | الشعار والأيقونات و`tokens.json` (انظر [DESIGN.md](DESIGN.md)) |

- **Mac:** `bash mac/build.sh` (يشغّله `GameTranslator.command` عند أول تشغيل) يبني `mac/NativeApp.swift` بـ `swiftc`،
  ويجمع `dist/GameTranslator.app` مع حزم التلفزيون والمساعدات، ويوقّعه وينسخه إلى `/Applications`.
- **Windows:** يحتاج إلى ‎.NET 10 SDK. `GameTranslator.bat` يبني ويشغّل، و`Build-Windows.bat` ينتج نسخة مستقلة في
  `bin\GameTranslator.exe`؛ أو يدويًا:
  `dotnet publish windows/src/GameTranslator.App/GameTranslator.App.csproj -c Release -r win-x64 --self-contained -o bin`.
  الاختبارات: `dotnet test windows/GameTranslator.Windows.slnx`. شرح المستخدم: [WINDOWS_SETUP.md](WINDOWS_SETUP.md).

---

## 7. مبادئ المساهمة

* **بلا تبعيات ثقيلة:** الخادم والمسار الأساسي لا يعتمدان على حزم خارجية ثقيلة (Electron، React)؛ بل يستخدمان
  إمكانات نظام التشغيل نفسه.
* **التوافق مع النسخ السابقة:** أي حقل جديد في `style` أو `subtitle` يجب ألا يعطّل نسخ تطبيق التلفزيون الأقدم؛ يتجاهل
  التطبيق الحقول التي لا يعرفها، وتُعطى الحقول الجديدة قيمًا افتراضية.
* **العربية أولًا:** نصوص الواجهة عربية، والمخرجات تُعرض من اليمين إلى اليسار؛ اختبر أي تغيير في العرض بسطر عربي يحتوي
  على اسم متكلم وعلامات ترقيم.
