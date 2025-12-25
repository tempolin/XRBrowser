using UnityEngine;
using TMPro;

public class JavaSmokeTest : MonoBehaviour
{
    public TextMeshProUGUI text;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Write("JAVA-SMOKE\nStart reached");

            using var cls = new AndroidJavaClass("com.example.webviewgpu.NativeBridge");

            int v = cls.CallStatic<int>("getMagicNumber");
            Write($"JAVA-SMOKE\ngetMagicNumber={v}");

            // ★Step1
            int r = cls.CallStatic<int>("nativeAdd", 2, 3);
            Write($"JAVA-SMOKE\nnativeAdd(2,3)={r}");
        }
        catch (System.Exception e)
        {
            Write("JAVA-SMOKE\nEX\n" + e.GetType().Name + "\n" + e.Message);
        }
#else
        Write("JAVA-SMOKE\neditor");
#endif
    }

    void Write(string s)
    {
        if (text != null) text.text = s;
        Debug.Log(s);
    }
}
