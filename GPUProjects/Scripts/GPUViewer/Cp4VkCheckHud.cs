// Cp4VkCheckHud.cs
// Unity6 + URP + Vulkan / GLES 両対応の「生存確認HUD」
// 目的：IssuePluginEvent が回っているか、Native側カウンタが増えるかだけを確認する
// 注意：この段階では CreateExternalTexture をしない（VulkanでVkImage*を渡すとUnityが落ちやすい）

using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class Cp4VkCheckHud : MonoBehaviour
{
    [Header("On-screen debug text (TextMeshProUGUI)")]
    public TextMeshProUGUI debugText;

    [Header("Update HUD every N frames")]
    [Range(1, 120)]
    public int uiUpdateIntervalFrames = 5;

    [Header("Verbose Unity Debug.Log")]
    public bool verboseUnityLog = true;

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string DLL = "webview_gpu";
#else
    private const string DLL = "webview_gpu";
#endif

    // ===== Native exports (Vulkan) =====
    [DllImport(DLL)] private static extern IntPtr GetRenderEventFuncVk();
    [DllImport(DLL)] private static extern int VkCheckVk_GetLastEventId();
    [DllImport(DLL)] private static extern int VkCheckVk_GetInitCount();
    [DllImport(DLL)] private static extern int VkCheckVk_GetTickCount();
    [DllImport(DLL)] private static extern int VkCheckVk_GetLastErrorCode();

    // ===== Native exports (GLES) =====
    // もしGL側にも同様の関数があるなら差し替えて使ってください。
    // 無いなら、このGLESブロックは消してOKです。
    [DllImport(DLL)] private static extern IntPtr GetRenderEventFuncGl();
    [DllImport(DLL)] private static extern int VkCheckGl_GetLastEventId();
    [DllImport(DLL)] private static extern int VkCheckGl_GetInitCount();
    [DllImport(DLL)] private static extern int VkCheckGl_GetTickCount();
    [DllImport(DLL)] private static extern int VkCheckGl_GetLastErrorCode();

    // Plugin event IDs（Native側と一致させる）
    // Vulkan
    private const int EVT_VK_INIT = 2001;
    private const int EVT_VK_TICK = 2002;
    // GLES（番号が違うなら合わせてください）
    private const int EVT_GL_INIT = 1001;
    private const int EVT_GL_TICK = 1002;

    private IntPtr _fn = IntPtr.Zero;
    private GraphicsDeviceType _api;
    private bool _issuedInit = false;
    private int _lastUiFrame = -9999;

    void Start()
    {
        _api = SystemInfo.graphicsDeviceType;

        // どのAPIで動いているかによって、RenderEventFunc を選ぶ
        try
        {
            if (_api == GraphicsDeviceType.Vulkan)
            {
                _fn = GetRenderEventFuncVk();
                if (_fn == IntPtr.Zero)
                {
                    Fail("GetRenderEventFuncVk returned NULL.");
                    enabled = false;
                    return;
                }
                GL.IssuePluginEvent(_fn, EVT_VK_INIT);
                _issuedInit = true;
                Info($"IssuePluginEvent VK INIT ({EVT_VK_INIT})");
            }
            else if (_api == GraphicsDeviceType.OpenGLES3)
            {
                _fn = GetRenderEventFuncGl();
                if (_fn == IntPtr.Zero)
                {
                    Fail("GetRenderEventFuncGl returned NULL.");
                    enabled = false;
                    return;
                }
                GL.IssuePluginEvent(_fn, EVT_GL_INIT);
                _issuedInit = true;
                Info($"IssuePluginEvent GL INIT ({EVT_GL_INIT})");
            }
            else
            {
                Warn($"Unsupported GraphicsDeviceType={_api}. (Expect Vulkan or OpenGLES3)");
                // ここでは落とさずHUDで表示だけする
            }
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEvent INIT failed: {e}");
            enabled = false;
            return;
        }

        UpdateHud(force: true);
    }

    void Update()
    {
        // 毎フレームTICK（ただし対応APIのときだけ）
        try
        {
            if (_fn != IntPtr.Zero)
            {
                if (_api == GraphicsDeviceType.Vulkan)
                    GL.IssuePluginEvent(_fn, EVT_VK_TICK);
                else if (_api == GraphicsDeviceType.OpenGLES3)
                    GL.IssuePluginEvent(_fn, EVT_GL_TICK);
            }
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEvent TICK failed: {e}");
            enabled = false;
            return;
        }

        UpdateHud(force: false);
    }

    private void UpdateHud(bool force)
    {
        if (!debugText) return;
        if (!force && (Time.frameCount - _lastUiFrame) < uiUpdateIntervalFrames) return;
        _lastUiFrame = Time.frameCount;

        int lastEvent, initCount, tickCount, errCode;

        if (_api == GraphicsDeviceType.Vulkan)
        {
            lastEvent = SafeGet(VkCheckVk_GetLastEventId);
            initCount = SafeGet(VkCheckVk_GetInitCount);
            tickCount = SafeGet(VkCheckVk_GetTickCount);
            errCode = SafeGet(VkCheckVk_GetLastErrorCode);
        }
        else if (_api == GraphicsDeviceType.OpenGLES3)
        {
            lastEvent = SafeGet(VkCheckGl_GetLastEventId);
            initCount = SafeGet(VkCheckGl_GetInitCount);
            tickCount = SafeGet(VkCheckGl_GetTickCount);
            errCode = SafeGet(VkCheckGl_GetLastErrorCode);
        }
        else
        {
            lastEvent = initCount = tickCount = errCode = -9999;
        }

        bool pass =
            (_api == GraphicsDeviceType.Vulkan || _api == GraphicsDeviceType.OpenGLES3) &&
            _issuedInit &&
            initCount > 0 &&
            tickCount > 0 &&
            errCode == 0;

        debugText.text =
            $"CP4 VkCheck HUD [{(pass ? "PASS" : "CHECK")}]\n" +
            $"frame={Time.frameCount}\n" +
            $"gfx={_api}\n" +
            $"issuedInit={_issuedInit}\n" +
            $"initCount={initCount}  tickCount={tickCount}\n" +
            $"lastEventId={lastEvent}  lastErrorCode={errCode}\n" +
            $"fnPtr={_fn}\n" +
            $"NOTE: no CreateExternalTexture in this phase";
    }

    private int SafeGet(Func<int> f)
    {
        try { return f(); }
        catch { return -9999; }
    }

    private void Info(string msg)
    {
        if (verboseUnityLog) Debug.Log("[CP4_VkCheck] " + msg);
    }

    private void Warn(string msg)
    {
        if (verboseUnityLog) Debug.LogWarning("[CP4_VkCheck] " + msg);
    }

    private void Fail(string msg)
    {
        Debug.LogError("[CP4_VkCheck] " + msg);
        if (debugText)
            debugText.text = "FAIL\n" + msg + "\n\n" + debugText.text;
    }
}
