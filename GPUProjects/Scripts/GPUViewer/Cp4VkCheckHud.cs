// Cp4VkCheckHud.cs
// Vulkan-check HUD (NO logcat).
// Purpose:
//  - Vulkan onlyでも .so がロードされる
//  - GL.IssuePluginEvent が呼べる
//  - native側 OnRenderEvent が回っている（= VkCheck_* counters が増える）
//
// Native side (DummyTexPlugin.cpp) must export:
//   IntPtr GetRenderEventFunc();
//   int VkCheck_GetLastEventId();
//   int VkCheck_GetInitCount();
//   int VkCheck_GetTickCount();
//   int VkCheck_GetLastErrorCode();
//
// Unity sends eventId:
//   1001 = init
//   1002 = tick

using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class Cp4VkCheckHud : MonoBehaviour
{
    [Header("On-screen debug text (TextMeshProUGUI)")]
    public TextMeshProUGUI debugText;

    [Header("HUD refresh interval (frames)")]
    [Range(1, 120)]
    public int uiUpdateIntervalFrames = 10;

    [Header("If true, writes some Unity logs too")]
    public bool verboseUnityLog = false;

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string DLL = "webview_gpu"; // libwebview_gpu.so -> "webview_gpu"
#else
    private const string DLL = "webview_gpu";
#endif

    // Unity render-thread event entry
    [DllImport(DLL)] private static extern IntPtr GetRenderEventFunc();

    // Native counters (from DummyTexPlugin.cpp)
    [DllImport(DLL)] private static extern int VkCheck_GetLastEventId();
    [DllImport(DLL)] private static extern int VkCheck_GetInitCount();
    [DllImport(DLL)] private static extern int VkCheck_GetTickCount();
    [DllImport(DLL)] private static extern int VkCheck_GetLastErrorCode();

    private const int EVT_INIT = 1001;
    private const int EVT_TICK = 1002;

    private bool _libOk = false;
    private bool _issuedInit = false;
    private string _lastErr = "";

    private int _lastUiFrame = -9999;

    void Start()
    {
        // 1) Try touch native: GetRenderEventFunc()
        try
        {
            IntPtr p = GetRenderEventFunc();
            _libOk = (p != IntPtr.Zero);
            if (!_libOk) _lastErr = "GetRenderEventFunc returned NULL";
            if (verboseUnityLog) Debug.Log($"[VKCHK] GetRenderEventFunc ptr=0x{p.ToInt64():X} libOk={_libOk}");
        }
        catch (Exception e)
        {
            _libOk = false;
            _lastErr = "DllImport/load failed: " + e.GetType().Name;
            if (verboseUnityLog) Debug.LogError("[VKCHK] " + _lastErr);
        }

        // 2) Issue init once
        if (_libOk)
        {
            try
            {
                GL.IssuePluginEvent(GetRenderEventFunc(), EVT_INIT);
                _issuedInit = true;
                if (verboseUnityLog) Debug.Log("[VKCHK] Issued init event (1001)");
            }
            catch (Exception e)
            {
                _issuedInit = false;
                _lastErr = "IssuePluginEvent(init) failed: " + e.GetType().Name;
                if (verboseUnityLog) Debug.LogError("[VKCHK] " + _lastErr);
            }
        }

        UpdateHud(force: true);
    }

    void Update()
    {
        // Tick every frame
        if (_libOk)
        {
            try
            {
                GL.IssuePluginEvent(GetRenderEventFunc(), EVT_TICK);
            }
            catch (Exception e)
            {
                _lastErr = "IssuePluginEvent(tick) failed: " + e.GetType().Name;
                if (verboseUnityLog) Debug.LogError("[VKCHK] " + _lastErr);
                enabled = false;
                return;
            }
        }

        UpdateHud(force: false);
    }

    private void UpdateHud(bool force)
    {
        if (!debugText) return;

        if (!force && (Time.frameCount - _lastUiFrame) < uiUpdateIntervalFrames)
            return;

        _lastUiFrame = Time.frameCount;

        var api = SystemInfo.graphicsDeviceType;
        var pipe = GraphicsSettings.currentRenderPipeline
            ? GraphicsSettings.currentRenderPipeline.GetType().Name
            : "Built-in";

        int lastEvent = -1;
        int initCnt = -1;
        int tickCnt = -1;
        int errCode = -999;

        if (_libOk)
        {
            try
            {
                lastEvent = VkCheck_GetLastEventId();
                initCnt = VkCheck_GetInitCount();
                tickCnt = VkCheck_GetTickCount();
                errCode = VkCheck_GetLastErrorCode();
            }
            catch (Exception e)
            {
                _lastErr = "Reading VkCheck_* failed: " + e.GetType().Name;
            }
        }

        debugText.text =
            $"VK CHECK HUD (NO logcat)\n" +
            $"frame={Time.frameCount}\n" +
            $"gfx={api}  pipeline={pipe}\n" +
            $"libOk={_libOk}  issuedInit={_issuedInit}\n" +
            $"native: lastEvent={lastEvent} initCnt={initCnt} tickCnt={tickCnt}\n" +
            $"nativeErrCode={errCode}\n" +
            $"lastErr={_lastErr}\n" +
            $"NOTE: Build Vulkan-only. This HUD is PASS if tickCnt increases.";
    }
}
