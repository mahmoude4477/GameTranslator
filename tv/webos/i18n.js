// SPDX-License-Identifier: AGPL-3.0-only
// نصوص تطبيق التلفزيون. الواجهة عربية دائمًا؛ الإنجليزية تُستخدم فقط إن نقص مفتاح من العربية.
(function () {
  'use strict';

  var TEXTS = {
    ar: {
      waiting: 'افتح تطبيق GameTranslator على الكمبيوتر.',
      waitingHint: 'سيعثر التطبيق على هذا التلفزيون ويرسل الترجمة إليه.',
      panelTitle: 'GameTranslator',
      hdmi: 'HDMI',
      lastSubtitle: 'آخر ترجمة',
      diagnostics: 'التشخيص',
      server: 'التطبيق',
      connection: 'الاتصال',
      loading: '(جارٍ التحميل)',
      videoReady: '(وصلت الصورة)',
      playing: '(يعمل)',
      inputError: '(خطأ: تعذّر فتح المنفذ)',
      sourceError: '(خطأ: المصدر غير مدعوم)',
      auto: 'تلقائي',
      consoleNotFound: 'لم يُعثر على جهاز الألعاب',
      noEventSource: 'هذا التلفزيون غير مدعوم (لا يوجد EventSource)',
      connecting: 'جارٍ الاتصال…',
      connected: 'متصل',
      reconnecting: 'لا يوجد اتصال، جارٍ إعادة المحاولة…',
      none: '—',
      keys: 'موافق: هذه اللوحة · ◀▶: منفذ HDMI (واللوحة مفتوحة) · 1–4: منفذ HDMI · 0: البحث عن جهاز الألعاب تلقائيًا · ▲▼: تحريك الترجمة لأعلى/لأسفل · الأحمر: ترجمة تجريبية · الأزرق: عرض تجريبي · الأخضر: هذه اللوحة',
      localTest: 'اختبار: هل يظهر هذا النص فوق اللعبة؟',
      positionPreview: 'ستظهر الترجمة هنا',
      demoButton: 'شاهد العرض التجريبي',
      demoBadge: 'عرض تجريبي',
      demoHint: 'مثال: تظهر ترجمة اللعبة الإنجليزية هنا بالعربية. اضغط «رجوع» للخروج.'
    },
    en: {
      waiting: 'Open the GameTranslator app on your computer.',
      waitingHint: 'The app will find this TV and send subtitles here.',
      panelTitle: 'GameTranslator',
      hdmi: 'HDMI',
      lastSubtitle: 'Last subtitle',
      diagnostics: 'Diagnostics',
      server: 'App',
      connection: 'Connection',
      loading: '(loading)',
      videoReady: '(picture received)',
      playing: '(playing)',
      inputError: '(error: input could not be opened)',
      sourceError: '(error: source not supported)',
      auto: 'automatic',
      consoleNotFound: 'console not found',
      noEventSource: 'This TV is not supported (no EventSource)',
      connecting: 'connecting…',
      connected: 'connected',
      reconnecting: 'disconnected, retrying…',
      none: '—',
      keys: 'OK: this panel · ◀▶: HDMI input (panel open) · 1–4: HDMI input · 0: find console automatically · ▲▼: move subtitles up/down · Red: test subtitle · Blue: demo · Green: this panel',
      localTest: 'Test: is this text shown on top of the game?',
      positionPreview: 'Subtitles will appear here',
      demoButton: 'Watch the demo',
      demoBadge: 'DEMO',
      demoHint: 'Example: the game\'s English subtitles appear here in Arabic. Press Back to exit.'
    }
  };

  var lang = 'ar';

  window.t = function (key) {
    return (TEXTS[lang] && TEXTS[lang][key]) || TEXTS.en[key] || key;
  };

  // يملأ نص العناصر التي تحمل الخاصية data-i18n، ويجعل اللوحة ومربّع الانتظار والشارة من اليمين إلى اليسار.
  window.applyI18n = function () {
    var nodes = document.querySelectorAll('[data-i18n]');
    for (var i = 0; i < nodes.length; i++) {
      nodes[i].textContent = window.t(nodes[i].getAttribute('data-i18n'));
    }
    var boxes = ['panel', 'waiting', 'demo-badge'];
    for (var j = 0; j < boxes.length; j++) {
      var box = document.getElementById(boxes[j]);
      if (box) box.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
    }
  };
})();
