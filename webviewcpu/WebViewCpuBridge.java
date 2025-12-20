package webviewcpu;

import android.app.Activity;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.webkit.ValueCallback;
import android.webkit.WebSettings;
import android.webkit.WebView;

import org.json.JSONObject;

public class WebViewCpuBridge {
    private final int width, height;
    private WebView webView;
    private Bitmap bitmap;
    private Canvas canvas;
    private int[] pixelBuf;
    private byte[] rgbaBuf;

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

            // draw安定化（CPU）
            webView.setLayerType(WebView.LAYER_TYPE_SOFTWARE, null);

            webView.layout(0, 0, width, height);

            bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            canvas = new Canvas(bitmap);
            pixelBuf = new int[width * height];
            rgbaBuf = new byte[width * height * 4];

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

    // (x,y) は WebView ピクセル座標（左上原点）
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

    // 互換のため残す（Unity側が呼ぶならこれでもOK）
    public void keyEnter() {
        key(KeyEvent.KEYCODE_ENTER);
    }

    // 端末差が出るので「おまけ」
    public void text(String s) {
        if (!ready || webView == null)
            return;

        mainHandler.post(() -> {
            String escaped = s
                    .replace("\\", "\\\\")
                    .replace("'", "\\'")
                    .replace("\n", "\\n")
                    .replace("\r", "");

            String js = "(function(){"
                    + "var el=document.activeElement;"
                    + "if(!el) return;"
                    + "if('value' in el){"
                    + "  el.value=(el.value||'')+'" + escaped + "';"
                    + "  el.dispatchEvent(new Event('input',{bubbles:true}));"
                    + "  el.dispatchEvent(new Event('change',{bubbles:true}));"
                    + "}"
                    + "})();";

            webView.evaluateJavascript(js, null);
        });
    }

    // 安定：フォーカス中の input/textarea に値を流し込む（全文同期）
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
    // JS helpers (VRキーボード向け)
    // -------------------------

    // 1文字/文字列を「カーソル位置に挿入」
    public void insertText(String text) {
        if (!ready || webView == null)
            return;

        final String quoted = JSONObject.quote(text);
        final String js = "(function(){"
                + "var e=document.activeElement;"
                + "if(!e) return 'no_active';"
                + "var tag=(e.tagName||'').toUpperCase();"
                + "if(tag!=='INPUT' && tag!=='TEXTAREA' && !e.isContentEditable) return 'not_editable';"
                + "var v=('value' in e)?(e.value||''):(e.textContent||'');"
                + "var s=(e.selectionStart!=null)?e.selectionStart:v.length;"
                + "var t=(e.selectionEnd!=null)?e.selectionEnd:v.length;"
                + "var ins=" + quoted + ";"
                + "var nv=v.substring(0,s)+ins+v.substring(t);"
                + "if('value' in e) e.value=nv; else e.textContent=nv;"
                + "var p=s+ins.length;"
                + "if(e.setSelectionRange) e.setSelectionRange(p,p);"
                + "e.dispatchEvent(new Event('input',{bubbles:true}));"
                + "return 'ok';"
                + "})();";

        mainHandler.post(() -> webView.evaluateJavascript(js, null));
    }

    // Backspace（カーソルの左1文字 or 選択範囲削除）
    public void backspace() {
        if (!ready || webView == null)
            return;

        final String js = "(function(){"
                + "var e=document.activeElement;"
                + "if(!e) return 'no_active';"
                + "var tag=(e.tagName||'').toUpperCase();"
                + "if(tag!=='INPUT' && tag!=='TEXTAREA' && !e.isContentEditable) return 'not_editable';"
                + "var v=('value' in e)?(e.value||''):(e.textContent||'');"
                + "var s=(e.selectionStart!=null)?e.selectionStart:v.length;"
                + "var t=(e.selectionEnd!=null)?e.selectionEnd:v.length;"
                + "if(s===t && s>0){ s=s-1; }"
                + "var nv=v.substring(0,s)+v.substring(t);"
                + "if('value' in e) e.value=nv; else e.textContent=nv;"
                + "if(e.setSelectionRange) e.setSelectionRange(s,s);"
                + "e.dispatchEvent(new Event('input',{bubbles:true}));"
                + "return 'ok';"
                + "})();";

        mainHandler.post(() -> webView.evaluateJavascript(js, null));
    }

    // Enter（textareaは改行、inputはsubmit試行）
    public void enter() {
        if (!ready || webView == null)
            return;

        final String js = "(function(){"
                + "var e=document.activeElement;"
                + "if(!e) return 'no_active';"
                + "var tag=(e.tagName||'').toUpperCase();"
                + "if(tag==='TEXTAREA'){"
                + "  var v=e.value||'';"
                + "  var s=(e.selectionStart!=null)?e.selectionStart:v.length;"
                + "  var t=(e.selectionEnd!=null)?e.selectionEnd:v.length;"
                + "  var nv=v.substring(0,s)+'\\n'+v.substring(t);"
                + "  e.value=nv;"
                + "  var p=s+1;"
                + "  e.setSelectionRange(p,p);"
                + "  e.dispatchEvent(new Event('input',{bubbles:true}));"
                + "  return 'ok_textarea';"
                + "}"
                + "if(e.form){"
                + "  try{ e.form.requestSubmit(); return 'ok_submit'; }catch(_){"
                + "    try{ e.form.submit(); return 'ok_submit2'; }catch(__){}"
                + "  }"
                + "}"
                + "e.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));"
                + "e.dispatchEvent(new KeyboardEvent('keyup',{key:'Enter',bubbles:true}));"
                + "return 'ok_event';"
                + "})();";

        mainHandler.post(() -> webView.evaluateJavascript(js, null));
    }

    // フォーカス中要素の情報を JSON で返す（Unityが「今入力中？」判定に使う）
    public String getFocusedInfoJson() {
        if (!ready || webView == null)
            return null;

        final Object lock = new Object();
        final String[] result = { null };
        final boolean[] done = { false };

        final String js = "(function(){"
                + "var e=document.activeElement;"
                + "if(!e) return JSON.stringify({hasFocus:false});"
                + "var tag=(e.tagName||'').toLowerCase();"
                + "var editable=(tag==='input'||tag==='textarea'||e.isContentEditable);"
                + "var v=('value' in e)?(e.value||''):(e.textContent||'');"
                + "var s=(e.selectionStart!=null)?e.selectionStart:null;"
                + "var t=(e.selectionEnd!=null)?e.selectionEnd:null;"
                + "var r=(e.getBoundingClientRect)?e.getBoundingClientRect():null;"
                + "return JSON.stringify({"
                + "hasFocus:true,editable:editable,tag:tag,type:(e.type||null),"
                + "value:v,selStart:s,selEnd:t,"
                + "rect:r?{x:r.x,y:r.y,w:r.width,h:r.height}:null"
                + "});"
                + "})();";

        mainHandler.post(() -> webView.evaluateJavascript(js, new ValueCallback<String>() {
            @Override
            public void onReceiveValue(String value) {
                // value は JSON文字列が "..." で返ることがある（そのまま返す）
                result[0] = value;
                synchronized (lock) {
                    done[0] = true;
                    lock.notifyAll();
                }
            }
        }));

        synchronized (lock) {
            try {
                if (!done[0])
                    lock.wait(80);
            } catch (InterruptedException ignored) {
            }
        }
        return result[0];
    }

    // -------------------------
    // Capture
    // -------------------------

    public byte[] captureRgba() {
        if (!ready || webView == null)
            return null;

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

        bitmap.getPixels(pixelBuf, 0, width, 0, 0, width, height);

        for (int i = 0; i < pixelBuf.length; i++) {
            int c = pixelBuf[i]; // ARGB
            int o = i * 4;
            rgbaBuf[o] = (byte) ((c >> 16) & 0xFF);
            rgbaBuf[o + 1] = (byte) ((c >> 8) & 0xFF);
            rgbaBuf[o + 2] = (byte) (c & 0xFF);
            rgbaBuf[o + 3] = (byte) ((c >> 24) & 0xFF);
        }
        return rgbaBuf;
    }

    // -------------------------
    // Capture (P0 fix)
    // -------------------------
    public boolean captureInto(byte[] outRgba) {
        if (!ready || webView == null)
            return false;
        if (outRgba == null || outRgba.length < width * height * 4)
            return false;

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

        bitmap.getPixels(pixelBuf, 0, width, 0, 0, width, height);

        // Unity側の配列に直接書き込む（JNI側での毎フレームnewを回避）
        for (int i = 0; i < pixelBuf.length; i++) {
            int c = pixelBuf[i]; // ARGB
            int o = i * 4;
            outRgba[o] = (byte) ((c >> 16) & 0xFF);
            outRgba[o + 1] = (byte) ((c >> 8) & 0xFF);
            outRgba[o + 2] = (byte) (c & 0xFF);
            outRgba[o + 3] = (byte) ((c >> 24) & 0xFF);
        }
        return true;
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
