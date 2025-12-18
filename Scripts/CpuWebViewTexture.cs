using UnityEngine;

public class CpuWebViewTexture : MonoBehaviour
{
    [Header("Render")]
    public Renderer targetRenderer;    // ★WebQuadのRenderer（自分のRendererでもOK）
    public int width = 1024;
    public int height = 1024;
    public int fps = 10;              // CPU重いので 5〜10 推奨
    public string startUrl = "https://www.google.com";

    Texture2D tex;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject bridge;
#endif

    void Awake()
    {
        // Quadに貼るTextureを用意
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

        var mat = targetRenderer.material;
        mat.mainTexture = tex;

        // WebViewが上下逆になることが多いのでUVで反転
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

        // 少し待ってからキャプチャ開始（真っ黒回避）
        InvokeRepeating(nameof(PullFrame), 1.5f, 1f / Mathf.Max(1, fps));
#endif
    }

    void PullFrame()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge == null || tex == null) return;

        var rgba = bridge.Call<byte[]>("captureRgba");
        if (rgba == null || rgba.Length != width * height * 4) return;

        tex.LoadRawTextureData(rgba);
        tex.Apply(false, false);
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { bridge?.Call("dispose"); } catch { }
        bridge = null;
#endif
    }

    // ---- Web API（他スクリプトから呼ぶ） ----
    public void LoadUrl(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("loadUrl", url);
#endif
    }

    // WebViewの左上原点ピクセル座標
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
