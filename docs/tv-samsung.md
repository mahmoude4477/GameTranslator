# تطبيق Samsung TV (Tizen)

الحالة: ثُبّت على تلفزيون Samsung حقيقي (75" Neo QLED) وعمل. خطوات
التثبيت: [tv-setup.ar.md](tv-setup.ar.md#samsung-tv).

## البنية

شيفرة الواجهة مشتركة مع LG (`tv/webos/`). وما يخص Samsung وحده موجود في `tv/tizen/`:

- `platform.js`: صورة HDMI (`tizen.tvwindow`، `BEHIND`)، وأزرار الريموت، ومعاملات التشغيل
- `config.xml`: معرّف التطبيق والأذونات

يجمع `./scripts/package-tizen.sh` هذه الملفات في `dist/tv-tizen/app`، وينتج الحزمة غير الموقّعة
`dist/tv-app/gametranslator-samsung_<الإصدار>.wgt`. النسخة الجاهزة منها في `tv/packages/`.

## ما ينبغي تجربته

- هل تظهر صورة HDMI خلف الترجمة النصية، وهل يصل الصوت؟
- هل يُختار منفذ HDMI الوحيد المتصل تلقائيًا؟
- في ريموت بلا أزرار أرقام وألوان: هل يفتح زر OK اللوحة، وهل يبدّل زرّا اليسار واليمين منفذ HDMI؟
- هل يمكن فتح التطبيق من الكمبيوتر عبر `ed.apps.launch`؟ هناك من أبلغ أنه لا يعمل في الموديلات الحديثة.
- هل تظهر الأسطر العربية من اليمين إلى اليسار، وعلامات الترقيم واسم المتكلم في الطرف الصحيح؟

## الفرق عن LG

في Samsung تُوقَّع الحزمة بشهادة موزّع (distributor) تتضمن DUID الخاص بالتلفزيون. لذلك لا يمكن تثبيت حزمة واحدة على
تلفزيونات الجميع. يقوم تطبيق Windows بذلك نيابة عن المستخدم (`windows/src/GameTranslator.TvInstall/Samsung/`)؛ والشيفرة
نفسها متاحة أداةَ سطر أوامر باسم `gametranslator-tv-helper` (`windows/src/GameTranslator.TvHelper`، الأمر `samsung-install`)
يبنيها `scripts/build-tv-helper.sh` لنظام macOS:

1. يتصل بالتلفزيون عبر sdb (المنفذ 26101) ويقرأ DUID بالأمر `0 getduid`.
2. إن لم توجد شهادة صالحة لهذا التلفزيون يفتح صفحة SignInGate الخاصة بـ Samsung في المتصفح؛ ويُرسَل الرمز (token) بطلب
   POST إلى `localhost:4794`.
3. يرسل طلبَي CSR للمؤلف (author) والموزّع إلى `svdca.samsungqbe.com` (في CSR الموزّع: `URN:tizen:deviceid=<DUID>`)،
   ويكتب الشهادات المستلمة مع شهادة Samsung الوسيطة في ملف PKCS#12 (في Windows: `%LOCALAPPDATA%\GameTranslator\samsung`).
4. يوقّع الحزمة غير الموقّعة `gametranslator-samsung_<الإصدار>.wgt` المرفقة مع التطبيق، ويثبّتها بالأمر `vd_appinstall` ويفتحها.

نقاط النهاية (endpoints) غير موثّقة ومأخوذة من Apps2Samsung؛ وإذا غيّرتها Samsung يُحدَّث هذا الجزء. ويبقى الطريق الدائم
للجميع هو النشر في المتجر (`docs/store-listing.md`).
