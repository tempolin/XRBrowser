using UnityEngine;

public class CpuWebViewTexture : MonoBehaviour
{
    [Header("Render")]
    public Renderer targetRenderer;
    public int width = 1024;
    public int height = 1024;
    public int fps = 10;
    public string startUrl = "https://www.google.com";

    Texture2D tex;
    byte[] rgbaBuf; // ★追加：Unity側で1回だけ確保して使い回す

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject bridge;
#endif

    void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogError("[CpuWebViewTexture] targetRenderer is null.");
            enabled = false;
            return;
        }

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // ★追加：使い回しバッファ（1回だけ）
        rgbaBuf = new byte[width * height * 4];

        var mat = targetRenderer.material;
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(1f, -1f);
        mat.mainTextureOffset = new Vector2(0f, 1f);

#if UNITY_ANDROID && !UNITY_EDITOR
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        bridge = new AndroidJavaObject("webviewcpu.WebViewCpuBridge", activity, width, height);
#endif
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(startUrl))
            bridge?.Call("loadUrl", startUrl);

        InvokeRepeating(nameof(PullFrame), 1.5f, 1f / Mathf.Max(1, fps));
#endif
    }

    void PullFrame()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge == null || tex == null || rgbaBuf == null) return;

        // ★変更：戻り値 byte[] ではなく、Unityの配列に書き込ませる
        bool ok = bridge.Call<bool>("captureInto", rgbaBuf);
        if (!ok)
        {
            Debug.LogWarning("[WebViewCapture] captureInto failed");
            return;
        }

        tex.LoadRawTextureData(rgbaBuf);
        tex.Apply(false, false);
#endif
    }



    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { bridge?.Call("dispose"); } catch { }
        bridge = null;
#endif
        if (tex != null) { Destroy(tex); tex = null; }
        rgbaBuf = null;
    }

    public void LoadUrl(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("loadUrl", url);
#endif
    }

    public void TapPixel(float x, float y)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("tap", x, y);
#endif
    }

    public void SetFocusedInputValue(string s)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("setFocusedInputValue", s);
#endif
    }

    public void Backspace()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("backspace");
#endif
    }

    public void Enter()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("enter");
#endif
    }
}
