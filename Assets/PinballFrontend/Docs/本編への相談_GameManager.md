# 本編への相談：GameManager にルールを入れる口を足したい

日付：2026-11-24　相談する人：パッケージング担当（FE）
対象：`Assets/Pinball/Scripts/GameManager.cs`（対戦ルール）
状態：**実施済み**（2026-11-24。結果は 7 章）

## 1. 何をしたいか

FE（タイトルとモード選択のシーン）で選んだ「モード／先取点／CPU の強さ」で、対戦ルールの試合を始めたい。

## 2. 今のコードで困るところ

- `StartMatch()` は、public の `mode`／`targetScore`／`cpuLevel` を読みません。private の `modeIndex`／`targetIndex`／`levelIndex` から値を作り直します（207〜209 行目）。
- その 3 つの index は、`Awake()` で public の項目から一度だけ作られます（144〜146 行目）。その後は、タイトル画面（OnGUI）でしか変わりません。
- FE が値を入れられるのは、シーンを読み込んだ後（`SceneManager.sceneLoaded`。Awake の後、Start の前）です。そのため、public の項目を書き換えても `StartMatch()` で上書きされてしまいます。

## 3. お願いしたい変更（案）

**public のメソッドを 1 つ足すだけ**です。今あるコードの動きは何も変わりません。

```csharp
        /// <summary>
        /// 外部（フロントエンドなど）から試合のルールを入れる。タイトル画面の選択状態も合わせる。
        /// 次の StartMatch() からこの値で始まる。
        /// targetScore が TargetOptions に無い値なら、Awake と同じく TargetOptions[0] になる
        /// </summary>
        public void ApplyRules(MatchMode newMode, int newTargetScore, CpuPlayer.Level newCpuLevel)
        {
            mode = newMode;
            targetScore = newTargetScore;
            cpuLevel = newCpuLevel;
            modeIndex = mode == MatchMode.VsCpu ? 0 : 1;
            targetIndex = Mathf.Max(0, System.Array.IndexOf(TargetOptions, targetScore));
            levelIndex = (int)cpuLevel;
        }
```

- **置き場所**：「外部から呼ばれる」の区切り（166 行目あたり）の中。
- **FE からの呼び方**：`sceneLoaded` で `ApplyRules(...)` を呼び、そのあと 1 回目の `onTitle` で `StartMatch()` を呼びます。本編のタイトル画面は描かれません。
- **今の動きへの影響**：ありません。足すだけで、誰も呼ばなければ今と同じです。

### よければ一緒に（任意）

`Awake()` の 144〜146 行目と、このメソッドの後半は同じ処理です。まとめると次のようになります。今の 3 行を書き換えることになるので、本編の判断にお任せします。

```csharp
            // Awake() の中
            ApplyRules(mode, targetScore, cpuLevel);
```

## 4. ほかの案と、選ばなかった理由

| 案 | 内容 | 選ばなかった理由 |
|---|---|---|
| `StartMatch()` が public の項目を読むように変える | index を使わない形にする | タイトル画面（OnGUI）の作りも変える必要があり、直すところが広くなる |
| `StartMatch(MatchMode, int, CpuPlayer.Level)` の重ね書きを足す | 引数つきで始める | 決着の後の再戦（Enter）は引数なしの `StartMatch()` を呼ぶ。そのとき値が戻ってしまわないよう、結局 index も合わせる必要がある |
| FE がリフレクションで private の index を書き換える | 本編は直さない | 本編で名前が変わると、黙って効かなくなる |

## 5. ついでに聞いておきたいこと（急ぎません）

| # | 内容 | 今の FE での扱い |
|---|---|---|
| 1 | ドッジボール（`DBGameManager`）には、試合中にタイトルへ戻る操作がありません。本編に足す予定はありますか | FE が Esc（パッドは Select）を読んで、FE のシーンに戻します |
| 2 | `DBGameManager` に、タイトルへ戻ったときのイベント（例：`event Action Returned`）を足してもらえますか | FE が毎フレーム `CurrentState` を見張ります。決着の後に戻るとき、DBHud のタイトルが 1 フレームだけ描かれます |
| 3 | `PinBall_DodgeMode.unity` の `skipTitle` がオンで保存されています。わざとですか | FE から入るときは FE が値を入れるので、関係ありません |
| 4 | `Pinball_VersusMode.unity` の Main Camera に、スクリプトが見つからないコンポーネントが 1 つあります（GUID `54ff06997df0a53408a4273538465992`）。ビルドを起動すると「The referenced script on this Behaviour (Game Object 'Main Camera') is missing!」が出ます。Assets にも PackageCache にも、2026-11-24 のバックアップにもない GUID なので、フォルダの組み直しより前からあったものです。外してよいですか | FE では触りません。警告が出るだけで、動きには影響していないようです |

## 6. 返事をもらったら

- **3 の案でよい場合**：本編側で足してもらうか、FE 担当が足すかを決めてもらいます。FE 担当が足すなら、差分はこの資料の 3 章のとおりです。
- **形を変えたい場合**：名前や引数は変えてかまいません。FE は `FEVersusAdapter` の 1 か所から呼ぶだけです。
- **足せない場合**：FE がリフレクションで index を書き換えます（FE設計.md 3.3 の案 B）。

## 7. 実施した結果（2026-11-24）

開発者が 1 人の間は、FE 担当が本編を直してよいことになったので、FE 担当が足した。

- `GameManager.ApplyRules(MatchMode newMode, int newTargetScore, CpuPlayer.Level newCpuLevel, int firstServer = -1)` を「外部から呼ばれる」の区切りに足した。中身は 3 章の案のとおり。
- 3 章の案に、**最初のサーブ番**の引数を 1 つ足した（あそびかたで「サーブは P1 から」にするため）。0 = P1、1 = P2、-1 = 今までどおり試合ごとにランダム。`StartMatch()` の `firstServer` を決める 1 行だけ変えた。
- `Awake()` の 3 行はそのまま（3 章の「よければ一緒に」はしていない）。
- FE 側：`FEVersusAdapter` が `ApplyRules` を呼ぶ（警告は消した）。`FEVersusTutorial` は最初のサーブ番を P1 にして入れ直す。モード選択の「設定がまだ反映されない」という注意書きは消した。
- コンパイル：Editor の外で、実行時のスクリプト 64 本と FE の Editor スクリプト、エラー 0 件。Unity で遊んで、設定が反映されることを確かめた（2026-11-24）。
