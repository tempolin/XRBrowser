cmake_minimum_required(VERSION 3.22.1)
project(webview_gpu)

add_library(webview_gpu SHARED
        native-lib.cpp
        native_bridge_jni.cpp
)

target_link_libraries(webview_gpu
        log
)
