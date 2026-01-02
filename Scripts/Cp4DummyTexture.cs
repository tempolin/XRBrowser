// Cp4DummyTexture.cs
// CP4: Native plugin が RenderThread 上で作った GL texture(texId) を
// Unity の ExternalTexture として貼り、毎フレーム更新できるか確認する。
// さらに「迷子防止」のために、画面Text(TMP)とUnityログで状態を出す。

using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

public class Cp4DummyTexture : MonoBehaviour
{
    [Header("Target Renderer (Quad/Plane etc). If null, uses GetComponent<Renderer>().")]
    public Renderer targetRenderer;

    [Header("Use Unlit material recommended (URP Unlit / Unlit-Texture).")]
    public bool forceUnlitWarning = true;

    [Header("External texture size (must match native texture size)")]
    public int width = 512;
    public int height = 512;

    [Header("Optional: On-screen debug text (TextMeshProUGUI)")]
    public TextMeshProUGUI debugText;

    [Header("How often to refresh on-screen text (frames). 1 = every frame.")]
    [Range(1, 120)]
    public int uiUpdateIntervalFrames = 5;

    [Header("Enable verbose Debug.Log()")]
    public bool verboseUnityLog = true;

    private Texture2D _externalTex;
    private Material _mat;

    private int _lastUiUpdateFrame = -9999;

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string DLL = "webview_gpu"; // ★あなたの.so名に合わせる（libwebview_gpu.so → "webview_gpu"）
#else
    private const string DLL = "webview_gpu";
#endif

    // ===== Native exports (must exist in your webview_gpu .so) =====
    [DllImport(DLL)] private static extern IntPtr GetRenderEventFunc();
    [DllImport(DLL)] private static extern UIntPtr GetTextureId();
    [DllImport(DLL)] private static extern void SetTextureSize(int w, int h);

    // ===== State =====
    private bool _issuedInitEvent = false;
    private bool _createdExternal = false;
    private ulong _lastTexId = 0;

    // Cached for display
    private string _statusLine1 = "";
    private string _statusLine2 = "";
    private string _statusLine3 = "";
    private string _statusLine4 = "";

    void Start()
    {
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        if (!targetRenderer)
        {
            Fail("No targetRenderer found. Assign Renderer or attach to an object with Renderer.");
            enabled = false;
            return;
        }

        _mat = targetRenderer.material;

        if (forceUnlitWarning && _mat && _mat.shader && !_mat.shader.name.ToLower().Contains("unlit"))
        {
            Warn($"Material shader is not Unlit: {_mat.shader.name}. CP4 debugging is easier with Unlit.");
        }

        // Optional: ask native to use same texture size
        try
        {
            SetTextureSize(width, height);
            Info($"SetTextureSize({width},{height}) called.");
        }
        catch (Exception e)
        {
            Warn($"SetTextureSize call failed (can be ignored if not exported): {e.GetType().Name}");
        }

        // Issue init event once
        try
        {
            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
            _issuedInitEvent = true;
            Info("Issued PluginEvent: init (eventId=1)");
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEvent(init) failed: {e}");
        }

        UpdateStatusUI(force: true);
    }

    void Update()
    {
        // Always tick an update event (RenderThread)
        try
        {
            GL.IssuePluginEvent(GetRenderEventFunc(), 2);
        }
        catch (Exception e)
        {
            Fail($"IssuePluginEvent(update) failed: {e}");
            enabled = false;
            return;
        }

        // Try fetch texId
        ulong texId = 0;
        try
        {
            texId = GetTextureId().ToUInt64();
        }
        catch (Exception e)
        {
            Fail($"GetTextureId failed: {e}");
            enabled = false;
            return;
        }

        if (texId != _lastTexId)
        {
            Info($"texId changed: {_lastTexId} -> {texId}");
            _lastTexId = texId;
        }

        // Create external texture once texId becomes non-zero
        if (_externalTex == null && texId != 0)
        {
            try
            {
                IntPtr nativePtr = (IntPtr)texId;

                _externalTex = Texture2D.CreateExternalTexture(
                    width, height,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: false,
                    nativeTex: nativePtr
                );

                _externalTex.wrapMode = TextureWrapMode.Clamp;
                _externalTex.filterMode = FilterMode.Bilinear;

                _mat.mainTexture = _externalTex;
                _createdExternal = true;

                Info($"CreateExternalTexture OK. texId={texId}");
            }
            catch (Exception e)
            {
                Fail($"CreateExternalTexture failed: {e}");
                enabled = false;
                return;
            }
        }

        // Tell Unity: external texture content updated
        // (even if texId is same, your native is doing TexSubImage etc)
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

        UpdateStatusUI(force: false);
    }

    private void UpdateStatusUI(bool force)
    {
        if (!debugText) return;

        if (!force && (Time.frameCount - _lastUiUpdateFrame) < uiUpdateIntervalFrames)
            return;

        _lastUiUpdateFrame = Time.frameCount;

        // You can extend these lines freely
        _statusLine1 = $"CP4 DEBUG  (frame={Time.frameCount})";
        _statusLine2 = $"issuedInit={_issuedInitEvent}  externalCreated={_createdExternal}";
        _statusLine3 = $"texId={_lastTexId}  extTexNull={(_externalTex == null)}";
        _statusLine4 = $"gfxApiHint: set Vulkan OFF, OpenGLES3 ONLY";

        debugText.text = _statusLine1 + "\n" + _statusLine2 + "\n" + _statusLine3 + "\n" + _statusLine4;
    }

    // ===== Logging helpers =====
    private void Info(string msg)
    {
        if (verboseUnityLog) Debug.Log("[CP4] " + msg);
        if (debugText) AppendSmall(msg);
    }

    private void Warn(string msg)
    {
        if (verboseUnityLog) Debug.LogWarning("[CP4] " + msg);
        if (debugText) AppendSmall("WARN: " + msg);
    }

    private void Fail(string msg)
    {
        Debug.LogError("[CP4] " + msg);
        if (debugText) AppendSmall("FAIL: " + msg);
    }

    // Adds a small rolling log under the main status (kept short to avoid huge strings)
    private void AppendSmall(string msg)
    {
        // If you prefer not to append, comment this out.
        // Keep last ~6 lines max
        var lines = debugText.text.Split('\n');
        int keep = Math.Min(lines.Length, 6);
        string tail = "";
        for (int i = Math.Max(0, lines.Length - keep); i < lines.Length; i++)
            tail += lines[i] + "\n";

        // Put tail + latest msg at bottom (avoid growing forever)
        debugText.text = _statusLine1 + "\n" + _statusLine2 + "\n" + _statusLine3 + "\n" + _statusLine4
                         + "\n---\n" + tail + msg;
    }
}
