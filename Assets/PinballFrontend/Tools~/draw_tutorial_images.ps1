# あそびかた（チュートリアル）の説明の画像を描く。Windows PowerShell 5.1 の System.Drawing（GDI+）だけを使う。
# 使い方（プロジェクトの Assets フォルダで）:
#   powershell -ExecutionPolicy Bypass -File PinballFrontend\Tools~\draw_tutorial_images.ps1
# 出力：Assets/PinballFrontend/UI/Tutorial/*.png（1600×900）
# 色と調子は、今ある操作説明の画像（ControlsGuide）に合わせてある。
# 方針：ボタンの名前（A／X など）は画像に焼き込まない。パッドの絵だけは、実物のボタンの位置（LB／RB／L スティック）を描く。
#       ボタン名は、右の説明の文（Input Actions から読む）で出す。
# このフォルダ（Tools~）は名前が ~ で終わるので、Unity は読み込まない。

param(
    [string]$AssetsDir = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$fontPath = Join-Path $AssetsDir "PinballUI\Fonts\BIZUDPGothic-Regular.ttf"
$outDir = Join-Path $AssetsDir "PinballFrontend\UI\Tutorial"
New-Item -ItemType Directory -Force $outDir | Out-Null

$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile($fontPath)
$family = $fonts.Families[0]

# ---- 色（今の画像から取った値） ----
function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
$BG      = C 18 26 52
$OWN     = C 30 46 82
$OPP     = C 60 36 52
$GOLD    = C 255 214 90
$WALL    = C 200 205 220
$PAD     = C 52 60 88
$PADDIM  = C 92 100 128
$BOX     = C 26 36 70
$BOXEDGE = C 52 64 104
$RED     = C 255 110 100
$BLUE    = C 90 170 255
$GREEN   = C 110 220 140
$ORANGE  = C 255 170 80
$WHITE   = C 235 238 245
$SUB     = C 160 168 190
$BALL    = C 235 238 245

# ---- 道具 ----
function New-Canvas {
    $bmp = New-Object System.Drawing.Bitmap 1600, 900
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear($BG)
    return @{ Bmp = $bmp; G = $g }
}

function Save-Canvas($c, [string]$name) {
    $path = Join-Path $outDir $name
    $c.G.Dispose()
    $c.Bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $c.Bmp.Dispose()
    Write-Host "wrote $path"
}

# 文字。align = Near / Center / Far。bold は少しずらして 2 回描いて太くする（フォントが Regular だけのため）
function Text($g, [string]$s, [float]$x, [float]$y, [float]$size, $color, [string]$align = "Near", [switch]$Bold) {
    $font = New-Object System.Drawing.Font($family, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush($color)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::$align
    $w = 1400
    $rx = switch ($align) { "Near" { $x } "Center" { $x - $w / 2 } "Far" { $x - $w } }
    $rect = New-Object System.Drawing.RectangleF($rx, $y, $w, ($size * 1.6))
    $g.DrawString($s, $font, $brush, $rect, $fmt)
    if ($Bold) {
        $rect2 = New-Object System.Drawing.RectangleF(($rx + 1.1), $y, $w, ($size * 1.6))
        $g.DrawString($s, $font, $brush, $rect2, $fmt)
    }
    $font.Dispose(); $brush.Dispose(); $fmt.Dispose()
}

function RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function FillRound($g, $x, $y, $w, $h, $r, $color) {
    $p = RoundPath $x $y $w $h $r; $b = New-Object System.Drawing.SolidBrush($color)
    $g.FillPath($b, $p); $b.Dispose(); $p.Dispose()
}
function StrokeRound($g, $x, $y, $w, $h, $r, $color, [float]$width = 2) {
    $p = RoundPath $x $y $w $h $r; $pen = New-Object System.Drawing.Pen($color, $width)
    $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
}
function FillRect($g, $x, $y, $w, $h, $color) {
    $b = New-Object System.Drawing.SolidBrush($color); $g.FillRectangle($b, [float]$x, [float]$y, [float]$w, [float]$h); $b.Dispose()
}
function FillCircle($g, $cx, $cy, $r, $color) {
    $b = New-Object System.Drawing.SolidBrush($color); $g.FillEllipse($b, [float]($cx - $r), [float]($cy - $r), [float]($r * 2), [float]($r * 2)); $b.Dispose()
}
function StrokeCircle($g, $cx, $cy, $r, $color, [float]$width = 2) {
    $pen = New-Object System.Drawing.Pen($color, $width); $g.DrawEllipse($pen, [float]($cx - $r), [float]($cy - $r), [float]($r * 2), [float]($r * 2)); $pen.Dispose()
}
function Line($g, $x1, $y1, $x2, $y2, $color, [float]$width = 2, [switch]$Dash) {
    $pen = New-Object System.Drawing.Pen($color, $width)
    if ($Dash) { $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash }
    $g.DrawLine($pen, [float]$x1, [float]$y1, [float]$x2, [float]$y2); $pen.Dispose()
}
# 折れ線の矢印（点の配列）。最後の点に矢じり
function Arrow($g, [float[]]$pts, $color, [float]$width = 4) {
    $pen = New-Object System.Drawing.Pen($color, $width)
    $pen.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap(4, 4)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $list = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt $pts.Length; $i += 2) { $list.Add((New-Object System.Drawing.PointF($pts[$i], $pts[$i + 1]))) }
    if ($list.Count -gt 2) { $g.DrawCurve($pen, $list.ToArray(), 0.4) } else { $g.DrawLine($pen, $list[0], $list[1]) }
    $pen.Dispose()
}
# 選手・ブロッカー（横長のカプセル）
function Capsule($g, $cx, $cy, $w, $h, $color) { FillRound $g ($cx - $w / 2) ($cy - $h / 2) $w $h ($h / 2) $color }

# ---- ゲームパッドの絵（LB・RB・L スティックを金色で目立たせる） ----
function Draw-Pad($g) {
    # 本体と握り
    FillCircle $g 590 560 120 $PAD
    FillCircle $g 1010 560 120 $PAD
    FillRound $g 470 300 660 260 120 $PAD
    # LB／RB
    FillRound $g 500 262 150 44 18 $GOLD
    FillRound $g 950 262 150 44 18 $GOLD
    Text $g "LB" 575 266 30 $BG "Center" -Bold
    Text $g "RB" 1025 266 30 $BG "Center" -Bold
    # L スティック（金）、右スティック・十字キー・顔のボタン（暗く）
    FillCircle $g 590 420 62 (C 40 46 70)
    FillCircle $g 590 420 46 $GOLD
    FillCircle $g 590 420 30 (C 232 192 70)
    FillCircle $g 910 520 50 (C 40 46 70)
    FillCircle $g 910 520 34 $PADDIM
    FillRect $g 650 506 82 26 $PADDIM
    FillRect $g 678 478 26 82 $PADDIM
    foreach ($b in @(@(1010, 368), @(958, 420), @(1062, 420), @(1010, 472))) { FillCircle $g $b[0] $b[1] 24 $PADDIM }
    FillRound $g 738 388 38 18 9 $PADDIM
    FillRound $g 824 388 38 18 9 $PADDIM
}

function Callout($g, [float]$tx, [float]$ty, [string]$head, [string[]]$lines, [string]$align, [float[]]$line) {
    Line $g $line[0] $line[1] $line[2] $line[3] $GOLD 2
    if ($line.Length -ge 6) { Line $g $line[2] $line[3] $line[4] $line[5] $GOLD 2 }
    FillCircle $g $line[$line.Length - 2] $line[$line.Length - 1] 5 $GOLD
    Text $g $head $tx $ty 36 $GOLD $align -Bold
    $y = $ty + 52
    foreach ($l in $lines) { Text $g $l $tx $y 25 $WHITE $align; $y += 34 }
}

function Draw-ControlsImage([string]$name, [string]$title, $lb, $rb, $stick, [string[]]$note) {
    $c = New-Canvas; $g = $c.G
    Text $g $title 800 36 52 $WHITE "Center" -Bold
    Text $g "P1・P2 とも同じ配置です（1 台目のパッド = P1、2 台目 = P2）" 800 108 26 $SUB "Center"
    Draw-Pad $g
    Callout $g 420 170 $lb.Head $lb.Lines "Far" @(430, 196, 575, 196, 575, 262)
    Callout $g 1150 170 $rb.Head $rb.Lines "Near" @(1140, 196, 1025, 196, 1025, 262)
    Callout $g 420 470 $stick.Head $stick.Lines "Far" @(430, 496, 470, 496, 528, 430)
    FillRound $g 60 730 1480 130 24 $BOX
    StrokeRound $g 60 730 1480 130 24 $BOXEDGE 2
    $y = 754
    foreach ($l in $note) { Text $g $l 100 $y 26 $WHITE "Near"; $y += 40 }
    Save-Canvas $c $name
}

# ================================================================
# 対戦ルール：操作
Draw-ControlsImage "Versus_Controls.png" "操作（ゲームパッド）" `
    @{ Head = "LB"; Lines = @("左のフリッパー", "サーブ前：ルート切り替え", "ラリー中：ブロッカーで跳ね返す") } `
    @{ Head = "RB"; Lines = @("右のフリッパー", "サーブ：長押しで溜めて離すと発射", "キャッチ：ラリー中に押したまま", "離すと発射") } `
    @{ Head = "L スティック"; Lines = @("ブロッカーを動かす", "キャッチ中は左右で狙う向き") } `
    @("フリッパーと、サーブ・キャッチ・跳ね返しは同じボタンです。", "押すと、フリッパーも一緒に動きます。")

# ドッジボール：操作
Draw-ControlsImage "Dodgeball_Controls.png" "操作（ゲームパッド）" `
    @{ Head = "LB"; Lines = @("左のフリッパー", "サーブ前：ルート切り替え") } `
    @{ Head = "RB"; Lines = @("右のフリッパー", "サーブ：長押しで溜めて離すと発射", "キャッチ：球が来たら押したまま", "離すと投げる") } `
    @{ Head = "L スティック"; Lines = @("選手を動かす", "投げる向き（左右）") } `
    @("球が来る直前に押してつかむと「ジャストキャッチ」。投げる球が速くなります。", "フリッパーと、サーブ・キャッチ・ルート切り替えは同じボタンです。")

# ================================================================
# サーブ（対戦ルール・ドッジボール共通の形。下の 2 行だけちがう）
function Draw-ServeImage([string]$name, [string[]]$topNote, [string]$fault2) {
    $c = New-Canvas; $g = $c.G
    # 盤面（相手の陣地：上、自分の陣地：下）
    FillRect $g 280 60 620 270 $OPP
    FillRect $g 280 330 620 530 $OWN
    Line $g 280 330 900 330 (C 200 205 220 160) 2 -Dash
    Text $g "相手の陣地" 300 292 26 $RED
    Text $g "自分の陣地" 300 338 26 $BLUE
    # 外壁（切れ目が 2 つ）と、発射レーン
    FillRect $g 900 60 16 90 $WALL
    FillRect $g 900 240 16 320 $WALL
    FillRect $g 900 640 16 220 $WALL
    FillRect $g 1010 120 14 740 $WALL
    # ルートの矢印
    Arrow $g @(985, 550, 985, 230, 950, 186, 700, 196) $ORANGE 4
    Arrow $g @(968, 760, 968, 640, 940, 592, 700, 596) $GREEN 4
    Text $g "相手陣地ルート" 668 176 36 $ORANGE "Far" -Bold
    Text $g "相手の陣地に出る" 668 226 26 $SUB "Far"
    Text $g "自陣ルート" 668 576 36 $GREEN "Far" -Bold
    Text $g "自分の陣地に出る" 668 626 26 $SUB "Far"
    # 球とプランジャー（ボタン名は書かない）
    FillCircle $g 968 765 13 $BALL
    FillRound $g 930 790 80 50 6 $GREEN
    # 溜めのゲージ
    StrokeRound $g 1060 520 30 320 2 $WALL 2
    FillRect $g 1064 620 22 216 $GOLD
    # 説明
    $y = 320
    foreach ($l in $topNote) { Text $g $l 1130 $y 30 $WHITE; $y += 44 }
    Text $g "長押しで溜めて" 1130 540 36 $GOLD -Bold
    Text $g "離すと発射" 1130 590 36 $GOLD -Bold
    Text $g "弱すぎて戻ると フォルト" 1130 744 30 $RED -Bold
    Text $g $fault2 1130 790 26 $SUB
    Save-Canvas $c $name
}

Draw-ServeImage "Versus_Serve.png" @("打つ前ならルートを", "切り替えられる") "2 回続くと相手に 1 点"
Draw-ServeImage "Dodgeball_Serve.png" @("サーブは試合の始めだけ", "打つ前ならルートを", "切り替えられる") "2 回続くとサーブが相手に移る"

# ================================================================
# 対戦ルール：ブロッカー（ボタン名を書かない形で描き直し）
$c = New-Canvas; $g = $c.G
FillRound $g 60 100 960 660 24 (C 30 44 84)
$pen = New-Object System.Drawing.Pen($WALL, 2); $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawRectangle($pen, 120, 170, 840, 520); $pen.Dispose()
Text $g "動ける範囲" 945 650 26 $SUB "Far"
# ブロッカー 2 つ（青）と HP
Capsule $g 440 470 160 44 $BLUE
Capsule $g 640 470 160 44 $BLUE
foreach ($x in @(410, 440, 470, 610, 640, 670)) { FillCircle $g $x 522 9 $GOLD }
Text $g "HP 3" 540 545 30 $GOLD "Center" -Bold
# 跳ね返す
FillCircle $g 470 330 14 $BALL
Arrow $g @(462, 252, 470, 312) (C 160 168 190) 3
Arrow $g @(475, 318, 430, 214) $GOLD 4
foreach ($a in @(-60, -35, -10, 15, 40)) {
    $r = [Math]::PI * $a / 180
    Line $g (440 + 44 * [Math]::Sin($r)) (440 - 30 * [Math]::Cos($r)) (440 + 60 * [Math]::Sin($r)) (440 - 42 * [Math]::Cos($r)) $GOLD 3
}
Text $g "跳ね返す" 150 190 36 $GOLD -Bold
Text $g "当たる直前に押す" 150 238 26 $SUB
# キャッチ
FillCircle $g 660 432 16 $BALL
Arrow $g @(660, 408, 700, 262) $GREEN 4
Text $g "キャッチ" 712 222 36 $GREEN -Bold
Text $g "押している間つかむ" 712 270 24 $SUB
Text $g "離して発射" 712 302 24 $SUB
Text $g "（左右で狙う）" 712 334 24 $SUB
# スティック
FillCircle $g 230 610 30 (C 92 100 128)
Arrow $g @(198, 610, 168, 610) $WHITE 3
Arrow $g @(262, 610, 292, 610) $WHITE 3
Arrow $g @(230, 578, 230, 548) $WHITE 3
Arrow $g @(230, 642, 230, 672) $WHITE 3
Text $g "スティックで移動" 310 590 32 $WHITE -Bold
# HP とダウン、すり抜け、アウトレーン
Text $g "強い球が当たると HP -1" 1110 150 34 $WHITE -Bold
Text $g "HP 0 で 4 秒ダウン" 1110 198 34 $RED -Bold
FillRound $g 1130 268 160 44 22 (C 50 56 80)
StrokeRound $g 1130 268 160 44 22 (C 92 100 128) 2
Text $g "→ すり抜ける" 1310 274 26 $SUB
FillRect $g 1390 420 16 420 $WALL
FillRect $g 1406 420 74 420 (C 92 40 48)
FillRect $g 1480 360 16 480 $WALL
Arrow $g @(1443, 440, 1443, 820) $RED 4
Text $g "ダウン中は" 1370 518 28 $WHITE "Far" -Bold
Text $g "同じ側のアウトレーンが開く" 1370 558 28 $WHITE "Far" -Bold
Text $g "入ると失点" 1370 598 28 $RED "Far" -Bold
Text $g "自分の打った球は" 1120 688 24 $SUB
Text $g "自分のブロッカーを" 1120 720 24 $SUB
Text $g "すり抜ける" 1120 752 24 $SUB
Save-Canvas $c "Versus_Blocker.png"

# ================================================================
# ドッジボール：ルール
$c = New-Canvas; $g = $c.G
$L = 610; $R = 990; $T = 60; $B = 840; $M = 450
FillRect $g $L $T ($R - $L) ($M - $T) $OPP
FillRect $g $L $M ($R - $L) ($B - $M) $OWN
Line $g $L $M $R $M (C 200 205 220 160) 2 -Dash
FillRect $g $L ($T - 12) ($R - $L) 12 $RED
FillRect $g $L $B ($R - $L) 12 $BLUE
$pen = New-Object System.Drawing.Pen($WALL, 3); $g.DrawRectangle($pen, $L, $T, ($R - $L), ($B - $T)); $pen.Dispose()
# 選手（内野）3 人ずつ。P2 の 1 人はアウト（暗く）
Capsule $g 700 190 90 28 $RED
Capsule $g 800 160 90 28 (C 120 70 70)
Capsule $g 900 190 90 28 $RED
Capsule $g 700 712 90 28 $BLUE
Capsule $g 800 742 90 28 $BLUE
Capsule $g 900 712 90 28 $BLUE
# 外野：アウトになった P2 の選手は、P1 の陣地の左右（選手の範囲のすぐ外）に出る
Capsule $g 640 600 28 90 $RED
# 球（P1 の色 = P1 の球）と、当たってアウト
Arrow $g @(800, 712, 780, 470, 805, 190) $BLUE 4
FillCircle $g 806 176 13 $BLUE
foreach ($a in 0..7) {
    $r = [Math]::PI * $a / 4
    Line $g (806 + 22 * [Math]::Cos($r)) (176 + 22 * [Math]::Sin($r)) (806 + 36 * [Math]::Cos($r)) (176 + 36 * [Math]::Sin($r)) $GOLD 3
}
Text $g "アウト！" 852 120 30 $GOLD -Bold
# 左の説明
Text $g "P2 / CPU の陣地" 570 220 34 $RED "Far" -Bold
Text $g "（上・赤）" 570 264 26 $SUB "Far"
Text $g "P1 の陣地" 570 650 34 $BLUE "Far" -Bold
Text $g "（下・青）" 570 694 26 $SUB "Far"
Text $g "外野" 570 560 30 $RED "Far" -Bold
Line $g 580 584 622 600 $RED 2
Text $g "アウトになった選手は、" 570 470 24 $SUB "Far"
Text $g "相手の陣地の左右で外野に" 570 502 24 $SUB "Far"
# 右の説明
Line $g 900 200 1030 200 $GOLD 2
Text $g "相手の選手 3 人を" 1040 150 36 $GOLD -Bold
Text $g "全員アウトにしたら勝ち" 1040 196 36 $GOLD -Bold
Text $g "相手の色の球に当たるとアウト" 1040 300 30 $WHITE -Bold
Text $g "球の色 = いま誰の球か" 1040 352 28 $WHITE
Text $g "（白 = だれの球でもない）" 1040 392 26 $SUB
Text $g "外野が投げた球で" 1040 560 30 $WHITE -Bold
Text $g "相手をアウトにすると" 1040 604 30 $WHITE -Bold
Text $g "外野から戻ってくる" 1040 648 30 $GREEN -Bold
Save-Canvas $c "Dodgeball_Rules.png"

$fonts.Dispose()
