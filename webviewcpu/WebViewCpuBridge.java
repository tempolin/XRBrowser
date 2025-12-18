package webviewcpu;

import android.app.Activity;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.webkit.WebSettings;
import android.webkit.WebView;

import org.json.JSONObject;

public class WebViewCpuBridge {
    private final int width, height;
    private WebView webView;
    private Bitmap bitmap;
    private Canvas canvas;

    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private volatile boolean ready = false;

    public WebViewCpuBridge(Activity activity, int width, int height) {
        this.width = width;
        this.height = height;

        mainHandler.post(() -> {
            webView = new WebView(activity);
            webView.setWillNotDraw(false);

            WebSettings s = webView.getSettings();
            s.setJavaScriptEnabled(true);
            s.setDomStorageEnabled(true);

            // これがないと draw が安定しないことがある
            webView.setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);

            webView.layout(0, 0, width, height);

            bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            canvas = new Canvas(bitmap);

            ready = true;
        });
    }

    public void loadUrl(String url) {
        mainHandler.post(() -> {
            if (!ready || webView == null)
                return;
            webView.loadUrl(url);
        });
    }

    // -------------------------
    // Input: Tap / Key / Text
    // -------------------------

    // (x,y) は WebView ピクセル座標（左上原点, 0..width / 0..height）
    public void tap(float x, float y) {
        if (!ready || webView == null)
            return;

        mainHandler.post(() -> {
            long now = SystemClock.uptimeMillis();
            MotionEvent down = MotionEvent.obtain(now, now, MotionEvent.ACTION_DOWN, x, y, 0);
            MotionEvent up = MotionEvent.obtain(now, now + 10, MotionEvent.ACTION_UP, x, y, 0);
            webView.dispatchTouchEvent(down);
            webView.dispatchTouchEvent(up);
            down.recycle();
            up.recycle();
        });
    }

    public void key(int keyCode) {
        if (!ready || webView == null)
            return;

        mainHandler.post(() -> {
            webView.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_DOWN, keyCode));
            webView.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_UP, keyCode));
        });
    }

    public void keyEnter() {
        key(KeyEvent.KEYCODE_ENTER);
    }

    // 端末差が出るので「おまけ」。基本は setFocusedInputValue 推奨
    public void text(String s) {
        if (!ready || webView == null)
            return;

        mainHandler.post(() -> {
            for (int i = 0; i < s.length(); i++) {
                KeyEvent[] events = KeyEvent.getEvents(String.valueOf(s.charAt(i)));
                if (events != null) {
                    for (KeyEvent e : events)
                        webView.dispatchKeyEvent(e);
                }
            }
        });
    }

    // 安定：フォーカス中の input/textarea に値を流し込む
    public void setFocusedInputValue(String value) {
        if (!ready || webView == null)
            return;

        final String quoted = JSONObject.quote(value);
        final String js = "(function(){"
                + "var e=document.activeElement;"
                + "if(e && ('value' in e)){"
                + "  e.value=" + quoted + ";"
                + "  e.dispatchEvent(new Event('input',{bubbles:true}));"
                + "}"
                + "})();";

        mainHandler.post(() -> webView.evaluateJavascript(js, null));
    }

    // -------------------------
    // Capture
    // -------------------------

    public byte[] captureRgba() {
        if (!ready || webView == null)
            return null;

        // UIスレッドで draw して同期する
        final Object lock = new Object();
        final boolean[] done = { false };

        mainHandler.post(() -> {
            try {
                webView.draw(canvas);
            } catch (Exception ignored) {
            }
            synchronized (lock) {
                done[0] = true;
                lock.notifyAll();
            }
        });

        synchronized (lock) {
            try {
                if (!done[0])
                    lock.wait(50);
            } catch (InterruptedException ignored) {
            }
        }

        int[] pixels = new int[width * height];
        bitmap.getPixels(pixels, 0, width, 0, 0, width, height);

        byte[] out = new byte[width * height * 4];
        for (int i = 0; i < pixels.length; i++) {
            int c = pixels[i]; // ARGB
            int a = (c >> 24) & 0xFF;
            int r = (c >> 16) & 0xFF;
            int g = (c >> 8) & 0xFF;
            int b = (c) & 0xFF;

            int o = i * 4;
            out[o] = (byte) r;
            out[o + 1] = (byte) g;
            out[o + 2] = (byte) b;
            out[o + 3] = (byte) a;
        }
        return out;
    }

    // -------------------------
    // Cleanup
    // -------------------------

    public void dispose() {
        mainHandler.post(() -> {
            if (webView != null) {
                webView.destroy();
                webView = null;
            }
            bitmap = null;
            canvas = null;
            ready = false;
        });
    }
}
