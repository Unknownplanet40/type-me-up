#!/usr/bin/env python3
"""TypeForge asset pipeline: raw AI-generated PNGs -> app-ready assets.

Usage:  python tools/assets/build_assets.py <raw_dir> <out_dir>
Needs:  pip install pillow numpy
"""
from __future__ import annotations

import argparse
import io
import struct
import sys
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

BG = (13, 13, 13)          # #0D0D0D canvas
ALPHA_KNEE = 22            # channels at/below this are treated as background (raw bg noise is 11-18)


@dataclass(frozen=True)
class RawNames:
    icon: str = "typeforge-icon-master.png"
    lockup: str = "typeforge-logo-lockup.png"
    splash: str = "typeforge-splash.png"
    hero: str = "hero-welcome.png"
    stats: str = "empty-stats.png"
    weak: str = "empty-weak-keys.png"
    tests: str = "empty-test-history.png"
    pause: str = "break-reminder.png"


def load_rgb(path: Path) -> Image.Image:
    if not path.is_file():
        raise FileNotFoundError(f"Missing raw asset: {path}")
    return Image.open(path).convert("RGB")


# ---------- background cleanup + keying (illustrations) ----------

def key_to_alpha(img: Image.Image) -> Image.Image:
    """Dark-theme transparent PNG: alpha from brightness, colour un-premultiplied vs knee."""
    p = np.asarray(img, dtype=np.float32)
    m = p.max(axis=2)
    alpha = np.clip((m - ALPHA_KNEE) / (255.0 - ALPHA_KNEE), 0.0, 1.0)
    safe = np.maximum(alpha, 1e-4)[..., None]
    rgb = np.clip((p - ALPHA_KNEE * (1.0 - alpha[..., None])) / safe, 0, 255)
    rgb[alpha < 1e-4] = 0
    rgba = np.dstack([rgb, alpha * 255.0]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def autocrop(img: Image.Image, pad: int) -> Image.Image:
    box = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    if box is None:
        return img
    l, t, r, b = box
    return img.crop((max(l - pad, 0), max(t - pad, 0), min(r + pad, img.width), min(b + pad, img.height)))


def resize_to_width(img: Image.Image, width: int) -> Image.Image:
    height = round(img.height * width / img.width)
    return img.resize((width, height), Image.LANCZOS)


def save_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, optimize=True)


# ---------- icon ----------

def fullbleed_icon(master: Image.Image) -> Image.Image:
    """Master has white corners baked in. Make it a flat full-bleed #0D0D0D square."""
    return Image.composite(master, Image.new("RGB", master.size, BG), rounded_mask(master)).convert("RGB")


def rounded_mask(master: Image.Image) -> Image.Image:
    a = np.asarray(master, dtype=np.uint8)
    first_dark = int(np.argmax(a[0].max(axis=1) < 128))
    radius = max(first_dark, 1)
    mask = Image.new("L", master.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((2, 2, master.width - 3, master.height - 3), radius=radius, fill=255)
    return mask


def mark_boxes(flat: Image.Image) -> tuple[tuple[int, int, int, int], tuple[int, int, int, int]]:
    """Return (full mark box, keycap-only box) measured from the flat icon."""
    a = np.asarray(flat, dtype=np.uint8).max(axis=2)
    inner = a[150:874, 150:874] > 30
    ys, xs = np.where(inner)
    l, r, t, b = xs.min() + 150, xs.max() + 150, ys.min() + 150, ys.max() + 150
    full = (int(l), int(t), int(r), int(b))
    # Keycap-only: measure on the left of the spark (spark starts at x~665) and below it, so no spark pixels leak in.
    cap_cols = np.where((a[420:700, 150:874] > 30).any(axis=0))[0] + 150
    cap_rows = np.where((a[300:700, int(cap_cols.min()):int(cap_cols.min()) + 300] > 30).any(axis=1))[0] + 300
    cap = (int(cap_cols.min()), int(cap_rows.min()), int(cap_cols.max()), int(cap_rows.max()))
    return full, cap


def icon_at(flat: Image.Image, size: int, boxes) -> Image.Image:
    full, cap = boxes
    box, fill = (cap, 0.80) if size <= 24 else (full, 0.78 if size <= 48 else 0.58)
    l, t, r, b = box
    side = max(r - l, b - t)
    cx, cy = (l + r) / 2, (t + b) / 2
    crop = flat.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
    canvas = Image.new("RGB", (size, size), BG)
    inner = max(1, round(size * fill))
    canvas.paste(crop.resize((inner, inner), Image.LANCZOS), ((size - inner) // 2, (size - inner) // 2))
    return canvas


def round_corners(img: Image.Image, ratio: float = 0.16) -> Image.Image:
    """Rounded-square alpha (matches the master tile's ~16% radius). Used for ICO + unplated PNGs."""
    size = img.width
    big = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(big).rounded_rectangle((0, 0, size * 4 - 1, size * 4 - 1), radius=round(size * ratio * 4), fill=255)
    out = img.convert("RGBA")
    out.putalpha(big.resize((size, size), Image.LANCZOS))
    return out


def write_ico(frames: dict[int, Image.Image], path: Path) -> None:
    blobs = []
    for size in sorted(frames):
        buf = io.BytesIO()
        frames[size].convert("RGBA").save(buf, format="PNG")
        blobs.append((size, buf.getvalue()))
    header = struct.pack("<HHH", 0, 1, len(blobs))
    offset = 6 + 16 * len(blobs)
    entries, data = b"", b""
    for size, blob in blobs:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset + len(data))
        data += blob
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(header + entries + data)


def build_icons(raw: Path, out: Path, names: RawNames) -> None:
    flat = fullbleed_icon(load_rgb(raw / names.icon))
    boxes = mark_boxes(flat)
    print("mark boxes (full, cap):", boxes)

    ico_sizes = (16, 24, 32, 48, 64, 128, 256)
    write_ico({s: round_corners(icon_at(flat, s, boxes)) for s in ico_sizes}, out / "Assets" / "TypeForge.ico")
    save_png(flat.resize((1024, 1024), Image.LANCZOS), out / "Assets" / "Branding" / "typeforge-icon-1024.png")

    assets = out / "Assets"
    for s in (16, 24, 32, 48, 256):
        save_png(round_corners(icon_at(flat, s, boxes)), assets / f"Square44x44Logo.targetsize-{s}_altform-unplated.png")
    save_png(icon_at(flat, 88, boxes), assets / "Square44x44Logo.scale-200.png")
    save_png(icon_at(flat, 300, boxes), assets / "Square150x150Logo.scale-200.png")
    save_png(icon_at(flat, 100, boxes), assets / "StoreLogo.scale-200.png")
    save_png(icon_at(flat, 50, boxes), assets / "StoreLogo.png")
    save_png(icon_at(flat, 48, boxes), assets / "LockScreenLogo.scale-200.png")


def build_splash(raw: Path, out: Path, names: RawNames) -> None:
    img = load_rgb(raw / names.splash)
    target_ratio = 620 / 300
    h = round(img.width / target_ratio)
    top = (img.height - h) // 2
    wide = img.crop((0, top, img.width, top + h))
    assets = out / "Assets"
    save_png(wide.resize((620, 300), Image.LANCZOS), assets / "Wide310x150Logo.scale-200.png")
    save_png(wide.resize((1240, 600), Image.LANCZOS), assets / "SplashScreen.scale-200.png")


# ---------- illustrations ----------

def build_illustrations(raw: Path, out: Path, names: RawNames) -> None:
    ill = out / "Assets" / "Illustrations"
    jobs = {
        names.hero: ("hero-welcome", (512, 1024)),            # 1x, 2x display widths (dp 512)
        names.stats: ("empty-stats", (320, 640, 960)),
        names.weak: ("empty-weak-keys", (320, 640, 960)),
        names.tests: ("empty-test-history", (320, 640, 960)),
        names.pause: ("break-reminder", (320, 640, 960)),
    }
    for src, (stem, widths) in jobs.items():
        keyed = key_to_alpha(load_rgb(raw / src))
        for width, scale in zip(widths, (100, 200, 300)):
            save_png(resize_to_width(keyed, width), ill / f"{stem}.scale-{scale}.png")

    lockup = autocrop(key_to_alpha(load_rgb(raw / names.lockup)), pad=24)
    for width, scale in zip((380, 760), (100, 200)):
        save_png(resize_to_width(lockup, width), out / "Assets" / "Branding" / f"typeforge-logo-lockup.scale-{scale}.png")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("raw_dir", type=Path)
    ap.add_argument("out_dir", type=Path, help="e.g. src/TypeForge.App")
    args = ap.parse_args()
    names = RawNames()
    build_icons(args.raw_dir, args.out_dir, names)
    build_splash(args.raw_dir, args.out_dir, names)
    build_illustrations(args.raw_dir, args.out_dir, names)
    print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
