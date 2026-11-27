using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PinballUI
{
    /// <summary>
    /// HUD（左右パネル）と盤面上の表示の見た目・文言の設定。ゲーム本体（Assets/Pinball）とは別のアセット。
    /// Project ウィンドウで右クリック > Create > Pinball UI > HUD Theme でも作れる。
    /// </summary>
    [CreateAssetMenu(fileName = "PinballHudTheme", menuName = "Pinball UI/HUD Theme")]
    public class PinballHudTheme : ScriptableObject
    {
        [Header("文字")]
        [Tooltip("日本語が □ になる場合は、日本語フォントから作った TextMeshPro の Font Asset を入れる（README 参照）")]
        public TMP_FontAsset font;
        public float headerFontSize = 56f;
        public float badgeFontSize = 26f;
        public float statusFontSize = 26f;
        public float promptFontSize = 28f;
        public float keyFontSize = 26f;
        public float commonFontSize = 24f;
        public float bannerFontSize = 96f;
        public float bannerSubFontSize = 34f;
        public float boardFontSize = 28f;

        [Header("形")]
        [Tooltip("丸（HP の点・時間の円など）に使う画像。HUD 作成メニューで Unity 標準の丸が入る")]
        public Sprite circleSprite;

        [Header("色")]
        public Color p1Color = new Color(0.35f, 0.7f, 1f);
        public Color p2Color = new Color(1f, 0.45f, 0.4f);
        public Color panelColor = new Color(0f, 0f, 0f, 0.55f);
        public Color textColor = Color.white;
        public Color dimTextColor = new Color(1f, 1f, 1f, 0.45f);
        public Color keyColor = new Color(1f, 1f, 1f, 0.92f);
        public Color keyTextColor = new Color(0.08f, 0.08f, 0.12f);
        public Color routeColor = new Color(0.4f, 1f, 0.6f);
        public Color gaugeLowColor = new Color(1f, 0.75f, 0.25f);
        public Color gaugeOkColor = new Color(0.4f, 1f, 0.6f);
        public Color hpEmptyColor = new Color(1f, 1f, 1f, 0.18f);
        public Color warnColor = new Color(1f, 0.85f, 0.3f);

        [Header("名前")]
        public string p1Name = "P1";
        public string p2Name = "P2";
        public string cpuName = "CPU";

        [Header("文言：操作の案内（今押せるボタン）")]
        public string launchText = "長押し → 離して発射";
        public string routeText = "ルート切り替え（今：{0}）";
        public string routeOwn = "自陣";
        public string routeOpp = "相手陣地";
        public string moveText = "ブロッカーを動かす";
        public string flipperText = "フリッパー";
        public string bounceText = "跳ね返し";
        public string catchHoldText = "長押しでキャッチ";
        public string catchReleaseText = "離して発射";
        public string aimText = "左右で狙う";

        [Header("文言：状態")]
        public string serveBadge = "サーブ";
        public string waitServeText = "相手のサーブ";
        public string cpuStatus = "CPU（{0}）";
        public string downText = "ダウン {0:0.0}";
        public string firstToText = "{0}点先取";
        public string guideText = "F1  操作説明";
        public string backText = "Esc  タイトル";

        [Header("文言：中央の知らせ")]
        public string serveBanner = "{0} のサーブ";
        public string goalBanner = "{0} のゴール！";
        public string faultBanner = "フォルト";
        public string faultSub = "パワー不足。もう少し溜めよう";
        public string doubleFaultBanner = "ダブルフォルト";
        public string letBanner = "やり直し";
        public string winBanner = "{0} の勝ち！";
        public string winSub = "Enter  もう一度　／　Esc  タイトル";

        [Header("パワーショット")]
        public string powerLabel = "パワー";
        public string powerReadyText = "パワーショット OK！ フリッパーで打つと発動";
        public string powerReadyBadge = "MAX";
        public string powerShotText = "パワーショット！";
        [Tooltip("ゲージが溜まったときに盤面に出す文字（{0} = 量）")]
        public string powerChargeText = "+{0:0.#}";
        public Color powerReadyColor = new Color(1f, 0.85f, 0.25f);
        [Tooltip("パワーショットにフリッパーが押し負けたとき")]
        public string flipperBrokenText = "押し負け！";
        [Tooltip("パワーショットをタイミングよく打ち返したとき")]
        public string counterText = "カウンター！";
        public Color stunColor = new Color(1f, 0.3f, 0.25f);
        [Header("アウトレーン")]
        [Tooltip("開いている間、入口の横に出す文字")]
        public string outlaneOpenText = "アウトレーン開放！";
        [Tooltip("攻める側のパネルに出す一言（相手のアウトレーンが開いている）")]
        public string outlaneAttackText = "相手のアウトレーンが開いた！ 横を狙え";
        [Tooltip("守る側のパネルに出す一言（自分のアウトレーンが開いている）")]
        public string outlaneDefendText = "アウトレーンが開いている！";

        [Tooltip("同じ陣地のロールオーバーを全部点灯させたとき")]
        public string rolloverCompleteText = "ロールオーバー達成！";

        [Tooltip("パワーショットがブロッカーを壊したとき")]
        public string blockerBreakText = "ブレイク！";
        public Color powerBarBgColor = new Color(1f, 1f, 1f, 0.15f);
        [Tooltip("パネルのゲージの大きさ")]
        public Vector2 powerBarSize = new Vector2(220f, 16f);

        [Header("文言：盤面")]
        public string gaugeLabel = "パワー";
        public string routeHere = "ここから入る";
        public string railEntranceText = "近道 → 相手陣地";
        public string railIncomingText = "近道から来る！";

        [Header("盤面：届くパワーの目安（発射の速さ）")]
        [Tooltip("自陣ルートで届く発射の速さの目安（ゲージに線で表示）")]
        public float ownRouteNeedSpeed = 12.5f;
        [Tooltip("相手陣地ルートで届く発射の速さの目安")]
        public float oppRouteNeedSpeed = 15f;

        [Header("ボタン名の上書き（空なら Input Actions の割り当てを自動表示）")]
        [Tooltip("パッドに印字されている名前で表示する。初期値は L/R = フリッパー、A = サーブ・キャッチ、X = ルート・跳ね返し")]
        public List<KeyLabel> keyLabels = DefaultPadLabels();

        /// <summary>パッドの印字どおりの表示名（キーボードは空 = 自動）</summary>
        public static List<KeyLabel> DefaultPadLabels()
        {
            return new List<KeyLabel>
            {
                new KeyLabel { action = "FlipperTop", gamepad = "L" },
                new KeyLabel { action = "FlipperBottom", gamepad = "R" },
                new KeyLabel { action = "Plunger", gamepad = "A" },
                new KeyLabel { action = "BlockerCatch", gamepad = "A" },
                new KeyLabel { action = "RouteToggle", gamepad = "X" },
                new KeyLabel { action = "BlockerBounce", gamepad = "X" },
                new KeyLabel { action = "MoveBlockers", gamepad = "Lスティック" },
            };
        }

        // ステップ31：サーブ・キャッチ B → A、ルート・跳ね返し Y → X。前の初期値（B / Y）のまま保存されているテーマを直す
        [SerializeField, HideInInspector] int padLabelVersion = 0; // 保存されていない古いデータは 0

        void OnEnable() { MigratePadLabels(); }

        void MigratePadLabels()
        {
            if (padLabelVersion >= 31) return;
            if (keyLabels != null)
                foreach (var k in keyLabels)
                {
                    if (k == null) continue;
                    if ((k.action == "Plunger" || k.action == "BlockerCatch") && k.gamepad == "B") k.gamepad = "A";
                    if ((k.action == "RouteToggle" || k.action == "BlockerBounce") && k.gamepad == "Y") k.gamepad = "X";
                }
            padLabelVersion = 31;
        }

        /// <summary>パッドの表示名を初期値（L/R/A/X）で上書きする。キーボードの設定は残す</summary>
        public void ApplyDefaultPadLabels()
        {
            foreach (var d in DefaultPadLabels())
            {
                var k = keyLabels.Find(x => x != null && x.action == d.action);
                if (k == null) keyLabels.Add(d);
                else k.gamepad = d.gamepad;
            }
        }

        [Serializable]
        public class KeyLabel
        {
            [Tooltip("FlipperTop / FlipperBottom / Plunger / RouteToggle / MoveBlockers / BlockerBounce / BlockerCatch")]
            public string action;
            [Tooltip("パッドのときの表示（例: L、B、スティック）")]
            public string gamepad;
            [Tooltip("キーボードのときの表示")]
            public string keyboard;
        }

        public Color PlayerColor(int p) => p == 0 ? p1Color : p2Color;

        /// <summary>上書きがあればそれを返す。無ければ null</summary>
        public string OverrideLabel(string action, bool gamepad)
        {
            foreach (var k in keyLabels)
            {
                if (k == null || k.action != action) continue;
                string s = gamepad ? k.gamepad : k.keyboard;
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }
    }
}
