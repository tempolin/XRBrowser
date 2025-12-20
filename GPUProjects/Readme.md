
いいね、その方式は「勝ち筋」なんだけど、**途中経過が真っ黒になりがち**で迷子になりやすい。なので最初から **“実機で確認できる中間地点（チェックポイント）”** を細かく切って、各地点で「これが見えたらOK」を作るのが一番安全。

下に、**Quest実機で確実に確認できる5段階の中間地点**と、各段階で入れるべき**最小ログ / 目視テスト**をセットで出すよ。
（この順に進めば、どこで壊れてるか即わかる）

---

## チェックポイント0：Unityは何もせず、Android側だけで WebView が動いてる

**目的**：Unity絡める前に「WebViewが実機で描ける」ことを確定させる

* Android側で普通に `WebView` を作って `Activity.setContentView(webView)` で表示
* Quest上でページが動く（スクロールできる）

✅ 成功条件：Questで普通にブラウザっぽく表示される
🧯 失敗したら：そもそも WebView/権限/ネット/UA の問題で、VirtualDisplay以前

---

## チェックポイント1：Presentation + VirtualDisplay で “別Display上” に WebView が動いてる（Unityなし）

**目的**：「Presentation上じゃないと更新されない」問題をここで潰す

* `VirtualDisplay` を作る
* `Presentation` を作る（`Display` に VirtualDisplay の display を渡す）
* `Presentation.onCreate()` で WebView を作って `setContentView(webView)`
* ここでは **ImageReaderをまだ使わない**（まず Presentation が生きてるか確認）

✅ 成功条件（実機で確認する方法）

* Presentationのrootに `TextView` を置いて、**1秒ごとにカウントアップ**（Handler）
  → **表示が更新される**なら Presentation が生きてる

🧠 ここが通ると「黒画面の根本原因（Presentationに乗ってない）」を切り分けできる

---

## チェックポイント2：ImageReader でフレームが “取れてる” だけ確認（Unityなし）

**目的**：「VirtualDisplay→ImageReaderの配線」が正しいことを確定させる
まだGPU変換しない。**Acquireできてるかだけ**見る。

* `ImageReader.newInstance(w,h, PixelFormat.RGBA_8888 or PRIVATE, maxImages)`
* `VirtualDisplay` の surface に `imageReader.getSurface()` を渡す
* `setOnImageAvailableListener` で `acquireLatestImage()`
* 取れたら `image.getTimestamp()` と `image.getHardwareBuffer()!=null` をログ

✅ 成功条件：ログが 30fps 近くで回る（最低でも連続で出る）
例：`onImageAvailable ts=... hb=true`

🧯 ここで詰まる典型

* `maxImages` が小さすぎて詰まる（まず 3〜5）
* `acquireNextImage()` で遅延が溜まる（必ず latest）

---

## チェックポイント3：NDKで “OESテクスチャIDを作れる” だけ確認（Unityなし）

**目的**：HardwareBuffer→EGLImage→OES の変換が成立するか

ここは「Unityに返す前」に、NDK側で

* `glGenTextures` で textureId 生成
* `glBindTexture(GL_TEXTURE_EXTERNAL_OES, texId)`
* `eglCreateImageKHR` → `glEGLImageTargetTexture2DOES`

✅ 成功条件（Unityなしでどう確認？）

* 変換成功/失敗を **戻り値とログ**で確認

  * `eglGetError()`
  * `glGetError()`
* さらに確実にするなら、NDK側で **1回だけFBOに描いてglReadPixels**してCRCをログ（※毎フレームやらない）
  → 「真っ黒しか出てない」をここで検出できる

---

## チェックポイント4：Unityに “外部テクスチャとして貼れる” ことだけ確認（WebView不要）

**目的**：Unity ExternalTexture + RenderThread 更新経路が正しいかを確定

WebViewはまだ繋がなくていい。まず

* NDKで **固定のGLテクスチャ**（例えば単色や簡単なグラデ）を作る
* Unityに `CreateExternalTexture(texId)` で貼る
* `GL.IssuePluginEvent` で毎フレーム更新（またはダミーでも可）

✅ 成功条件：Quest上のQuadに「ダミー画像」が安定表示される
🧯 ここが通らないと「RenderThread更新が間違い」なのでWebView以前に直す

---

## チェックポイント5：WebView→HardwareBuffer→OES→Unity を繋ぐ（最終統合）

**目的**：全部つないで “30fps + タップ反応” を確認

ここまで来たら、詰まる場所がかなり限定される。

✅ 最小成功条件（あなたが言ってたやつ）

* Unity Quad に WebView が動いて映る
* タップでリンクが反応する

---

# 迷子防止の「実機デバッグUI」を最初から入れる

中間地点が分かりにくい問題の解決策はこれが強い：

## Android側：常時オーバーレイ文字

Presentation内の最前面に `TextView` を置いて

* FPS
* lastImageTimestamp
* “HB OK / EGL OK / OES OK”
  を毎秒更新

→ Unityに映ってるか以前に、**Android側で状態が見える**

## Unity側：常時表示テキスト（Debug Canvas）

* pluginState（init済/textureId/lastUpdateFrame）
* 入力イベント数（tap/move/up）
* レイキャストhit座標（u,v）

---

# ここまでの方針で「中間地点」を設けた実装の進め方

あなたの目的に合わせると、進め方はこの順が一番ラク：

1. **CP1** Presentationでカウントアップが動く
2. **CP2** ImageReaderが連続で取れる（ログでOK）
3. **CP4** Unityにダミーテクスチャが貼れる
4. **CP3** NDKでHB→OESが成功する（ログ）
5. **CP5** 統合してWebViewが映る

※CP3とCP4は順番前後してもOK。迷子になりやすい人は **CP4先**が安心。

---

もし「今すぐ中間地点が見える形で作りたい」なら、次の返事では **CP1〜CP2の“実機で確認できる最小Androidコード”** を丸ごと出せるよ（Presentation内にデバッグTextView付き）。
パッケージ名が不明でも、`com.your.app` 仮で書いてそのまま置き換えできる形にして出す。
