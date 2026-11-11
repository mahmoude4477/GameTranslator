// SPDX-License-Identifier: AGPL-3.0-only
package com.gametranslator.tv;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.media.tv.TvContract;
import android.media.tv.TvInputInfo;
import android.media.tv.TvInputManager;
import android.media.tv.TvView;
import android.os.Bundle;
import android.util.DisplayMetrics;
import android.view.KeyEvent;
import android.view.View;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.widget.FrameLayout;

import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/**
 * يعرض منفذ HDMI بملء الشاشة عبر TvView؛ وفوقه WebView شفاف تعمل فيه الواجهة المشتركة مع LG و Samsung.
 */
public class MainActivity extends Activity {
    private TvView tvView;
    private WebView webView;
    private String launchParams = "";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        launchParams = paramsOf(getIntent());

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);
        if (getSystemService(TV_INPUT_SERVICE) != null) {
            tvView = new TvView(this);
            tvView.setCallback(new TvView.TvInputCallback() {
                @Override public void onConnectionFailed(String inputId) { status("inputError"); }
                @Override public void onDisconnected(String inputId) { status("inputError"); }
                @Override public void onVideoAvailable(String inputId) { status("playing"); }
                @Override public void onVideoUnavailable(String inputId, int reason) { status("inputError"); }
            });
            root.addView(tvView);
        }

        webView = new WebView(this);
        webView.setBackgroundColor(Color.TRANSPARENT);
        webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        // يُستخدم عرض الصفحة 1920 بكسل ويُلاءَم مع الشاشة (انظر index.html)؛ وإلا ظهر النص بضعف حجمه.
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(true);
        settings.setSupportZoom(false);
        // المقياس نسبة مئوية لكل بكسل فعلي: 1920 بكسل CSS تملأ عرض الشاشة كله (100% على 1080p).
        webView.setInitialScale(fitScalePercent());
        webView.addJavascriptInterface(new Bridge(), "AndroidTV");
        root.addView(webView);
        setContentView(root);
        webView.loadUrl("file:///android_asset/tv/index.html");
        webView.requestFocus();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        String params = paramsOf(intent);
        if (!params.isEmpty()) {
            launchParams = params;
            evaluate("window.__onRelaunch(" + quoted(params) + ")");
        }
    }

    /** مقياس WebView (نسبة مئوية) الذي يلائم عرض الصفحة 1920 بكسل CSS مع العرض الفعلي للشاشة. */
    private int fitScalePercent() {
        DisplayMetrics metrics = new DisplayMetrics();
        getWindowManager().getDefaultDisplay().getRealMetrics(metrics);
        int width = Math.max(metrics.widthPixels, metrics.heightPixels);
        return width > 0 ? Math.max(1, Math.round(width * 100f / 1920f)) : 0;
    }

    private static String paramsOf(Intent intent) {
        String params = intent == null ? null : intent.getStringExtra("params");
        return params == null ? "" : params;
    }

    /** يبلّغ الصفحة بزر الرجوع وأزرار الألوان؛ بقية الأزرار تصل إلى WebView تلقائيًا. */
    @Override
    public boolean dispatchKeyEvent(KeyEvent event) {
        int code = pageKey(event.getKeyCode());
        if (code == 0) return super.dispatchKeyEvent(event);
        if (event.getAction() == KeyEvent.ACTION_DOWN) evaluate("window.__onKey(" + code + ")");
        return true;
    }

    private static int pageKey(int keyCode) {
        switch (keyCode) {
            case KeyEvent.KEYCODE_BACK: return 4;
            case KeyEvent.KEYCODE_PROG_RED: return 403;
            case KeyEvent.KEYCODE_PROG_GREEN: return 404;
            case KeyEvent.KEYCODE_PROG_BLUE: return 406;
            default: return 0;
        }
    }

    @Override
    protected void onDestroy() {
        if (tvView != null) tvView.reset();
        webView.destroy();
        super.onDestroy();
    }

    private void status(String kind) {
        evaluate("window.__onHdmiStatus(" + quoted(kind) + ")");
    }

    private void evaluate(String script) {
        runOnUiThread(() -> webView.evaluateJavascript(script, null));
    }

    private static String quoted(String text) {
        return "\"" + text.replace("\\", "\\\\").replace("\"", "\\\"") + "\"";
    }

    /** مداخل HDMI في التلفزيون مرتبة بحسب المعرّف. رقم المدخل هو ترتيبه في القائمة. */
    private List<TvInputInfo> hdmiInputs() {
        List<TvInputInfo> inputs = new ArrayList<>();
        TvInputManager manager = (TvInputManager) getSystemService(TV_INPUT_SERVICE);
        if (manager == null) return inputs;
        for (TvInputInfo input : manager.getTvInputList()) {
            if (input.getType() == TvInputInfo.TYPE_HDMI && input.isPassthroughInput()) inputs.add(input);
        }
        Collections.sort(inputs, Comparator.comparing(TvInputInfo::getId));
        return inputs;
    }

    private class Bridge {
        @JavascriptInterface
        public String launchParams() { return launchParams; }

        @JavascriptInterface
        public void showHdmi(int port) {
            runOnUiThread(() -> {
                List<TvInputInfo> inputs = hdmiInputs();
                if (tvView == null || port < 1 || port > inputs.size()) {
                    status("sourceError");
                    return;
                }
                String id = inputs.get(port - 1).getId();
                tvView.tune(id, TvContract.buildChannelUriForPassthroughInput(id));
            });
        }

        /** رقم مدخل HDMI الموصول إن كان واحدًا فقط، وإلا 0. */
        @JavascriptInterface
        public int consolePort() {
            TvInputManager manager = (TvInputManager) getSystemService(TV_INPUT_SERVICE);
            if (manager == null) return 0;
            List<TvInputInfo> inputs = hdmiInputs();
            int found = 0;
            for (int i = 0; i < inputs.size(); i++) {
                if (manager.getInputState(inputs.get(i).getId()) != TvInputManager.INPUT_STATE_CONNECTED) continue;
                if (found != 0) return 0;
                found = i + 1;
            }
            return found;
        }

        @JavascriptInterface
        public String localAddress() {
            try {
                for (NetworkInterface face : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                    if (!face.isUp() || face.isLoopback()) continue;
                    for (InetAddress address : Collections.list(face.getInetAddresses())) {
                        if (address instanceof Inet4Address && address.isSiteLocalAddress()) return address.getHostAddress();
                    }
                }
            } catch (Exception ignored) {
                return "";
            }
            return "";
        }

        @JavascriptInterface
        public void exit() { runOnUiThread(MainActivity.this::finish); }
    }
}
