# النشر في متاجر التلفزيون

الناشر في المشروع الأصلي: Sefflex (حساب بائع تجاري). من ينشر هذه النسخة العربية يسجّل حساب بائع باسمه.
اسم التطبيق في كل مكان **GameTranslator**. لا يُستخدم اسم PlayStation/PS5 ولا شعاره في الاسم ولا في الأيقونة
ولا في لقطات الشاشة. أقصى ما يُذكر في الوصف «لأجهزة الألعاب المتصلة عبر HDMI»، وفي آخره ملاحظة عدم الارتباط بـ Sony.

## المتطلبات المشتركة

- سياسة الخصوصية: https://github.com/mahmoude4477/GameTranslator/blob/main/PRIVACY.md
- الدعم: https://github.com/mahmoude4477/GameTranslator/issues
- **الوضع التجريبي (Demo):** من يختبر التطبيق في المتجر ليس لديه PS5 ولا كمبيوتر. زر مشاهدة العرض التجريبي في شاشة
  الانتظار (زر OK) أو الزر الأزرق يشغّل ترجمات نموذجية. يُكتب ذلك بوضوح في ملاحظة الاختبار وفي شرح الواجهة (pptx).
- الريموت: أزرار الاتجاهات و OK والرجوع تعمل، والعنصر المحدّد ظاهر. زر الرجوع في شاشة البداية يعود إلى الشاشة
  الرئيسية للتلفزيون.
- لا توجد في الشيفرة المصدرية كلمة مرور ولا مفتاح ولا token.
- التطبيق يعرض الترجمة العربية من اليمين إلى اليسار؛ لقطات الشاشة تُظهر سطرًا عربيًا فوق صورة لعبة.

## نص صفحة المتجر

الحقول الإنجليزية مطلوبة في المتجرين؛ والنص العربي يُضاف لغةً ثانية.

**App title:** GameTranslator

**Short description (English):** Arabic subtitles on your TV for games on a console connected over HDMI.

**Description (English):**

> GameTranslator shows Arabic subtitles on top of your game. Your console stays connected to the TV over HDMI, so the
> picture and sound are untouched. The free GameTranslator app on your Mac or Windows PC reads the game's English
> subtitles, translates them to Arabic online, and sends the text to this TV app over your home network. The TV app
> shows the Arabic line, right-to-left, over the game: covering the English line, above it, below it, or at a fixed
> height.
>
> Requires the free GameTranslator computer app on the same network. Without it, press "Watch the demo" to see sample
> subtitles.
>
> GameTranslator is not affiliated with Sony Interactive Entertainment.

**الوصف المختصر (العربية):** ترجمة عربية على تلفزيونك لألعاب جهاز الألعاب المتصل عبر HDMI.

**الوصف (العربية):**

> يعرض GameTranslator الترجمة العربية فوق لعبتك. يبقى جهاز الألعاب متصلًا بالتلفزيون عبر HDMI، فلا تتأثر الصورة ولا
> الصوت. تطبيق GameTranslator المجاني على كمبيوتر Mac أو Windows يقرأ الترجمة النصية الإنجليزية للعبة، ويترجمها إلى
> العربية عبر الإنترنت، ويرسل النص إلى تطبيق التلفزيون هذا عبر شبكتك المنزلية. يعرض تطبيق التلفزيون السطر العربي من
> اليمين إلى اليسار فوق اللعبة: مغطّيًا السطر الإنجليزي، أو فوقه، أو تحته، أو على ارتفاع ثابت.
>
> يتطلب تطبيق GameTranslator المجاني للكمبيوتر على الشبكة نفسها. ومن دونه اضغط زر العرض التجريبي لمشاهدة ترجمات
> نموذجية.
>
> GameTranslator غير مرتبط بشركة Sony Interactive Entertainment.

## LG Content Store (Seller Lounge)

1. يُفتح على seller.lgappstv.com تسجيل **بائع تجاري** باسم الناشر (بحساب LG).
2. قبل الإرسال يُطرح سؤال مكتوب عبر Seller Lounge Q&A: صورة HDMI تُعرض بالمصدر غير الموثّق
   `ext://hdmi`، ولا تقدّم LG واجهة API رسمية لذلك. يُسأل هل تُقبل هذه الطريقة في تطبيق متجر.
   وعند الحاجة يُقدَّم طلب إلى webOS Partner Zone.
3. إذا جاء الرد إيجابيًا يُقدَّم عند الإرسال ما يلي (قالب Self-Checklist يؤخذ من Seller Lounge):
   - IPK (ينتجه `scripts/package-ipk.py`)
   - الأيقونة: 400×400
   - لقطات الشاشة: 1 رئيسية + 2–5 إضافية، 1280×720
   - خلفية البداية: 1920×1080
   - الوصف: 50–4000 حرف
   - التصنيف العمري، والفئة، واختيار الدول
   - Self-Checklist (xlsx)
   - UX Scenario (pptx)
4. تستغرق المراجعة عادة من شهر إلى 3 أشهر.

إذا لم تصل موافقة المتجر يُثبَّت التطبيق بوضع المطوّر. جلسة هذا الوضع تدوم 1000 ساعة على الأكثر ويجب تمديدها قبل
انتهائها، وإلا حُذف التطبيق المثبَّت. تطبيق Windows يجدّد المدة بنفسه عند الاتصال بالتلفزيون
(`DevModeInstaller.RenewSessionAsync`)؛ انظر [tv-setup.ar.md](tv-setup.ar.md#lg-tv).

## Samsung (TV Seller Office, Tizen)

1. يُسجَّل في Seller Office بحساب Samsung. هذا الحساب يسمح في المرحلة الأولى بالنشر في الولايات المتحدة فقط.
2. للنشر في دول أخرى (الدول العربية مثلًا) يجب أن يصبح الناشر **Partner Seller**:
   - يُوقَّع عقد مع Samsung (المقر الرئيسي أو فرع Samsung في المنطقة).
   - يُفتح طلب من القائمة Membership › Partnership ببريد مدير المحتوى في المنطقة. وإن لم يُعرف هذا
     البريد يُطلب عبر 1:1 Q&A.
   - هذه أطول الخطوات مدة؛ فيجب البدء بها فورًا.
3. تطبيق Tizen يعرض صورة HDMI خلف طبقة HTML عبر `tizen.tvwindow` (`BEHIND`).
   هذه الواجهة موثّقة ومستوى صلاحيتها public. قبل الإرسال يُجرَّب على QN70F بوضع المطوّر.
4. يُقدَّم عند الإرسال ما يلي:
   - حزمة ‎.wgt (موقّعة بشهادتَي author و distributor؛ وتُحفظ نسخة احتياطية من شهادة author)
   - الأيقونة: 1920×1080
   - 4 لقطات شاشة بالضبط: 1920×1080، كل واحدة 500 kB على الأكثر
   - App UI Description (pptx)
   - العنوان والوصف لكل لغة
5. تستغرق المراجعة عادة من شهر إلى 4 أشهر.
