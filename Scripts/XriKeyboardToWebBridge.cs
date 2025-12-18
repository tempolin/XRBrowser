using UnityEngine;
using TMPro;

public class XriKeyboardToWebBridge : MonoBehaviour
{
    [Header("Input from XRI Keyboard")]
    public TMP_InputField input;

    [Header("Android WebView bridge")]
    public WebViewCpuClient web;

    [Header("Send throttling (seconds)")]
    public float sendInterval = 0.08f; // 80ms

    private float timer;
    private string lastSent = "";

    void Reset()
    {
        input = GetComponent<TMP_InputField>();
    }

    void OnEnable()
    {
        if (input == null) return;
        input.onValueChanged.AddListener(OnChanged);
        input.onEndEdit.AddListener(OnEndEdit);
    }

    void OnDisable()
    {
        if (input == null) return;
        input.onValueChanged.RemoveListener(OnChanged);
        input.onEndEdit.RemoveListener(OnEndEdit);
    }

    void OnChanged(string _)
    {
        // Update() ‘¤‚ÅŠÔˆø‚¢‚Ä‘—‚é
    }

    void Update()
    {
        if (input == null || web == null) return;

        timer += Time.deltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        var cur = input.text ?? "";
        if (cur == lastSent) return;

        // ˆÀ’èF‘S•¶‚ðŠÛ‚²‚Æ Web ‚É”½‰f
        web.SetFocusedInputValue(cur);
        lastSent = cur;
    }

    void OnEndEdit(string finalText)
    {
        if (web == null) return;

        web.SetFocusedInputValue(finalText ?? "");
        lastSent = finalText ?? "";
    }
}
