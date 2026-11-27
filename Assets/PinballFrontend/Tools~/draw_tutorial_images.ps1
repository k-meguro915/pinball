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

# 2026-11-27 の見直し：「文字だらけで読めない」という意見を受けて、画像の文字は大きな見出しの言葉だけにした。
#   灰色の補足や、文になった説明は描かない（くわしいことは、遊びながら覚えてもらう）。

function Callout($g, [float]$tx, [float]$ty, [string]$head, [string[]]$lines, [string]$align, [float[]]$line) {
    Line $g $line[0] $line[1] $line[2] $line[3] $GOLD 2
    if ($line.Length -ge 6) { Line $g $line[2] $line[3] $line[4] $line[5] $GOLD 2 }
    FillCircle $g $line[$line.Length - 2] $line[$line.Length - 1] 5 $GOLD
    Text $g $head $tx $ty 40 $GOLD $align -Bold
    $y = $ty + 58
    foreach ($l in $lines) { Text $g $l $tx $y 34 $WHITE $align -Bold; $y += 46 }
}

function Draw-ControlsImage([string]$name, $lb, $rb, $stick) {
    $c = New-Canvas; $g = $c.G
    Draw-Pad $g
    Callout $g 420 170 $lb.Head $lb.Lines "Far" @(430, 196, 575, 196, 575, 262)
    Callout $g 1150 170 $rb.Head $rb.Lines "Near" @(1140, 196, 1025, 196, 1025, 262)
    Callout $g 420 470 $stick.Head $stick.Lines "Far" @(430, 496, 470, 496, 528, 430)
    Text $g "P1・P2 同じ配置" 800 760 30 $SUB "Center"
    Save-Canvas $c $name
}

# ================================================================
# 対戦ルール：操作
Draw-ControlsImage "Versus_Controls.png" `
    @{ Head = "LB"; Lines = @("左フリッパー", "ルート切替・跳ね返し") } `
    @{ Head = "RB"; Lines = @("右フリッパー", "サーブ・キャッチ") } `
    @{ Head = "L スティック"; Lines = @("ブロッカー移動") }

# ドッジボール：操作
Draw-ControlsImage "Dodgeball_Controls.png" `
    @{ Head = "LB"; Lines = @("ルート切替") } `
    @{ Head = "RB"; Lines = @("サーブ・キャッチ", "離して投げる") } `
    @{ Head = "L スティック"; Lines = @("選手移動") }

# ================================================================
# サーブ（対戦ルール・ドッジボール共通の形。下の 1 行だけちがう）
function Draw-ServeImage([string]$name, [string]$faultNote) {
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
    Text $g "相手陣地へ" 668 172 44 $ORANGE "Far" -Bold
    Text $g "自陣へ" 668 572 44 $GREEN "Far" -Bold
    # 球とプランジャー（ボタン名は書かない）
    FillCircle $g 968 765 13 $BALL
    FillRound $g 930 790 80 50 6 $GREEN
    # 溜めのゲージ
    StrokeRound $g 1060 520 30 320 2 $WALL 2
    FillRect $g 1064 620 22 216 $GOLD
    # 説明（大きな言葉だけ）
    Text $g "溜めて" 1130 560 48 $GOLD -Bold
    Text $g "離す！" 1130 624 48 $GOLD -Bold
    Text $g "弱いと フォルト" 1130 760 36 $RED -Bold
    if ($faultNote) { Text $g $faultNote 1130 812 30 $SUB }
    Save-Canvas $c $name
}

Draw-ServeImage "Versus_Serve.png" "2 回で相手に 1 点"
Draw-ServeImage "Dodgeball_Serve.png" ""

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
Text $g "跳ね返す" 150 196 44 $GOLD -Bold
# キャッチ
FillCircle $g 660 432 16 $BALL
Arrow $g @(660, 408, 700, 262) $GREEN 4
Text $g "キャッチ" 712 226 44 $GREEN -Bold
# スティック
FillCircle $g 230 610 30 (C 92 100 128)
Arrow $g @(198, 610, 168, 610) $WHITE 3
Arrow $g @(262, 610, 292, 610) $WHITE 3
Arrow $g @(230, 578, 230, 548) $WHITE 3
Arrow $g @(230, 642, 230, 672) $WHITE 3
Text $g "移動" 310 586 40 $WHITE -Bold
# HP とダウン、アウトレーン
Text $g "HP 0 で ダウン" 1080 180 44 $RED -Bold
FillRect $g 1390 360 16 480 $WALL
FillRect $g 1406 360 74 480 (C 92 40 48)
FillRect $g 1480 300 16 540 $WALL
Arrow $g @(1443, 380, 1443, 820) $RED 4
Text $g "ダウン中は" 1360 520 36 $WHITE "Far" -Bold
Text $g "失点しやすい" 1360 572 36 $RED "Far" -Bold
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
Text $g "P2 / CPU" 570 220 40 $RED "Far" -Bold
Text $g "P1" 570 650 40 $BLUE "Far" -Bold
Text $g "外野" 570 556 36 $RED "Far" -Bold
Line $g 580 584 622 600 $RED 2
# 右の説明
Line $g 900 200 1030 200 $GOLD 2
Text $g "3 人アウトで" 1040 150 44 $GOLD -Bold
Text $g "勝ち！" 1040 206 44 $GOLD -Bold
Text $g "相手の色の球で" 1040 330 36 $WHITE -Bold
Text $g "アウト" 1040 380 36 $WHITE -Bold
Text $g "外野で当てると" 1040 580 36 $WHITE -Bold
Text $g "復活" 1040 630 36 $GREEN -Bold
Save-Canvas $c "Dodgeball_Rules.png"

# ================================================================
# 対戦ルール：ルール（もとは ControlsGuide の画像を写していた。文字を減らすため、ここで描き直す）
$c = New-Canvas; $g = $c.G
$L = 610; $R = 990; $T = 70; $B = 830; $M = 450
FillRect $g $L $T ($R - $L) ($M - $T) $OPP
FillRect $g $L $M ($R - $L) ($B - $M) $OWN
Line $g $L $M $R $M (C 200 205 220 160) 2 -Dash
FillRect $g $L ($T - 14) ($R - $L) 14 $RED
FillRect $g $L $B ($R - $L) 14 $BLUE
$pen = New-Object System.Drawing.Pen($WALL, 3); $g.DrawLine($pen, $L, $T, $L, $B); $g.DrawLine($pen, $R, $T, $R, $B); $pen.Dispose()
# フリッパー（上：P2、下：P1）
function Flipper($g, $cx, $cy, $up, $color) {
    $dy = if ($up) { -12 } else { 12 }
    $pen = New-Object System.Drawing.Pen($color, 14)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, [float]($cx - 60), [float]$cy, [float]($cx - 6), [float]($cy + $dy))
    $g.DrawLine($pen, [float]($cx + 60), [float]$cy, [float]($cx + 6), [float]($cy + $dy))
    $pen.Dispose()
}
Flipper $g 800 160 $false $RED
Flipper $g 800 740 $true $BLUE
# 球は、今いる側のゴールへ転がる（両側の矢印）
foreach ($x in @(650, 950)) {
    Arrow $g @($x, 420, $x, 300) (C 255 150 140) 3
    Arrow $g @($x, 480, $x, 600) (C 150 200 255) 3
}
# 球と、相手のゴールへの矢印
FillCircle $g 800 640 16 $BALL
Arrow $g @(800, 620, 850, 380, 800, 100) $GOLD 4
# 左の説明
Text $g "P2 / CPU" 570 220 40 $RED "Far" -Bold
Text $g "真ん中が山" 570 426 36 $WHITE "Far" -Bold
Text $g "P1" 570 610 40 $BLUE "Far" -Bold
# 右の説明
Line $g 880 200 1030 200 $GOLD 2
Text $g "相手のゴールに" 1040 150 44 $GOLD -Bold
Text $g "入れて 1 点" 1040 206 44 $GOLD -Bold
Text $g "3／5／10 点" 1040 396 44 $WHITE -Bold
Text $g "先取で勝ち" 1040 452 36 $WHITE -Bold
Line $g 870 740 1030 740 $SUB 2
Text $g "フリッパーで守る" 1040 716 36 $WHITE -Bold
Save-Canvas $c "Versus_Rules.png"

$fonts.Dispose()
