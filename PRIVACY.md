# الخصوصية / Privacy

*آخر تحديث / Last updated: 2026-11-11*

## العربية

GameTranslator برنامج مفتوح المصدر. المشروع الأصلي من تطوير Sefflex، وهذه هي النسخة العربية منه. لا يجمع البرنامج
أي بيانات شخصية، ولا يرسل أي بيانات إلى خوادم تخصنا؛ فليس لدينا أي خادم. ولا يحتوي على إعلانات ولا على شيفرة
تحليلات أو تتبّع.

**ما يبقى على الكمبيوتر**

- صورة اللعبة القادمة من PS5 تُعالَج في الذاكرة فقط لقراءة الترجمة النصية. التعرّف على النص (OCR) يجري على
  جهازك (Apple Vision في Mac، و Windows.Media.Ocr في Windows). الصورة لا تُحفظ ولا تُرسل إلى أي مكان.
- النص العربي المترجَم يُرسل فقط إلى التلفزيون (أو إلى طبقة OBS أو المتصفح) داخل شبكتك المنزلية.
- معرّف حساب PlayStation Network (رقمي) ومفتاح إقران PS5 ومفتاح إقران التلفزيون تُحفظ على الكمبيوتر: في Windows
  داخل ملف مشفّر لا يفتحه إلا حساب المستخدم الخاص بك (DPAPI)، وفي Mac ضمن تفضيلات التطبيق على جهازك.
  تُكتب بيانات إقران PS5 أيضًا في إعدادات برنامج Chiaki على جهازك حتى يتمكن من فتح بث Remote Play.

**ما يُرسل إلى خارج شبكتك**

- **Google Translate (الافتراضي):** الترجمة تجري عبر الإنترنت. نص الترجمة الإنجليزي الذي قُرئ من الشاشة يُرسل إلى
  خدمة Google Translate (`translate.googleapis.com`) ليُترجم إلى العربية. يُرسل النص فقط؛ لا تُرسل الصورة ولا الصوت
  ولا معلومات الحساب. تسري على ذلك شروط الخصوصية الخاصة بـ Google، وترى الخدمة عنوان IP الخاص بك كأي طلب ويب.
- **DeepL (فقط إذا فعّلته أنت):** إذا أدخلت مفتاح DeepL الخاص بك في الإعدادات، يُرسل نص الترجمة الإنجليزي إلى DeepL
  (`api.deepl.com` أو `api-free.deepl.com`) بدلًا من Google. لا تُرسل الصورة ولا الصوت ولا معلومات الحساب. تسري
  شروط الخصوصية الخاصة بـ DeepL. في Windows يُحفظ المفتاح في الملف المشفّر بـ DPAPI، وإذا تعذّر الوصول إلى DeepL
  يعود التطبيق إلى Google Translate. في Mac يبقى المفتاح في ذاكرة التطبيق أثناء تشغيله ولا يُحفظ. إن لم تُدخل
  مفتاحًا فلا يُرسل أي شيء إلى DeepL.
- **Sony:** تسجيل الدخول إلى PlayStation Network يجري في صفحة Sony نفسها؛ لا يرى GameTranslator كلمة مرورك. بعد
  الدخول يُطلب من Sony معرّف الحساب فقط، ولا يُحفظ مفتاح الجلسة.
- **GitHub (في Windows فقط):** يتحقق التطبيق من وجود إصدار جديد في صفحة إصدارات المستودع
  `https://github.com/mahmoude4477/GameTranslator`، ويُنزّل التحديث منها إذا طلبت ذلك. لا تُرسل أي بيانات شخصية.
- **LG و Samsung (في Windows، فقط عند تثبيت تطبيق التلفزيون):** لتمديد جلسة وضع المطوّر في تلفزيون LG يتصل
  التطبيق بموقع مطوّري LG (`developer.lge.com`). ولتثبيت التطبيق على تلفزيون Samsung تسجّل الدخول في صفحة Samsung
  نفسها، ثم يطلب التطبيق من خوادم Samsung شهادة توقيع خاصة بتلفزيونك. تسري شروط الخصوصية الخاصة بكل شركة.
- **الشبكة المنزلية:** البحث عن PS5 والتلفزيون والإقران معهما يجري عبر الشبكة المحلية فقط.

تطبيق التلفزيون يستقبل الترجمة فقط من كمبيوتر GameTranslator داخل الشبكة المنزلية، ولا يتصل بالإنترنت.

لا توجد في هذه النسخة ترجمة تجري على الجهاز من دون إنترنت، ولا دبلجة صوتية.

للأسئلة: https://github.com/mahmoude4477/GameTranslator/issues

## English

GameTranslator is open-source software. The original project is by Sefflex; this is its Arabic edition. It does not
collect personal data and does not send any data to servers of ours (we do not operate any). It contains no
advertising, analytics or tracking code.

**Stays on your computer:** the game picture received from the PS5 is processed in memory only to read
subtitles; text recognition (OCR) runs on your device (Apple Vision on Mac, Windows.Media.Ocr on Windows), and the
picture is never stored or uploaded. The translated Arabic text is sent only to the TV (or the OBS overlay or a
browser) on your home network. Your numeric PlayStation Network account ID and the PS5 and TV pairing keys are stored
locally (a file encrypted for your Windows user account with DPAPI on Windows; the app's preferences on Mac). The PS5
pairing data is also written to the settings of Chiaki on your computer so that it can open the Remote Play stream.

**Sent outside your network:** translation is done online. The English subtitle text that was read is sent to
Google Translate (`translate.googleapis.com`) to be translated into Arabic; if you enter your own DeepL key in
settings, it is sent to DeepL (`api.deepl.com` or `api-free.deepl.com`) instead. Only the text is sent: no picture,
audio or account data. Google's and DeepL's own privacy terms apply, and the service sees your IP address like any
web request. On Windows the DeepL key is stored in the DPAPI-encrypted file, and Google Translate is used when DeepL
cannot be reached; on Mac the key is kept in memory while the app runs and is not saved. Nothing is sent to DeepL
unless you enter a key. PlayStation Network sign-in happens on Sony's own page, and GameTranslator never sees your
password; afterwards only the account ID is requested from Sony, and no session token is kept. On Windows the app
checks the Releases page of `https://github.com/mahmoude4477/GameTranslator` for a new version and downloads it from
there if you ask; no personal data is sent. On Windows, only when you install the TV app: the app contacts LG's
developer site (`developer.lge.com`) to extend the developer mode session of an LG TV, and for a Samsung TV you sign
in on Samsung's own page and the app requests a signing certificate for your TV from Samsung's servers. PS5 and TV
discovery and pairing use your local network only.

The TV app receives subtitles only from the GameTranslator computer on your home network and does not connect
to the internet.

This edition has no on-device (offline) translation and no dubbing.

Contact: https://github.com/mahmoude4477/GameTranslator/issues
