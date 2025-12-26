cmake_minimum_required(VERSION 3.18)
project(webview_gpu)

add_library(webview_gpu SHARED
        native_bridge_jni.cpp
        native-lib.cpp

        DummyTexPlugin.cpp
)

target_include_directories(webview_gpu PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/UnityPluginAPI
)

find_library(log-lib log)
find_library(android-lib android)
find_library(glesv3-lib GLESv3)
# 将来EGL使うなら（今は不要でもOK）
find_library(egl-lib EGL)

target_link_libraries(webview_gpu
        ${log-lib}
        ${android-lib}
        ${glesv3-lib}
        ${egl-lib}
)
