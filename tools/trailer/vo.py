#!/usr/bin/env python3
"""Narration for the trailer, spoken by Piper TTS (voice: en_GB cori, public domain).

    python3 tools/trailer/vo.py <voice.onnx> <out_dir>
"""
import sys, wave
from piper import PiperVoice, SynthesisConfig

LINES = {
    "v01": "Before form. Before light. There was the boundless.",
    "v02": "Now, there is a world.",
    "v03": "Its people are not written.",
    "v04": "Every one of them thinks, remembers, and chooses, on your own machine.",
    "g1": "Move.", "g2": "See.", "g3": "Hear.", "g4": "Talk.", "g5": "Reap.", "g6": "Pray.",
    "v05": "But the world does not pause.",
    "v06": "Hunger is real. Night is real. And the beasts are hungry too.",
    "v07": "Somewhere lies a hidden altar.",
    "v08": "Ask, and three gods answer, in parable.",
    "v09": "Earn the rib. Name your beloved.",
    "v10": "Raise a lineage that outlives you.",
    "v11": "No cloud. No servers. Nothing phones home.",
    "v12": "Ayn Sofe.",  # spelled for the voice: Ain Soph
}

voice = PiperVoice.load(sys.argv[1])
cfg = SynthesisConfig(length_scale=1.18, noise_scale=0.6, noise_w_scale=0.7)
for key, text in LINES.items():
    with wave.open(f"{sys.argv[2]}/{key}.wav", "wb") as w:
        voice.synthesize_wav(text, w, syn_config=cfg)
    print(key, text)
