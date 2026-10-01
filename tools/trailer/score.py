"""Trailer score and sound design, synthesised from sines and noise.

Original material (no samples), so it is MIT like the rest of the project.
Every function adds into a stereo float buffer at a time in seconds.
"""
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve

SR = 48000
rng = np.random.default_rng(1)


def buf(seconds):
    return np.zeros((int(seconds * SR), 2))


def _t(seconds):
    return np.arange(int(seconds * SR)) / SR


def _lp(x, hz, order=2):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x, axis=0)


def _hp(x, hz, order=2):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x, axis=0)


def _add(out, at, sig, gain=1.0, pan=0.0):
    if sig.ndim == 1:
        sig = np.stack([sig * (1 - max(pan, 0)), sig * (1 + min(pan, 0))], axis=1)
    i = int(at * SR)
    if i >= len(out):
        return
    n = min(len(sig), len(out) - i)
    out[i:i + n] += sig[:n] * gain


def _reverb_ir(seconds=3.0, damp=2.2):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)) * np.exp(-t * damp)[:, None]
    ir = _lp(ir, 5000)
    ir[: int(0.01 * SR)] *= np.linspace(0, 1, int(0.01 * SR))[:, None]
    return ir / np.sqrt((ir ** 2).sum(axis=0))


IR = None


def reverb(x, wet=0.35, seconds=3.0):
    global IR
    if IR is None or len(IR) != int(seconds * SR):
        IR = _reverb_ir(seconds)
    y = np.stack([fftconvolve(x[:, c], IR[:, c])[: len(x)] for c in range(2)], axis=1)
    return x * (1 - wet) + y * wet


def _saw(freq, t):
    return 2 * ((freq * t) % 1.0) - 1


# ── Elements ──────────────────────────────────────────────────────────────

def drone(out, start, end, root=55.0, gain=0.22, fade_in=3.0, fade_out=2.0, bright=600):
    """A low, slowly breathing chord: root, fifth, octave, with air."""
    dur = end - start
    t = _t(dur)
    sig = np.zeros((len(t), 2))
    for mult, g, det in [(1, 1.0, 0.0), (1.5, 0.55, 0.4), (2, 0.6, -0.3), (3, 0.25, 0.2), (4.5, 0.12, 0.5)]:
        for side, d in ((0, det), (1, -det)):
            f = root * mult + d
            sig[:, side] += g * (np.sin(2 * np.pi * f * t) + 0.3 * _saw(f, t))
    sig = _lp(sig, bright)
    sig *= (0.75 + 0.25 * np.sin(2 * np.pi * 0.11 * t))[:, None]
    air = _lp(_hp(rng.standard_normal((len(t), 2)), 300), 2500) * 0.08
    sig = sig / np.abs(sig).max() + air
    e = np.ones(len(t))
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    e[:fi] = np.linspace(0, 1, fi) ** 2
    e[-fo:] *= np.linspace(1, 0, fo) ** 2
    _add(out, start, sig * e[:, None], gain)


def hit(out, at, gain=0.9, low=48.0):
    """Trailer impact: pitched-down sub thump, a body of noise, a long tail."""
    t = _t(3.5)
    f = low + 90 * np.exp(-t * 18)
    sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 1.6)
    body = _lp(rng.standard_normal(len(t)), 900) * np.exp(-t * 7) * 0.9
    crack = _hp(rng.standard_normal(len(t)), 2500) * np.exp(-t * 30) * 0.35
    sig = sub + body + crack
    sig = np.stack([sig, sig], axis=1)
    sig = reverb(sig / np.abs(sig).max(), wet=0.4, seconds=3.0)
    _add(out, at, sig, gain)


def braam(out, at, dur=3.2, root=55.0, gain=0.55):
    """A wall of detuned brass-like saws that opens and closes."""
    t = _t(dur)
    sig = np.zeros((len(t), 2))
    for mult in (0.5, 1, 1.5, 2, 3):
        for side, det in ((0, 1.003), (1, 0.997)):
            sig[:, side] += _saw(root * mult * det, t) / mult ** 0.5
    # filter sweep: open fast, close slow — process in blocks
    out_sig = np.zeros_like(sig)
    block = int(0.05 * SR)
    for i in range(0, len(t), block):
        x = t[i] / dur
        cutoff = 200 + 2400 * (min(1, x * 6) * (1 - x) ** 1.5)
        out_sig[i:i + block] = _lp(sig[i:i + block], max(cutoff, 120))
    e = np.minimum(1, t / 0.08) * (1 - t / dur) ** 1.2
    out_sig *= e[:, None]
    out_sig = reverb(out_sig / np.abs(out_sig).max(), wet=0.3)
    _add(out, at, out_sig, gain)


def riser(out, start, end, gain=0.35):
    """Noise and a sine sweeping up, cut dead at the end."""
    dur = end - start
    t = _t(dur)
    x = t / dur
    noise = rng.standard_normal((len(t), 2))
    sig = np.zeros_like(noise)
    block = int(0.04 * SR)
    for i in range(0, len(t), block):
        sig[i:i + block] = _hp(noise[i:i + block], 200 + 5000 * x[i] ** 2)
    f = 110 * 2 ** (x * 3)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.5
    sig = (sig * 0.6 + tone[:, None]) * (x ** 2.2)[:, None]
    _add(out, start, sig, gain)


def whoosh(out, at, dur=0.6, gain=0.25):
    t = _t(dur)
    x = t / dur
    sig = _lp(_hp(rng.standard_normal((len(t), 2)), 400), 3000) * (np.sin(np.pi * x) ** 2)[:, None]
    _add(out, at - dur / 2, sig, gain)


def tick(out, at, gain=0.35):
    """A clock tick — dry, close."""
    t = _t(0.08)
    sig = _hp(rng.standard_normal(len(t)), 3000) * np.exp(-t * 120)
    sig += np.sin(2 * np.pi * 1800 * t) * np.exp(-t * 90) * 0.5
    _add(out, at, sig, gain)


def heartbeat(out, at, gain=0.6):
    """Low double thump."""
    for off, g in ((0.0, 1.0), (0.22, 0.7)):
        t = _t(0.35)
        f = 45 + 50 * np.exp(-t * 30)
        sig = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14)
        _add(out, at + off, sig, gain * g)


def pulse_bed(out, start, end, bpm=96, gain=0.45, accent_every=4):
    """Driving low ostinato: tom on each beat, tick on the offbeat."""
    beat = 60 / bpm
    n = int((end - start) / beat)
    for i in range(n):
        at = start + i * beat
        x = i / max(1, n - 1)
        t = _t(0.4)
        f = 70 + 80 * np.exp(-t * 25)
        tom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
        tom += _lp(rng.standard_normal(len(t)), 500) * np.exp(-t * 25) * 0.4
        _add(out, at, tom, gain * (0.6 + 0.4 * x) * (1.3 if i % accent_every == 0 else 1))
        tick(out, at + beat / 2, gain * 0.25 * (0.5 + x))


def ostinato(out, start, end, bpm=96, root=110.0, gain=0.16):
    """Plucked minor figure in eighths — the tension line."""
    eighth = 30 / bpm
    notes = [0, 0, 3, 0, 7, 0, 3, 5]  # semitones above root
    n = int((end - start) / eighth)
    for i in range(n):
        f = root * 2 ** (notes[i % len(notes)] / 12)
        t = _t(0.5)
        s = (_saw(f, t) * 0.6 + np.sin(2 * np.pi * f * t)) * np.exp(-t * 7)
        s = _lp(s, 1600)
        x = i / max(1, n - 1)
        _add(out, start + i * eighth, s, gain * (0.5 + 0.5 * x), pan=0.3 if i % 2 else -0.3)


def pad_chord(out, start, end, freqs, gain=0.18, fade_in=1.5, fade_out=3.0):
    """Wide sustained chord for the title and the Council."""
    dur = end - start
    t = _t(dur)
    sig = np.zeros((len(t), 2))
    for f in freqs:
        for side, det in ((0, 1.002), (1, 0.998)):
            sig[:, side] += _saw(f * det, t) * 0.3 + np.sin(2 * np.pi * f * det * t)
    sig = _lp(sig, 1800)
    e = np.ones(len(t))
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    e[:fi] = np.linspace(0, 1, fi) ** 2
    e[-fo:] *= np.linspace(1, 0, fo) ** 1.5
    sig = reverb(sig / np.abs(sig).max() * e[:, None], wet=0.5, seconds=4.0)
    _add(out, start, sig, gain)


def bell(out, at, f=440.0, gain=0.3):
    """Inharmonic bell, long ring — the Council."""
    t = _t(5.0)
    sig = sum(a * np.sin(2 * np.pi * f * r * t) * np.exp(-t * d)
              for r, a, d in [(1, 1, 0.8), (2.76, 0.5, 1.4), (5.4, 0.3, 2.5), (8.9, 0.15, 4)])
    _add(out, at, reverb(np.stack([sig, sig], 1) / 2, wet=0.45, seconds=4.0), gain)


# ── Mixing ────────────────────────────────────────────────────────────────

def duck(music, voice, depth=0.55, attack=0.05, release=0.4):
    """Lower the music under the narration."""
    env = np.abs(voice).max(axis=1)
    win = int(0.03 * SR)
    env = np.convolve(env, np.ones(win) / win, mode="same")
    env = np.clip(env * 8, 0, 1)
    sm = np.empty_like(env)
    a, r = np.exp(-1 / (attack * SR)), np.exp(-1 / (release * SR))
    acc = 0.0
    # simple attack/release follower, vectorised in blocks of the decimated signal
    dec = 48
    small = env[::dec]
    out = np.empty_like(small)
    a, r = np.exp(-dec / (attack * SR)), np.exp(-dec / (release * SR))
    for i, v in enumerate(small):
        acc = a * acc + (1 - a) * v if v > acc else r * acc + (1 - r) * v
        out[i] = acc
    sm = np.repeat(out, dec)[: len(env)]
    if len(sm) < len(env):
        sm = np.pad(sm, (0, len(env) - len(sm)), mode="edge")
    return music * (1 - depth * sm)[:, None]


def master(x, ceiling=0.95):
    """Gentle tanh limiting to a ceiling."""
    peak = np.abs(x).max()
    x = x / peak * 1.4
    return np.tanh(x) / np.tanh(1.4) * ceiling
