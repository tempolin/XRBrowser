package com.example.comfirmcp1cp2;

import android.graphics.ImageFormat;
import android.hardware.HardwareBuffer;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.view.Display;
import android.view.Surface;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

public class MainActivity extends AppCompatActivity {

    // Logcat検索用（重要ログだけ拾う）
    private static final String TAG_I = "CP2_IMPORTANT";

    private VirtualDisplay virtualDisplay;
    private ImageReader imageReader;
    private DebugPresentation presentation;

    private HandlerThread imageThread;
    private Handler imageHandler;

    private TextView tv;

    private static final int W = 512;
    private static final int H = 512;
    private static final int MAX_IMAGES = 3;

    private int frames = 0;
    private long lastTs = 0;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Log.i(TAG_I, "onCreate entered");

        tv = new TextView(this);
        tv.setTextSize(16);
        tv.setText("CP2 stable starting... (Logcat search: CP2_IMPORTANT)");
        setContentView(tv);

        startCP2Stable();
    }

    private void startCP2Stable() {
        try {
            int dpi = getResources().getDisplayMetrics().densityDpi;
            Log.i(TAG_I, "startCP2Stable begin dpi=" + dpi + " W=" + W + " H=" + H);

            imageThread = new HandlerThread("ImageReaderThread");
            imageThread.start();
            imageHandler = new Handler(imageThread.getLooper());
            Log.i(TAG_I, "HandlerThread started");

            imageReader = ImageReader.newInstance(
                    W, H,
                    ImageFormat.PRIVATE,
                    MAX_IMAGES
            );
            Surface surface = imageReader.getSurface();
            Log.i(TAG_I, "ImageReader(PRIVATE) created maxImages=" + MAX_IMAGES);

            DisplayManager dm = (DisplayManager) getSystemService(DISPLAY_SERVICE);

            // ★安定優先：余計なフラグを付けない（OWN_CONTENT_ONLYは付けない）
            // PUBLICは端末/OS実装でCAPTURE扱いを引く場合があるので、まずはPRESENTATIONのみで試す
            virtualDisplay = dm.createVirtualDisplay(
                    "CP2-VirtualDisplay",
                    W, H, dpi,
                    surface,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_PRESENTATION
            );

            Display vdDisplay = virtualDisplay.getDisplay();
            Log.i(TAG_I, "VirtualDisplay OK displayId=" + vdDisplay.getDisplayId());

            presentation = new DebugPresentation(this, vdDisplay);
            presentation.show();
            Log.i(TAG_I, "Presentation show()");

            imageReader.setOnImageAvailableListener(reader -> {
                Image img = null;
                try {
                    img = reader.acquireLatestImage();
                    if (img == null) return;

                    frames++;
                    long ts = img.getTimestamp();
                    lastTs = ts;

                    HardwareBuffer hb = img.getHardwareBuffer();
                    boolean hasHb = (hb != null);
                    if (hb != null) hb.close();

                    // 10フレームごとに重要ログ
                    if ((frames % 10) == 0) {
                        Log.i(TAG_I, "frame=" + frames + " hb=" + hasHb + " ts=" + ts);
                        runOnUiThread(() -> tv.setText(
                                "CP2 stable running\n" +
                                        "W,H=" + W + "," + H + "\n" +
                                        "frames=" + frames + "\n" +
                                        "lastTs=" + lastTs + "\n" +
                                        "hb(last)=" + hasHb + "\n" +
                                        "Logcat search: CP2_IMPORTANT"
                        ));
                    }

                } catch (Throwable t) {
                    Log.e(TAG_I, "onImageAvailable error", t);
                } finally {
                    if (img != null) img.close();
                }
            }, imageHandler);

            tv.setText("CP2 stable running... (Logcat search: CP2_IMPORTANT)");
            Log.i(TAG_I, "Listener set. Waiting frames...");

        } catch (Throwable t) {
            Log.e(TAG_I, "startCP2Stable failed", t);
            tv.setText("CP2 stable failed:\n" + t);
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();

        Log.i(TAG_I, "onDestroy begin");

        if (presentation != null) {
            try { presentation.dismiss(); } catch (Throwable ignored) {}
            presentation = null;
        }
        if (virtualDisplay != null) {
            try { virtualDisplay.release(); } catch (Throwable ignored) {}
            virtualDisplay = null;
        }
        if (imageReader != null) {
            try { imageReader.close(); } catch (Throwable ignored) {}
            imageReader = null;
        }
        if (imageThread != null) {
            imageThread.quitSafely();
            imageThread = null;
        }

        Log.i(TAG_I, "onDestroy end");
    }
}
