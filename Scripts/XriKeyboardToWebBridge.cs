using UnityEngine;
using TMPro;

public class XriKeyboardToWebBridge : MonoBehaviour
{
    public TMP_InputField input;      // 自分（World Space TMP_InputField）
    public CpuWebViewTexture web;     // WebQuadの CpuWebViewTexture

    public float sendInterval = 0.08f; // 80ms
    float t;
    string last = "";

    void Reset()
    {
        input = GetComponent<TMP_InputField>();
    }

    void Update()
    {
        if (!input || !web) return;

        t += Time.deltaTime;
        if (t < sendInterval) return;
        t = 0f;

        var cur = input.text ?? "";
        if (cur == last) return;

        web.SetFocusedInputValue(cur);
        last = cur;
    }
}
