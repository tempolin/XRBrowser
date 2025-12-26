#include <jni.h>
#include <android/log.h>
#include <android/hardware_buffer.h>
#include <android/hardware_buffer_jni.h>   // ★これが必要

#define LOG_TAG "JAVA-SMOKE"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

/*
 * ===== Step1: JNI 導通確認 =====
 * JNI: int NativeBridge.nativeAdd(int a, int b)
 * Java package:
 *   com.example.webviewgpu.NativeBridge
 */
extern "C"
JNIEXPORT jint JNICALL
Java_com_example_webviewgpu_NativeBridge_nativeAdd(
        JNIEnv* /*env*/,
        jclass /*clazz*/,
        jint a,
        jint b)
{
    LOGI("nativeAdd called: a=%d, b=%d", a, b);
    jint result = a + b;
    LOGI("nativeAdd result=%d", result);
    return result;
}

/*
 * ===== CP3-2: HardwareBuffer を native で受け取る =====
 * JNI:
 *   int NativeBridge.nativeSubmitHardwareBuffer(
 *       HardwareBuffer hb, int w, int h, long ts)
 */

// 受け取った HardwareBuffer を保持（次の CP3-3 で使用）
static AHardwareBuffer* g_ahb = nullptr;
static int g_w = 0;
static int g_h = 0;
static long long g_ts = 0;

extern "C"
JNIEXPORT jint JNICALL
Java_com_example_webviewgpu_NativeBridge_nativeSubmitHardwareBuffer(
        JNIEnv* env,
        jclass /*clazz*/,
        jobject hardwareBufferObj,
        jint w,
        jint h,
        jlong ts)
{
    if (hardwareBufferObj == nullptr) {
        LOGE("nativeSubmitHardwareBuffer: hardwareBufferObj is null");
        return 0;
    }

    // 以前の HardwareBuffer を解放
    if (g_ahb) {
        AHardwareBuffer_release(g_ahb);
        g_ahb = nullptr;
    }

    // Java HardwareBuffer → AHardwareBuffer*
    AHardwareBuffer* ahb = AHardwareBuffer_fromHardwareBuffer(env, hardwareBufferObj);
    if (!ahb) {
        LOGE("AHardwareBuffer_fromHardwareBuffer FAILED");
        return 0;
    }

    // 参照カウント保持（native 側で使うため）
    AHardwareBuffer_acquire(ahb);
    g_ahb = ahb;
    g_w = (int)w;
    g_h = (int)h;
    g_ts = (long long)ts;

    // デバッグ情報
    AHardwareBuffer_Desc desc{};
    AHardwareBuffer_describe(g_ahb, &desc);

    LOGI(
            "submitHB OK ahb=%p w=%d h=%d ts=%lld format=%u usage=0x%llx",
            g_ahb,
            g_w,
            g_h,
            g_ts,
            desc.format,
            (unsigned long long)desc.usage
    );

    return 1; // 成功
}
