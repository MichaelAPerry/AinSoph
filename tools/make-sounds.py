#!/usr/bin/env python3
"""Synthesise Ain Soph's sounds into assets/audio/*.ogg.

Everything here is generated from sine waves and noise — original, so it ships
with the game under the project's own license. Needs numpy and ffmpeg.

    python3 tools/make-sounds.py
"""
import os
import subprocess
import tempfile
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "audio")
rng = np.random.default_rng(7)


def t(seconds):
    return np.arange(int(seconds * SR)) / SR


def env(n, attack, release):
    """Linear attack, exponential-ish release, in samples."""
    e = np.ones(n)
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r) ** 2
    return e


def lowpass(x, cutoff):
    """One-pole low-pass, vectorised enough for short clips."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def note(freq, seconds, harmonics=(1.0, 0.35, 0.12), detune=0.0):
    tt = t(seconds)
    s = np.zeros_like(tt)
    for k, amp in enumerate(harmonics, start=1):
        s += amp * np.sin(2 * np.pi * freq * k * tt * (1 + detune))
    return s


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def save(name, x, loop=False):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) or 1.0
    x = x / peak * (0.8 if loop else 0.9)
    pcm = (x * 32767).astype(np.int16)
    os.makedirs(OUT, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
        tmp = f.name
    with wave.open(tmp, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    dst = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
    os.remove(tmp)
    print("wrote", os.path.relpath(dst))


def add_circular(buf, clip, start):
    """Mix a clip into a looping buffer, wrapping its tail to the start."""
    n = len(buf)
    for i in range(0, len(clip), n):
        part = clip[i:i + n]
        s = (start + i) % n
        end = s + len(part)
        if end <= n:
            buf[s:end] += part
        else:
            buf[s:] += part[: n - s]
            buf[: end - n] += part[n - s:]


# ── Music: a slow modal theme, seamless loop ─────────────────────────────────

def music():
    bar = 8.0
    chords = [  # D dorian: Dm  C  Bb  Am  Dm  F  C  Am
        [50, 57, 62, 65], [48, 55, 60, 64], [46, 53, 58, 62], [45, 52, 57, 60],
        [50, 57, 62, 65], [41, 53, 57, 60], [48, 55, 60, 64], [45, 52, 57, 64],
    ]
    total = int(bar * len(chords) * SR)
    buf = np.zeros(total)
    for i, chord in enumerate(chords):
        length = bar + 3.0  # overlap into the next chord
        for m in chord:
            pad = note(hz(m), length, (1.0, 0.25, 0.08), detune=0.0007) + \
                  note(hz(m), length, (1.0, 0.2), detune=-0.0009)
            pad *= env(len(pad), 2.5, 3.5) * 0.18
            add_circular(buf, pad, int(i * bar * SR))
    # A sparse melody of soft plucks in the same mode
    melody = [74, None, 72, 69, None, 67, 69, None, 65, None, 67, 69, 72, None, 74, None,
              69, None, 67, 65, None, 62, 65, None, 67, None, 69, 67, 65, None, 62, None]
    step = bar * len(chords) / len(melody)
    for i, m in enumerate(melody):
        if m is None:
            continue
        p = note(hz(m), 2.6, (1.0, 0.4, 0.15, 0.05))
        p *= np.exp(-t(2.6) * 2.2) * env(len(p), 0.01, 0.4) * 0.22
        add_circular(buf, p, int(i * step * SR))
    save("music_theme", buf, loop=True)


# ── Ambience: wind over open ground, seamless loop ──────────────────────────

def ambience():
    seconds = 24.0
    n = int(seconds * SR)
    noise = rng.standard_normal(n + SR)
    wind = lowpass(noise, 400)[:n + SR]
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * t((n + SR) / SR) / 8.0) ** 2
    wind = wind * gust
    # crossfade the extra second onto the start so the loop is seamless
    fade = np.linspace(0, 1, SR)
    loop = wind[:n].copy()
    loop[:SR] = loop[:SR] * fade + wind[n:n + SR] * (1 - fade)
    save("ambience_wind", loop, loop=True)


# ── Effects ──────────────────────────────────────────────────────────────────

def sfx():
    # Footstep: a soft low thud
    n = rng.standard_normal(int(0.09 * SR))
    save("step", lowpass(n, 260) * env(len(n), 0.003, 0.08))

    # UI click
    c = note(1250, 0.035, (1.0, 0.2)) * env(int(0.035 * SR), 0.001, 0.03)
    save("click", c)

    # Speech blip — an NPC says something
    b = np.concatenate([note(660, 0.05, (1, .3)), note(880, 0.06, (1, .3))])
    save("speech", b * env(len(b), 0.003, 0.05) * 0.6)

    # Eat: two rising notes
    e = np.concatenate([note(hz(72), 0.12, (1, .3)) * env(int(.12 * SR), .005, .08),
                        note(hz(79), 0.35, (1, .3, .1)) * np.exp(-t(.35) * 6)])
    save("eat", e)

    # Reap: a whoosh into a low strike
    w = lowpass(rng.standard_normal(int(0.25 * SR)), 1800) * np.linspace(0, 1, int(0.25 * SR)) ** 2
    hit = note(70, 0.4, (1, .5, .25)) * np.exp(-t(.4) * 9)
    save("reap", np.concatenate([w * 0.5, hit]))

    # Council bell: inharmonic partials, long decay
    tt = t(4.0)
    bell = sum(a * np.sin(2 * np.pi * 220 * r * tt) * np.exp(-tt * d)
               for r, a, d in [(1.0, 1.0, 0.9), (2.76, 0.5, 1.4), (5.40, 0.25, 2.2), (8.93, 0.12, 3.0)])
    save("council", bell * env(len(tt), 0.002, 0.5))

    # Rib / birth: a rising shimmer
    arp = np.zeros(int(2.2 * SR))
    for i, m in enumerate([62, 66, 69, 74, 78, 81]):
        p = note(hz(m), 1.4, (1, .3, .1)) * np.exp(-t(1.4) * 3)
        s = int(i * 0.13 * SR)
        arp[s:s + len(p)] += p[: len(arp) - s]
    save("rib", arp * env(len(arp), 0.01, 0.6))
    save("birth", arp[: int(1.3 * SR)] * env(int(1.3 * SR), 0.01, 0.5))

    # Warning: two low pulses
    p = note(196, 0.18, (1, .5)) * env(int(.18 * SR), .005, .1)
    save("warning", np.concatenate([p, np.zeros(int(.12 * SR)), p]))

    # Death: a slow descending tone
    tt = t(2.4)
    f = 220 * (0.5 ** (tt / 2.4))
    d = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 0.9)
    save("death", d * env(len(tt), 0.05, 0.8))

    # Pray: a single soft bell
    tt = t(2.0)
    pr = np.sin(2 * np.pi * 523 * tt) * np.exp(-tt * 2.5) + 0.3 * np.sin(2 * np.pi * 1046 * tt) * np.exp(-tt * 4)
    save("pray", pr)


if __name__ == "__main__":
    sfx()
    ambience()
    music()
