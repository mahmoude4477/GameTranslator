// SPDX-License-Identifier: AGPL-3.0-only
// Android TV'ye özgü işler; asıl iş MainActivity.java'dadır. webOS karşılığı: tv-webos/platform.js
(function () {
  'use strict';

  var bridge = window.AndroidTV;
  var hdmiStatus = function () {};
  var relaunch = function () {};

  window.__onHdmiStatus = function (kind) { hdmiStatus(kind); };
  window.__onRelaunch = function (raw) { relaunch(raw); };
  // Kumanda tuşunu sayfaya iletir (Geri ve renk tuşları sayfaya kendiliğinden gelmez).
  window.__onKey = function (code) {
    var event = new Event('keydown', { cancelable: true });
    event.keyCode = code;
    document.dispatchEvent(event);
  };

  window.GAMETRANSLATOR_PLATFORM = {
    backKey: 4,

    init: function () {},

    launchParams: function () { return bridge.launchParams(); },

    onRelaunch: function (callback) { relaunch = callback; },

    // onStatus: 'loading' | 'ready' | 'playing' | 'inputError' | 'sourceError'
    showHdmi: function (port, host, onStatus) {
      hdmiStatus = onStatus;
      onStatus('loading');
      bridge.showHdmi(port);
    },

    findConsolePort: function (callback) {
      var port = bridge.consolePort();
      callback(port > 0 ? port : null, port > 0 ? 'HDMI ' + port : null);
    },

    localAddress: function (callback) { callback(bridge.localAddress() || null); },

    exit: function () { bridge.exit(); }
  };
})();
