// SPDX-License-Identifier: AGPL-3.0-only
// تطبيق GameTranslator للتلفزيون (مشترك بين webOS و Tizen و Android TV).
// يعرض منفذ HDMI بملء الشاشة ويرسم فوقه الترجمة القادمة من تطبيق GameTranslator على الكمبيوتر.
// ما يخص كل تلفزيون موجود في platform.js. كُتب بصيغة ES5 ليعمل على متصفحات التلفزيونات القديمة.
(function () {
  'use strict';

  // إصدار تطبيق التلفزيون: يبقى مطابقًا لما في appinfo.json و tv/tizen/config.xml و tv/android/app/build.gradle.
  var APP_VERSION = '1.0.0';
  var t = window.t;
  var config = window.GAMETRANSLATOR_CONFIG || window.GAMETRANSLATOR_CONFIG || {};
  var platform = window.GAMETRANSLATOR_PLATFORM || window.GAMETRANSLATOR_PLATFORM;
  var KEY = { RED: 403, GREEN: 404, BLUE: 406, LEFT: 37, UP: 38, RIGHT: 39, DOWN: 40, OK: 13, BACK: platform.backKey };

  var server = null;
  var eventSource = null;
  // إن كان هناك منفذ اختير بالريموت ومحفوظ فهو المستخدم، وإلا يُبحث عن PS5 تلقائيًا.
  var savedPort = Number(load('hdmi'));
  var hdmiPort = savedPort || 1;

  var bottomPercent = 7;
  // موضع الترجمة: تغطي سطر اللعبة الإنجليزي (cover)، أو فوقه (above)، أو تحته (below)،
  // أو على ارتفاع ثابت (fixed). يبقى ثابتًا مع إصدارات GameTranslator القديمة التي لا ترسل الموضع.
  var PLACEMENTS = { cover: 1, above: 1, below: 1, fixed: 1 };
  var placement = 'fixed';
  // مكان سطر اللعبة الإنجليزي للسطر المعروض (نسبةً إلى الشاشة)، أو null إن لم يُعرف.
  var currentBox = null;

  // إن ظهر السطر السابق أقل من هذه المدة فقد لا يكون قُرئ؛ يبقى قليلًا فوق السطر الجديد.
  var PREVIOUS_HOLD_MS = 2500;
  var shownAt = 0;
  var previousTimer = null;

  var hideTimer = null;
  var panelTimer = null;

  // حدث «المسح» القادم من الكمبيوتر لا يزيل السطر قبل هذه المدة: إن اختفى سطر اللعبة أثناء وصول الترجمة كان النص
  // يظهر على التلفزيون لحظة ثم يختفي. المدة بنفس قاعدة زمن القراءة في الكمبيوتر، وحدّها الأعلى أقصر.
  var HOLD_MAX_MS = 5000;
  var holdUntil = 0;
  // عند انقطاع الاتصال للحظة لا يُمسح النص؛ يُمسح فقط إن لم يعد الاتصال خلال هذه المدة.
  var DROP_HIDE_MS = 6000;
  var dropTimer = null;

  // معلومات التشخيص الظاهرة في اللوحة: منها نعرف السبب عند بلاغات «النص لا يصل إلى التلفزيون».
  var diag = { subtitles: 0, clears: 0, drops: 0, backgrounded: 0, lastHide: '-', place: '-', error: '' };

  function $(id) { return document.getElementById(id); }

  function renderDiag() {
    var el = $('st-diag');
    if (!el) return;
    el.textContent = 'sub ' + diag.subtitles + ' · clear ' + diag.clears + ' · drop ' + diag.drops +
      ' · bg ' + diag.backgrounded + ' · hide ' + diag.lastHide + ' · ' + diag.place +
      (window.__screenSaverStatus ? ' · saver ' + window.__screenSaverStatus : '') +
      (diag.error ? ' · err ' + diag.error : '');
  }

  function noteError(message) {
    diag.error = String(message).slice(0, 120);
    renderDiag();
  }

  window.onerror = function (message, source, line) {
    noteError(message + ' @' + line);
    return false;
  };

  document.addEventListener('visibilitychange', function () {
    if (document.hidden) diag.backgrounded++;
    renderDiag();
  });

  function load(key) {
    try { return window.localStorage.getItem('gametranslator.' + key) || window.localStorage.getItem('gametranslator.' + key); } catch (e) { return null; }
  }

  function save(key, value) {
    try { window.localStorage.setItem('gametranslator.' + key, String(value)); } catch (e) {}
  }

  function setStatus(id, text, cls) {
    var el = $(id);
    el.textContent = text;
    el.className = cls || '';
  }

  // --- معاملات التشغيل ---

  function parseParams(raw) {
    if (!raw) return {};
    if (typeof raw === 'object') return raw;
    try { return JSON.parse(raw) || {}; } catch (e) { return {}; }
  }

  // يطبّق الخادم ومنفذ HDMI الواردين في المعاملات؛ وإن تغيّر الخادم يعيد الاتصال.
  function applyParams(params) {
    if (params.hdmi >= 1 && params.hdmi <= 4) {
      hdmiPort = Number(params.hdmi);
      save('hdmi', hdmiPort);
      showHdmi(hdmiPort);
    }
    if (params.server && params.server !== server) {
      save('server', params.server);
      connect(params.server);
    }
  }

  // --- صورة HDMI ---

  function showHdmi(port) {
    setStatus('st-hdmi', String(port));
    var statuses = {
      loading: ['loading', 'muted'],
      ready: ['videoReady', 'ok'],
      playing: ['playing', 'ok'],
      inputError: ['inputError', 'bad'],
      sourceError: ['sourceError', 'bad']
    };
    platform.showHdmi(port, $('video-host'), function (kind) {
      setStatus('st-video', t(statuses[kind][0]), statuses[kind][1]);
    });
  }

  function autoSelectConsole(onNotFound) {
    platform.findConsolePort(function (port, label) {
      if (!port) return onNotFound && onNotFound();
      if (port !== hdmiPort) {
        hdmiPort = port;
        showHdmi(port);
      }
      setStatus('st-hdmi', port + ' — ' + label + ' (' + t('auto') + ')');
    });
  }

  // --- الترجمة النصية ---

  // في اللغات المكتوبة من اليمين إلى اليسار كالعربية يُرتَّب السطر من اليمين إلى اليسار؛ وإلا بقيت علامات الترقيم
  // واسم المتكلم في الطرف الخطأ. يُعرف الاتجاه من النص نفسه، فيعمل مع إصدارات الكمبيوتر القديمة أيضًا.
  var RTL_CHARS = /[\u0590-\u08FF\uFB1D-\uFDFF\uFE70-\uFEFC]/;

  function isRtl(text) { return RTL_CHARS.test(text || ''); }

  function setDirection(el, text) {
    var rtl = isRtl(text);
    el.setAttribute('dir', rtl ? 'rtl' : 'ltr');
    return rtl;
  }

  // هل أرقام المربّع صالحة (بين 0 و1، اليمين أكبر من اليسار، والأسفل أكبر من الأعلى).
  function validBox(box) {
    if (!box || typeof box !== 'object') return null;
    var left = Number(box.left), top = Number(box.top), right = Number(box.right), bottom = Number(box.bottom);
    if (isNaN(left) || isNaN(top) || isNaN(right) || isNaN(bottom)) return null;
    left = Math.max(0, Math.min(0.98, left));
    right = Math.max(left + 0.01, Math.min(1.0, right));
    top = Math.max(0, Math.min(0.98, top));
    bottom = Math.max(top + 0.01, Math.min(1.0, bottom));
    return { left: left, top: top, right: right, bottom: bottom };
  }

  // paragraphs: النصوص ذات العنوان والبنود (مثل لوحة المهام) تُعرض سطرًا سطرًا بمحاذاة بداية السطر.
  function showSubtitle(text, duration, box, paragraphs) {
    // لم يعد السطر السابق يُعرض منفصلًا: كان المربّعان يبدوان متراكبين، وقراءتان للجملة نفسها
    // تبدوان كترجمة ثانية. السطر الجديد يحلّ محل القديم.
    hidePrevious();
    shownAt = Date.now();
    holdUntil = shownAt + Math.min(HOLD_MAX_MS, 2000 + text.length * 60);
    currentBox = validBox(box);
    $('subtitle-text').textContent = text;
    var rtl = setDirection($('subtitle-text'), text);
    $('subtitle-text').style.whiteSpace = paragraphs ? 'pre-line' : '';
    $('subtitle-text').style.textAlign = paragraphs ? (rtl ? 'right' : 'left') : '';
    $('subtitle').className = '';
    layout();
    setStatus('st-last', text.length > 40 ? text.slice(0, 40) + '…' : text);
    setDirection($('st-last'), text);
    notePlace();
    clearTimeout(hideTimer);
    // حتى لا تزيل مدةٌ غير صالحة أو قصيرة جدًا النص فور ظهوره.
    var ms = duration > 0 ? Math.max(1000, duration) : Math.min(8000, 2000 + text.length * 60);
    hideTimer = setTimeout(function () { hideSubtitle('timer'); }, ms);
  }

  // المكان الفعلي للنص على الشاشة وهل هو ظاهر (للتشخيص).
  function notePlace() {
    var el = $('subtitle-text');
    var rect = el.getBoundingClientRect();
    var height = document.documentElement.clientHeight || window.innerHeight || 0;
    diag.place = 'y ' + Math.round(rect.top) + '-' + Math.round(rect.bottom) + '/' + height + ' ' +
      window.getComputedStyle($('subtitle')).visibility + ' f' + window.getComputedStyle(el).fontSize;
    renderDiag();
  }

  // reason: ما الذي أزال النص؛ يظهر السبب في اللوحة مع مدة العرض.
  function hideSubtitle(reason) {
    clearTimeout(hideTimer);
    if ($('subtitle').className !== 'hidden') {
      diag.lastHide = (reason || '?') + ' ' + (Date.now() - shownAt) + 'ms';
      renderDiag();
    }
    $('subtitle').className = 'hidden';
    $('subtitle-cover').className = 'hidden';
    hidePrevious();
  }

  // «المسح» القادم من الكمبيوتر: إن لم يظهر النص مدة تكفي لقراءته يُزال بعد انقضاء المدة الباقية.
  function onClear() {
    diag.clears++;
    renderDiag();
    var left = holdUntil - Date.now();
    if (left <= 0 || $('subtitle').className === 'hidden') return hideSubtitle('clear');
    clearTimeout(hideTimer);
    hideTimer = setTimeout(function () { hideSubtitle('clear-held'); }, left);
  }

  function showPrevious(text) {
    $('subtitle-prev-text').textContent = text;
    setDirection($('subtitle-prev-text'), text);
    $('subtitle-prev').className = '';
    clearTimeout(previousTimer);
    previousTimer = setTimeout(hidePrevious, PREVIOUS_HOLD_MS);
  }

  function hidePrevious() {
    clearTimeout(previousTimer);
    $('subtitle-prev').className = 'hidden';
  }

  // --- ترجمة الشاشة ---
  // يقرأ الكمبيوتر الشاشة كلها ويرسل ترجمة كل مجموعة نص مع مكانها. كل ترجمة تغطي نصها الإنجليزي؛
  // يُختار حجم الخط من ارتفاع سطر ذلك النص، وإن طالت الترجمة يصغر الخط حتى تتسع.

  var screenTimer = null;
  var SCREEN_PAD = 0.004;
  var SCREEN_MIN_FONT = 14;

  function showScreen(items, duration) {
    var host = $('screen');
    // حتى لا تبقى عبارة «جارٍ ترجمة الشاشة…» فوق النتائج.
    hideSubtitle('screen');
    host.innerHTML = '';
    host.className = '';
    var width = window.innerWidth || 1920;
    var height = window.innerHeight || 1080;
    for (var i = 0; i < (items || []).length; i++) {
      var item = items[i];
      var box = validBox(item && item.box);
      if (!box || !item.text) continue;
      var el = document.createElement('div');
      el.className = item.paragraphs === true ? 'item paragraphs' : 'item';
      el.textContent = item.text;
      // النص ذو البنود يُحاذى إلى اليمين في اللغات المكتوبة من اليمين إلى اليسار.
      if (setDirection(el, item.text) && item.paragraphs === true) el.style.textAlign = 'right';
      var left = Math.max(0, box.left - SCREEN_PAD);
      var top = Math.max(0, box.top - SCREEN_PAD);
      var boxHeight = (box.bottom - box.top + 2 * SCREEN_PAD) * height;
      el.style.left = (left * 100) + '%';
      el.style.top = (top * 100) + '%';
      el.style.width = Math.max(80, (Math.min(1, box.right + SCREEN_PAD) - left) * width) + 'px';
      el.style.minHeight = Math.round(boxHeight) + 'px';
      var line = typeof item.line === 'number' && item.line > 0 ? item.line * height : box.bottom * height - box.top * height;
      var size = Math.max(SCREEN_MIN_FONT, Math.min(96, Math.round(line * 0.95)));
      el.style.fontSize = size + 'px';
      host.appendChild(el);
      // قد تكون الترجمة أطول من الإنجليزية: يُسمح للمربّع بأن يطول قليلًا، وما زاد يصغر له الخط.
      var limit = Math.max(boxHeight * 1.5, line * 2.6);
      while (el.scrollHeight > limit && size > SCREEN_MIN_FONT) {
        size -= 2;
        el.style.fontSize = size + 'px';
      }
    }
    clearTimeout(screenTimer);
    screenTimer = setTimeout(hideScreen, duration || 20000);
  }

  function hideScreen() {
    clearTimeout(screenTimer);
    $('screen').className = 'hidden';
    $('screen').innerHTML = '';
  }

  // يُضبط الموضع من تطبيق GameTranslator ومن أسهم الريموت معًا. الموضع المختار بالريموت
  // يبقى حتى يتغيّر الإعداد في التطبيق (آخر تغيير هو المعتمد).
  function setBottom(percent) {
    bottomPercent = Math.max(0, Math.min(85, percent));
    layout();
  }

  // يضع الترجمة بحسب الموضع المختار. إن لم يُعرف مكان السطر الإنجليزي يُستخدم الارتفاع الثابت.
  function layout() {
    var el = $('subtitle');
    var cover = $('subtitle-cover');
    var box = placement === 'fixed' ? null : currentBox;

    el.style.left = '4%';
    el.style.right = '4%';
    el.style.top = 'auto';
    el.style.bottom = Math.max(2, Math.min(88, bottomPercent)) + '%';
    el.style.transform = 'none';
    el.style.webkitTransform = 'none';
    cover.className = 'hidden';

    // حتى لا يبقى الغطاء وحده حين لا توجد ترجمة أو لا يكون المربّع معرّفًا
    if (!box || el.className === 'hidden') return;

    var mode = placement;
    // إن لم يكن هناك مكان عند حافة الشاشة يُنتقل إلى الجهة الأخرى.
    if (mode === 'below' && box.bottom > 0.88) mode = 'above';
    if (mode === 'above' && box.top < 0.12) mode = 'below';

    if (mode === 'cover') {
      // خلفية داكنة تغطي السطر الإنجليزي تمامًا
      var padX = 0.02; // Yatayda %2 pay
      var padY = 0.015; // Dikeyde %1.5 pay
      var cLeft = Math.max(0.01, box.left - padX);
      var cTop = Math.max(0.01, box.top - padY);
      var cRight = Math.min(0.99, box.right + padX);
      var cBottom = Math.min(0.99, box.bottom + padY);

      cover.style.left = (cLeft * 100).toFixed(2) + '%';
      cover.style.top = (cTop * 100).toFixed(2) + '%';
      cover.style.width = ((cRight - cLeft) * 100).toFixed(2) + '%';
      cover.style.height = ((cBottom - cTop) * 100).toFixed(2) + '%';
      cover.className = '';

      // الترجمة تتوسط هذا الغطاء عموديًا
      var centerBottom = (1.0 - (box.top + box.bottom) / 2.0) * 100.0;
      el.style.top = 'auto';
      el.style.bottom = Math.max(2, Math.min(92, centerBottom)).toFixed(2) + '%';
      el.style.transform = 'translateY(50%)';
      el.style.webkitTransform = 'translateY(50%)';
    } else if (mode === 'above') {
      // فوق السطر الإنجليزي مباشرة (لا تغطيه)
      cover.className = 'hidden';
      var aboveBottom = (1.0 - box.top) * 100.0 + 1.5;
      el.style.top = 'auto';
      el.style.bottom = Math.max(5, Math.min(94, aboveBottom)).toFixed(2) + '%';
      el.style.transform = 'none';
      el.style.webkitTransform = 'none';
    } else if (mode === 'below') {
      // تحت السطر الإنجليزي مباشرة
      cover.className = 'hidden';
      var belowTop = box.bottom * 100.0 + 1.5;
      el.style.bottom = 'auto';
      el.style.top = Math.max(5, Math.min(94, belowTop)).toFixed(2) + '%';
      el.style.transform = 'none';
      el.style.webkitTransform = 'none';
    }
  }

  // يمنع خروج الموضع الثابت عن الشاشة في بعض التلفزيونات.
  function keepOnScreen(el, cover) {
    if (placement === 'fixed') {
      bottomPercent = Math.max(2, Math.min(85, bottomPercent));
      el.style.bottom = bottomPercent + '%';
    }
  }

  function applyStyle(style) {
    if (style.fontSize) {
      $('subtitle-text').style.fontSize = style.fontSize + 'px';
      $('subtitle-prev-text').style.fontSize = Math.round(style.fontSize * 0.8) + 'px';
    }
    if (style.color) {
      $('subtitle-text').style.color = style.color;
      $('subtitle-prev-text').style.color = style.color;
    }
    if (PLACEMENTS[style.placement]) placement = style.placement;
    if (typeof style.background === 'number') {
      var alpha = Math.max(0, Math.min(100, style.background)) / 100;
      $('subtitle-text').style.background = 'rgba(0, 0, 0, ' + alpha + ')';
      $('subtitle-prev-text').style.background = 'rgba(0, 0, 0, ' + (alpha * 0.83).toFixed(2) + ')';
      var coverEl = $('subtitle-cover');
      if (coverEl) coverEl.style.background = 'rgba(0, 0, 0, ' + Math.max(0.92, alpha) + ')';
    }
    if (typeof style.bold === 'boolean') {
      $('subtitle-text').style.fontWeight = style.bold ? '700' : '600';
      $('subtitle-prev-text').style.fontWeight = style.bold ? '700' : '600';
    }
    if (style.bottomPercent >= 0) {
      bottomPercent = Math.max(0, Math.min(92, Number(style.bottomPercent)));
    }
    layout();
  }

  function nudgeSubtitle(step) {
    // إن كان الموضع تابعًا لسطر اللعبة فلا يُحرَّك بالريموت.
    if (placement !== 'fixed') return;
    setBottom(bottomPercent + step);
    save('bottom', bottomPercent);
    // إن لم يكن هناك نص يُعرض مثال ليُرى أين صار الموضع.
    if ($('subtitle').className === 'hidden') showSubtitle(t('positionPreview'), 2500);
  }

  // --- الاتصال بالخادم (Server-Sent Events) ---

  function onEvent(name, handler) {
    eventSource.addEventListener(name, function (e) {
      var data;
      try {
        data = JSON.parse(e.data);
      } catch (err) {
        noteError(name + ': bad json');
        return;
      }
      try {
        handler(data);
      } catch (err) {
        noteError(name + ': ' + err.message);
      }
    });
  }

  function connect(address) {
    if (demoRunning()) stopDemo();
    if (eventSource) eventSource.close();
    server = address;
    $('waiting').className = 'hidden';
    setStatus('st-server', address);
    if (!window.EventSource) {
      setStatus('st-conn', t('noEventSource'), 'bad');
      return;
    }
    setStatus('st-conn', t('connecting'), 'muted');
    eventSource = new EventSource(address + '/events');
    eventSource.onopen = function () {
      clearTimeout(scanTimer);
      clearTimeout(dropTimer);
      dropTimer = null;
      setStatus('st-conn', t('connected'), 'ok');
    };
    eventSource.onerror = function () {
      // إن لم يعد الاتصال خلال مدة قصيرة فلا تبقى الترجمة القديمة معلّقة على الشاشة. الانقطاع اللحظي لا يمسح النص.
      diag.drops++;
      renderDiag();
      if (dropTimer === null) {
        dropTimer = setTimeout(function () {
          dropTimer = null;
          hideSubtitle('drop');
          hideScreen();
        }, DROP_HIDE_MS);
      }
      setStatus('st-conn', t('reconnecting'), 'bad');
      // قد يكون عنوان الكمبيوتر تغيّر.
      scheduleScan();
    };
    onEvent('subtitle', function (data) {
      diag.subtitles++;
      renderDiag();
      if (data.text) showSubtitle(data.text, data.duration, data.box, data.paragraphs === true);
    });
    onEvent('clear', onClear);
    onEvent('screen', function (data) { showScreen(data.items, data.duration); });
    onEvent('screen-clear', hideScreen);
    onEvent('style', applyStyle);
  }

  // --- العثور على الكمبيوتر في الشبكة ---
  // إن لم يستطع الكمبيوتر فتح تطبيق التلفزيون فلن يصل العنوان. في التلفزيونات التي تعرف عنوانها يفحص التطبيق الشبكة نفسها.

  var SERVER_PORT = 8787;
  var SCAN_PARALLEL = 24;
  var SCAN_TIMEOUT_MS = 1200;
  var SCAN_PAUSE_MS = 5000;
  var scanTimer = null;
  var scanning = false;

  function scheduleScan() {
    if (!platform.localAddress || scanning) return;
    clearTimeout(scanTimer);
    scanTimer = setTimeout(scanForServer, SCAN_PAUSE_MS);
  }

  function probe(address, done) {
    var request = new XMLHttpRequest();
    var finished = false;
    function finish(ok) {
      if (finished) return;
      finished = true;
      done(ok);
    }
    request.onload = function () { finish(request.status === 200 && (request.responseText.indexOf('gametranslator') >= 0 || request.responseText.indexOf('gametrans') >= 0)); };
    request.onerror = function () { finish(false); };
    request.ontimeout = function () { finish(false); };
    try {
      request.open('GET', address + '/hello');
      request.timeout = SCAN_TIMEOUT_MS;
      request.send();
    } catch (e) {
      finish(false);
    }
  }

  function scanForServer() {
    if (scanning || !platform.localAddress) return;
    platform.localAddress(function (ip) {
      var match = /^(\d+\.\d+\.\d+)\.\d+$/.exec(ip || '');
      if (!match) return scheduleScan();
      scanning = true;
      var next = 1, active = 0, found = false;
      function pump() {
        while (!found && active < SCAN_PARALLEL && next <= 254) {
          active++;
          tryAddress('http://' + match[1] + '.' + (next++) + ':' + SERVER_PORT);
        }
        if (!found && !active && next > 254) {
          scanning = false;
          scheduleScan();
        }
      }
      function tryAddress(address) {
        probe(address, function (ok) {
          active--;
          if (found) return;
          if (!ok) return pump();
          found = true;
          scanning = false;
          // إن كان الكمبيوتر نفسه فإن EventSource يعيد الاتصال وحده.
          if (address !== server) {
            save('server', address);
            connect(address);
          }
        });
      }
      pump();
    });
  }

  // --- العرض التجريبي ---
  // لمراجعة المتجر ولمن يفتح التطبيق أول مرة: تُعرض ترجمات نموذجية بلا PS5 ولا كمبيوتر.

  var DEMO_LINES = [
    'ليس لدينا وقت نضيعه هنا.',
    'MIRA: الباب لن يصمد أكثر من ذلك!',
    'ابقَ قريبًا. الطريق أمامنا خطر.',
    'انتظر! من أنت؟',
    'هذا الأمر لم ينتهِ بعد.'
  ];
  var DEMO_LINE_MS = 3200;
  var DEMO_GAP_MS = 700;
  var demoTimer = null;
  var demoIndex = 0;

  function demoRunning() { return demoTimer !== null; }

  function nextDemoLine() {
    showSubtitle(DEMO_LINES[demoIndex], DEMO_LINE_MS);
    demoIndex = (demoIndex + 1) % DEMO_LINES.length;
    demoTimer = setTimeout(nextDemoLine, DEMO_LINE_MS + DEMO_GAP_MS);
  }

  function startDemo() {
    if (demoRunning()) return;
    hidePanel();
    $('waiting').className = 'hidden';
    $('demo-badge').className = '';
    demoIndex = 0;
    nextDemoLine();
  }

  function stopDemo() {
    clearTimeout(demoTimer);
    demoTimer = null;
    hideSubtitle('demo');
    $('demo-badge').className = 'hidden';
    // إن لم يكن متصلًا بالتطبيق يعود إلى شاشة الانتظار.
    if (!server) $('waiting').className = '';
  }

  // زر الرجوع: يغلق العرض التجريبي أو اللوحة أولًا؛ ومن الشاشة الرئيسية للتطبيق يعود إلى شاشة التلفزيون الرئيسية.
  function goBack() {
    if (demoRunning()) return stopDemo();
    if ($('panel').className !== 'hidden') return hidePanel();
    platform.exit();
  }

  // --- اللوحة والريموت ---

  // يقسم سطر «1–4: منفذ HDMI · 0: …» إلى سطر لكل زر؛ واسم الزر بلون التمييز.
  function formatKeys() {
    var el = $('keys');
    var parts = el.textContent.split(' · ');
    el.innerHTML = '';
    for (var i = 0; i < parts.length; i++) {
      var colon = parts[i].indexOf(':');
      var row = document.createElement('div');
      row.className = 'key';
      var key = document.createElement('b');
      key.textContent = colon > 0 ? parts[i].slice(0, colon) : '';
      row.appendChild(key);
      row.appendChild(document.createTextNode(colon > 0 ? parts[i].slice(colon + 1).trim() : parts[i]));
      el.appendChild(row);
    }
  }

  // عند فتح اللوحة يُغلق مربّع الانتظار: إذا تراكبا تعذّرت قراءة النص.
  function showPanel(autoHide) {
    $('waiting').className = 'hidden';
    $('panel').className = '';
    clearTimeout(panelTimer);
    if (autoHide) panelTimer = setTimeout(hidePanel, 8000);
  }

  function hidePanel() {
    $('panel').className = 'hidden';
    if (!server && !demoRunning()) $('waiting').className = '';
  }

  function togglePanel() {
    if ($('panel').className === 'hidden') showPanel(false); else hidePanel();
  }

  // للريموت الذي لا أزرار أرقام فيه: واللوحة مفتوحة يغيّر زرا اليسار واليمين منفذ HDMI.
  function stepHdmi(step) {
    hdmiPort = (hdmiPort + step + 3) % 4 + 1;
    save('hdmi', hdmiPort);
    showHdmi(hdmiPort);
    showPanel(true);
  }

  document.addEventListener('keydown', function (e) {
    var code = e.keyCode;
    if (code === 48) { // 0: انسَ الاختيار اليدوي وابحث عن جهاز الألعاب من جديد
      save('hdmi', '');
      autoSelectConsole(function () {
        setStatus('st-hdmi', hdmiPort + ' (' + t('consoleNotFound') + ')', 'bad');
      });
      showPanel(true);
    } else if (code >= 49 && code <= 52) { // 1–4
      hdmiPort = code - 48;
      save('hdmi', hdmiPort);
      showHdmi(hdmiPort);
      showPanel(true);
    } else if (code === KEY.RED) {
      showSubtitle(t('localTest'), 4000);
    } else if (code === KEY.GREEN) {
      togglePanel();
    } else if (code === KEY.BLUE) {
      if (demoRunning()) stopDemo(); else startDemo();
    } else if (code === KEY.OK && $('waiting').className !== 'hidden') {
      startDemo();
    } else if (code === KEY.OK && !demoRunning()) {
      togglePanel();
    } else if ((code === KEY.LEFT || code === KEY.RIGHT) && $('panel').className !== 'hidden') {
      stepHdmi(code === KEY.RIGHT ? 1 : -1);
    } else if (code === KEY.BACK) {
      goBack();
      e.preventDefault();
    } else if (code === KEY.UP || code === KEY.DOWN) {
      nudgeSubtitle(code === KEY.UP ? 3 : -3);
      e.preventDefault();
    }
  });

  // إن أُعيد تشغيل التطبيق وهو مفتوح (مثلًا حين يُحضره GameTranslator إلى الواجهة) تصل المعاملات هنا.
  platform.onRelaunch(function (raw) { applyParams(parseParams(raw)); });

  platform.init();
  window.applyI18n();
  formatKeys();
  var savedBottom = load('bottom') || load('appBottom');
  if (savedBottom) setBottom(Number(savedBottom));
  showHdmi(hdmiPort);
  if (!savedPort) autoSelectConsole();

  $('app-version').textContent = APP_VERSION;
  renderDiag();
  var initial = parseParams(platform.launchParams());
  var address = initial.server || load('server') || config.server;
  applyParams(initial);
  if (!server) {
    if (address) connect(address); else $('waiting').className = '';
  }
  if (!server) scanForServer();
  // إن كانت شاشة الانتظار ظاهرة فلا تُفتح اللوحة تلقائيًا؛ يمكن فتحها بالزر الأخضر.
  if (server) showPanel(true); else hidePanel();
})();
