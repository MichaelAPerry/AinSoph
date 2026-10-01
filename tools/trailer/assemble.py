#!/usr/bin/env python3
"""Cut the trailer: raw in-engine footage + narration + synthesised score.

    python3 tools/trailer/assemble.py <raw.avi> <raw.log> <vo_dir> <fonts_dir> <out.mp4>

raw.avi/raw.log come from `--trailer` (see tools/record-trailer.sh); the log's
TRAILER-MARK lines say where each beat starts. vo_dir holds the narration
from vo.py. Everything below the EDL heading is the edit.
"""
import os
import re
import subprocess
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
import score as S  # noqa: E402

RAW, LOG, VO, FONTS, OUT = sys.argv[1:6]
W, H, FPS = 1920, 1080, 30
GOLD = (255, 214, 120)
CREAM = (238, 230, 208)

marks = {m.group(1): int(m.group(2)) / FPS
         for m in re.finditer(r"TRAILER-MARK (\S+) (\d+)", open(LOG).read())}
texts = {m.group(1): m.group(2).strip()
         for m in re.finditer(r"TRAILER-TEXT (\S+) (.*)", open(LOG).read())}


def src(mark, offset=0.0):
    return marks[mark] + offset


def font(size, family="Cinzel", weight="Bold"):
    f = ImageFont.truetype(f"{FONTS}/{family}[wght].ttf", size)
    try:
        f.set_variation_by_name(weight)
    except Exception:
        pass
    return f


# ══ EDL ═══════════════════════════════════════════════════════════════════
# Shots: (timeline start, duration, source time, zoom from, zoom to, focus x, y)
# Focus is the fraction of the frame the zoom closes on (0.5, 0.5 = centre).

SHOTS = []
TEXT = []    # (start, end, text, size, y, colour, family, weight)
FLASH = []   # timeline times of white flashes
BLACK = []   # (start, end) forced black
DIM = []     # (start, end, amount) darken the picture under type
VOICE = []   # (time, key)
SFX = []     # (timeline start, source start, duration, gain) — game audio from the raw


def shot(at, dur, source, z0=1.0, z1=1.04, fx=0.5, fy=0.5, game_audio=0.0):
    SHOTS.append((at, dur, source, z0, z1, fx, fy))
    if game_audio:
        SFX.append((at, source, dur, game_audio))


def say(at, key):
    VOICE.append((at, key))


def text(start, end, s, size=96, y=0.5, colour=GOLD, family="Cinzel", weight="Bold"):
    TEXT.append((start, end, s, size, y, colour, family, weight))


def first_sentences(s, limit=110):
    parts = re.split(r"(?<=[.!?])\s+", s.strip())
    out = parts[0]
    for p in parts[1:]:
        if len(out) + len(p) + 1 > limit:
            break
        out += " " + p
    return out


def council_quote(s):
    """The homily of a seat that voted yes (else the first), first sentence."""
    lines = [l.strip() for l in s.split("|") if l.strip()]
    seats = [(lines[i], lines[i + 1]) for i in range(len(lines) - 1) if lines[i].startswith("[")]
    for head, homily in seats:
        if "YES" in head:
            return head.strip("[] ").split("—")[0].strip().title(), first_sentences(homily, 120)
    return (seats[0][0].strip("[] ").split("—")[0].strip().title(), first_sentences(seats[0][1], 120)) if seats else ("", "")


def wrap(s, width):
    words, lines, cur = s.split(), [], ""
    for w in words:
        if len(cur) + len(w) + 1 > width and cur:
            lines.append(cur); cur = w
        else:
            cur = f"{cur} {w}".strip()
    return lines + [cur] if cur else lines


def quote(start, end, s, caption, size=58, centre=0.4):
    lines = wrap(f"\u201c{s}\u201d", 42)
    top = centre - (len(lines) - 1) * size * 1.3 / H / 2 - 0.03
    for i, line in enumerate(lines):
        text(start, end, line, size, y=top + i * size * 1.3 / H, colour=CREAM,
             family="CormorantGaramond", weight="SemiBold")
    text(start + 0.4, end, caption, 30, y=top + len(lines) * size * 1.3 / H + 0.03,
         colour=(205, 180, 120), family="Cinzel", weight="Regular")
    DIM.append((start - 0.2, end + 0.1, 0.85))


def build_edl():
    # ── I. The boundless ───────────────────────────────────────────────────
    BLACK.append((0.0, 2.0))
    say(0.8, "v01")
    shot(2.0, 3.6, src("night", 0.2), 1.12, 1.0)
    FLASH.append(5.6)
    shot(5.6, 3.4, src("dawn", 0.05), 1.0, 1.06)
    say(5.9, "v02")
    shot(9.0, 2.0, src("wide", 0.5), 1.0, 1.05)
    say(9.3, "v03")
    shot(11.0, 2.2, src("npc", 0.3), 1.0, 1.08)
    say(11.4, "v04")
    reply = os.environ.get("QUOTE", "reply")  # which of the NPC's answers to quote
    shot(13.2, 4.4, src(reply, 0.3), 1.0, 1.06)
    if reply in texts:
        quote(13.4, 17.5, first_sentences(texts[reply]), "UNSCRIPTED  ·  SPOKEN BY THE AI ON YOUR PC")

    # ── II. Six gifts ──────────────────────────────────────────────────────
    gifts = [("g1", "MOVE", src("walk", 0.4)), ("g2", "SEE", src("dawn", 0.0)),
             ("g3", "HEAR", src("npc", 1.0)), ("g4", "TALK", src("talk", 0.5)),
             ("g5", "REAP", src("reap", 0.1)), ("g6", "PRAY", src("pray", 0.2))]
    for i, (key, word, s0) in enumerate(gifts):
        at = 17.8 + i * 1.25
        shot(at, 1.25, s0, 1.06, 1.0, game_audio=0.5)
        text(at + 0.02, at + 1.2, word, 150)
        say(at + 0.1, key)
        FLASH.append(at)

    # ── III. The world does not pause ─────────────────────────────────────
    BLACK.append((25.3, 28.6))
    say(26.0, "v05")
    text(26.3, 28.5, "THE WORLD DOES NOT PAUSE", 64, colour=CREAM)
    shot(28.6, 1.8, src("nightfall", 0.0), 1.0, 1.08)
    say(29.0, "v06")
    shot(30.4, 1.6, src("hunger", 0.1), 1.05, 1.1, game_audio=0.6)
    text(30.5, 32.0, "You must eat within the hour.", 46, y=0.78, colour=CREAM,
         family="CormorantGaramond", weight="SemiBold")
    shot(32.0, 2.6, src("attack", -2.0), 1.0, 1.15, game_audio=0.8)
    FLASH.append(34.6)
    BLACK.append((34.6, 37.0))
    text(34.7, 36.9, "Miriam was killed by a lion.", 60, colour=CREAM,
         family="CormorantGaramond", weight="SemiBold")

    # ── IV. The Council ───────────────────────────────────────────────────
    BLACK.append((37.0, 37.4))
    shot(37.4, 2.8, src("altar", 0.3), 1.0, 1.1)
    say(37.6, "v07")
    shot(40.2, 4.8, src("verdict", 0.4), 1.0, 1.06)
    if "verdict" in texts:
        seat, homily = council_quote(texts["verdict"])
        quote(40.5, 44.9, homily, f"THE COUNCIL  ·  SEAT OF {seat.upper()}")
    say(40.3, "v08")

    # ── V. The rib ────────────────────────────────────────────────────────
    shot(45.0, 2.6, src("rib", 1.2), 1.0, 1.04)
    say(45.2, "v09")
    shot(47.6, 2.8, src("birth", 0.2), 1.0, 1.06, game_audio=0.6)
    say(47.8, "v10")

    # ── VI. Montage ───────────────────────────────────────────────────────
    cuts = [src("walk", 2.0), src("menu", 0.5), src("verdict", 2.0),
            src("eat", 0.4), src("lion", 1.5), src("reply2", 0.5)]
    for i, s0 in enumerate(cuts):
        at = 50.4 + i * 0.42
        shot(at, 0.42, s0, 1.1, 1.0)
        FLASH.append(at)

    shot(52.92, 4.3, src("wide", 1.0), 1.0, 1.08)
    say(53.1, "v11")

    # ── VII. Title ────────────────────────────────────────────────────────
    BLACK.append((57.2, 68.0))
    FLASH.append(57.2)
    text(57.4, 66.8, "AIN SOPH", 200)
    say(58.0, "v12")
    text(59.4, 66.8, "THE BOUNDLESS", 44, y=0.64, colour=CREAM, family="Cinzel", weight="Regular")
    text(61.0, 66.8, "Free  ·  Open source  ·  Runs entirely offline", 38, y=0.76,
         colour=CREAM, family="CormorantGaramond", weight="Medium")
    text(63.2, 66.8, "Windows · Linux   —   github.com/MichaelAPerry/AinSoph", 30, y=0.86,
         colour=(190, 182, 160), family="CormorantGaramond", weight="Medium")


LENGTH = 68.0


# ══ Score ═════════════════════════════════════════════════════════════════

def build_audio(vo):
    music = S.buf(LENGTH)
    fx = S.buf(LENGTH)

    S.drone(music, 0.0, 25.4, root=55.0, gain=0.3, fade_in=3.5, fade_out=0.15)
    S.hit(fx, 5.6, gain=0.55)
    S.pad_chord(music, 5.6, 17.8, [110, 164.8, 220, 277.2], gain=0.12, fade_in=2.5, fade_out=1.5)
    S.whoosh(fx, 9.0); S.whoosh(fx, 11.0); S.whoosh(fx, 13.2)

    # Gifts: pulse at 96 bpm, a hit on each word
    S.pulse_bed(music, 17.8, 25.3, bpm=96, gain=0.5)
    S.ostinato(music, 17.8, 25.3, bpm=96, root=110, gain=0.14)
    for i in range(6):
        S.hit(fx, 17.8 + i * 1.25, gain=0.42 + i * 0.06)
    S.riser(fx, 23.6, 25.3, gain=0.4)

    # Silence, and the clock
    for k in range(4):
        S.tick(fx, 25.6 + k * 0.75, gain=0.5)
    S.braam(fx, 28.6, dur=3.6, gain=0.7)
    S.drone(music, 28.6, 37.0, root=41.2, gain=0.3, fade_in=0.2, fade_out=0.3, bright=400)
    for k in range(9):
        S.heartbeat(fx, 29.0 + k * 0.62, gain=0.5 + k * 0.04)
    S.riser(fx, 32.4, 34.6, gain=0.45)
    S.hit(fx, 34.6, gain=1.0, low=40)

    # The Council
    S.pad_chord(music, 37.4, 45.4, [146.8, 220, 293.7, 349.2], gain=0.16, fade_in=0.6, fade_out=1.0)
    S.bell(fx, 37.5, f=293.7, gain=0.28)
    S.bell(fx, 40.2, f=220.0, gain=0.3)

    # The rib, building
    S.ostinato(music, 45.0, 52.9, bpm=96, root=130.8, gain=0.13)
    S.pulse_bed(music, 45.0, 52.9, bpm=96, gain=0.45)
    S.drone(music, 45.0, 52.9, root=65.4, gain=0.18, fade_in=1.0, fade_out=0.1)
    for i in range(6):
        S.hit(fx, 50.4 + i * 0.42, gain=0.35 + i * 0.07)
    S.riser(fx, 51.0, 52.9, gain=0.45)

    # Breath before the title
    S.pad_chord(music, 52.9, 57.4, [110, 164.8, 246.9, 329.6], gain=0.14, fade_in=0.3, fade_out=0.5)

    # Title
    S.hit(fx, 57.2, gain=1.0, low=36)
    S.braam(fx, 57.2, dur=5.0, root=55.0, gain=0.6)
    S.pad_chord(music, 57.4, LENGTH, [55, 110, 164.8, 220, 277.2, 329.6], gain=0.2, fade_in=2.0, fade_out=4.0)
    S.bell(fx, 59.4, f=440.0, gain=0.18)

    music = S.duck(music, vo, depth=0.5)
    return music, fx


def load_vo():
    out = S.buf(LENGTH)
    for at, key in VOICE:
        with wave.open(f"{VO}/{key}.wav") as w:
            sr = w.getframerate()
            x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16) / 32768.0
        n = int(len(x) * S.SR / sr)
        x = np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x)
        x = S._lp(S._hp(x, 80), 9000)
        sig = S.reverb(np.stack([x, x], 1), wet=0.12, seconds=1.6)
        S._add(out, at, sig, 1.0)
    return out


def load_game_audio():
    """Game sound effects from the raw recording, cut to the shots that use them."""
    out = S.buf(LENGTH)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", RAW, "-f", "f32le", "-ac", "2",
                          "-ar", str(S.SR), "-"], capture_output=True).stdout
    a = np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)
    for at, s0, dur, gain in SFX:
        i, n = int(s0 * S.SR), int(dur * S.SR)
        seg = a[i:i + n].copy()
        if len(seg):
            ramp = min(len(seg) // 2, int(0.02 * S.SR))
            seg[:ramp] *= np.linspace(0, 1, ramp)[:, None]
            seg[-ramp:] *= np.linspace(1, 0, ramp)[:, None]
            S._add(out, at, seg, gain)
    return out


# ══ Picture ═══════════════════════════════════════════════════════════════

def vignette():
    y, x = np.mgrid[0:H, 0:W]
    d = np.sqrt(((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2)
    return np.clip(1.08 - 0.42 * d ** 2.2, 0, 1)[:, :, None].astype(np.float32)


VIG = None


def read_frames(start, count):
    proc = subprocess.Popen(["ffmpeg", "-v", "error", "-ss", f"{start:.3f}", "-i", RAW,
                             "-frames:v", str(count), "-vf", f"scale={W}:{H}:flags=neighbor",
                             "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
    frames = []
    for _ in range(count):
        b = proc.stdout.read(W * H * 3)
        if len(b) < W * H * 3:
            break
        frames.append(np.frombuffer(b, np.uint8).reshape(H, W, 3))
    proc.wait()
    while len(frames) < count:
        frames.append(frames[-1] if frames else np.zeros((H, W, 3), np.uint8))
    return frames


def zoom(frame, z, fx, fy):
    if abs(z - 1) < 1e-3:
        return frame
    cw, ch = W / z, H / z
    x0 = (W - cw) * fx
    y0 = (H - ch) * fy
    img = Image.fromarray(frame).resize((W, H), Image.BICUBIC, box=(x0, y0, x0 + cw, y0 + ch),
                                        reducing_gap=None)
    return np.asarray(img)


_text_cache = {}


def text_layer(s, size, colour, family, weight):
    key = (s, size, colour, family, weight)
    if key not in _text_cache:
        f = font(size, family, weight)
        pad = size
        box = ImageDraw.Draw(Image.new("L", (1, 1))).textbbox((0, 0), s, font=f)
        tw, th = box[2] - box[0] + pad * 2, box[3] - box[1] + pad * 2
        mask = Image.new("L", (tw, th), 0)
        ImageDraw.Draw(mask).text((pad - box[0], pad - box[1]), s, font=f, fill=255)
        glow = mask.filter(ImageFilter.GaussianBlur(size / 6))
        shadow = mask.filter(ImageFilter.GaussianBlur(size / 10))
        _text_cache[key] = (np.asarray(mask) / 255.0, np.asarray(glow) / 255.0,
                            np.asarray(shadow) / 255.0)
    return _text_cache[key]


def draw_text(frame, t):
    for start, end, s, size, y, colour, family, weight in TEXT:
        if not (start <= t < end):
            continue
        a = min(1, (t - start) / 0.35, (end - t) / 0.5)
        grow = 1 + 0.04 * (t - start) / (end - start)  # slow push on the type
        mask, glow, shadow = text_layer(s, int(size * grow), colour, family, weight)
        th, tw = mask.shape
        x0, y0 = (W - tw) // 2, int(H * y - th / 2)
        xs, ys = max(0, x0), max(0, y0)
        xe, ye = min(W, x0 + tw), min(H, y0 + th)
        m = mask[ys - y0:ye - y0, xs - x0:xe - x0, None] * a
        g = glow[ys - y0:ye - y0, xs - x0:xe - x0, None] * a
        sh = shadow[ys - y0:ye - y0, xs - x0:xe - x0, None] * a
        region = frame[ys:ye, xs:xe]
        region *= 1 - 0.65 * sh
        region += np.array([255, 170, 70], np.float32) * g * 0.45
        region[:] = region * (1 - m) + np.array(colour, np.float32) * m
    return frame


def render(audio_path):
    global VIG
    VIG = vignette()
    enc = subprocess.Popen(
        ["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
         "-r", str(FPS), "-i", "-", "-i", audio_path, "-c:v", "libx264", "-preset", "slow",
         "-crf", "19", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k",
         "-movflags", "+faststart", "-shortest", OUT], stdin=subprocess.PIPE)

    total = int(LENGTH * FPS)
    shots = sorted(SHOTS)
    cache = {}
    for n in range(total):
        t = n / FPS
        frame = None
        if not any(b0 <= t < b1 for b0, b1 in BLACK):
            for i, (at, dur, s0, z0, z1, fx, fy) in enumerate(shots):
                if at <= t < at + dur:
                    if i not in cache:
                        cache.clear()
                        cache[i] = read_frames(s0, int(round(dur * FPS)) + 1)
                    k = min(int((t - at) * FPS), len(cache[i]) - 1)
                    x = (t - at) / dur
                    z = z0 + (z1 - z0) * (x * x * (3 - 2 * x))
                    frame = zoom(cache[i][k], z, fx, fy).astype(np.float32)
                    # fade in from black on the first shot after a black stretch
                    for b0, b1 in BLACK:
                        if abs(at - b1) < 1e-3 and t - at < 0.6 and at < 5:
                            frame *= (t - at) / 0.6 if at > 1.5 else 1
                    break
        if frame is None:
            frame = np.zeros((H, W, 3), np.float32)
        # grade: lift shadows a touch toward blue, warm highlights
        frame = frame * VIG
        for d0, d1, amount in DIM:
            if d0 <= t < d1:
                a = min(1, (t - d0) / 0.3, (d1 - t) / 0.3) * amount
                frame *= 1 - a
        frame = draw_text(frame, t)
        for f in FLASH:
            if 0 <= t - f < 0.2:
                frame += (255 - frame) * (1 - (t - f) / 0.2) * 0.85
        if t > LENGTH - 1.2:
            frame *= max(0, (LENGTH - t) / 1.2)
        enc.stdin.write(np.clip(frame, 0, 255).astype(np.uint8).tobytes())
        if n % 150 == 0:
            print(f"  frame {n}/{total}", flush=True)
    enc.stdin.close()
    enc.wait()


def main():
    build_edl()
    print("marks:", {k: round(v, 2) for k, v in marks.items()})
    vo = load_vo()
    music, fx = build_audio(vo)
    game = load_game_audio()
    mix = S.master(music + fx * 0.8 + vo * 1.25 + game * 0.5)
    raw_wav, wav = OUT + ".raw.wav", OUT + ".wav"
    with wave.open(raw_wav, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(S.SR)
        w.writeframes((mix * 32767).astype(np.int16).tobytes())
    # Streaming loudness: -14 LUFS, peaks under -1.5 dBTP
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", raw_wav, "-af",
                    "loudnorm=I=-14:TP=-1.5:LRA=11", "-ar", str(S.SR), wav], check=True)
    print("audio done")
    render(wav)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
