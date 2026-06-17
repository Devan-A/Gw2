"""Generates the PNG assets used by the Item Helper Blish HUD module.

Pure-Python (zlib only) PNG writer + tiny supersampled drawer so we don't
need Pillow. Run from the repo root:  python3 tools/gen_assets.py
"""
import math
import os
import struct
import zlib

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "ItemHelper", "ref")


def write_png(path, width, height, pixels):
    """pixels: flat bytearray of RGBA, length width*height*4."""
    def chunk(typ, data):
        return (struct.pack(">I", len(data)) + typ + data +
                struct.pack(">I", zlib.crc32(typ + data) & 0xffffffff))

    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)  # filter type 0 (None)
        raw += pixels[y * stride:(y + 1) * stride]

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


def blend(dst, gx, gy, w, color):
    """Alpha-composite color (r,g,b,a) onto dst at (gx,gy)."""
    i = (gy * w + gx) * 4
    sr, sg, sb, sa = color
    a = sa / 255.0
    dst[i] = int(sr * a + dst[i] * (1 - a))
    dst[i + 1] = int(sg * a + dst[i + 1] * (1 - a))
    dst[i + 2] = int(sb * a + dst[i + 2] * (1 - a))
    dst[i + 3] = max(dst[i + 3], sa)


def gen_icon(path, size=128, ss=4):
    """A gold magnifying-glass glyph with a dark outline (corner icon)."""
    W = size * ss
    cov = bytearray(W * W)  # coverage mask for the glyph
    out = bytearray(W * W)  # coverage mask for the dark outline

    cx, cy = W * 0.42, W * 0.42
    ring_r = W * 0.26
    ring_t = W * 0.075          # ring half-thickness
    # handle from the ring's lower-right toward the corner
    hx0 = cx + math.cos(math.radians(45)) * ring_r
    hy0 = cy + math.sin(math.radians(45)) * ring_r
    hx1, hy1 = W * 0.82, W * 0.82
    handle_t = W * 0.06

    for y in range(W):
        for x in range(W):
            d_ring = abs(math.hypot(x - cx, y - cy) - ring_r)
            on_ring = d_ring <= ring_t
            # distance from point to handle segment
            vx, vy = hx1 - hx0, hy1 - hy0
            seg = vx * vx + vy * vy
            t = max(0.0, min(1.0, ((x - hx0) * vx + (y - hy0) * vy) / seg))
            px, py = hx0 + t * vx, hy0 + t * vy
            on_handle = math.hypot(x - px, y - py) <= handle_t
            if on_ring or on_handle:
                cov[y * W + x] = 255
            # outline = slightly fatter version
            if d_ring <= ring_t + W * 0.02 or math.hypot(x - px, y - py) <= handle_t + W * 0.02:
                out[y * W + x] = 255

    gold = (232, 198, 107)
    dark = (28, 22, 14)
    pix = bytearray(size * size * 4)
    for y in range(size):
        for x in range(size):
            ar = ag = ab = a_o = a_g = 0
            for dy in range(ss):
                for dx in range(ss):
                    gi = (y * ss + dy) * W + (x * ss + dx)
                    a_o += out[gi]
                    a_g += cov[gi]
            n = ss * ss
            a_o //= n
            a_g //= n
            i = (y * size + x) * 4
            # paint outline first, then gold on top
            if a_o:
                pix[i:i + 4] = bytes((dark[0], dark[1], dark[2], a_o))
            if a_g:
                blend(pix, x, y, size, (gold[0], gold[1], gold[2], a_g))
    write_png(path, size, size, pix)


def gen_background(path, w=520, h=660):
    """A dark, mostly-opaque panel with a thin gold border + corner accents."""
    border = (190, 158, 80)
    fill = (24, 21, 17, 245)
    pix = bytearray(w * h * 4)
    for y in range(h):
        for x in range(w):
            i = (y * w + x) * 4
            edge = min(x, y, w - 1 - x, h - 1 - y)
            if edge < 3:
                pix[i:i + 4] = bytes((border[0], border[1], border[2], 255))
            else:
                # subtle vertical gradient
                g = 1.0 - (y / h) * 0.18
                pix[i:i + 4] = bytes((int(fill[0] * g), int(fill[1] * g),
                                      int(fill[2] * g), fill[3]))
    write_png(path, w, h, pix)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    gen_icon(os.path.join(OUT_DIR, "icon.png"))
    gen_background(os.path.join(OUT_DIR, "background.png"))
    print("wrote icon.png and background.png to", os.path.normpath(OUT_DIR))
