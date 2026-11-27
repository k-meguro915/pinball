# Pinball UI（試合中の画面表示）

ゲーム本体（`Assets/Pinball`）とは別のフォルダです。`Assets/Pinball` を差し替えても、
このフォルダ・見た目の設定（テーマ）・シーンに置いた HUD の位置はそのまま残ります。

## できること
- **案① 今押せるボタンの表示**：台の左右の空きにプレイヤーごとのパネル
  - P1 = 左下、P2 / CPU = 右上（自分の陣地の横）。共通情報（先取点数・F1・Esc）は左上
  - 場面に合わせて中身が変わる
    - サーブ中（サーブ側）：`[B] 長押し → 離して発射` `[Y] ルート切り替え（今：自陣）` `[スティック] ブロッカーを動かす`
    - 打ち合い中：`[L / R] フリッパー` `[Y] 跳ね返し`（使えない間は薄く） `[B] 長押しでキャッチ` `[スティック] ブロッカーを動かす`
    - キャッチ中：`[B] 離して発射` `[スティック] 左右で狙う`
  - ボタン名は Input Actions の割り当てから自動表示（パッドがあればパッド、無ければキーボード）
  - スコア・サーブの印・ブロッカーの HP（点）も同じパネルに
  - 中央に知らせ（〇〇のサーブ／ゴール！／フォルト「パワー不足。もう少し溜めよう」／勝ち）
- **案② 盤面の上の表示**
  - サーブ中：プランジャーのレーンに**パワーゲージ**。白い線が「届く目安」で、越えると緑に変わる
  - サーブ中：選んだルートの**入口に点滅する矢印**と「ここから入る」
  - ブロッカー：前に **HP の点**、ダウン中は**戻るまでの時間の円**、跳ね返しが効いている間は**光る輪**
  - キャッチ中：**飛ぶ向きの矢印**と**自動発射までの残り時間**のバー

## 置き方
1. Window > TextMeshPro > **Import TMP Essential Resources**（初回だけ）
2. メニュー **Tools > Pinball UI > Create HUD In Scene**
   - 見た目の設定 `Assets/PinballUI/Data/PinballHudTheme.asset` ができる
   - **同梱の日本語フォント（BIZ UDPゴシック）から TextMeshPro のフォントを自動で作って設定**（`Assets/PinballUI/Fonts`）
   - シーンに `PinballHUD` が置かれ、GameManager の内蔵の表示（スコア等）がオフになる（タイトル画面はそのまま）
3. **シーンを保存**（Ctrl+S）
4. Game ビューを **横長（16:9 など）** にして Play → タイトルで START すると左右にパネルが出る（タイトル画面の間は HUD は隠れています）

### 日本語フォントについて
- 同梱：`BIZUDPGothic-Regular.ttf`（モリサワ BIZ UDPゴシック、**SIL Open Font License 1.1**。ゲームに入れて配布できます。ライセンス文は `BIZUDPGothic-OFL.txt`。フォント単体での販売は不可）
- 作り直したいとき：**Tools > Pinball UI > Setup Japanese Font**
- 操作説明パネル（Controls Guide）は別の仕組み（Legacy Text）なので、`ControlsGuideData` の `Font` 欄に `BIZUDPGothic-Regular.ttf` をそのまま入れてください

### UI が出ないとき
- Play 中に Console を見る：`[Pinball UI]` / `[Pinball]` で始まる警告に原因が出ます
- Hierarchy に `PinballHUD` があるか（無ければ Create HUD In Scene → シーン保存）
- HUD が動いていないときは、自動で元の表示（内蔵の HUD）が出ます

## 調整
| 何を | どこで |
|---|---|
| パネルの位置・大きさ | シーンの `PinballHUD` の `P1Panel` / `P2Panel` / `CommonPanel` を動かす（Rect Tool） |
| 操作の案内1行の見た目 | 各パネルの `Prompts/ChipTemplate`（Key = ボタンの枠、KeyText = ボタン名、Text = 説明） |
| 色・文字の大きさ・フォント | テーマ（PinballHudTheme） |
| 文言（日本語の言い回し） | テーマの「文言」欄（`{0}` には名前や数字が入る） |
| ボタン名の表記（例：LB → L） | テーマの `Key Labels` に追加：Action = `FlipperTop`、Gamepad = `L` など |
| 届くパワーの目安の線 | テーマの `Own Route Need Speed` / `Opp Route Need Speed`（発射の速さ。実際に遊んで合わせる） |
| 盤面の表示の大きさ | `PinballHUD/BoardIndicators` の Inspector |

- 縦長の画面（スマホなど）では、パネルを台の上下に自動で移します（`PinballHud` の `Adapt To Portrait`）
- 操作説明パネル（Controls Guide）が台に重なる場合は、`ControlsGuide` の Anchor を `(0.02, 0.5)`、Pivot を `(0, 0.5)` にすると左側に出ます
- 元の表示に戻したいときは、GameManager の `Use Built In Hud` をオンにして `PinballHUD` を消す
