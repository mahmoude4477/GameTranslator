// SPDX-License-Identifier: AGPL-3.0-only
// ما يخص Tizen (تلفزيونات Samsung). مقابله في webOS: tv/webos/platform.js
// تُعرض صورة HDMI خلف طبقة HTML عبر واجهة TVWindow الرسمية.
(function () {
  'use strict';

  var KEYS = ['0', '1', '2', '3', '4', 'ColorF0Red', 'ColorF1Green', 'ColorF3Blue'];

  function hdmiSources(callback) {
    try {
      tizen.systeminfo.getPropertyValue('VIDEOSOURCE', function (info) {
        var isHdmi = function (source) { return source.type === 'HDMI'; };
        callback((info.connected || []).filter(isHdmi), (info.disconnected || []).filter(isHdmi));
      }, function () { callback([], []); });
    } catch (e) {
      callback([], []);
    }
  }

  // تطبيق GameTranslator يمرّر المعاملات داخل PAYLOAD عند فتح تطبيق التلفزيون.
  function paramsOf(appControl) {
    var data = (appControl && appControl.data) || [];
    for (var i = 0; i < data.length; i++) {
      if (data[i].key !== 'PAYLOAD' || !data[i].value || !data[i].value.length) continue;
      try {
        var payload = JSON.parse(data[i].value[0]);
        return payload && payload.values !== undefined ? payload.values : payload;
      } catch (e) {
        return null;
      }
    }
    return null;
  }

  function requestedParams() {
    try {
      return paramsOf(tizen.application.getCurrentApplication().getRequestedAppControl().appControl);
    } catch (e) {
      return null;
    }
  }

  window.GAMETRANSLATOR_PLATFORM = window.GAMETRANSLATOR_PLATFORM = {
    backKey: 10009,

    init: function () {
      // أزرار الألوان والأرقام يستخدمها التلفزيون لنفسه إن لم تُطلب.
      for (var i = 0; i < KEYS.length; i++) {
        try { tizen.tvinputdevice.registerKey(KEYS[i]); } catch (e) {}
      }
      // اللعب يكون دون لمس ريموت التلفزيون؛ حتى لا يظن التلفزيون أنه خامل فيشغّل شاشة التوقف (Samsung AppCommon API).
      // في التلفزيونات القديمة التي لا تملك الواجهة يُتجاوز الأمر بصمت.
      try {
        webapis.appcommon.setScreenSaver(webapis.appcommon.AppCommonScreenSaverState.SCREEN_SAVER_OFF);
      } catch (e) {}
    },

    launchParams: requestedParams,

    onRelaunch: function (callback) {
      window.addEventListener('appcontrol', function () {
        var params = requestedParams();
        if (params) callback(params);
      });
    },

    // onStatus: 'loading' | 'ready' | 'playing' | 'inputError' | 'sourceError'
    showHdmi: function (port, host, onStatus) {
      onStatus('loading');
      hdmiSources(function (connected, disconnected) {
        var all = connected.concat(disconnected);
        var source = all.filter(function (s) { return s.number === port; })[0];
        if (!source) return onStatus('sourceError');
        var failed = function () { onStatus('inputError'); };
        try {
          tizen.tvwindow.setSource(source, function () {
            tizen.tvwindow.show(function () { onStatus('playing'); }, failed,
              ['0', '0', '100%', '100%'], 'MAIN', 'BEHIND');
          }, failed);
        } catch (e) {
          failed();
        }
      });
    },

    // عنوان التلفزيون على الشبكة؛ للبحث عن الكمبيوتر.
    localAddress: function (callback) {
      function read(property, otherwise) {
        try {
          tizen.systeminfo.getPropertyValue(property, function (info) {
            if (info && info.ipAddress) callback(info.ipAddress); else otherwise();
          }, otherwise);
        } catch (e) {
          otherwise();
        }
      }
      read('ETHERNET_NETWORK', function () {
        read('WIFI_NETWORK', function () { callback(null); });
      });
    },

    // إن كان هناك منفذ HDMI واحد موصول فهو المطلوب؛ وإن تعددت يُفضَّل المدخل الحالي للتلفزيون.
    findConsolePort: function (callback) {
      hdmiSources(function (connected) {
        var pick = connected.length === 1 ? connected[0] : null;
        if (!pick && connected.length > 1) {
          try {
            var current = tizen.tvwindow.getSource();
            pick = connected.filter(function (s) { return s.number === current.number && current.type === 'HDMI'; })[0] || null;
          } catch (e) {}
        }
        callback(pick ? pick.number : null, pick ? 'HDMI ' + pick.number : null);
      });
    },

    exit: function () {
      try { tizen.application.getCurrentApplication().exit(); } catch (e) {}
    }
  };
})();
