using System;
using UnityEngine;
using TMPro;

public class JavaSmokeTest : MonoBehaviour
{
    public TextMeshProUGUI text;

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Clear();
        Line("JAVA-SMOKE (screen)");
        Line("Start reached ✅");

        // パス情報（目視用）
        Line("persistentDataPath:");
        Line(Application.persistentDataPath);
        Line("temporaryCachePath:");
        Line(Application.temporaryCachePath);
        Line("streamingAssetsPath:");
        Line(Application.streamingAssetsPath);
        Line("dataPath:");
        Line(Application.dataPath);

        try
        {
            Line("");
            Line("Step1: new AndroidJavaClass...");
            using var cls = new AndroidJavaClass("com.example.webviewgpu.NativeBridge");
            Line("Step1 OK ✅ (class found)");

            Line("Step2: CallStatic<int>(getMagicNumber)...");
            int v = cls.CallStatic<int>("getMagicNumber");
            Line("Step2 OK ✅");
            Line("getMagicNumber = " + v);

            // もしここまで出たら完全に通電してる
            Line("");
            Line("RESULT: JAVA -> C# OK ✅");
        }
        catch (AndroidJavaException aje)
        {
            Line("");
            Line("RESULT: AndroidJavaException ❌");
            Line("Type: " + aje.GetType().FullName);
            Line("Msg: " + aje.Message);
            Line("");
            Line("Stack:");
            Line(aje.StackTrace);
        }
        catch (Exception e)
        {
            Line("");
            Line("RESULT: Exception ❌");
            Line("Type: " + e.GetType().FullName);
            Line("Msg: " + e.Message);
            Line("");
            Line("Stack:");
            Line(e.StackTrace);
        }
#else
        Clear();
        Line("JAVA-SMOKE");
        Line("editor (not running on Android)");
#endif
    }

    void Clear()
    {
        if (text) text.text = "";
    }

    void Line(string s)
    {
        if (text) text.text += s + "\n";
    }
}
