using UnityEngine;

public class CpuWebViewTexture : MonoBehaviour
{
    [Header("Render Target")]
    public Renderer targetRenderer;     // QuadのRendererを入れる
    public int width = 512;
    public int height = 512;
    public int fps = 5;

    [Header("Ray Input (optional)")]
    public Camera rayCamera;            // CenterEye Camera（無ければ Camera.main）
    public Collider targetCollider;      // QuadのCollider（無ければ自動取得）
    public bool enableMouseTapDebug = true;

    [Header("Debug Text (optional)")]
    public bool enableKeyboardDebug = false;
    public string testText = "hello quest";

    [Header("URL")]
    public string startUrl = "https://www.google.com";

    private Texture2D tex;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject bridge;
#endif

    // --- ログ制御用 ---
    float _nextLogTime = 0f;
    string _lastLog = "";

    void LogPerSecond(string msg)
    {
#if UNITY_EDITOR
        return; // Editorでは抑制（必要なら消してOK）
#endif
        if (Time.time < _nextLogTime) return;
        _nextLogTime = Time.time + 1f;

        if (msg == _lastLog) return;
        _lastLog = msg;

        Debug.Log("[CPUWebView] " + msg);
    }

    void Start()
    {
        // --- 安全ガード ---
        if (targetRenderer == null)
        {
            Debug.LogError("[CPUWebView] targetRenderer is NULL. Assign Quad's Renderer in Inspector.");
            enabled = false;
            return;
        }

        // Collider 自動取得（QuadにBoxCollider推奨）
        if (targetCollider == null)
            targetCollider = targetRenderer.GetComponent<Collider>();

        // Texture 作成
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Quadに貼る（上下反転はUVで対応）
        var mat = targetRenderer.material;
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(1f, -1f);
        mat.mainTextureOffset = new Vector2(0f, 1f);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            LogPerSecond("creating bridge");
            bridge = new AndroidJavaObject("webviewcpu.WebViewCpuBridge", activity, width, height);
            LogPerSecond("bridge created");

            if (!string.IsNullOrEmpty(startUrl))
            {
                LoadUrl(startUrl);
                LogPerSecond("loadUrl called");
            }

            // 少し待ってからキャプチャ開始（描画前の真っ黒回避）
            InvokeRepeating(nameof(PullFrame), 1.5f, 1f / Mathf.Max(1, fps));
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CPUWebView] Start Exception: " + e);
        }
#endif
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge == null) return;

        // --- デバッグ：マウスクリックでtap（Editor/PC用） ---
        if (enableMouseTapDebug)
        {
            var cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam != null && targetCollider != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    var ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hit, 100f))
                    {
                        if (hit.collider == targetCollider)
                        {
                            Vector2 uv = hit.textureCoord; // 0..1
                            float x = uv.x * width;
                            float y = (1f - uv.y) * height;

                            TapPixel(x, y);
                            LogPerSecond($"tap x={x:0} y={y:0}");
                        }
                    }
                }
            }
        }

        // --- デバッグ：Spaceで全文入力+Enter ---
        if (enableKeyboardDebug)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SetFocusedInputValue(testText);
                Enter();
                LogPerSecond("setFocusedInputValue + enter");
            }
        }
#endif
    }

    // -------------------------
    // Public API（他スクリプトから呼ぶ用）
    // -------------------------

    public void LoadUrl(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("loadUrl", url);
#endif
    }

    // WebView ピクセル座標（左上原点）
    public void TapPixel(float x, float y)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("tap", x, y);
#endif
    }

    // 1文字/文字列を「カーソル位置に挿入」
    public void InsertText(string s)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bridge?.Call("insertText", s);
#endif
    }

    // 入力欄に「全文」を反映（安定）
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

    public string GetFocusedInfoJson()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return bridge?.Call<string>("getFocusedInfoJson");
#else
        return null;
#endif
    }

    // -------------------------
    // Capture
    // -------------------------

    void PullFrame()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var rgba = bridge?.Call<byte[]>("captureRgba");

            if (rgba == null)
            {
                LogPerSecond("rgba = null");
                return;
            }
            if (rgba.Length != width * height * 4)
            {
                LogPerSecond($"rgba bad len={rgba.Length}");
                return;
            }

            tex.LoadRawTextureData(rgba);
            tex.Apply(false, false);

            LogPerSecond($"rgba ok len={rgba.Length}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CPUWebView] PullFrame Exception: " + e);
        }
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            bridge?.Call("dispose");
        }
        catch { }
        bridge = null;
#endif
    }
}
