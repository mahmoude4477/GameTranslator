# 🎮 GameTranslator — النسخة العربية
### ترجمة نصية عربية فورية لألعاب PlayStation 5 على تلفزيونك، من دون أي تأخير في صورة اللعبة

[![المنصة: macOS | Windows](https://img.shields.io/badge/%D8%A7%D9%84%D9%85%D9%86%D8%B5%D8%A9-macOS%20%7C%20Windows-blue?style=flat-square)](https://github.com/mahmoude4477/GameTranslator)
[![التلفزيون: Android TV | LG webOS | Samsung Tizen](https://img.shields.io/badge/%D8%A7%D9%84%D8%AA%D9%84%D9%81%D8%B2%D9%8A%D9%88%D9%86-Android%20TV%20%7C%20LG%20webOS%20%7C%20Samsung%20Tizen-green?style=flat-square)](https://github.com/mahmoude4477/GameTranslator)
[![الترجمة: العربية](https://img.shields.io/badge/%D8%A7%D9%84%D8%AA%D8%B1%D8%AC%D9%85%D8%A9-%D8%A7%D9%84%D8%B9%D8%B1%D8%A8%D9%8A%D8%A9-brightgreen?style=flat-square)](#how-it-works)
[![صورة اللعبة: مباشرة عبر HDMI](https://img.shields.io/badge/%D8%B5%D9%88%D8%B1%D8%A9%20%D8%A7%D9%84%D9%84%D8%B9%D8%A8%D8%A9-%D9%85%D8%A8%D8%A7%D8%B4%D8%B1%D8%A9%20%D8%B9%D8%A8%D8%B1%20HDMI-brightgreen?style=flat-square)](#how-it-works)
[![الرخصة: AGPL-3.0](https://img.shields.io/badge/%D8%A7%D9%84%D8%B1%D8%AE%D8%B5%D8%A9-AGPL--3.0-red?style=flat-square)](LICENSE)

**GameTranslator** نظام مفتوح المصدر يتيح لك أن تلعب ألعاب PlayStation 5 التي لا تدعم العربية وأنت ترى **الترجمة العربية** على تلفزيونك. صورة اللعبة وصوتها يصلان إلى التلفزيون مباشرة عبر HDMI، فلا يتأثر زمن استجابة ذراع التحكم ولا جودة الصورة.

هذه هي النسخة العربية من المشروع: لغة الترجمة والواجهة هي العربية، وتُعرض الأسطر من اليمين إلى اليسار. المشروع الأصلي هو [GameTranslator من تطوير Sefflex](https://github.com/Sefflex/GameTranslator).

---

<a id="how-it-works"></a>

## 🧠 الفكرة الأساسية: كيف يعمل في 3 جمل؟

1. **الصورة تبقى على التلفزيون (بلا تأخير في اللعب):** جهاز PS5 موصول بتلفزيونك مباشرة **بكابل HDMI**. صورة اللعبة وصوتها يأتيان من التلفزيون كما هما، وذراع التحكم DualSense يعمل من دون أي تأخير إضافي.
2. **الكمبيوتر يتابع في الخلفية:** الكمبيوتر (Mac أو Windows) يتصل بجهاز PS5 عبر الشبكة المنزلية باستخدام Remote Play. وحتى لا ينفصل ذراع التحكم عن التلفزيون يتصل الكمبيوتر بـ**حساب ثانٍ مجاني** على جهاز الألعاب. يقرأ الكمبيوتر الترجمة الإنجليزية من الصورة (OCR) على الجهاز نفسه، ثم يترجمها إلى العربية عبر الإنترنت (Google Translate افتراضيًا، أو DeepL بمفتاحك الخاص).
3. **التلفزيون يستقبل الترجمة فقط:** يرسل الكمبيوتر النص العربي إلى تطبيق GameTranslator على التلفزيون (Android TV أو LG webOS أو Samsung Tizen)، فيعرضه التلفزيون في طبقة شفافة فوق صورة HDMI، من اليمين إلى اليسار، فوق السطر الإنجليزي الأصلي.

> **ملاحظة صادقة عن السرعة:** صورة اللعبة لا تتأخر إطلاقًا لأنها لا تمر بالكمبيوتر. أما الترجمة النصية نفسها فتظهر بعد لحظة قصيرة من ظهور السطر الإنجليزي، لأنها تحتاج إلى التعرّف على النص ثم طلب ترجمة عبر الإنترنت. لذلك يحتاج الكمبيوتر إلى اتصال بالإنترنت أثناء اللعب.

---

## 🖥️ واجهة سطح المكتب (Mac و Windows)

تطبيق GameTranslator تطبيق أصلي على **macOS (SwiftUI)** وعلى **Windows (WPF / .NET 10)** بواجهة عربية داكنة وبسيطة، ولا يحتاج إلى متصفح. تختلف بعض التفاصيل بين النظامين، لكن العناصر الأساسية واحدة:

* **اللوحة الرئيسية:**
  * **▶ ابدأ الترجمة / ■ أوقف الترجمة:** زر واحد يشغّل النظام كله ويوقفه.
  * **حالة الاتصال:** نقاط ملوّنة (أخضر / برتقالي / رمادي) تبيّن حالة الاتصال بجهاز PS5 وبالتلفزيون.
  * **🔊 صوت اللعبة على الكمبيوتر:** صوت اللعبة يصل أصلًا من التلفزيون عبر HDMI، لذا يمكنك كتم صوت البث على الكمبيوتر حتى لا تسمع الصوت مرتين.
  * **آخر سطر ترجمة:** يعرض آخر سطر تُرجم، مع إمكانية نسخه.

* **الإعدادات والمعايرة:**
  * **منطقة القراءة:** إطار تحدّده فوق صورة اللعبة (16:9) ليعرف التطبيق أين يقرأ الترجمة الإنجليزية، مع أشرطة تمرير دقيقة للحدود العليا والسفلى واليمنى واليسرى.
  * **موضع الترجمة على التلفزيون:** يحسب التطبيق مكان السطر الإنجليزي وحجمه من نتيجة OCR، فيضع الترجمة العربية فوقه لتغطيته، أو أعلاه، أو أسفله، أو في موضع ثابت على الشاشة.
  * **مظهر الترجمة:** حجم الخط، الخط العريض، شفافية الخلفية، والارتفاع عن أسفل الشاشة.
  * **محرّك الترجمة:** Google Translate افتراضيًا، أو DeepL عند إدخال مفتاحك (انظر [الجدول أدناه](#translation-engines)).
  * **OBS:** لأصحاب البث المباشر رابط طبقة جاهز يضاف في OBS كمصدر متصفح (Browser Source): `http://localhost:8787/obs`.

---

## 🎯 دليل الإعداد لكل منصة (خطوة بخطوة، لمن لا يعرف البرمجة)

> ### 📄 دليل تثبيت تطبيق التلفزيون المصوَّر (PDF)
> دليل عربي مفصّل يشرح تثبيت التطبيق على تلفزيونك (Android TV و LG webOS و Samsung Tizen):
> 👉 **[دليل إعداد تطبيق التلفزيون (PDF)](GameTranslator_TV_Setup_Guide.pdf)** — والنسخة النصية منه في [`docs/tv-setup.ar.md`](docs/tv-setup.ar.md).

تحتاج إلى ثلاثة أشياء: كمبيوتر (Mac أو Windows)، وجهاز PS5 موصول بالتلفزيون عبر HDMI، وتلفزيون ذكي مدعوم. يجب أن تكون الأجهزة الثلاثة على **الشبكة المنزلية نفسها**.

---

### 1. 🍏 Mac (معالجات Apple Silicon: M1 / M2 / M3 / M4، نظام macOS 14 أو أحدث)
> التعرّف على النص يجري على الجهاز باستخدام Apple Vision. الترجمة تجري عبر الإنترنت (Google Translate، أو DeepL بمفتاحك).

#### 🟢 الطريقة (أ): بناء وتشغيل تلقائي بنقرة واحدة (موصى بها)
1. **نزّل المشروع من GitHub:** افتح `https://github.com/mahmoude4477/GameTranslator`، اضغط الزر الأخضر **`<> Code`** ثم اختر **`Download ZIP`**، وفُكّ ضغط الملف في مجلد.
2. **انقر نقرًا مزدوجًا على المشغّل:** داخل المجلد انقر مرتين على الملف **`GameTranslator.command`**.
   * *في المرة الأولى:* يلاحظ المشغّل أن التطبيق لم يُبنَ بعد، فيبنيه من الملف `mac/NativeApp.swift`، ويوقّعه محليًا، ويثبّته في `/Applications/GameTranslator.app` ثم يفتحه. يستغرق ذلك وقتًا قصيرًا في المرة الأولى فقط.
   * *في المرات التالية:* يفتح التطبيق مباشرة.
3. **Remote Play على Mac:** يفتح التطبيق بث PS5 عبر برنامج Chiaki المفتوح المصدر، ويبحث عنه في `/Applications/Chiaki.app` أو `/Applications/chiaki-ng.app`. إن لم يكن مثبتًا فثبّت [chiaki-ng](https://github.com/chiaki-ng/chiaki-ng/releases) أولًا.
4. **⚠️ نصائح الأمان في Mac (عند الفتح أول مرة):**
   * **إذا ظهرت رسالة أن الملف لا يمكن فتحه لأن مطوّره غير موثَّق، أو أنه لا توجد صلاحية:**
     انقر على الملف **بالزر الأيمن** ثم اختر **«فتح»**، واضغط **«فتح»** مرة أخرى في التحذير. أو افتح Terminal داخل المجلد ونفّذ:
     ```bash
     chmod +x GameTranslator.command
     ```
   * **إذا ظهر تحذير بأن الأمر `swiftc` يحتاج إلى أدوات المطوّر:**
     اضغط **«تثبيت»** في نافذة Apple الرسمية؛ تُثبَّت أدوات Apple Command Line Tools مجانًا وتلقائيًا، ثم شغّل الملف من جديد.
   * **إذن تسجيل الشاشة (ضروري لقراءة الترجمة):**
     افتح **«إعدادات النظام»** ثم **«الخصوصية والأمان»** ثم **«تسجيل الشاشة وصوت النظام»**، وفعّل المفتاح أمام **`GameTranslator`**.

#### 🛠️ الطريقة (ب): البناء يدويًا من Terminal
1. افتح تطبيق **Terminal** (اضغط `Command + Space` واكتب `Terminal`).
2. ادخل إلى مجلد المشروع (اكتب `cd ` ثم اسحب المجلد إلى نافذة Terminal واضغط Enter):
   ```bash
   cd /path/to/GameTranslator-main
   ```
3. شغّل سكربت البناء:
   ```bash
   bash mac/build.sh
   ```
4. عند انتهاء البناء تظهر رسالة نجاح، ويُثبَّت التطبيق تلقائيًا في `/Applications/GameTranslator.app`.
5. افتحه بالنقر المزدوج على `GameTranslator.command` أو من مجلد «التطبيقات» (Applications).

---

### 2. 🪟 Windows (نظام Windows 10 / 11)
> تطبيق سطح مكتب أصلي مبني بـ C# / .NET 10 و WPF. التعرّف على النص يجري على الجهاز باستخدام Windows.Media.Ocr، والترجمة تجري عبر الإنترنت (Google Translate، أو DeepL بمفتاحك).

#### 🟢 الطريقة (أ): إصدار جاهز (إن نُشر إصدار جاهز)
1. افتح صفحة الإصدارات: `https://github.com/mahmoude4477/GameTranslator/releases`. إن وجدت فيها إصدارًا جاهزًا فنزّل الملف **`GameTranslator-Windows-x64.zip`**. وإن لم يُنشر إصدار بعد فاستخدم الطريقة (ب)؛ البناء من المصدر متاح دائمًا.
2. انقر على ملف ZIP بالزر الأيمن واختر **«استخراج الكل»** إلى مجلد. لا تشغّل التطبيق من داخل الملف المضغوط.
3. انقر مرتين على **`GameTranslator.exe`** (أو على `GameTranslator-Start.bat` الذي يزيل حظر الملفات ثم يشغّل التطبيق).
4. **⚠️ نصائح الأمان في Windows (عند الفتح أول مرة):**
   * **إذا ظهرت نافذة SmartScreen الزرقاء (`Windows protected your PC`):**
     اضغط **«معلومات إضافية»** (`More info`) ثم **«تشغيل على أي حال»** (`Run anyway`). هذا معتاد في المشاريع المفتوحة المصدر الجديدة التي لم تشترِ شهادة توقيع رقمي؛ ويمكنك دائمًا مراجعة الشيفرة وبناؤها بنفسك.
   * **إذا ظهر تنبيه جدار الحماية (إذن الشبكة):**
     حتى يجد التطبيق جهاز PS5 وتطبيق التلفزيون على الشبكة المحلية ويرسل إليه الترجمة، ضع علامة على **«الشبكات الخاصة»** ثم اضغط **«السماح بالوصول»**.

#### 🛠️ الطريقة (ب): التشغيل أو البناء من الشيفرة المصدرية
تحتاج إلى **.NET 10 SDK** (مجاني من `https://dotnet.microsoft.com/download`). بعد تنزيل المشروع (ZIP أو `git clone`):
1. **تشغيل بنقرة واحدة:** انقر مرتين على **`GameTranslator.bat`** في مجلد المشروع؛ يبني المشروع ويفتحه.
2. **بناء ملف `.exe` مستقل:** انقر مرتين على **`Build-Windows.bat`**؛ يبني المشروع كتطبيق مستقل (Self-Contained) ويحفظه في المجلد `bin\` باسم **`GameTranslator.exe`**.
3. **💻 التشغيل من سطر الأوامر (PowerShell / CMD):**
   ```cmd
   dotnet run --project "windows/src/GameTranslator.App/GameTranslator.App.csproj" -c Release
   ```

لتفاصيل الإعداد وحل المشكلات في Windows انظر [`docs/WINDOWS_SETUP.md`](docs/WINDOWS_SETUP.md).

---

### 3. 📺 التلفزيون الذكي (تطبيق GameTranslator للتلفزيون)
> يعرض التلفزيون صورة اللعبة القادمة من HDMI، ويضع فوقها طبقة شفافة فيها الترجمة العربية من اليمين إلى اليسار. الحزم الجاهزة موجودة في المجلد `tv/packages/`.
>
> 💡 **دليل مفصّل لكل أنواع التلفزيونات:** [دليل إعداد تطبيق التلفزيون (PDF)](GameTranslator_TV_Setup_Guide.pdf) أو [`docs/tv-setup.ar.md`](docs/tv-setup.ar.md).

* **Android TV / Google TV (مثل TCL و Sony و Philips و Xiaomi):**
  * **الحزمة الجاهزة:** `tv/packages/gametranslator-androidtv_1.0.0.apk`
  * **الطريقة 1 (من تطبيق الكمبيوتر، عبر الشبكة):** فعّل «تصحيح أخطاء USB» في خيارات المطوّر على التلفزيون، ثم اختر Android TV في خطوة التلفزيون داخل GameTranslator؛ يثبّت التطبيق الحزمة ويفتحها.
  * **الطريقة 2 (بذاكرة USB):** انسخ الملف `gametranslator-androidtv_1.0.0.apk` إلى ذاكرة USB، وصِلها بالتلفزيون، ثم افتح الملف من أي مدير ملفات على التلفزيون وثبّته.
  * **الطريقة 3 (يدويًا عبر ADB):**
    ```bash
    adb connect <TV-IP>
    adb install tv/packages/gametranslator-androidtv_1.0.0.apk
    ```
  * افتح التطبيق على التلفزيون واختر منفذ HDMI الموصول به جهاز PS5 (مثل HDMI 1).

* **LG (webOS):**
  * **الطريقة 1 (تطبيق دائم، موصى بها):**
    1. من متجر LG Content Store على التلفزيون ثبّت التطبيق المجاني **Developer Mode** وسجّل الدخول بحساب مطوّر LG المجاني.
    2. في Windows يثبّت GameTranslator تطبيق التلفزيون بنفسه من خطوة التلفزيون. أو ثبّته يدويًا: نزّل [webOS Dev Manager](https://github.com/webosbrew/dev-manager-desktop/releases) على الكمبيوتر واتصل بالتلفزيون بعنوان IP الخاص به.
    3. اسحب الحزمة `tv/packages/com.gametranslator.tv_1.0.0_all.ipk` إلى webOS Dev Manager وثبّتها، ثم افتح التطبيق على التلفزيون.
  * **الطريقة 2 (للتجربة السريعة من متصفح التلفزيون):**
    1. افتح **متصفح الويب** على تلفزيون LG.
    2. اكتب في شريط العنوان عنوان الكمبيوتر على الشبكة المحلية:
       `http://<COMPUTER-IP>:8787/obs` (مثال: `http://192.168.1.50:8787/obs`)
    3. يعرض المتصفح أسطر الترجمة فور بدء الترجمة. انتبه: المتصفح لا يعرض صورة HDMI خلف الترجمة، فهذه الطريقة للتجربة فقط؛ ولرؤية الترجمة فوق اللعبة ثبّت التطبيق.

* **Samsung (Tizen):**
  * **الطريقة 1 (تثبيت التطبيق):**
    1. فعّل وضع المطوّر على التلفزيون: افتح شاشة التطبيقات (Apps) واضغط على الريموت `1 2 3 4 5`، ثم اكتب عنوان IP للكمبيوتر في خانة Host PC IP وأعد تشغيل التلفزيون.
    2. تطلب Samsung توقيع التطبيق لكل تلفزيون على حدة. في Windows يتولى GameTranslator ذلك بنفسه من خطوة التلفزيون بعد تسجيل دخول واحد بحساب Samsung مجاني.
    3. أو ثبّت يدويًا ببرنامج **Apps2Samsung**: أدخل عنوان IP للتلفزيون، اختر الملف `tv/packages/gametranslator-samsung_1.0.0.wgt` واضغط **Install**، ثم افتح التطبيق على التلفزيون.
  * **الطريقة 2 (للتجربة السريعة من متصفح التلفزيون):** افتح متصفح الإنترنت في تلفزيون Samsung وادخل إلى `http://<COMPUTER-IP>:8787/obs`. تظهر أسطر الترجمة فقط، من دون صورة اللعبة.

> ملاحظة: حزمتا LG (`.ipk`) و Samsung (`.wgt`) مبنيتان من الشيفرة الحالية في `tv/webos`. حزمة Android TV (`.apk`) هي البناء السابق ولم يُعَد بناؤها، لكنها تعرض الترجمة العربية بشكل صحيح.

---

### 4. 📱 الهاتف والجهاز اللوحي (iOS / Android) — 🧪 تجريبي
> لا يوجد تطبيق خاص للهواتف حاليًا. يمكنك مع ذلك متابعة الترجمة على شاشة ثانية:
> * صِل الهاتف أو الجهاز اللوحي بشبكة Wi-Fi نفسها التي يتصل بها الكمبيوتر، وافتح في المتصفح (Safari أو Chrome) العنوان `http://<COMPUTER-IP>:8787/obs`؛ تظهر أسطر الترجمة العربية مباشرة أثناء اللعب.

---

## 🎮 إعداد PlayStation 5 (لماذا لا ينفصل ذراع التحكم؟)

عند بدء Remote Play يفصل PlayStation عادةً ذراع التحكم عن التلفزيون. الحل بسيط:
1. في PS5 افتح **«الإعدادات»** ثم **«النظام»** ثم **«اللعب عن بُعد»** وفعّل خيار **«تفعيل اللعب عن بُعد»** (بالإنجليزية: `Settings > System > Remote Play > Enable Remote Play`).
2. أضف على جهاز PS5 **مستخدمًا ثانيًا بحساب PSN مجاني**.
3. **كيف تلعب؟**
   - أمام التلفزيون ادخل بـ**حسابك الأساسي** وأمسك ذراع التحكم.
   - الكمبيوتر يتصل بجهاز الألعاب في الخلفية بـ**الحساب الثاني**.
   - هكذا يبقى ذراع التحكم DualSense متصلًا بالتلفزيون **دائمًا**.

---

<a id="translation-engines"></a>

## 🧠 أي محرّك ترجمة أختار؟

في التطبيق محرّكان فقط، وكلاهما يعمل **عبر الإنترنت**:

| المحرّك | كيف يعمل؟ | متى تختاره؟ |
| :--- | :--- | :--- |
| **Google Translate (الافتراضي)** | يرسل النص الإنجليزي إلى خدمة Google Translate ويستقبل الترجمة العربية. لا يحتاج إلى مفتاح ولا إلى إعداد. | يعمل فور تشغيل التطبيق. مناسب للبدء السريع ولمعظم الألعاب. |
| **DeepL** | يرسل النص إلى DeepL باستخدام **مفتاح DeepL API الخاص بك** الذي تدخله في الإعدادات. | عندما تريد صياغة أكثر سلاسة. يلزمك حساب DeepL API (توجد خطة مجانية بحصة شهرية). |

* لا توجد ترجمة من دون إنترنت في هذه النسخة: لا نموذج محلي، ولا ترجمة عبر Gemini، ولا ترجمة Apple على الجهاز. ما يجري على الجهاز هو التعرّف على النص (OCR) فقط.
* في Windows: إذا تعذّر الوصول إلى DeepL يعود التطبيق إلى Google Translate. ويقبل مفاتيح DeepL المجانية والمدفوعة.
* في Mac: يُستخدم DeepL متى أُدخل المفتاح (عبر `api-free.deepl.com`)، وإلا فـ Google Translate. قد تعرض شاشة الإعدادات في Mac أسماء «نماذج» أخرى، لكن المحرّكين المذكورين هما الوحيدان اللذان يعملان فعلًا.

---

## 📁 دليل مجلدات المشروع

```text
GameTranslator/
├── mac/                            # 🍏 تطبيق macOS
│   ├── NativeApp.swift             # التطبيق كله في ملف واحد (SwiftUI + Apple Vision OCR)
│   ├── build.sh                    # سكربت البناء والتثبيت
│   └── Resources/                  # الصور
│
├── windows/                        # 🪟 تطبيق Windows (.NET 10 / C# / WPF)
│   ├── GameTranslator.Windows.slnx # ملف الحل
│   ├── src/                        # App, Shell, Core, Translation, RemotePlay, TvInstall, TvHelper
│   ├── tests/                      # الاختبارات
│   └── native/                     # غلاف بلغة C حول chiaki
│
├── tv/                             # 📺 تطبيقات التلفزيون
│   ├── webos/                      # تطبيق الويب المشترك (LG، ويستخدمه Tizen و Android أيضًا)
│   ├── tizen/                      # ملفات Samsung Tizen
│   ├── android/                    # تطبيق Android TV / Google TV
│   └── packages/                   # حزم جاهزة للتثبيت:
│       ├── gametranslator-androidtv_1.0.0.apk
│       ├── com.gametranslator.tv_1.0.0_all.ipk
│       └── gametranslator-samsung_1.0.0.wgt
│
├── docs/                           # 📖 أدلة الإعداد ووثائق البنية
├── design/                         # 🎨 الشعار والأيقونات و tokens.json
├── scripts/                        # 🛠️ أدوات تحزيم تطبيقات التلفزيون وتثبيتها
│
├── GameTranslator.command          # 🚀 مشغّل Mac بنقرة واحدة
├── GameTranslator.bat              # 🚀 مشغّل Windows بنقرة واحدة
├── Build-Windows.bat               # بناء ملف exe مستقل لـ Windows
├── generate_pdf_guide.py           # يولّد دليل التلفزيون بصيغة PDF
├── GameTranslator_TV_Setup_Guide.pdf
├── PRIVACY.md                      # الخصوصية
├── THIRD-PARTY-NOTICES.md          # إشعارات مكوّنات الطرف الثالث
├── README.md
└── LICENSE                         # رخصة AGPL-3.0
```

---

## 👨‍💻 للمطوّرين: بناء الشيفرة

```bash
# 🍏 macOS (Apple Silicon)
bash mac/build.sh

# 🪟 Windows (.NET 10 SDK)
dotnet run --project windows/src/GameTranslator.App/GameTranslator.App.csproj

# 📺 LG: tv/packages/com.gametranslator.tv_1.0.0_all.ipk
python3 scripts/package-ipk.py

# 📺 Samsung: dist/tv-app/gametranslator-samsung_<version>.wgt
sh scripts/package-tizen.sh

# 📺 Android TV (Gradle + Android SDK)
cd tv/android && gradle assembleRelease

# 📄 PDF: docs/GameTranslator_TV_Setup_Guide.pdf
python3 generate_pdf_guide.py
```

وثائق أخرى: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) للبنية، و[`docs/DESIGN.md`](docs/DESIGN.md) للتصميم.

---

## ⚖️ الرخصة والحقوق والشكر

هذا المشروع مفتوح المصدر برخصة **[GNU AGPL-3.0](LICENSE)**.

* **المشروع الأصلي:** [GameTranslator](https://github.com/Sefflex/GameTranslator). المطوّر: **Sefflex** — حقوق النشر © 2026 GameTranslator Pro. جميع الحقوق محفوظة. هذه النسخة العربية مشتقة منه وتُنشر بالرخصة نفسها.
* **مستودع النسخة العربية:** `https://github.com/mahmoude4477/GameTranslator`
* **[chiaki-ng](https://github.com/chiaki-ng/chiaki-ng):** المشروع المفتوح المصدر الذي يعتمد عليه الاتصال بـ PS5 عبر Remote Play واستقبال البث.
* **[CouchSubs](https://github.com/yorulmazsinan/couchsubs):** المشروع الذي استُفيد من أسسه في تطبيقات الترجمة على التلفزيون.

إشعارات مكوّنات الطرف الثالث في [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)، وسياسة الخصوصية في [`PRIVACY.md`](PRIVACY.md).

GameTranslator غير تابع لشركة Sony Interactive Entertainment ولا معتمد منها. «PlayStation» و«PS5» علامتان تجاريتان لشركة Sony Interactive Entertainment Inc.
