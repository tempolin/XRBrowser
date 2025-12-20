了解。**Quest内で完結**かつ **30fps**、さらに **入力（タップ/スクロール/キーボード）も繋げる**前提で考えると、現実的に勝てるのはほぼ1本道です。

---

# Quest内完結で30fpsを狙う「勝ち筋」

## ✅ WebViewをGPUで描かせて、その出力をUnityのテクスチャとして直接使う

**CPUで `Bitmap/getPixels/byte[]` を触ったら負け**なので、あなたの今の方式は捨てる。

### 構成（最短で現実的）

1. Android側で **VirtualDisplay** を作る（W×H）
2. そこに **WebViewを表示**（Presentationを使う）
3. VirtualDisplay の出力先を **ImageReader（PRIVATE）** にする
4. `ImageReader` から `HardwareBuffer` を取り出す
5. **NDK(EGL/GL)** で `HardwareBuffer → EGLImage → OES External Texture` に変換
6. Unity側はその textureId を **ExternalTexture** としてQuadに貼る
7. 入力はUnity→Javaに座標/キーを渡して、WebViewに `dispatchTouchEvent` / `evaluateJavascript`

この方式なら、理屈として **30fpsが到達可能**です。

---

# なぜNDKが必要か（重要）

Java/Kotlinだけだと、どうしても最終的に

* `Bitmap` へコピー
* `byte[]` へコピー
  みたいな **CPUコピー**に落ちやすいです。

**HardwareBufferを“GPUテクスチャとして扱う”ところがNDK領域**なので、ここだけは避けにくい。

---

# 実装を3ブロックに分ける（迷子防止）

## A. Java/Kotlin（WebView + VirtualDisplay + ImageReader）

* WebViewを **必ずDisplay上に載せる**（Presentation）
* `ImageReader` の `Surface` を VirtualDisplayの出力にする
* `ImageAvailable` で最新Imageを拾って `HardwareBuffer` をNDKへ渡す

## B. NDK C++（HardwareBuffer → OES texture）

* `AHardwareBuffer*` を受け取る
* `eglCreateImageKHR` で EGLImage
* `glEGLImageTargetTexture2DOES` で OESテクスチャにバインド
* textureId を保持し、Unityへ返す

## C. Unity C#（ExternalTextureとして描画）

* `Texture2D.CreateExternalTexture(...)`
* `GL.IssuePluginEvent` で RenderThread で更新
* Quadのmaterialへ貼る

---

# 入力のつなぎ方（Quest内完結でも簡単）

あなたの今の `WebQuadTapperXR` の計算はそのまま使える。

### タップ（Unity → Java）

* Unityで `hit.textureCoord` → `(x,y)` ピクセル変換
* Javaで `dispatchTouchEvent(MotionEvent.obtain(...))`

### スクロール/ドラッグ

* `ACTION_DOWN` → `ACTION_MOVE`（複数回）→ `ACTION_UP`
* “指ID”を固定して送ると安定

### キーボード

* いまの `setFocusedInputValue` / `insertText` / `backspace` / `enter` が流用できる
  （表示経路を変えるだけで入力は使い回せる）

---

# まず作るべき「最小成功条件」

30fps以前に、まずこれが出たら勝ちです：

✅ Quest上で

* VirtualDisplay上のWebViewが更新され
* UnityのQuadに “GPU経由で” 映る
* タップが反応する

その上で、更新が詰まるなら

* 解像度（W×H）
* バッファ数（ImageReader maxImages）
* acquireLatestImage運用
  で詰めていきます。

---

# 次に、こちらから「動く最小コード」を出すために必要な情報（質問じゃなく分岐条件）

聞き返しを減らすため、ここは前提を置いて進めます：

* Unityは **OpenXR + URP**
* Androidプラグインは **.aar + NDK(lib*.so)** でOK
* WebView解像度はまず **1024×1024**（後で調整）

この前提で、次の返答で僕が出せます：

1. Java側：VirtualDisplay + Presentation(WebView) + ImageReader の最小実装
2. C++側：AHardwareBuffer → EGLImage → OES texture の最小実装骨格
3. Unity側：ExternalTexture受け取り＆Quad表示の最小実装

---

## 先に言っておく「落とし穴」3つ（踏むとハマる）

* **Unity RenderThreadでGL更新しない**と真っ黒/クラッシュ（`GL.IssuePluginEvent`必須）
* `ImageReader.acquireNextImage()` 連打は遅延が溜まる（基本 `acquireLatestImage()`）
* WebViewは**Presentation上**に乗せないと更新しない端末がある（今の黒画面の原因）

---

この方向でいくなら、次は
**「Unity ⇄ Android プラグインの呼び出し口（JNI名/パッケージ名）」**をこちらで仮決めして、最小実装を丸ごと提示します。
あなたのプロジェクトの `package name`（例：`com.example.app`）だけ分かれば、そのままコピペ形にできます。



うん、**「できるかどうか調査が必要」**はその通りで、しかも調査ポイントがハッキリあります。
結論から言うと——

* **理屈としては可能**（Androidは `ImageReader(PRIVATE) → HardwareBuffer → EGLImage → glEGLImageTargetTexture2DOES` の流れが公式・実装例あり） ([Android Git Repositories][1])
* ただし **Quest（Horizon OS）の実機で安定して30fps出るか**は、**3つの実証が必要**です（ここが調査パート）。

---

# 調査すべき「できる/できない」を決める3チェック

## ① QuestのWebViewを VirtualDisplay 上で回して “毎フレーム更新されるか”

今あなたがハマってる「黒画面」は、WebViewがViewツリー/表示系に依存する端末差で起きがち。
VirtualDisplay + Presentation で別Displayに載せると、更新が進む構成にできる可能性が高い（＝この方式の第一関門）。
※ここはWeb検索だけじゃなく実機テスト必須。

## ② ImageReader(PRIVATE) で HardwareBuffer が取れるか

Android P以降、`ImageFormat.PRIVATE` でも `Image.getHardwareBuffer()` でアクセス可能、planesは空になるのが仕様。 ([Stack Overflow][2])
Questがこれをちゃんと返すかを実機で確認。

## ③ HardwareBuffer → EGLImage → OESテクスチャ が作れるか

CTS（Android公式テスト）側に、`EGL_ANDROID_image_native_buffer` を確認して `eglCreateImageKHR(...EGL_NATIVE_BUFFER_ANDROID...)` → `glEGLImageTargetTexture2DOES` するコードが実在します。 ([Android Git Repositories][1])
つまり技術的には「正攻法」がある。ただ、端末GPU/ドライバ相性で詰む可能性はある（ここが第二関門）。

---

# 30fpsの現実ライン

この方式がうまくいけば、**CPUで getPixels/byte[] を触らない**ので、あなたの今の方式より圧倒的に30fpsへ近いです。
逆に、少しでもCPUコピーに落ちると 30fps は一気に厳しくなります（いまの方式がまさにそれ）。

---

# “調査”の最短手順（やることが明確）

「調査」といっても、やるべきは実はこの **ミニ実験 3つ**だけです：

1. **VirtualDisplay に WebView を出して、アニメ（CSSで色が変わるだけ）を表示**
2. **ImageReader で acquireLatestImage() して HardwareBuffer が non-null か確認** ([Stack Overflow][2])
3. **NDKで EGLImage 化して OES texture に貼れるか確認**（1枚でも貼れたら勝ち） ([Android Git Repositories][1])

この3つが通ったら、「Quest内完結30fps Web表示」は前に進めます。
どれかが落ちたら、別案（WebRTC/既製プラグイン等）に切り替える判断ができます。

---

必要なら、次は僕がこの「ミニ実験用プロジェクト（Java+NDK+Unity側の最小）」の骨格を、**コピペで組める形**にして出します。

[1]: https://android.googlesource.com/platform/cts/%2B/master/tests/tests/nativehardware/jni/AHardwareBufferGLTest.cpp?utm_source=chatgpt.com "tests/tests/nativehardware/jni/AHardwareBufferGLTest.cpp"
[2]: https://stackoverflow.com/questions/61152730/accessing-hardwarebuffer-from-imagereader?utm_source=chatgpt.com "android - Accessing HardwareBuffer from ImageReader"

