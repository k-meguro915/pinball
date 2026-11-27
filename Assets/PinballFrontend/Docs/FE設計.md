# FE（フロントエンド）設計

作った日：2026-11-24　状態：**案**（まだコードは書いていない）
もとにした資料：[フォルダとアセットの内訳.md](フォルダとアセットの内訳.md)、[フォルダ構成案.md](フォルダ構成案.md)

決まっていること：2 つのルールを 1 本にまとめる／Windows 向け／タイトルとモード選択は別のシーン／置き場所は `Assets/PinballFrontend/`／namespace は `PinballFrontend`／クラス名は `FE` で始める／本編を直すときは先に相談する

## 1. 画面の流れ

```
FE_Title ──決定──▶ FE_ModeSelect ──スタート──▶ Pinball_VersusMode（対戦ルール）
   │                 ▲   │                          │
   │終了              │   │                          │ Esc／決着の後の Esc
   ▼                 │   └──スタート──▶ PinBall_DodgeMode（ドッジボール）
 アプリを終える        │                              │ Esc／決着の後の Esc（Select）
                     └──────────────────────────────┘
                       本編の「タイトルへ」は FE_ModeSelect に戻す
```

- **FE_Title**：ゲームの名前、「スタート」「終了」。終了は `Application.Quit()`。
- **FE_ModeSelect**：ルールを選び、そのルールの設定を選んで「スタート」。キャンセルで FE_Title に戻る。
- **本編**：始まったらすぐに試合に入る。本編の中のタイトル画面（OnGUI／DBHud）は出さない。
- **戻り先**：本編の「タイトルへ」は、どちらのルールでも FE_ModeSelect に戻す。続けて別の設定で遊べるようにするため。
- **再戦**：決着の後の Enter での再戦は、本編のものをそのまま使う（シーンは読み直さない）。

## 2. モード選択で選べる項目

| ルール | 項目 | 選べる値 | 本編の受け口 |
|---|---|---|---|
| 共通 | モード | VS CPU／1 VS 1 | `GameManager.mode`／`DBGameManager.mode` |
| 共通 | CPU の強さ（VS CPU のときだけ） | EASY／NORMAL／HARD | `GameManager.cpuLevel`／`DBGameManager.cpuLevel` |
| 対戦 | 先取点 | 3／5／10 | `GameManager.targetScore`（`TargetOptions` の値だけ） |
| ドッジボール | 外野の投げ方 | 全自動／ボタン | `outfieldControl` |
| ドッジボール | シュートポストの撃ち方 | 全自動／ボタン | `shootControl` |
| ドッジボール | フリッパー（P1／P2） | 手動／自動 | `flipperAuto[0]`／`[1]`（CPU 側はいつも自動） |

前回選んだ値は `PlayerPrefs` に残し、次に開いたときの初期値にする（9 章の決めてほしいこと 2）。

## 3. 設定の受け渡し

### 3.1 しくみ

```
FE_ModeSelect                         本編シーン
  FEModeSelectScreen                    （読み込み）
   └ FESession.Request(設定) ──▶ 静的に保持
        SceneManager.LoadScene         Awake / OnEnable（本編）
                                       ↓
                               sceneLoaded ← FESession が受け取る
                                       ↓  設定を本編に入れ、FE の部品を足す
                                       Start（本編）→ 試合が始まる
```

- `FESession` は static のクラスにする。`[RuntimeInitializeOnLoadMethod]` で `SceneManager.sceneLoaded` に登録する。
- `sceneLoaded` が呼ばれるのは、読み込んだシーンの **Awake／OnEnable の後、Start の前**。
- **FE から始めたときだけ働く**（`FESession` に設定が入っているときだけ）。本編のシーンを Editor で直接再生したときは何もしない。本編の作業を邪魔しないため。
- 本編のシーンには FE の部品を置かない。`sceneLoaded` で、FE の GameObject（`FEMatchBridge`）を本編のシーンに足す。

### 3.2 ドッジボール：本編を直さずにできる

- `DBGameManager` は、`Start()` で `skipTitle` を見て、`StartMatch()` の中で各項目を読む。
- なので `sceneLoaded` で項目と `skipTitle = true` を入れれば、そのまま設定どおりに試合が始まる。

### 3.3 対戦ルール：1 か所が引っかかる

- **引っかかるところ**：`GameManager.StartMatch()` は public の `mode`／`targetScore`／`cpuLevel` を読まない。Awake で作った private の `modeIndex`／`targetIndex`／`levelIndex` から上書きする。`sceneLoaded` の時点では Awake が終わっているので、public の項目を書き換えても効かない。
- **試合の始め方**：`Start()` → `ToTitle()` → `onTitle.Invoke()` の順に呼ばれる。FE は `sceneLoaded` で `onTitle` に登録しておき、1 回目の `onTitle` で `StartMatch()` を呼ぶ。こうすれば本編のタイトルを一度も描かずに試合に入れる（`State` が Serve に変わってから最初の OnGUI が来るため）。
- **index の入れ方**は 2 案あり、どちらかを選ぶ（9 章の決めてほしいこと 1）。

| 案 | やり方 | 本編を直すか | 良いところ | 気になるところ |
|---|---|---|---|---|
| **A（おすすめ）** | `GameManager` に、項目と index をまとめて入れる public のメソッドを 1 つ足してもらう（例：`ApplyRules(MatchMode, int targetScore, CpuPlayer.Level)`） | 直す（数行） | 名前が変わっても壊れない。本編の作者が分かる形で残る | 本編の作者に相談が要る |
| B | リフレクションで private の 3 つの index を書き換える | 直さない | 今すぐできる | 本編で名前が変わると黙って効かなくなる（FE 側で見つからないときは警告を出す） |

本編も ControlsGuide をリフレクションで読んでいるので、B も前例はある。

**決定（2026-11-24）：最初から A で本編に相談する。** 相談の中身は [本編への相談_GameManager.md](本編への相談_GameManager.md)。B は、相談の結果「足せない」となったときだけ使う。

### 3.4 FE に戻る

| ルール | 「タイトルへ」の場面 | FE の拾い方 |
|---|---|---|
| 対戦 | 試合中の Esc、決着の後の Esc → `ToTitle()` | 2 回目以降の `onTitle` で FE_ModeSelect を読む |
| ドッジボール | 決着の後の Esc／Select → `GoTitle()` | `FEMatchBridge` が毎フレーム `CurrentState` を見て、Title になったら FE_ModeSelect を読む |
| ドッジボール | 試合中 | **本編にタイトルへ戻る操作がない。** FE 側で Esc（パッドは Select）を読んで戻す |

ドッジボールで決着の後に戻るとき、DBHud のタイトルが 1 フレームだけ描かれる。気になるようなら、本編に戻るときのイベントを足してもらうよう相談する（8 章）。

## 4. 画面（UI）

- **uGUI＋TextMeshPro** で作る。本編の HUD（PinballUI）と同じ組み合わせ。
- **フォント**：PinballUI の `BIZUDPGothic-Regular SDF` を参照して使う（コピーはしない）。TMP Settings のフォールバックにも入っているので、日本語が出る。
- **入力**：EventSystem＋`InputSystemUIInputModule`。Project-wide Actions の `UI` マップ（Navigate／Submit／Cancel）をそのまま使う。プロジェクトは Input System だけを使う設定（Active Input Handling = Input System）。キーボード、パッド、マウスで操作できる。
- **画面の大きさ**：Windows の既定はフルスクリーン（FullScreenWindow）で、横長の画面になる。Canvas Scaler の基準は 1920×1080、Match = 0.5 にする。どちらの本編もカメラが画面の縦横比に合わせるので、FE 側で向きを決める必要はない。
- **見た目**：まずは決まった色と文字だけの簡単なものにする。見た目を整えるのは、流れが動いてから。

## 5. 本編シーンに入るときと出るときに気をつけること

| 項目 | 内容 | FE でやること |
|---|---|---|
| `Time.timeScale` | 対戦ルールは操作説明が開いている間 0 にする（OnDestroy で戻す）。ドッジボールは触らない | FE のシーンに入ったら 1 に戻す（念のため） |
| `PinballInput` | 対戦ルールの Awake で Configure、OnDestroy で Shutdown | FE を必ず間に挟むので、本編から本編へ直接移ることはない |
| 操作説明（ControlsGuide） | 対戦ルールのシーンでは、始まると開く（`showOnStart`）。開いている間は試合が止まる | そのままにする（試合は、閉じてから進む） |
| `Screen.orientation` | 対戦ルールの Awake で縦に固定する | Windows では効かないので何もしない |
| `GameManager.ExternalHudActive` | PinballHud が OnEnable／OnDisable で切り替える（static） | 触らない |

## 6. クラス

namespace はすべて `PinballFrontend`。置き場所は `Assets/PinballFrontend/Scripts/`。MonoBehaviour は 1 ファイルに 1 つ、ファイル名はクラス名と同じにする（ビルドした後に起動できなくなるのを防ぐため）。

| クラス | 種類 | 役割 |
|---|---|---|
| `FERule` | enum | `Versus`／`Dodgeball` |
| `FEMatchSettings` | `[Serializable]` のクラス | 2 章の項目をまとめたもの。`PlayerPrefs` との読み書きもここでする |
| `FESession` | static のクラス | 選んだ設定を持つ。本編のシーンを読み、`sceneLoaded` で `FEMatchBridge` を足す。FE に戻るメソッドも持つ |
| `FEConfig` | ScriptableObject（`Data/FEConfig.asset`） | シーン名（FE_Title／FE_ModeSelect／2 つの本編）、ゲームの表示名 |
| `FEMatchBridge` | MonoBehaviour（本編のシーンに実行時に足す） | 設定を本編に入れる。戻るのを見張る。ルールごとの違いは下の 2 つに分ける |
| `FEVersusAdapter` | 普通のクラス | `GameManager` に設定を入れる（3.3 の A か B）。`onTitle` を見張る |
| `FEDodgeballAdapter` | 普通のクラス | `DBGameManager` に設定を入れる。`CurrentState` と Esc を見張る |
| `FETitleScreen` | MonoBehaviour（FE_Title） | スタート／終了 |
| `FEModeSelectScreen` | MonoBehaviour（FE_ModeSelect） | ルールと項目を選んで、`FESession` に渡す |
| `FEOptionRow` | MonoBehaviour | 「項目名：値」の 1 行。左右で値を変える |
| `Editor/FEBuildMenu` | Editor | Build Settings にシーンを 4 つ並べる。Windows 向けにビルドする |

本編の型（`Pinball.GameManager`、`PinballDodgeball.DBGameManager`）を直接参照するのは、2 つの Adapter だけにする。本編が変わったときに、直すところを 2 か所にまとめておくため。

## 7. Build Settings

| 番号 | シーン |
|---|---|
| 0 | `PinballFrontend/Scenes/FE_Title.unity` |
| 1 | `PinballFrontend/Scenes/FE_ModeSelect.unity` |
| 2 | `Scenes/Pinball_VersusMode.unity` |
| 3 | `Scenes/PinBall_DodgeMode.unity`（今は入っていない） |

シーンは名前で読む（`FEConfig` に名前を持たせる）。番号がずれても困らないようにするため。並べるのは `FEBuildMenu` で行い、手で並べる手間とまちがいを減らす。

## 8. 本編に相談したいこと

| # | 内容 | 急ぐか |
|---|---|---|
| 1 | `GameManager` に、項目と index をまとめて入れるメソッドを足す（3.3 の案 A） | **要る。相談中**（[本編への相談_GameManager.md](本編への相談_GameManager.md)） |
| 2 | ドッジボールで、試合中にタイトルへ戻る操作。本編に足すか、FE が Esc を読むままでよいか | 後でよい（FE で代わりにできる） |
| 3 | `DBGameManager` に、タイトルへ戻ったときのイベント（例：`event Action Returned`）を足す | 後でよい（1 フレームのちらつきが気になるなら） |
| 4 | ドッジボールのシーンの `skipTitle` が、今はオンで保存されている。FE から入るときは FE が入れるので関係ないが、直接再生したときの既定をどうするか | 本編の都合で決めてよい |

## 9. 決めてほしいこと

1. ~~3.3 を A にするか B にするか。~~ → **決定：最初から A で相談する**（2026-11-24）
2. **前回の設定を残すか**（PlayerPrefs）。案では残す。
3. **ドッジボールの開発用の項目**（外野、シュートポスト、フリッパー）を、モード選択に全部出すか。案では全部出す。減らすなら、出さない項目は本編シーンの値のままにする。
4. **ゲームの表示名**（タイトル画面と、ビルドしたときのアプリの名前）。今の `productName` は `pinball`、`companyName` は `DefaultCompany`。

## 10. 作る順番

1. `FEMatchSettings`、`FESession`、2 つの Adapter、`FEMatchBridge`。まずは仮のボタンだけのシーンから、両方のルールに設定どおり入れて、戻ってこられることを確かめる。ドッジボールの方は相談の返事を待たずに作れる。対戦ルールの方は、`ApplyRules` が入ってから仕上げる（それまでは、本編シーンの値のままで試合に入るところまで）。
2. `FEConfig`、`FEBuildMenu`。Build Settings を並べて、Windows 向けにビルドして確かめる。フォルダ構成案の「5. ビルドで確かめる」も、ここで一緒に済ませる。
3. FE_Title、FE_ModeSelect の画面。
4. `PlayerPrefs`、見た目の仕上げ。

## 11. 作ったもの（2026-11-24、10 章の 1 の途中）

| ファイル | 中身 |
|---|---|
| `Scripts/FERule.cs` | `FERule`、`FEPlayMode`、`FECpuLevel`、`FEControl` |
| `Scripts/FEMatchSettings.cs` | 2 章の項目（PlayerPrefs はまだ） |
| `Scripts/IFEMatchAdapter.cs` | Adapter の約束（`Apply`／`Tick`） |
| `Scripts/FESession.cs` | 本編シーンを読み、`sceneLoaded` で設定を入れて `FEMatchBridge` を足す。戻る処理 |
| `Scripts/FEMatchBridge.cs` | 本編シーンに実行時に足す。Adapter が「戻る」と言ったら戻す |
| `Scripts/FEDodgeballAdapter.cs` | ドッジボール：設定と `skipTitle` を入れる。決着の後のタイトル、試合中の Esc／Select で戻る |
| `Scripts/FEVersusAdapter.cs` | 対戦ルール：1 回目の `onTitle` で `StartMatch()`、2 回目から戻る。**設定はまだ入らない**（`ApplyRules` の相談中。入れる場所は TODO にしてある） |
| `Scripts/FEDevLauncher.cs` | 【仮】確かめるための IMGUI の起動画面。FE_Title／FE_ModeSelect ができたら消す |
| `Scripts/Editor/FEDevMenu.cs` | 【仮】Tools > Pinball Frontend > Create Dev Launcher Scene／Add Missing Scenes To Build Settings |

- コンパイル：Editor の外で .NET SDK の Roslyn を使い、本編の Assembly-CSharp.dll を参照してコンパイルした。エラーは 0 件。Unity の Editor でもエラーなくコンパイルできた。
- 動かしての確認（2026-11-24、仮の起動シーンから）：
  - ドッジボール：試合に入れた（例外なし）。試合中の Esc で起動画面に戻れた。CPU の強さを変えて、もう一度入れた
  - 対戦ルール：本編のタイトルを出さずに試合に入れた。試合中の Esc で起動画面に戻れた。ApplyRules がまだ無いという警告は想定どおり出た（選んだ設定は入らない）
  - **まだ**：ドッジボールの設定（外野／シュートポスト／フリッパー）が試合に反映されるか、両方のルールで決着の後の Esc で戻れるか
  - 戻るときはログが出ないので、どこまで進んだか追いにくかった。`FESession.ReturnToMenu` に「FE に戻る」のログを足した
- Build Settings：Add Missing Scenes は、今の並び（対戦ルールが 0 番）を変えずに後ろに足すだけ。本番の並びは FEBuildMenu で行う。

### あとで直すこと
- FE で Space を押してドッジボールに入ると、P1 のサーブのため（Space）が始まることがある。決定のキーとサーブのキーが同じため。FE の決定を Enter／パッドの South にするか、本編に入ってから数フレーム入力を待つかを、画面を作るときに決める。
- どちらのルールでも、本編のタイトルへ戻ったフレームだけ、本編のタイトルが 1 回描かれる（シーンの切り替えはフレームの終わりのため）。

## 12. 作ったもの（2026-11-24、10 章の 2）

| ファイル | 中身 |
|---|---|
| `Scripts/FEConfig.cs` | ScriptableObject。ゲームの表示名、FE と本編のシーンの場所（パス） |
| `Scripts/Editor/FEBuildMenu.cs` | Tools > Pinball Frontend > **Select Config**（`Data/FEConfig.asset` を開く。無ければ作る）／**Apply Build Settings**／**Build Windows (x64)** |
| `Scripts/FEDevLauncher.cs`（変更） | `config` を入れておくと、本編シーンの場所を FEConfig から読む |

- **Apply Build Settings**：FEConfig の順番（FE_Title → FE_ModeSelect → 対戦 → ドッジボール）で並べ直し、すぐディスクに書き出す。FE_Title がまだ無いあいだは、【仮】の起動シーンを 0 番にする。無いシーンは飛ばして、確認の画面に書く。
- **Build Windows (x64)**：並べ直してから `Builds/Windows/<Product Name>.exe` にビルドする（プロジェクトの直下。Assets の外）。ビルドの前に確認の画面を出す。
  - 本編が `Shader.Find` で探す `Sprites/Default` と `Universal Render Pipeline/Lit` が、ビルドに入るかどうかも確かめて、確認の画面に書く（Always Included Shaders に入っているか、ビルドに入るシーンのマテリアルが使っているか）。直すのは自分でする（設定は変えない）。
- コンパイル：Editor の外でエラー 0 件。**Unity の Editor でのコンパイルと、メニューを動かしての確認はまだ。**

### ビルドで見ておくこと
- 両方のルールに入れて、戻ってこられるか（Editor で確かめたのと同じこと）
- ドッジボールの日本語（DBHud は IMGUI の既定フォント。ビルドすると四角になるかもしれない）
- 対戦ルールの操作説明（ControlsGuide）の画像。Resources から名前で読んでいる
- 本編の線や光（PowerShot、DBBall のトレイル、DBBoardIndicators）が出るか。`Sprites/Default` が落ちると出ない

### 1 回目のビルド（2026-11-24 15:28〜15:31）
- Build Profiles の画面の Build And Run でビルドされた（FEBuildMenu ではない）。出力先は `D:\hoby\pinball\build\pinball.exe`。結果は成功（114.7 MB）。
- **入ったシーンは対戦ルールの 1 つだけ**（`pinball_Data` に level0 しかない）。ディスク上の Build Settings が対戦ルールの 1 つのままだったため。FE とドッジボールは入っていないので、FE の確認にはなっていない。
- Player.log：対戦ルールは起動した。Main Camera のスクリプトが見つからないという警告が出た。これは本編シーンにもともとある問題で、本編に相談する（[本編への相談_GameManager.md](本編への相談_GameManager.md) の 5 章の 4）。
- 次：Tools > Pinball Frontend > Build Windows (x64) でビルドし直す（Build Settings を並べ直してからビルドする）。

### 2 回目のビルド（2026-11-24 15:37、FEBuildMenu）
- `Builds/Windows/pinball.exe`、115.0 MB、成功（警告 1 件。どの警告かは Editor.log では見つけられなかった）。
- Build Settings：0 = 【仮】起動シーン、1 = 対戦、2 = ドッジボール。ディスク（`ProjectSettings/EditorBuildSettings.asset`）にも書き出された。`Data/FEConfig.asset` もできた。
- Player.log：起動画面 → 対戦ルール（ApplyRules が無いという警告は想定どおり）→ 「FE に戻る」→ 起動画面、まで動いた。例外なし。Main Camera の警告は本編の問題（相談中）。
- ドッジボール（15:40）：起動画面 → ドッジボール → 「FE に戻る」→ 起動画面、まで動いた。例外なし。
- 目で見て確かめた（2026-11-24）：ドッジボールの日本語、トレイルや盤面の線、操作説明の画像は、どれもビルドで出ていた。

## 13. 作ったもの（2026-11-24、10 章の 3）

| ファイル | 中身 |
|---|---|
| `Scripts/FEUI.cs` | 画面を組み立てる道具（Canvas、文字、ボタン、色、選び直し、もどるの入力） |
| `Scripts/FEOptionRow.cs` | 「項目名　◀ 値 ▶」の 1 行（Selectable）。左右・クリックで値を変える |
| `Scripts/FETitleScreen.cs` | FE_Title：ゲームの名前、「スタート」「終了」 |
| `Scripts/FEModeSelectScreen.cs` | FE_ModeSelect：2 章の項目。ルールとモードに合わせて行を隠す。「スタート」「もどる」、Esc／B でタイトルへ |
| `Scripts/FEMatchSettings.cs`（変更） | `LoadLast()`／`SaveAsLast()`（PlayerPrefs）、`Validate()` |
| `Scripts/FESession.cs`（変更） | FE のシーン同士の移動 `LoadScene()` |
| `Scripts/Editor/FESceneMenu.cs` | Tools > Pinball Frontend > **Create FE Scenes**：2 つのシーンを作り、Build Settings を並べ直す |

- 画面の中身は、実行時にコードで作る（本編の PinballHud や ControlsGuide と同じやり方）。シーンにあるのはカメラ、EventSystem、画面の部品だけ。
- 入力は `InputSystemUIInputModule` の既定のアクション（キーボード、パッド、マウス）。決定は Enter／パッドの A なので、ドッジボールのサーブ（Space／パッドの B）とはぶつからない（11 章の「あとで直すこと」の 1 つめは、マウスかこのキーで決定すれば起きない）。
- フォントは BIZ UDP ゴシックの SDF（Dynamic）。
- 9 章の決めてほしいことは、案のまま進めた：2 = 前回の設定を残す、3 = ドッジボールの項目は全部出す、4 = ゲームの表示名は仮に「PINBALL」（`FEConfig.gameTitle` で変えられる）。
- ピンボール対戦を選んでいる間は「設定がまだ反映されない」と出す（`showVersusNotAppliedNote`。ApplyRules が入ったら OFF にする）。
- コンパイル：Editor の外でエラー 0 件。Unity の Editor でもエラーなし。
- 動かしての確認（2026-11-24 15:48）：Create FE Scenes で 2 つのシーンができ、Build Settings は FE_Title → FE_ModeSelect → 対戦 → ドッジボールになった（ディスクにも書き出された）。再生して、タイトル → モード選択 → 対戦ルール → Esc → モード選択、まで動いた（例外なし。ApplyRules の警告は想定どおり）。新しい画面からのドッジボール、前回の設定が残ること、日本語の表示、キーボード・パッド・マウスでの操作、行の出し分けも、どれも問題なかった（2026-11-24、目で見て確かめた）。
- 【仮】の起動シーン（FE_DevLauncher）は、FE_Title ができると Build Settings から外れる。新しい画面で確かめられたら、`FEDevLauncher.cs`、`FEDevMenu.cs`、シーンを消す。
- 【仮】の起動画面は消した（2026-11-24）：`FEDevLauncher.cs`、`Editor/FEDevMenu.cs`、`Scenes/FE_DevLauncher.unity`。`FEConfig.devLauncherScenePath` と、FEBuildMenu の「FE_Title が無いときは【仮】を最初にする」処理も外した。コンパイルはエラー 0 件。

### 残っていること（10 章の 4 と、その先）
- 見た目の仕上げ（今は決まった色と文字だけ）
- ゲームの表示名と Product Name を決める（今は仮に「PINBALL」と `pinball`）
- `GameManager.ApplyRules` の相談の返事を待つ。入ったら `FEVersusAdapter` の TODO を 1 行に置き換え、`showVersusNotAppliedNote` を OFF にする
- 新しい画面を入れた状態で、もう一度 Windows 向けにビルドして確かめる

## 14. 操作をそろえた（2026-11-24）
ドッジボールの操作を、対戦ルールの操作にそろえた。決めたことと直したファイルは [本編への相談_ドッジボールの操作.md](本編への相談_ドッジボールの操作.md) の 6 章。FE 側では、対戦ルールでもパッドの Select で戻れるようにした（`FEVersusAdapter`）。

## 15. 見た目を整えた（2026-11-24）

方針（`FEUI.cs` の先頭にも書いた）：
- 背景は暗い紺のグラデーション。左下に P1 の青、右上に P2 の赤の、ぼんやりした光（本編の GameManager の P1 / P2 の色と同じ）
- 選んでいる行・ボタンは金色（本編の DBHud と同じ #ffd84a）。左に細い帯が出て、文字も金色になる（`FESelectAccent.cs`）
- 行とボタンは角丸の半透明の板。設定の行は 1 枚の板の中にまとめた
- 角丸・グラデーション・光の画像は、実行時にコードで作る（アセットは増やしていない）
- 画面を開くとき、ふわっと出す（0.25〜0.35 秒）

| 画面 | 変えたところ |
|---|---|
| タイトル | 大きな題字（金色のにじみ）と下線、2 つのルール名（P1 / P2 の色の点つき）、角丸のボタン、下の帯に操作の案内と版（`ver 0.1.0`） |
| モード選択 | 「MODE SELECT」の小見出しと見出し、設定の行を 1 枚の板に、ルールの説明（色の点つき）、スタートを太字に、下の帯に操作の案内 |
| 共通 | 操作の案内は、パッドをつないでいるかどうかで変わる（パッド = 十字キー／A／B、キーボード = ↑↓←→／Enter／Esc） |

- コンパイル：Editor の外で、本編を含む実行時のスクリプト 59 本、エラー 0 件。Unity で見て、見た目はこれでよいことになった（2026-11-24）。
- シーンを作り直す必要はない（画面の中身は実行時に作るので、スクリプトを直せばそのまま変わる）。
- モード選択の上下の移り先を決めた（2026-11-24）：パッドで最後の行から下へ行くと「もどる」が先に選ばれていたので、最後の行 → スタート、スタート ⇔ もどる は左右、どちらのボタンからも上は最後の行、にした（`FEModeSelectScreen.UpdateNavigation()`。行を出し分けるたびに作り直す）。パッドで確かめて、スタートが先に選ばれた。

## 16. 名前を決めた（2026-11-24）

| 項目 | 値 | 入れ先 |
|---|---|---|
| ゲームの表示名（タイトル画面の題字） | THE PINBALL | `Data/FEConfig.asset` の `gameTitle` |
| Product Name | Pinball_Versus | `FEConfig.productName` → Player Settings |
| Company Name | ICE | `FEConfig.companyName` → Player Settings |

- Product Name と Company Name は `FEConfig` に持たせ、Tools > Pinball Frontend > **Apply Build Settings** と **Build Windows (x64)** のときに Player Settings に入れる（`FEBuildMenu.ApplyPlayerSettings()`）。Unity が開いている間に `ProjectSettings.asset` を外から直すと、Unity の持っている値で上書きされるおそれがあるため、Unity の中から入れる形にした。
- ビルドの出力は `Builds/Windows/Pinball_Versus.exe` になる。
- 名前を変えると PlayerPrefs の保存場所も変わるので、モード選択の「前回の設定」は一度初期値に戻る。
- コンパイル：Editor の外で、実行時のスクリプト 63 本と FE の Editor スクリプト、エラー 0 件。**Unity での確認はまだ。**
- ビルドとテストプレイ（2026-11-24）：Build Windows (x64) で `Pinball_Versus.exe` をビルドし、数回テストプレイした。Player.log（`AppData\LocalLow\ICE\Pinball_Versus\`）に例外なし。

## 17. リポジトリ（2026-11-27）
GitHub の非公開リポジトリ＋Git LFS で管理する。手順と 2 人で作業するときの決まりは、プロジェクト直下の `README.md`。`git init`、`.gitignore`、`.gitattributes`、LFS、UnityYAMLMerge の設定まで済み。**最初のコミットと、GitHub への送り出しはまだ。**
