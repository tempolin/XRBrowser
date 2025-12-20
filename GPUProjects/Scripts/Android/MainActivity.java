package com.example.comfirmcp1cp2;

import android.media.Image;
import android.media.ImageReader;
import android.graphics.ImageFormat;
import android.hardware.HardwareBuffer;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;

import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;

import android.util.Log;
import android.view.Display;
import android.view.Surface;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

public class MainActivity extends AppCompatActivity {

    // 重要ログ（Logcat検索欄にこれを入れる）
    private static final String TAG_I = "CP2_IMPORTANT";
    // 詳細ログ（必要なときだけ見る）
    private static final String TAG_D = "CP2";

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
        tv.setText("CP2 PRIVATE starting... (Logcat search: CP2_IMPORTANT)");
        setContentView(tv);

        startCP2Private();
    }

    private void startCP2Private() {
        try {
            int dpi = getResources().getDisplayMetrics().densityDpi;
            Log.i(TAG_I, "startCP2Private begin dpi=" + dpi + " W=" + W + " H=" + H);

            imageThread = new HandlerThread("ImageReaderThread");
            imageThread.start();
            imageHandler = new Handler(imageThread.getLooper());
            Log.i(TAG_I, "HandlerThread started");

            // PRIVATE
            imageReader = ImageReader.newInstance(
                    W, H,
                    ImageFormat.PRIVATE,
                    MAX_IMAGES
            );
            Surface surface = imageReader.getSurface();
            Log.i(TAG_I, "ImageReader(PRIVATE) created maxImages=" + MAX_IMAGES);

            DisplayManager dm = (DisplayManager) getSystemService(DISPLAY_SERVICE);
            virtualDisplay = dm.createVirtualDisplay(
                    "CP2-VirtualDisplay",
                    W, H, dpi,
                    surface,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_PRESENTATION
                            | DisplayManager.VIRTUAL_DISPLAY_FLAG_PUBLIC
                            | DisplayManager.VIRTUAL_DISPLAY_FLAG_OWN_CONTENT_ONLY
            );

            Display vdDisplay = virtualDisplay.getDisplay();
            Log.i(TAG_I, "VirtualDisplay OK displayId=" + vdDisplay.getDisplayId());

            // VirtualDisplay上に固定UIを描かせる
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

                    HardwareBuffer hb = img.getHardwareBuffer();
                    boolean hasHb = (hb != null);
                    if (hb != null) hb.close();

                    // 重要ログ：10フレームごと（出しすぎ防止）
                    if ((frames % 10) == 0) {
                        lastTs = ts;
                        Log.i(TAG_I, "frame=" + frames + " hb=" + hasHb + " ts=" + ts);

                        runOnUiThread(() -> tv.setText(
                                "CP2 PRIVATE running\n" +
                                        "W,H=" + W + "," + H + "\n" +
                                        "frames=" + frames + "\n" +
                                        "lastTs=" + lastTs + "\n" +
                                        "hb=" + hasHb + "\n" +
                                        "Logcat search: CP2_IMPORTANT"
                        ));
                    } else {
                        // 詳細ログ（必要なら）
                        // Log.d(TAG_D, "frame=" + frames + " hb=" + hasHb);
                    }

                } catch (Throwable t) {
                    Log.e(TAG_I, "onImageAvailable error", t);
                } finally {
                    if (img != null) img.close();
                }
            }, imageHandler);

            tv.setText("CP2 PRIVATE running... (Logcat search: CP2_IMPORTANT)");
            Log.i(TAG_I, "Listener set. Waiting frames...");

        } catch (Throwable t) {
            Log.e(TAG_I, "startCP2Private failed", t);
            tv.setText("CP2 PRIVATE failed:\n" + t);
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
