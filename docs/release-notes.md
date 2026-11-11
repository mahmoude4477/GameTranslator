# ملاحظات الإصدارات

## النسخة العربية

هذه النسخة من GameTranslator تعتمد العربية بدل التركية. تقرأ الترجمة النصية الإنجليزية لألعاب PS5 وتعرض ترجمتها العربية
على التلفزيون فوق اللعبة. المشروع الأصلي من تأليف Sefflex (AGPL-3.0).

**الحصول عليها:** من [المستودع](https://github.com/mahmoude4477/GameTranslator) (البناء من الشيفرة المصدرية متاح دائمًا)،
أو من صفحة [Releases](https://github.com/mahmoude4477/GameTranslator/releases) إن نُشر إصدار جاهز.

**الجديد في هذه النسخة**

- **المخرجات بالعربية وتُعرض من اليمين إلى اليسار** على تطبيق التلفزيون وعلى طبقة OBS. يحيط الكمبيوتر كل سطر عربي بعلامات
  الاتجاه أيضًا، فتظهر علامات الترقيم واسم المتكلم في الطرف الصحيح حتى في نسخ تطبيق التلفزيون الأقدم.
- **واجهة عربية** لتطبيقَي الكمبيوتر (Mac و Windows) ولتطبيق التلفزيون، ووثائق عربية.
- **الترجمة عبر الإنترنت:** Google افتراضيًا بلا مفتاح، أو DeepL حين يُدخل المستخدم مفتاحه.
- **أُزيلت** الدبلجة التركية (تحويل النص إلى كلام)، ونموذج الترجمة من الإنجليزية إلى التركية الذي كان يعمل بلا إنترنت،
  وقواميس الإنجليزية–التركية.
- **تطبيق Mac** الآن ملف واحد (`mac/NativeApp.swift`) يبنيه `mac/build.sh`؛ وأُزيل تطبيق macOS الثاني مع نواته المشتركة
  ومسار تحديثه.

**ملاحظات**

- الترجمة تحتاج إلى اتصال بالإنترنت. التعرّف على النص (OCR) يجري على الكمبيوتر نفسه.
- حزمة Android TV الجاهزة في `tv/packages` من البناء الأقدم (لم يُعَد بناؤها)، وتعرض الترجمة العربية بشكل صحيح.
  حزمتا LG (`.ipk`) و Samsung (`.wgt`) أُعيد بناؤهما.
- تثبيت تطبيق التلفزيون: [tv-setup.ar.md](tv-setup.ar.md). التثبيت على Windows: [WINDOWS_SETUP.md](WINDOWS_SETUP.md).
- لا تتوفر نسخ في المتاجر بعد. موديلات Linux من Vestel وArçelik وBeko وGrundig، وكذلك Philips Saphi وTitan OS، غير مدعومة.

---

## سجل إصدارات المشروع الأصلي

ما يلي ترجمة لملاحظات الإصدارات التجريبية للمشروع الأصلي (الذي كان يترجم إلى التركية)، وهي محفوظة هنا للتاريخ. بعض ما
تذكره (الدبلجة، النموذج الذي يعمل بلا إنترنت، تطبيق Mac بصيغة DMG، التحديث من داخل التطبيق) لم يعد موجودًا في هذه النسخة.

إصدار تجريبي جديد من GameTranslator. يقرأ الترجمة النصية الإنجليزية لألعاب PS5 التي لا تدعم اللغة التركية، ويعرض ترجمتها
التركية على التلفزيون فوق اللعبة، ويقرؤها بصوت تركي إن أردت.

**الجديد في هذا الإصدار**

- **إصلاح لظهور الترجمة لحظة ثم اختفائها على التلفزيون (تطبيق التلفزيون 0.9.7).** كانت الترجمة تُمسح فور اختفاء النص في اللعبة أو عند انقطاع الاتصال لحظة؛ والآن تبقى على الشاشة مدة القراءة. إذا استمرت المشكلة فأرسل صورة لسطر التشخيص الجديد في لوحة التلفزيون.
- **إصلاح تعذّر الاتصال في Windows عند تغيّر عنوان IP لجهاز PS5.** يعثر GameTranslator على PS5 في الشبكة من جديد ويحفظ العنوان الجديد.
- **ظهور السبب الحقيقي لأخطاء مطابقة PS5 في Windows.** تتضمن رسالة الخطأ رمز PS5؛ وإذا لم يستجب PS5 يُعاد البحث.
- **في دبلجة Windows يُعرض آخر سطر خطأ إذا توقف محرك الصوت التركي.**

**ما جاء في beta.20:** إصلاح الخطأ «تعذّر تثبيت تطبيق التلفزيون» على تلفزيون LG (أبلغ عنه: [yag_ats](https://x.com/yag_ats)).

**ما جاء في beta.19:** إصلاح الترجمة التي لا تظهر على Samsung و Android TV (تطبيق التلفزيون 0.9.6)، وإذن جدار الحماية في Windows، وترجمة أفضل في GTA V وعلى الخلفيات الملوّنة (Windows)، وبقاء مربّع الترجمة المتوسّطة في الوسط (Mac و Windows).

**ما جاء في beta.18:** طبقة OBS (Mac و Windows)، وسجل الترجمات (Mac، مساهمة: [rumpony](https://github.com/rumpony)).

**ما جاء في beta.17:** عودة تطبيق Mac إلى العمل على macOS 26، وإصلاح تعطّل Remote Play عند الإغلاق (Mac و Windows)، وتنبيه امتلاء القرص في Mac.

**ما جاء في beta.16:** ترجمة الشاشة (Num 9 في Windows، و ⌥⌘T في Mac: القوائم ولوحة المهام ومربّعات المعلومات تظهر بالتركية في مواضعها على التلفزيون)، وتطبيق التلفزيون 0.9.5، وفصل النصوص المتباعدة الواقعة على السطر نفسه في Windows.

**ما جاء في beta.15:** ترجمة Windows التي تجرّب عدة ترجمات وتختار أفضلها، وترجمة عبارات الألعاب ("friendlies"، "got your six") بمعناها، وترجمة DeepL الاختيارية (Mac و Windows)، وصوت تركي سريع بلا تنزيل في Windows.

**ما جاء في beta.14:** تنبيه حزمة التعرّف على النص الإنجليزي في Windows وتثبيتها بزر واحد، وعدم اختلاط القوائم والمؤشرات وتلميحات ذراع التحكم بالترجمة، وقطع جملة الدبلجة القديمة عند وصول ترجمة جديدة، والمنطقة المرسومة بالفأرة والمناطق الإضافية Num 1/2/3 (مساهمة: [sadumqn](https://github.com/sadumqn))، وعدم منع التثبيت القديم للإصدار الجديد على Samsung.

**ملفات التنزيل (في المشروع الأصلي)**

| الملف | الغرض |
|---|---|
| `GameTranslator-macos.dmg` | تثبيت Mac (Apple Silicon، macOS 26 فما فوق) |
| `GameTranslator-windows-Setup.exe` | تثبيت Windows 10 (2004) / 11، 64 بت |
| `GameTranslator-macos.zip`, `GameTranslator-windows-x64.zip` | لمن يريد الاستخدام بلا تثبيت |
| `com.gametranslator.tv_…_all.ipk` | تطبيق تلفزيون LG، لمن يريد التثبيت اليدوي |
| `GameTranslator-samsung-….zip` | مصدر تطبيق تلفزيون Samsung، لمن يريد التثبيت اليدوي عبر Tizen Studio |
| `GameTranslator-androidtv.apk` | تطبيق Android TV / Google TV، لمن يريد التثبيت بذاكرة USB |
| `appcast.xml`, `releases.win.json`, `*.nupkg` | ملفات التحديث من داخل التطبيق (لا تنزّلها يدويًا) |
| `SHA256SUMS.txt` | ملخّصات التحقق من الملفات |

**حالات معروفة**

- ملف تثبيت Windows غير موقّع: عند التنبيه «Windows protected your PC» اختر **More info › Run anyway**. يظهر التنبيه في التثبيت الأول فقط، ولا يظهر عند التحديثات.
- الدبلجة جديدة: لم يُجرَّب بعد على كل التلفزيونات تشغيل الصوت من التلفزيون مع صوت اللعبة. إذا لم يصدر صوت من التلفزيون فحوّل الصوت إلى الكمبيوتر من الإعدادات واكتب لنا موديل تلفزيونك. واخفض صوت الحوار من إعدادات الصوت في اللعبة.
- الأصوات اصطناعية وليست لأشخاص حقيقيين: Pocket TTS Turkish (wite-tech، CC BY 4.0) ببرنامج pocket-tts من Kyutai.
- تثبيت Samsung و Android TV لم يُجرَّب بعد إلا قليلًا على تلفزيونات حقيقية. إذا حدثت مشكلة فاكتب في صفحة [Issues](https://github.com/mahmoude4477/GameTranslator/issues) مع موديل تلفزيونك؛ السجلات في Windows داخل المجلد `%LOCALAPPDATA%\GameTranslator` (`error.log`، `remoteplay.log`).
- لأن PS5 يسمح باتصال Remote Play واحد في الوقت نفسه، لا يمكن استخدام GameTranslator أثناء اللعب بتطبيق PS Remote Play؛ ويجب أن يكون PS5 متصلًا بالتلفزيون عبر HDMI.
- لا تتوفر نسخ المتاجر بعد. موديلات Linux من Vestel وArçelik وBeko وGrundig، وكذلك Philips Saphi وTitan OS، غير مدعومة.

---

Historical note of the original project's beta.18 (it translated to Turkish): OBS overlay: streamers using a capture card can add a Browser source at `http://localhost:8787/obs` (1920×1080) and the Turkish subtitles appear over the game on screen and on stream, covering the English line when its position is known; the address is under Settings › OBS overlay. Subtitle history on Mac (contributed by rumpony): the last 50 translations of the session with their English text. Also includes beta.17's fixes (macOS 26 launch, Remote Play crash on disconnect, disk-full warning). In this Arabic edition the overlay shows Arabic subtitles, right-to-left.
