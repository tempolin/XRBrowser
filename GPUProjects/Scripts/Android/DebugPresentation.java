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
        title.setText("CP2.5 Presentation (PRIVATE)");
        title.setTextSize(24);

        TextView info = new TextView(getContext());
        info.setText("Updates ~60fps to force frames.");
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

                // たまに重要ログ（出しすぎ防止）
                if (tick % 600 == 0) { // 60fpsなら約10秒ごと
                    Log.i(TAG_I, "Presentation tick=" + tick);
                }

                uiHandler.postDelayed(this, 16); // 約60fps
            }
        };

        // 最初から回す（初動で迷子になりにくい）
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
