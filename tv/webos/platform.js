// SPDX-License-Identifier: AGPL-3.0-only
// ما يخص webOS. مقابله في Tizen: tv/tizen/platform.js
(function () {
  'use strict';

  window.GAMETRANSLATOR_PLATFORM = window.GAMETRANSLATOR_PLATFORM = {
    backKey: 461,

    // اللعب يكون بذراع التحكم ولا يُلمس ريموت التلفزيون؛ فيظن التلفزيون أنه خامل ويشغّل شاشة التوقف ثم ينام.
    // قبل تشغيل شاشة التوقف يرسل التلفزيون للمشتركين طلبًا بطابع زمني؛ والرد ack:false يؤجله
    // (كما تفعل نسخة Kodi على webOS). إن لم توجد الخدمة أو لم يُسمح بها يُتجاوز الأمر بصمت.
    init: function () {
      var CLIENT = 'com.gametranslator.tv';
      try {
        var register = new PalmServiceBridge();
        register.onservicecallback = function (message) {
          var request;
          try { request = JSON.parse(message); } catch (e) { return; }
          if (!request) return;
          // لسطر التشخيص في اللوحة: السبب إن رفضت الخدمة، وعدد الطلبات المؤجلة إن قبلت.
          if (request.returnValue === false) window.__screenSaverStatus = 'denied: ' + (request.errorText || request.errorCode);
          if (request.timestamp === undefined) return;
          window.__screenSaverCount = (window.__screenSaverCount || 0) + 1;
          window.__screenSaverStatus = 'deferred ' + window.__screenSaverCount;
          var reply = new PalmServiceBridge();
          reply.call('luna://com.webos.service.tvpower/power/responseScreenSaverRequest',
            JSON.stringify({ clientName: CLIENT, ack: false, timestamp: request.timestamp }));
          window.__screenSaverReply = reply;
        };
        register.call('luna://com.webos.service.tvpower/power/registerScreenSaverRequest',
          JSON.stringify({ subscribe: true, clientName: CLIENT }));
        // حتى لا يُحذف كائن الجسر من الذاكرة؛ يبقى الاشتراك ما دام التطبيق مفتوحًا.
        window.__screenSaverBridge = register;
      } catch (e) {}
    },

    launchParams: function () {
      return window.PalmSystem && window.PalmSystem.launchParams;
    },

    onRelaunch: function (callback) {
      document.addEventListener('webOSRelaunch', function (e) { callback(e.detail); });
    },

    // onStatus: 'loading' | 'ready' | 'playing' | 'inputError' | 'sourceError'
    showHdmi: function (port, host, onStatus) {
      host.innerHTML = '';

      var video = document.createElement('video');
      video.autoplay = true;
      var source = document.createElement('source');
      source.setAttribute('type', 'service/webos-external');
      source.setAttribute('src', 'ext://hdmi:' + port);
      video.appendChild(source);

      onStatus('loading');
      video.addEventListener('loadeddata', function () { onStatus('ready'); });
      video.addEventListener('playing', function () { onStatus('playing'); });
      video.addEventListener('error', function () { onStatus('inputError'); });
      source.addEventListener('error', function () { onStatus('sourceError'); });

      host.appendChild(video);
    },

    // يبحث في قائمة مداخل التلفزيون عن منفذ HDMI الموصول به جهاز الألعاب.
    // إن لم يجده يستدعي callback بالقيمة null.
    findConsolePort: function (callback) {
      var done = false;
      function finish(port, label) {
        if (done) return;
        done = true;
        callback(port, label);
      }
      try {
        var bridge = new PalmServiceBridge();
        bridge.onservicecallback = function (msg) {
          var devices;
          try { devices = JSON.parse(msg).devices || []; } catch (e) { return finish(null); }
          var hdmi = devices.filter(function (d) { return /^HDMI_/.test(d.id); });
          // وجهاز الألعاب في وضع الراحة لا يظهر المدخل نشطًا لكن تسميته تبقى؛ يُفضَّل المدخل النشط.
          var consoles = hdmi.filter(function (d) {
            return d.spdSourceDeviceInfo === 'GAME' || /playstation|ps5/i.test((d.label || '') + ' ' + (d.spdProductDescription || ''));
          });
          var pick = consoles.filter(function (d) { return d.activate; })[0] || consoles[0] ||
            hdmi.filter(function (d) { return d.activate; })[0];
          finish(pick ? pick.port : null, pick ? pick.label : null);
        };
        bridge.call('luna://com.webos.service.eim/getAllInputStatus', '{}');
        // يُحتفظ بكائن الجسر حتى يصل الرد كي لا يُحذف من الذاكرة.
        window.__eimBridge = bridge;
        setTimeout(function () { finish(null); }, 3000);
      } catch (e) {
        finish(null);
      }
    },

    localAddress: function (callback) {
      var done = false;
      function finish(ip) {
        if (done) return;
        done = true;
        callback(ip);
      }
      try {
        var bridge = new PalmServiceBridge();
        bridge.onservicecallback = function (msg) {
          try {
            var res = JSON.parse(msg) || {};
            var ip = (res.wired && res.wired.ipAddress) || (res.wifi && res.wifi.ipAddress);
            if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip)) return finish(ip);
          } catch (e) {}
          finish(null);
        };
        bridge.call('luna://com.palm.connectionmanager/getStatus', '{}');
        window.__netBridge = bridge;
        setTimeout(function () { finish(null); }, 2000);
      } catch (e) {
        finish(null);
      }
    },

    exit: function () {
      if (window.PalmSystem && window.PalmSystem.platformBack) window.PalmSystem.platformBack();
    }
  };
})();
