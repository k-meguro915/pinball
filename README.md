# リポジトリの手順（GitHub の非公開リポジトリ＋Git LFS）

作った日：2026-11-27　対象：`D:\hoby\pinball\pinball`（Unity 6000.5.10f1）
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

## 2. このあとすること（持ち主）

1. **GitHub に非公開のリポジトリを作る**（README などは付けない。空のまま）。
2. **最初のコミット**をする（中身は 1 章のとおり）。
3. **リポジトリを登録して送る**。
   ```
   git remote add origin https://github.com/<アカウント>/<リポジトリ名>.git
   git push -u origin main
   ```
   はじめて送るときは GitHub へのログインを聞かれる（Git Credential Manager の画面）。
4. **デザイナーを招待する**：リポジトリの Settings > Collaborators。
5. **作業前のバックアップ**（`D:\hoby\pinball\pinball_backup_20261124`）は、リポジトリに送れたことを確かめてから消してよい。

## 3. デザイナーの準備（1 回だけ）

1. **入れるもの**
   - Unity Hub と Unity **6000.5.10f1**（同じ版。版がちがうと、開いたときにアセットが書き換わる）
   - Git と Git LFS（Git for Windows には LFS が入っている）
   - 画面で操作したいなら GitHub Desktop など
2. **複製して、設定を 1 回入れる**（コマンドの場合）
   ```
   git lfs install
   git clone https://github.com/<アカウント>/<リポジトリ名>.git
   cd <リポジトリ名>
   git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   git config merge.unityyamlmerge.recursive binary
   ```
   Unity を入れた場所がちがうときは、`UnityYAMLMerge.exe` のパスを合わせる。
3. **Unity Hub で開く**：Add > 複製したフォルダ。はじめは `Library/` を作り直すので、開くまで時間がかかる。
4. 開いたら、`Assets/PinballFrontend/Scenes/FE_Title.unity` を開いて再生し、タイトル → モード選択 → 試合 → もどる、まで動くかを見る。

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
