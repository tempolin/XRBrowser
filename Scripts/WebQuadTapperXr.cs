using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class WebQuadTapperXR : MonoBehaviour
{
    [Header("Refs")]
    public CpuWebViewTexture web;          // WebQuadに付いてるやつ
    public Transform rayOrigin;            // 右手Rayの先端(InteractorのRay Originなど)
    public Collider quadCollider;          // WebQuadのCollider（BoxCollider推奨）
    public TMP_InputField dummyInputField; // XRI Keyboardのターゲット（任意）

    [Header("Input")]
    public InputActionProperty clickAction; // 右手トリガーなど（XRIのアクションを刺す）

    void Awake()
    {
        if (web == null) web = GetComponent<CpuWebViewTexture>();
        if (quadCollider == null) quadCollider = GetComponent<Collider>();
    }

    void OnEnable()
    {
        if (clickAction.action != null) clickAction.action.Enable();
    }

    void OnDisable()
    {
        if (clickAction.action != null) clickAction.action.Disable();
    }

    void Update()
    {
        if (web == null || rayOrigin == null || quadCollider == null) return;
        if (clickAction.action == null) return;

        if (!clickAction.action.WasPressedThisFrame()) return;

        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (!Physics.Raycast(ray, out var hit, 10f)) return;
        if (hit.collider != quadCollider) return;

        // textureCoord は 0..1
        Vector2 uv = hit.textureCoord;

        float x = uv.x * web.width;
        float y = (1f - uv.y) * web.height; // 上下反転してWebView座標(左上原点)へ

        web.TapPixel(x, y);

        // 任意：入力開始時にダミーInputFieldを選択しておく（キーボード連携が安定しやすい）
        if (dummyInputField != null)
            dummyInputField.Select();
    }
}
