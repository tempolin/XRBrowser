package com.example.webviewgpu;

import android.util.Log;

public class NativeBridge {
    static {
        Log.i("JAVA-SMOKE", "NativeBridge <clinit> reached");
        try {
            System.loadLibrary("webview_gpu");
            Log.i("JAVA-SMOKE", "System.loadLibrary(webview_gpu) OK");
        } catch (Throwable t) {
            Log.e("JAVA-SMOKE", "System.loadLibrary FAILED", t);
        }
    }

    public static int getMagicNumber() { return 12345; }

    // ★Step1: JNIで実装する関数
    public static native int nativeAdd(int a, int b);
}
