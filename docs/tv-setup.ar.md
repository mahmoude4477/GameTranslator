# تثبيت تطبيق GameTranslator على التلفزيون

(English: [tv-setup.md](tv-setup.md))

تطبيقات التلفزيون غير متوفرة في المتاجر بعد. يكفي أن تفعّل وضع المطوّر في تلفزيونك، ثم يثبّت GameTranslator التطبيق بنفسه.
وضع المطوّر مجاني ولا يضرّ بالتلفزيون. يعرض تطبيق الكمبيوتر هذه الخطوات في خطوة التلفزيون وينتظر التلفزيون بنفسه؛
وهذه الصفحة هي الشرح المفصّل للخطوات نفسها. انظر أولًا إلى أي مجموعة ينتمي تلفزيونك:

| التلفزيون | الحالة | الدليل |
|---|---|---|
| **LG** (webOS 4 فما فوق) | جُرّب على تلفزيون حقيقي. يثبّت GameTranslator تطبيق التلفزيون بنفسه | [LG](#lg-tv) |
| **Samsung** (Tizen، موديلات 2019 وما بعدها) | جُرّب على تلفزيون حقيقي (75" Neo QLED). يثبّته GameTranslator بنفسه ويطلب تسجيل الدخول إلى Samsung مرة واحدة | [Samsung](#samsung-tv) |
| **Android TV / Google TV** (TCL وSony وXiaomi وPhilips وVestel وArçelik وBeko وGrundig وغيرها) | جُرّب على تلفزيون حقيقي. يثبّته GameTranslator بنفسه عبر الشبكة | [Android TV](#android-tv--google-tv) |
| **موديلات Linux من Vestel وArçelik وBeko وGrundig، وPhilips Saphi وTitan OS** | غير مدعومة: لم نجد طريقة لتثبيت التطبيقات على هذه التلفزيونات | |

**هل تلفزيوني Android؟** إذا ظهر في الشاشة الرئيسية Google Play Store أو Google TV فهو Android. وإذا كان للشركة المصنّعة
متجر تطبيقات خاص بها ولا يوجد Play Store فالأرجح أنه ليس Android.

يجب أن يكون الكمبيوتر والتلفزيون على الشبكة المنزلية نفسها.

يعرض التلفزيون الترجمة العربية من اليمين إلى اليسار فوق صورة اللعبة القادمة من منفذ HDMI.

إن لم يعرض تطبيق الكمبيوتر خطوة التثبيت التلقائي لتلفزيونك، أو تعذّر التثبيت التلقائي، فاستخدم
[التثبيت اليدوي](#manual-install): الحزم الجاهزة موجودة في مجلد `tv/packages` من المستودع.

## LG TV

### 1. أنشئ حساب مطوّر لدى LG

1. افتح من الكمبيوتر الموقع [webostv.developer.lge.com](https://webostv.developer.lge.com).
2. اختر **SIGN IN** من أعلى اليمين ثم **Create Account** وأنشئ حسابًا مجانيًا.

### 2. فعّل وضع المطوّر في التلفزيون

1. تأكد من أن التلفزيون متصل بالإنترنت.
2. افتح LG Content Store في التلفزيون، وابحث عن **Developer Mode** وثبّت التطبيق.
3. افتح تطبيق Developer Mode وسجّل الدخول ببريد حسابك وكلمة مروره.
4. فعّل **Dev Mode Status**. سيُعاد تشغيل التلفزيون.

### 3. دع GameTranslator يثبّت تطبيق التلفزيون

1. افتح GameTranslator على الكمبيوتر. في خطوة التلفزيون من الإعداد اختر تلفزيونك واضغط **اتصل** (Connect)، ثم وافق على الإذن الذي يظهر على التلفزيون.
2. افتح تطبيق Developer Mode في التلفزيون مرة أخرى وفعّل **Key Server**. اترك التطبيق ظاهرًا على الشاشة.
3. تظهر في أسفل يسار شاشة التلفزيون عبارة **Passphrase** من 6 أحرف. اكتبها في GameTranslator مع الانتباه إلى الأحرف الكبيرة والصغيرة.
4. اضغط **ثبّت على التلفزيون** (Install on the TV).

تُطلب العبارة مرة واحدة فقط. أما الإصدارات الجديدة فيثبّتها GameTranslator بنفسه عند بدء الترجمة.

### مدّد المدة

يبقى وضع المطوّر مفعّلًا 1000 ساعة على الأكثر. **يعيد GameTranslator بدء المدة بنفسه في كل مرة تستخدمه** ويعرض المدة
المتبقية في خطوة التلفزيون. أما العدّاد **Remain Session** على شاشة التلفزيون نفسه فيُظهر ذلك بعد إعادة تشغيل التلفزيون.

إذا كنت لن تستخدم GameTranslator مدة طويلة، فاضغط **EXTEND** في تطبيق Developer Mode قبل انتهاء المدة والتلفزيون متصل
بالإنترنت. إذا انتهت المدة يُغلق وضع المطوّر ويُحذف تطبيق التلفزيون؛ ولا يمكن التمديد بعد انتهائها، بل يجب التثبيت
من جديد. ويحدث الأمر نفسه إذا أُعيد تشغيل التلفزيون 10 مرات وهو غير متصل بالإنترنت.

إذا أغلقت الوضع ثم فعّلته من جديد يولّد التلفزيون مفتاحًا جديدًا وتُطلب العبارة مرة أخرى.

للتثبيت بلا GameTranslator انظر [التثبيت اليدوي](#manual-install).

## Samsung TV

تشترط Samsung توقيع التطبيق لكل تلفزيون على حدة. يقوم GameTranslator بذلك نيابة عنك: كل المطلوب أن تسجّل الدخول مرة
واحدة بحساب Samsung مجاني. إذا واجهت مشكلة فاكتب لنا في صفحة
[Issues](https://github.com/mahmoude4477/GameTranslator/issues) مع ذكر موديل تلفزيونك.

### 1. فعّل وضع المطوّر في التلفزيون

1. افتح شاشة **Apps** (التطبيقات) في التلفزيون.
2. اضغط الأزرار `1 2 3 4 5` بالريموت (أو اكتب `12345` بلوحة المفاتيح التي على الشاشة). تُفتح نافذة وضع المطوّر.
   في بعض الموديلات الحديثة (مثل QN90F موديل 2025) يعمل ذلك في شاشة **Apps › App Settings**.
3. اجعل **Developer mode** على **On**.
4. اكتب عنوان كمبيوترك في الخانة **Host PC IP** واضغط **OK**. يعرض GameTranslator العنوان في خطوة التلفزيون.
5. أعد تشغيل التلفزيون. ستظهر عبارة **Develop Mode** في شاشة التطبيقات.

### 2. دع GameTranslator يثبّته

1. في خطوة التلفزيون من GameTranslator اختر تلفزيون Samsung واضغط **اتصل** (Connect) ووافق على الإذن الذي يظهر على التلفزيون.
2. حين يرى GameTranslator وضع المطوّر يفتح صفحة تسجيل الدخول إلى Samsung في المتصفح. سجّل الدخول بحساب Samsung
   (إن لم يكن لديك حساب فأنشئه مجانًا من الصفحة نفسها). إذا طلب المتصفح إذن الوصول إلى الشبكة المحلية فاختر **السماح** (Allow).
3. يحصل GameTranslator على الشهادة الخاصة بتلفزيونك، ويوقّع التطبيق ويثبّته ويفتحه. يُطلب تسجيل الدخول في التثبيت الأول فقط.

إذا ظهر التنبيه **لم يقبل التلفزيون هذا الكمبيوتر** فتحقق من أن **Host PC IP** في نافذة وضع المطوّر هو عنوان كمبيوترك
الحالي، ثم أعد تشغيل التلفزيون. وإذا تغيّر عنوان الكمبيوتر تُكرَّر هذه الخطوة.

**إذا حدثت مشكلة** (جُرّب على 75" Neo QLED):

- **إذا ظهرت الرسالة «تعذّر تثبيت تطبيق التلفزيون»:** أعد تشغيل التلفزيون بالضغط المطوّل على زر التشغيل في الريموت
  حتى يظهر شعار Samsung. ثم افتح الإعدادات في GameTranslator، وفي بطاقة **تطبيق GameTranslator للتلفزيون** اضغط
  **حاول مرة أخرى**. بذلك يُحلّ معظم حالات التثبيت غير المكتمل. وإن لم ينجح فستجد في البطاقة السبب الذي ذكره التلفزيون؛
  اكتبه في صفحة [Issues](https://github.com/mahmoude4477/GameTranslator/issues).
- **إذا ظلّت نافذة «السماح» تظهر على التلفزيون:** اختر **Allow** (السماح) مرة واحدة بالريموت. تظهر النافذة كلما حاول
  GameTranslator فتح تطبيق التلفزيون وهو غير مثبّت بعد، وتتوقف بعد تثبيته. يمكنك أيضًا جعل الكمبيوتر **Allow** من
  إعدادات التلفزيون: **Connection › External Device Manager › Device Connect Manager**
  (يختلف اسم القائمة قليلًا بحسب الموديل؛ ابحث في الإعدادات عن **Device**).
- **كيف أعرف أنه ثُبّت:** اضغط زر **OK** في الريموت وتطبيق GameTranslator مفتوح على التلفزيون، فتظهر في اللوحة
  **HDMI** (منفذ PS5 وبجانبه «يعمل»)، و**التطبيق** (عنوان الكمبيوتر)، و**الاتصال: متصل**. لإغلاق اللوحة اضغط **OK** مرة أخرى.

للتثبيت عبر Tizen Studio انظر [التثبيت اليدوي](#manual-install).

## Android TV / Google TV

جُرّب على تلفزيون حقيقي. بعض التلفزيونات قد لا تعطي صورة HDMI لتطبيق آخر؛ في هذه الحالة تظهر الترجمة لكن الخلفية
تبقى سوداء. اكتب لنا من فضلك في صفحة [Issues](https://github.com/mahmoude4477/GameTranslator/issues) مع ذكر موديل تلفزيونك.

### 1. فعّل تصحيح الأخطاء في التلفزيون

1. ادخل إلى **Settings › Device Preferences › About** (في Google TV: **Settings › System › About**).
2. اضغط زر OK سبع مرات على **Build** (رقم الإصدار). تظهر رسالة تفيد بأنك أصبحت مطوّرًا.
3. من **Device Preferences** (في Google TV: **System**) **› Developer options** فعّل **USB debugging**.
   وإن وُجد الخيار **Network debugging** ففعّله أيضًا.

قد تختلف أسماء القوائم قليلًا بحسب الشركة المصنّعة ولغة التلفزيون.

### 2. دع GameTranslator يثبّته

1. في خطوة التلفزيون من GameTranslator اختر **Android TV / Google TV**. يبحث GameTranslator عن التلفزيون في الشبكة.
2. يظهر على شاشة التلفزيون السؤال «Allow USB debugging?». ضع علامة على **Always allow from this computer**
   ثم اضغط **Allow**.
3. يثبّت GameTranslator التطبيق ويفتحه. ويجد تطبيق التلفزيون الكمبيوتر في الشبكة بنفسه.
4. إن لم تظهر الصورة فافتح اللوحة بزر OK واختر منفذ HDMI الصحيح بزرّي اليسار واليمين.

بعد التثبيت يمكنك إيقاف تصحيح الأخطاء؛ وعند تثبيت إصدار جديد يطلب منك GameTranslator تفعيله مرة أخرى.

### التثبيت بذاكرة USB

إذا لم يجد GameTranslator التلفزيون في الشبكة (بعض التلفزيونات لا تسمح بتصحيح الأخطاء عبر الشبكة):

1. من **Settings › Apps › Security & restrictions › Unknown sources** اسمح لمدير الملفات بالتثبيت.
2. انسخ الملف `tv/packages/gametranslator-androidtv_1.0.0.apk` من المستودع (أو نزّله من صفحة
   [Releases](https://github.com/mahmoude4477/GameTranslator/releases) إن نُشر إصدار جاهز) إلى ذاكرة USB وأدخلها في التلفزيون.
3. افتح الملف بمدير الملفات في التلفزيون وثبّته. إن لم يكن في تلفزيونك مدير ملفات فثبّت واحدًا مجانيًا من Play Store.
4. في خطوة التلفزيون من GameTranslator اختر **Android TV / Google TV**، وافتح GameTranslator على التلفزيون.

## Manual install

التثبيت اليدوي: إذا لم يعمل التثبيت التلقائي فثبّت الحزمة الجاهزة بنفسك. الحزم كلها في مجلد `tv/packages` من
[المستودع](https://github.com/mahmoude4477/GameTranslator)، وقد تجدها أيضًا في صفحة
[Releases](https://github.com/mahmoude4477/GameTranslator/releases) إن نُشر إصدار جاهز. وضع المطوّر يجب أن يكون مفعّلًا في
التلفزيون كما في الأقسام السابقة.

### LG (webOS)

ثبّت [Node.js](https://nodejs.org) ثم نفّذ في Terminal (في Windows: PowerShell) من مجلد المستودع:

```bash
npm install -g @webos-tools/cli
ares-setup-device --add tv --info "{'host':'TV_IP_ADDRESS','port':'9922','username':'prisoner'}"
ares-novacom --device tv --getkey
ares-install -d tv tv/packages/com.gametranslator.tv_1.0.0_all.ipk
```

ضع عنوان IP للتلفزيون مكان `TV_IP_ADDRESS`. يطلب الأمر `ares-novacom` عبارة **Passphrase** الظاهرة في تطبيق Developer Mode
(بعد تفعيل **Key Server**).

### Samsung (Tizen Studio)

ثبّت [Tizen Studio](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html):
من Package Manager ثبّت **Samsung Certificate Extension**، ومن **Device Manager** اتصل بالتلفزيون، وفي
**Certificate Manager** أنشئ ملف شهادة من النوع **Samsung** باستخدام DUID الخاص بالتلفزيون. ثم فكّ ضغط الحزمة غير
الموقّعة `tv/packages/gametranslator-samsung_1.0.0.wgt` (وهي ملف zip) في مجلد، ونفّذ:

```bash
tizen package -t wgt -s CERTIFICATE_PROFILE -- FOLDER
tizen install -n FOLDER/GameTranslator.wgt -t TV_NAME
```

ضع اسم ملف الشهادة مكان `CERTIFICATE_PROFILE`، ومسار المجلد مكان `FOLDER`، واسم التلفزيون كما يظهر في Device Manager
مكان `TV_NAME`.

### Android TV (USB / adb)

استخدم [التثبيت بذاكرة USB](#التثبيت-بذاكرة-usb) المذكور أعلاه، أو ثبّت الحزمة من الكمبيوتر بأداة `adb` بعد تفعيل تصحيح الأخطاء:

```bash
adb connect TV_IP_ADDRESS
adb install tv/packages/gametranslator-androidtv_1.0.0.apk
```
