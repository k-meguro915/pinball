# リポジトリの手順（GitHub の非公開リポジトリ＋Git LFS）

作った日：2026-11-27（デザイナーの準備を 2026-11-27 に直した）　対象：`D:\hoby\pinball\pinball`（Unity 6000.5.10f1）
デザイナーが開発に加わるので、プロジェクトを Git で管理する。置き場所は GitHub の非公開リポジトリ。

## 1. 済んでいること（この PC）

| 項目 | 内容 |
|---|---|
| Unity の設定 | アセットの保存形式 = Force Text、`.meta` を見える設定（どちらも前から） |
| `.gitignore` | `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`Builds/`、IDE のファイル（`*.csproj`、`*.sln`、`.vs/` など）を入れない |
| `.gitattributes` | 画像・フォント・音・3D などは Git LFS。シーン・プレハブ・アセット・`.meta` は UnityYAMLMerge でまとめる。改行は Git にまかせる |
| `git init` | 枝の名前は `main` |
| Git LFS | このリポジトリだけに入れた（`git lfs install --local`） |
| UnityYAMLMerge | このリポジトリの設定に入れた（下の 3 章の 2 と同じもの） |
| 最初の登録 | 454 ファイル。そのうち LFS は 9 ファイル（説明の画像 7 枚、フォント 2 つ）。`.git` は約 7.5MB |

## 2. 持ち主がすること
| # | 内容 | 状況 |
|---|---|---|
| 1 | GitHub に非公開のリポジトリを作り、送る（`https://github.com/k-meguro915/pinball.git`、枝は `main`） | 済み（2026-11-27） |
| 2 | **デザイナーを招待する**：GitHub のリポジトリの Settings > Collaborators > Add people で、デザイナーの GitHub アカウントを入れる | これから |
| 3 | 作業前のバックアップ（`D:\hoby\pinball\pinball_backup_20261124`）は、送れたことを確かめたので消してよい | 持ち主が判断 |

## 3. デザイナーの準備（1 回だけ）

### 3.1 先に用意するもの
1. **GitHub のアカウント**（なければ作る）。持ち主に伝えて、リポジトリに招待してもらう。招待のメールが届いたら **Accept invitation** を押す（押さないと、リポジトリが見えない）。
2. **Unity Hub** と **Unity 6000.5.10f1**（同じ版。版がちがうと、開いたときにアセットが書き換わる）
   - Unity Hub > Installs > Install Editor > Archive タブ（または「Unity のダウンロードアーカイブ」のページ）から 6000.5.10f1 を選ぶ。
   - モジュールは、Windows で遊ぶだけなら追加なしでよい。ビルドもするなら「Windows Build Support (IL2CPP)」と、Visual Studio（C# を見るなら）。
   - Unity のライセンス（Personal で可）で Unity Hub にサインインしておく。
3. **Git for Windows**（Git LFS が一緒に入る）。画面で操作したいなら **GitHub Desktop** でもよい（LFS も扱える）。

### 3.2 複製する（どちらか）
**A. コマンド（PowerShell）**
```
git lfs install
git clone https://github.com/k-meguro915/pinball.git
cd pinball
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
- はじめての `git clone` で、GitHub へのサインインの画面が出る（Git Credential Manager）。招待されたアカウントでサインインする。
- Unity を入れた場所がちがうときは、`UnityYAMLMerge.exe` のパスを合わせる。

**B. GitHub Desktop**
1. File > Clone repository > GitHub.com タブで `k-meguro915/pinball` を選び、置き場所を決めて Clone。
2. 複製したフォルダで PowerShell を開き、上の A の `git config` の 3 行だけを打つ（シーンを 2 人で直したときに、まとめるための設定）。

### 3.3 Unity で開く
1. Unity Hub > Projects > Add（Add project from disk）で、複製したフォルダ（`Assets` と `ProjectSettings` が入っているフォルダ）を選ぶ。
2. はじめは `Library/` を作り直すので、開くまで数分かかる。版を聞かれたら 6000.5.10f1 を選ぶ（ちがう版で開かない）。
3. 開いたら、次を確かめる。
   - Console にエラー（赤）が出ていない。
   - 説明の画像（`Assets/PinballFrontend/UI/Tutorial/*.png`）が開ける。開けない・中身が文字だけのときは、LFS の中身が来ていない。フォルダで `git lfs pull` を実行する。

### 3.4 動くかを見る
1. `Assets/PinballFrontend/Scenes/FE_Title.unity` を開いて再生する。
2. タイトル →「スタート」→ モード選択 → 試合 →（Esc／パッドの B で）もどる、まで動くかを見る。
3. 「あそびかた」と、タイトルの「せってい」も開けるかを見る。
4. パッドは 2 つまで使える（1 台目が P1、2 台目が P2）。キーボードでも動く。

## 4. 2 人で作業するときの決まり

1. **ファイルを動かす・名前を変える・消すのは、Unity の Project ウィンドウだけで**する（エクスプローラーでやると `.meta` がずれて、参照が切れる）。
2. **`.meta` は必ず一緒にコミット**する（`.meta` だけ・本体だけのコミットはしない）。
3. **作業の前に最新を取り込む**（pull）。終わったら早めに送る（push）。
4. **シーン（`.unity`）とプレハブは、同時に 2 人で触らない**。触る前に声をかける。
   - 同じシーンを 2 人で直してしまったら、UnityYAMLMerge でまとめられることが多いが、まとめきれないこともある。
   - 本編の 2 つのシーン：`Assets/Scenes/Pinball_VersusMode.unity`、`Assets/Scenes/PinBall_DodgeMode.unity`
   - FE のシーン：`Assets/PinballFrontend/Scenes/FE_Title.unity`、`FE_ModeSelect.unity`
5. **画像や音を足すとき**：`.gitattributes` に書いてある種類は、自動で LFS に入る。新しい種類（例：`.webp`）を使うときは、先に `.gitattributes` に足す。
6. **Unity の版は上げない**（上げるときは 2 人で決めてから）。
7. 枝（branch）の使い方は、はじめは `main` に直接入れる。作業がぶつかるようになったら、作業ごとに枝を分ける。

## 5. FE の説明の画像について
`Assets/PinballFrontend/UI/Tutorial/*.png` は `Assets/PinballFrontend/Tools~/draw_tutorial_images.ps1` で描いている（`Tools~` は Unity が読み込まないフォルダ。リポジトリには入っている）。デザイナーが描き直す場合は、スクリプトを使わずに同じ名前で上書きしてよい（1600×900）。

## 6. よくあるつまずき
| 困ったこと | わけと直し方 |
|---|---|
| リポジトリが見つからない（404、Repository not found） | 招待をまだ受けていない、またはちがうアカウントでサインインしている。招待のメールの Accept invitation を押し、招待されたアカウントでサインインし直す |
| 画像が壊れている、ピンクのマテリアル、フォントがない | LFS の中身が来ていない。`git lfs install` のあと `git lfs pull` |
| Unity が「別の版で開く」と言う | 6000.5.10f1 を入れて、その版で開く。ちがう版で開いて保存しない |
| シーンを開くと、触っていないのに変更が出る | Unity の版がちがうか、パッケージの読み込みの途中。版を確かめ、開き直す。自分で直していない変更はコミットしない |
| 送ろうとしたら断られた（rejected） | 先にもう 1 人が送っている。`git pull` で取り込んでから送る |
| 同じシーンを 2 人で直して、ぶつかった（conflict） | 3.2 の `git config` を入れていれば、UnityYAMLMerge が自動でまとめる。まとまらないときは、どちらの版を残すか話して決める |
