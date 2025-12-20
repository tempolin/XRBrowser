package com.example.comfirmcp1cp2;

import android.app.Presentation;
import android.content.Context;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Display;
import android.view.Gravity;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.util.Log;

public class DebugPresentation extends Presentation {

    private static final String TAG_I = "CP2_IMPORTANT";

    private Handler uiHandler;
    private Runnable tickTask;
    private int tick = 0;

    public DebugPresentation(Context outerContext, Display display) {
        super(outerContext, display);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout root = new LinearLayout(getContext());
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER);
        root.setPadding(40, 40, 40, 40);

        TextView title = new TextView(getContext());
        title.setText("CP2 Presentation (Stable)");
        title.setTextSize(24);

        TextView info = new TextView(getContext());
        info.setText("Updates every 1s to force frames.");
        info.setTextSize(18);

        TextView counter = new TextView(getContext());
        counter.setTextSize(22);
        counter.setText("tick: 0");

        root.addView(title);
        root.addView(info);
        root.addView(counter);
        setContentView(root);

        Log.i(TAG_I, "Presentation onCreate displayId=" + getDisplay().getDisplayId());

        uiHandler = new Handler(Looper.getMainLooper());
        tickTask = new Runnable() {
            @Override
            public void run() {
                tick++;
                counter.setText("tick: " + tick);

                // 5秒に1回だけログ（見える/動いてる確認）
                if (tick % 5 == 0) {
                    Log.i(TAG_I, "Presentation tick=" + tick);
                }

                uiHandler.postDelayed(this, 1000); // ★安定：1秒更新
            }
        };

        // すぐ開始
        uiHandler.post(tickTask);
    }

    @Override
    public void dismiss() {
        try {
            if (uiHandler != null && tickTask != null) {
                uiHandler.removeCallbacks(tickTask);
            }
        } catch (Throwable ignored) {}

        super.dismiss();
        Log.i(TAG_I, "Presentation dismiss()");
    }
}
