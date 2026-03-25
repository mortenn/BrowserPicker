#!/usr/bin/env python3
"""Resize master PNG to app Resources/web_icon.png and build web_icon.ico. Requires: pip install Pillow"""
from __future__ import annotations

import struct
import sys
from io import BytesIO

try:
	from PIL import Image
except ImportError:
	print("Install Pillow: pip install Pillow", file=sys.stderr)
	sys.exit(1)


def _write_ico_png_images(path: str, images: list[Image.Image]) -> None:
	"""Write a multi-size Windows ICO with PNG-compressed frames (Vista+).

	Pillow's ICO saver derives a maximum size from the *first* image passed to
	``save()``, so starting with 16×16 drops every larger size. It also refuses
	sizes above 256×256. Building the ICO here matches Pillow's PNG-in-ICO layout.
	"""
	frames: list[tuple[int, int, bytes]] = []
	for im in images:
		rgba = im.convert("RGBA")
		w, h = rgba.size
		buf = BytesIO()
		rgba.save(buf, format="PNG")
		frames.append((w, h, buf.getvalue()))

	count = len(frames)
	offset = 6 + 16 * count
	entries: list[bytes] = []
	for w, h, png in frames:
		bw = w if w < 256 else 0
		bh = h if h < 256 else 0
		entries.append(
			struct.pack(
				"<BBBBHHII",
				bw,
				bh,
				0,
				0,
				0,
				32,
				len(png),
				offset,
			)
		)
		offset += len(png)

	with open(path, "wb") as fp:
		fp.write(struct.pack("<HHH", 0, 1, count))
		for e in entries:
			fp.write(e)
		for _, _, png in frames:
			fp.write(png)


def main() -> None:
	if len(sys.argv) != 4:
		print("Usage: postprocess_web_icon_png.py <master.png> <out.png> <out.ico>", file=sys.stderr)
		sys.exit(2)
	_, master_path, out_png, out_ico = sys.argv
	img = Image.open(master_path).convert("RGBA")
	img.resize((256, 256), Image.Resampling.LANCZOS).save(out_png, "PNG")
	ico_sizes = (16, 32, 64, 128, 256, 512)
	icons = [img.resize((s, s), Image.Resampling.LANCZOS) for s in ico_sizes]
	_write_ico_png_images(out_ico, icons)
	print(f"Wrote {out_png} and {out_ico}")


if __name__ == "__main__":
	main()
