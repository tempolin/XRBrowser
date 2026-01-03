using UnityEngine;
using TMPro;

public class Cp3StatusHud : MonoBehaviour
{
    [Header("Assign a TextMeshProUGUI (or it will auto-find in children)")]
    public TextMeshProUGUI text;

    [Header("Polling interval (sec)")]
    [Range(0.05f, 2.0f)]
    public float pollInterval = 0.2f;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass bridge;
#endif

    private float nextT;
    private int lastOk = int.MinValue;
    private long lastTs = long.MinValue;

    void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            bridge = new AndroidJavaClass("com.example.webviewgpu.NativeBridge");

            // optional sanity check (only if method exists)
            int magic = bridge.CallStatic<int>("getMagicNumber");

            Write(
                "CP3 HUD\n" +
                "bridge class OK\n" +
                "magic=" + magic + "\n" +
                "pollInterval=" + pollInterval + "s"
            );
        }
        catch (System.Exception e)
        {
            Write(
                "CP3 HUD\n" +
                "bridge init EX\n" +
                e.GetType().Name + "\n" +
                e.Message
            );
        }
#else
        Write("CP3 HUD\neditor\n(only works on Android device)");
#endif
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridge == null) return;

        if (Time.unscaledTime < nextT) return;
        nextT = Time.unscaledTime + pollInterval;

        try
        {
            int ok = bridge.CallStatic<int>("getLastSubmitOk");
            long ts = bridge.CallStatic<long>("getLastSubmitTs");

            bool changed = (ok != lastOk) || (ts != lastTs);
            lastOk = ok;
            lastTs = ts;

            Write(
                "CP3 HUD\n" +
                "lastSubmitOk=" + ok + "\n" +
                "lastSubmitTs=" + ts + "\n" +
                "changed=" + (changed ? "YES" : "no") + "\n\n" +
                "(expect ok=1, ts increasing when submit happens)"
            );
        }
        catch (System.Exception e)
        {
            Write(
                "CP3 HUD\n" +
                "poll EX\n" +
                e.GetType().Name + "\n" +
                e.Message
            );
        }
#endif
    }

    void Write(string s)
    {
        if (text != null) text.text = s;
    }
}
