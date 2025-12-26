//
// Created by timpo on 2025/12/25.
//
// DummyTexPlugin.cpp (Android / OpenGL ES)
// CP4: RenderThread上で GL_TEXTURE_2D を生成・更新して Unity に渡す

#include <GLES3/gl3.h>
#include <android/log.h>
#include <stdint.h>
#include <string.h>

#include "IUnityInterface.h"
#include "IUnityGraphics.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO,  "CP4_NATIVE", __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "CP4_NATIVE", __VA_ARGS__)

static IUnityInterfaces* s_unity = nullptr;
static IUnityGraphics*   s_gfx   = nullptr;

static GLuint  s_texId = 0;
static int     s_w = 512;
static int     s_h = 512;
static int     s_frame = 0;

static void CreateOrRecreateTextureIfNeeded() {
    if (s_texId != 0) return;

    glGenTextures(1, &s_texId);
    glBindTexture(GL_TEXTURE_2D, s_texId);

    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

    // 初期データ（単色でもいいが、変化が分かるようにグラデ）
    const int W = s_w, H = s_h;
    const int N = W * H * 4;
    uint8_t* buf = new uint8_t[N];
    for (int y = 0; y < H; y++) {
        for (int x = 0; x < W; x++) {
            int i = (y * W + x) * 4;
            buf[i + 0] = (uint8_t)(x * 255 / (W - 1)); // R
            buf[i + 1] = (uint8_t)(y * 255 / (H - 1)); // G
            buf[i + 2] = 32;                           // B
            buf[i + 3] = 255;                          // A
        }
    }

    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, W, H, 0, GL_RGBA, GL_UNSIGNED_BYTE, buf);
    delete[] buf;

    GLenum err = glGetError();
    if (err != GL_NO_ERROR) {
        LOGE("Create texture failed: glGetError=0x%x", err);
    } else {
        LOGI("Texture created OK: texId=%u size=%dx%d", s_texId, s_w, s_h);
    }
}

static void UpdateTexture() {
    if (s_texId == 0) return;
    glBindTexture(GL_TEXTURE_2D, s_texId);

    // 目立つように色を毎フレーム変える（中央の小さな矩形だけ更新）
    const int rectW = 256, rectH = 256;
    const int N = rectW * rectH * 4;
    uint8_t* buf = new uint8_t[N];

    uint8_t r = (uint8_t)((s_frame * 3) & 255);
    uint8_t g = (uint8_t)((s_frame * 7) & 255);
    uint8_t b = (uint8_t)((s_frame * 11) & 255);

    for (int i = 0; i < rectW * rectH; i++) {
        buf[i * 4 + 0] = r;
        buf[i * 4 + 1] = g;
        buf[i * 4 + 2] = b;
        buf[i * 4 + 3] = 255;
    }

    int x0 = (s_w - rectW) / 2;
    int y0 = (s_h - rectH) / 2;
    glTexSubImage2D(GL_TEXTURE_2D, 0, x0, y0, rectW, rectH, GL_RGBA, GL_UNSIGNED_BYTE, buf);
    delete[] buf;

    s_frame++;
}

// ---- RenderThreadで呼ばれるコールバック ----
static void UNITY_INTERFACE_API OnRenderEvent(int eventId) {
    // eventId: 1=init, 2=update
    if (eventId == 1) {
        CreateOrRecreateTextureIfNeeded();
    } else if (eventId == 2) {
        CreateOrRecreateTextureIfNeeded();
        UpdateTexture();
    }
}

// Unity C# から GL.IssuePluginEvent に渡す関数ポインタ
extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc() {
    return OnRenderEvent;
}

// texIdを返す（CreateExternalTexture で使う）
extern "C" uintptr_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetTextureId() {
    return (uintptr_t)s_texId;
}

// サイズ変更したい場合（任意）
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API SetTextureSize(int w, int h) {
    s_w = w; s_h = h;
    // 次のinitで作り直す
    if (s_texId != 0) {
        GLuint t = s_texId;
        s_texId = 0;
        glDeleteTextures(1, &t);
        LOGI("Texture deleted for resize. next init will recreate.");
    }
}

// Unityプラグイン標準
static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType) {
if (eventType == kUnityGfxDeviceEventInitialize) {
LOGI("Gfx Initialize");
} else if (eventType == kUnityGfxDeviceEventShutdown) {
LOGI("Gfx Shutdown");
if (s_texId != 0) {
glDeleteTextures(1, &s_texId);
s_texId = 0;
}
}
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces) {
    s_unity = unityInterfaces;
    s_gfx = s_unity->Get<IUnityGraphics>();
    s_gfx->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    LOGI("UnityPluginLoad done");
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload() {
    if (s_gfx) s_gfx->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    s_unity = nullptr;
    s_gfx = nullptr;
    LOGI("UnityPluginUnload");
}
