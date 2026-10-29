"""生成 KeyMouse 流程编辑器的图标源图。

为什么自己画：仓库里没有产品图标（安装程序用的是默认的），而 Tauri 打包需要一套。
图形沿用界面里那枚标记的语义——"一列步骤"：朱砂底，纸上三条左对齐的横条，
从长到短，读起来就是"这条流程有三步"。整张图只用两个颜色，16px 下也不糊。

用法：
    python tools/build/make-icon.py            # 写 src/KeyMouse.EditorUI/src-tauri/icons/source.png
之后由 `pnpm tauri icon` 从这张源图生成 ico / png / icns 全套。
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 1024
SIGNAL = (194, 65, 12, 255)      # --kp-signal #C2410C
PAPER = (250, 247, 242, 255)     # --kp-paper  #FAF7F2

# 三条横条：(左边距, 顶边距, 宽, 高)，按 1024 的画布排，间距 148。
BARS = (
    (252, 300, 520, 96),
    (252, 448, 380, 96),
    (252, 596, 240, 96),
)
RADIUS_BAR = 48
RADIUS_TILE = 224


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    out = root / "src" / "KeyMouse.EditorUI" / "src-tauri" / "icons" / "source.png"
    out.parent.mkdir(parents=True, exist_ok=True)

    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, SIZE - 1, SIZE - 1), radius=RADIUS_TILE, fill=SIGNAL)
    for left, top, width, height in BARS:
        draw.rounded_rectangle(
            (left, top, left + width, top + height), radius=RADIUS_BAR, fill=PAPER
        )
    image.save(out, "PNG")
    print(f"icon source -> {out}  ({out.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
