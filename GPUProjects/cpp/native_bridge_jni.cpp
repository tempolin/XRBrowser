#include <jni.h>
#include <android/log.h>

#define LOG_TAG "JAVA-SMOKE"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

/*
 * JNI: int NativeBridge.nativeAdd(int a, int b)
 * Java package:
 *   com.example.webviewgpu.NativeBridge
 *
 * JNI symbol name must EXACTLY match:
 *   Java_<package>_<Class>_<method>
 */
extern "C"
JNIEXPORT jint JNICALL
Java_com_example_webviewgpu_NativeBridge_nativeAdd(
        JNIEnv* env,
        jclass clazz,
        jint a,
        jint b)
{
    LOGI("nativeAdd called: a=%d, b=%d", a, b);
    jint result = a + b;
    LOGI("nativeAdd result=%d", result);
    return result;
}
