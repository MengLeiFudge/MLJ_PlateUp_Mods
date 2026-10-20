from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont

# 用矢量图形和本机字体绘制工坊封面，缩小时保留清楚的标题与图标。
SIZE = 1024
SCALE = 3
OUT = Path(__file__).resolve().with_name("工坊封面.png")
FONT = "/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc"
image = Image.new("RGB", (SIZE * SCALE, SIZE * SCALE))
draw = ImageDraw.Draw(image)
navy = "#142F3D"
cream = "#FFF4DF"
mint = "#83DFC0"
coral = "#FF9273"
muted = "#A8C0C5"


def rect(box, fill, radius=0, outline=None, width=1):
    box = tuple(round(x * SCALE) for x in box)
    if radius:
        draw.rounded_rectangle(box, radius=radius * SCALE, fill=fill,
                               outline=outline, width=width * SCALE)
    else:
        draw.rectangle(box, fill=fill, outline=outline, width=width * SCALE)


def ellipse(box, fill, outline=None, width=1):
    draw.ellipse(tuple(round(x * SCALE) for x in box), fill=fill,
                 outline=outline, width=width * SCALE)


def line(points, fill, width=3):
    draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points],
              fill=fill, width=width * SCALE, joint="curve")


def text(value, xy, size, color=cream, anchor="lt"):
    font = ImageFont.truetype(FONT, size * SCALE, index=2)
    draw.text((xy[0] * SCALE, xy[1] * SCALE), value, fill=color,
              font=font, anchor=anchor)


# 深蓝底色与细网格。
for y in range(SIZE):
    t = y / (SIZE - 1)
    color = tuple(round(a + (b - a) * t) for a, b in zip((21, 48, 62), (12, 31, 43)))
    rect((0, y, SIZE, y + 1), color)
for x in range(24, SIZE, 48):
    for y in range(26, SIZE, 48):
        ellipse((x, y, x + 2, y + 2), "#2B4652")

text("PLATEUP!", (60, 43), 35)
text("COMMUNITY MOD", (272, 55), 17, muted)
rect((790, 41, 964, 87), coral, 23)
text("开发测试版", (877, 64), 22, navy, "mm")
line([(60, 111), (964, 111)], "#3B5360", 2)

text("自建服务器", (58, 145), 76)
text("联机中转", (52, 239), 136, coral)
text("游戏内填写 IP · 保留 Steam 邀请", (62, 406), 28, muted)

# 标题右侧的网络标志。
ellipse((716, 166, 964, 394), "#1D3E4C")
line([(774, 235), (891, 254), (821, 335), (774, 235)], mint, 7)
for x, y in ((774, 235), (891, 254), (821, 335)):
    ellipse((x - 19, y - 19, x + 19, y + 19), coral, navy, 5)

# 以餐盘作为房主与玩家节点，中间为服务器。
rect((52, 479, 972, 844), "#0E2531", 35, "#34505C", 2)
line([(272, 642), (380, 642)], "#2A5B5F", 17)
line([(644, 642), (752, 642)], "#2A5B5F", 17)
line([(272, 642), (380, 642)], mint, 6)
line([(644, 642), (752, 642)], mint, 6)
for x in (320, 694):
    line([(x - 9, 635), (x - 17, 642), (x - 9, 649)], cream, 4)
    line([(x + 9, 635), (x + 17, 642), (x + 9, 649)], cream, 4)


def plate(cx, cy):
    ellipse((cx - 93, cy - 79, cx + 93, cy + 107), "#081A24")
    ellipse((cx - 93, cy - 93, cx + 93, cy + 93), cream)
    ellipse((cx - 76, cy - 76, cx + 76, cy + 76), "#F3DFC2", coral, 4)
    ellipse((cx - 61, cy - 61, cx + 61, cy + 61), cream)
    ellipse((cx - 40, cy - 32, cx + 28, cy + 35), "#C87551")
    ellipse((cx - 33, cy - 25, cx + 20, cy + 25), coral)
    for shift in (-14, 2, 18):
        line([(cx - 23 + shift, cy - 15), (cx - 6 + shift, cy + 5)], "#A6533C", 4)
    ellipse((cx + 8, cy + 13, cx + 40, cy + 45), "#51B89C")
    ellipse((cx + 25, cy - 4, cx + 51, cy + 28), mint)
    ellipse((cx + 37, cy + 21, cx + 60, cy + 44), "#51B89C")
    # 小叉子与餐刀呼应烹饪主题。
    line([(cx - 112, cy - 14), (cx - 112, cy + 53)], mint, 6)
    for dx in (-9, 0, 9):
        line([(cx - 112 + dx, cy - 43), (cx - 112 + dx, cy - 16)], mint, 4)
    line([(cx - 121, cy - 16), (cx - 103, cy - 16)], mint, 4)
    rect((cx + 108, cy - 42, cx + 116, cy + 53), coral, 4)
    rect((cx + 104, cy - 42, cx + 116, cy + 1), coral, 5)


plate(184, 630)
plate(840, 630)
text("玩家", (184, 775), 30, cream, "mm")
text("房主", (840, 775), 30, cream, "mm")

# 服务器机箱与三个状态灯。
rect((383, 535, 653, 755), "#081A24", 22)
rect((374, 521, 644, 741), cream, 22)
rect((392, 542, 626, 594), navy, 10)
rect((392, 605, 626, 657), navy, 10)
rect((392, 668, 626, 720), navy, 10)
for y in (568, 631, 694):
    ellipse((410, y - 8, 426, y + 8), mint)
    line([(447, y), (507, y)], "#709093", 5)
    for x in (568, 584, 600):
        line([(x, y - 8), (x, y + 8)], cream, 3)
text("你的服务器", (510, 775), 30, mint, "mm")

# 功能提示保留在底部，缩略图仍以主标题为主。
rect((61, 890, 181, 951), mint, 14)
text("设置", (121, 919), 30, navy, "mm")
text("→ 游戏中转", (204, 899), 35)
text("SERVER RELAY", (961, 922), 22, muted, "rm")

image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
image.save(OUT, optimize=True)
print(OUT)
print(f"{image.width} × {image.height}; {OUT.stat().st_size:,} bytes")
