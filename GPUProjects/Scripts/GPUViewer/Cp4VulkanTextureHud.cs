// Cp4VulkanTextureHud.cs
// Unity6 + URP + Vulkan 専用
// Native plugin exports:
//   - GetRenderEventFuncVk()
//   - GetVulkanImagePtr()
//   - VkCheckVk_* counters
//
// 役割:
//   - Vulkan PluginEvent(init/tick) を発行
//   - VkImage* を ExternalTexture として Unity に渡す
//   - logcat不要、HUD(TextMeshPro)だけで状態確認

using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class Cp4VulkanTextureHud : MonoBehaviour
{
    [Header("Target Renderer (Quad/Plane). If null, uses GetComponent<Renderer>().")]
    public Renderer targetRenderer;

    [Header("External texture size (must match HardwareBuffer size)")]
    public int width = 1024;
    public int height = 1024;

    [Header("On-screen debug text (TextMeshProUGUI)")]
    public TextMeshProUGUI debugText;

    [Header("Update HUD every N frames")]
    [Range(1, 120)]
    public int uiUpdateIntervalFrames = 5;

    [Header("Warn if Graphics API is not Vulkan")]
    public bool warnIfNotVulkan = true;

    [Header("Verbose Unity Debug.Log")]
    public bool verboseUnityLog = true;

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string DLL = "webview_gpu";
#else
    private const string DLL = "webview_gpu";
#endif

    // ===== Native exports (Vulkan) =====
    [DllImport(DLL)] private static extern IntPtr GetRenderEventFuncVk();
    [DllImport(DLL)] private static extern IntPtr GetVulkanImagePtr();

    [DllImport(DLL)] private static extern int VkCheckVk_GetLastEventId();
    [DllImport(DLL)] private static extern int VkCheckVk_GetInitCount();
    [DllImport(DLL)] private static extern int VkCheckVk_GetTickCount();
    [DllImport(DLL)] private static extern int VkCheckVk_GetLastErrorCode();

    // ===== State =====
    private Texture2D _externalTex;
    private Material _mat;

    private bool _issuedInit = false;
    private bool _createdExternal = false;

    private IntPtr _lastVkImagePtr = IntPtr.Zero;
    private int _lastUiFrame = -9999;

    // Plugin event IDs (must match VulkanHbPlugin.cpp)
    private const int EVT_INIT = 2001;
    private const int EVT_TICK = 2002;

    void Start()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        if (!targetRenderer)
        {
            Fail("No targetRenderer found.");
            enabled = false;
            return;
        }

        _mat = targetRenderer.material;

        if (warnIfNotVulkan && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
        {
            Warn($"graphicsDeviceType is {SystemInfo.graphicsDeviceType}. Vulkan-only script.");
        }

        // Issue INIT once
        try
        {
            IntPtr fn = GetRenderEventFuncVk();
            if (fn == IntPtr.Zero)
            {
                Fail("GetRenderEventFuncVk returned NULL.");
                enabled = false;
                return;
            }

            GL.IssuePluginEvent(fn, EVT_INIT);
            _issuedInit = true;
            Info($"IssuePluginEventVk INIT ({EVT_INIT})");
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEventVk INIT failed: {e}");
            enabled = false;
            return;
        }

        UpdateHud(true);
    }

    void Update()
    {
        // TICK every frame
        try
        {
            GL.IssuePluginEvent(GetRenderEventFuncVk(), EVT_TICK);
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEventVk TICK failed: {e}");
            enabled = false;
            return;
        }

        // Fetch VkImage* pointer (address)
        IntPtr vkImagePtrAddr = IntPtr.Zero;
        try
        {
            vkImagePtrAddr = GetVulkanImagePtr();
        }
        catch (Exception e)
        {
            Fail($"GetVulkanImagePtr failed: {e}");
            enabled = false;
            return;
        }

        // Create ExternalTexture once pointer becomes non-zero
        if (!_createdExternal && vkImagePtrAddr != IntPtr.Zero)
        {
            try
            {
                _externalTex = Texture2D.CreateExternalTexture(
                    width, height,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: false,
                    nativeTex: vkImagePtrAddr
                );

                _externalTex.wrapMode = TextureWrapMode.Clamp;
                _externalTex.filterMode = FilterMode.Bilinear;

                _mat.mainTexture = _externalTex;
                _createdExternal = true;

                Info($"CreateExternalTexture OK (Vulkan). ptr={vkImagePtrAddr}");
            }
            catch (Exception e)
            {
                Fail($"CreateExternalTexture failed: {e}");
                enabled = false;
                return;
            }
        }

        // Tell Unity the external texture was updated
        if (_externalTex != null)
        {
            try
            {
                _externalTex.UpdateExternalTexture(_externalTex.GetNativeTexturePtr());
            }
            catch (Exception e)
            {
                Fail($"UpdateExternalTexture failed: {e}");
                enabled = false;
                return;
            }
        }

        _lastVkImagePtr = vkImagePtrAddr;
        UpdateHud(false);
    }

    private void UpdateHud(bool force)
    {
        if (!debugText) return;
        if (!force && (Time.frameCount - _lastUiFrame) < uiUpdateIntervalFrames) return;

        _lastUiFrame = Time.frameCount;

        int lastEvent = SafeGet(VkCheckVk_GetLastEventId);
        int initCount = SafeGet(VkCheckVk_GetInitCount);
        int tickCount = SafeGet(VkCheckVk_GetTickCount);
        int errCode = SafeGet(VkCheckVk_GetLastErrorCode);

        bool pass =
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan &&
            _issuedInit &&
            initCount > 0 &&
            tickCount > 0 &&
            errCode == 0;

        debugText.text =
            $"CP4 Vulkan HUD [{(pass ? "PASS" : "CHECK")}]\n" +
            $"frame={Time.frameCount}\n" +
            $"gfx={SystemInfo.graphicsDeviceType}\n" +
            $"issuedInit={_issuedInit}\n" +
            $"initCount={initCount}  tickCount={tickCount}\n" +
            $"lastEventId={lastEvent}  lastErrorCode={errCode}\n" +
            $"vkImagePtrAddr={_lastVkImagePtr}\n" +
            $"externalCreated={_createdExternal}\n" +
            $"size={width}x{height}";
    }

    private int SafeGet(Func<int> f)
    {
        try { return f(); }
        catch { return -9999; }
    }

    // ===== Logging helpers =====
    private void Info(string msg)
    {
        if (verboseUnityLog) Debug.Log("[CP4_VK] " + msg);
    }

    private void Warn(string msg)
    {
        if (verboseUnityLog) Debug.LogWarning("[CP4_VK] " + msg);
    }

    private void Fail(string msg)
    {
        Debug.LogError("[CP4_VK] " + msg);
        if (debugText)
            debugText.text = "FAIL\n" + msg + "\n\n" + debugText.text;
    }
}
