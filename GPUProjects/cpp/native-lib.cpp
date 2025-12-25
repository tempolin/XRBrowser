#include <jni.h>

extern "C" {

__attribute__((visibility("default")))
int GetMagicNumber() {
    return 12345;
}

// Java: com.example.webviewgpu.NativeBridge.nativeGetMagicNumber()
JNIEXPORT jint JNICALL
Java_com_example_webviewgpu_NativeBridge_nativeGetMagicNumber(JNIEnv* env, jclass clazz) {
    return (jint)GetMagicNumber();
}

} // extern "C"
