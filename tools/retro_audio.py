#!/usr/bin/env python3
"""Synthesise AVAWizardry's own retro sounds and themes (CC0; made for AVAWizardry, from AVAGard's tool).

Everything is generated here in the spirit of the Commodore 64's SID chip: pulse, triangle,
sawtooth and noise voices, ADSR envelopes, pitch sweeps and vibrato. No samples are used and
nothing is taken from any original game; the tunes are new compositions.

Pure standard-library Python (no numpy). Ogg encoding needs ffmpeg with libvorbis.

Usage:
    retro_audio.py <out-dir-for-effects-pack> <out-dir-for-music>
"""
import math
import os
import random
import struct
import subprocess
import sys
import wave

RATE = 22050
RNG = random.Random(1981)


# --- voices -------------------------------------------------------------------------------------

def tone(freq, dur, wave_kind="pulse", duty=0.5, attack=0.004, decay=0.05, sustain=0.6, release=0.08,
         slide_to=None, vibrato=0.0, vib_rate=6.0, gain=0.5, rate=RATE):
    """One SID-like note. freq may slide to slide_to (exponentially) over the note."""
    n = int(dur * rate)
    out = [0.0] * n
    phase = 0.0
    lfsr_val = 0.0
    lfsr_phase = 0.0
    for i in range(n):
        t = i / rate
        f = freq if slide_to is None else freq * (slide_to / freq) ** (t / dur)
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase = (phase + f / rate) % 1.0
        if wave_kind == "pulse":
            s = 1.0 if phase < duty else -1.0
        elif wave_kind == "tri":
            s = 4 * phase - 1 if phase < 0.5 else 3 - 4 * phase
        elif wave_kind == "saw":
            s = 2 * phase - 1
        else:  # noise: a new random value at the oscillator rate, like the SID's noise
            lfsr_phase += f / rate
            if lfsr_phase >= 1:
                lfsr_phase -= 1
                lfsr_val = RNG.uniform(-1, 1)
            s = lfsr_val
        # ADSR
        if t < attack:
            e = t / attack
        elif t < attack + decay:
            e = 1 - (1 - sustain) * (t - attack) / decay
        else:
            e = sustain
        if t > dur - release:
            e *= max(0.0, (dur - t) / release)
        out[i] = s * e * gain
    return out


def silence(dur, rate=RATE):
    return [0.0] * int(dur * rate)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def at(part, offset, rate=RATE):
    return silence(offset, rate) + part


def lowpass(samples, cutoff, rate=RATE):
    """One-pole low-pass: softens the harsh edges (the SID's filter, roughly)."""
    a = math.exp(-2 * math.pi * cutoff / rate)
    out, y = [], 0.0
    for x in samples:
        y = (1 - a) * x + a * y
        out.append(y)
    return out


def normalise(samples, peak=0.85):
    m = max((abs(s) for s in samples), default=0)
    return samples if m == 0 else [s * peak / m for s in samples]


def write_wav(path, samples, rate=RATE):
    samples = normalise(samples)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples))


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def arp(notes, step, kind="pulse", **kw):
    return cat(*[tone(hz(n), step, kind, **kw) for n in notes])


# --- sound effects (event names follow the Angband-style names used by sound packs) --------------

def effects():
    fx = {}
    fx["TURN"] = lowpass(tone(600, 0.025, "noise", attack=0.001, decay=0.01, sustain=0.2, release=0.01, gain=0.15), 1200)
    fx["DOOR"] = mix(tone(190, 0.32, "saw", attack=0.02, sustain=0.6, release=0.1, slide_to=120, vibrato=0.04, vib_rate=23, gain=0.25),
                     at(lowpass(tone(120, 0.12, "noise", decay=0.05, sustain=0.3, release=0.05, gain=0.5), 600), 0.28))
    fx["COINS"] = cat(*[tone(hz(n), 0.05, "tri", release=0.03, gain=0.4) for n in ["E6", "G6", "C7", "G6", "C7"]])
    fx["STEP"] = lowpass(tone(900, 0.03, "noise", attack=0.001, decay=0.01, sustain=0.3, release=0.015, gain=0.25), 1800)
    fx["BUMP"] = tone(110, 0.12, "pulse", duty=0.3, slide_to=55, decay=0.04, sustain=0.5, release=0.05)
    fx["ENCOUNTER"] = arp(["A4", "C5", "E5", "A5", "E5", "A5"], 0.06, "pulse", duty=0.25, release=0.02)
    fx["HIT"] = mix(tone(2400, 0.09, "noise", decay=0.03, sustain=0.3, release=0.04),
                    tone(160, 0.09, "pulse", duty=0.4, slide_to=80, gain=0.4))
    fx["MISS"] = lowpass(tone(3000, 0.18, "noise", attack=0.03, sustain=0.5, release=0.1, slide_to=600, gain=0.35), 2500)
    fx["HURT"] = tone(420, 0.2, "pulse", duty=0.5, slide_to=110, decay=0.05, sustain=0.6, release=0.06)
    fx["KILL"] = cat(arp(["E5", "C5", "A4", "E4"], 0.06, "tri", release=0.02),
                     tone(1500, 0.25, "noise", sustain=0.4, release=0.2, slide_to=200, gain=0.3))
    fx["SPELL"] = tone(hz("E5"), 0.5, "tri", attack=0.02, sustain=0.7, release=0.2, slide_to=hz("E6"), vibrato=0.02, vib_rate=11)
    fx["TREASURE"] = cat(tone(hz("E6"), 0.08, "tri", release=0.03), tone(hz("B6"), 0.22, "tri", release=0.15))
    fx["TRAP"] = mix(tone(1800, 0.35, "noise", sustain=0.7, release=0.2, gain=0.5),
                     tone(300, 0.35, "saw", slide_to=60, gain=0.35))
    fx["TELEPORT"] = cat(tone(200, 0.25, "pulse", duty=0.2, slide_to=2400, sustain=0.8, release=0.02),
                         tone(2400, 0.25, "pulse", duty=0.2, slide_to=200, sustain=0.8, release=0.1))
    fx["FALL"] = tone(1200, 0.9, "tri", slide_to=60, sustain=0.8, release=0.2, vibrato=0.01, vib_rate=14)
    fx["STAIRS"] = arp(["C4", "E4", "G4", "C5"], 0.09, "tri", release=0.03)
    fx["INN"] = cat(arp(["G4", "C5", "E5"], 0.14, "tri", release=0.05), tone(hz("G5"), 0.4, "tri", release=0.3))
    fx["LEVEL"] = cat(arp(["C5", "E5", "G5", "C6", "E6"], 0.08, "pulse", duty=0.25, release=0.02),
                      tone(hz("G6"), 0.45, "pulse", duty=0.25, release=0.3, vibrato=0.006))
    fx["LEVEL_DOWN"] = arp(["E5", "C5", "A4", "F4", "D4"], 0.12, "pulse", duty=0.4, release=0.05)
    fx["DEATH"] = cat(*[tone(hz(n), d, "tri", attack=0.02, sustain=0.8, release=0.15, vibrato=0.008, vib_rate=5)
                        for n, d in [("A4", 0.45), ("G4", 0.45), ("F4", 0.45), ("E4", 0.45), ("D4", 0.9), ("A3", 1.3)]])
    fx["GONG"] = mix(tone(98, 2.2, "tri", attack=0.005, decay=2.0, sustain=0.0, release=0.1, gain=0.6),
                     tone(101.5, 2.2, "tri", attack=0.005, decay=2.0, sustain=0.0, release=0.1, gain=0.45),
                     tone(196, 1.5, "tri", attack=0.005, decay=1.4, sustain=0.0, release=0.1, gain=0.25))
    fx["SHOCK"] = mix(tone(55, 0.45, "pulse", duty=0.1, sustain=0.9, release=0.05, gain=0.5),
                      tone(5000, 0.45, "noise", sustain=0.6, release=0.05, gain=0.3))
    fx["DRINK"] = cat(*[tone(RNG.uniform(500, 1100), 0.05, "tri", release=0.02, gain=0.4) for _ in range(6)])
    fx["THRONE"] = cat(arp(["D5", "A5"], 0.18, "pulse", duty=0.35, release=0.05),
                       tone(hz("D6"), 0.5, "pulse", duty=0.35, release=0.35, vibrato=0.006))
    fx["BREATH"] = lowpass(tone(1400, 1.2, "noise", attack=0.25, sustain=0.9, release=0.5, slide_to=500, gain=0.7), 1600)
    fx["PARALYZED"] = tone(hz("A2"), 0.7, "pulse", duty=0.5, sustain=0.8, release=0.2, vibrato=0.03, vib_rate=18)
    fx["DRAIN"] = tone(hz("E5"), 0.8, "saw", slide_to=hz("E3"), sustain=0.8, release=0.3, vibrato=0.02, vib_rate=7, gain=0.4)
    fx["CONFUSED"] = cat(*[tone(RNG.uniform(200, 900), 0.07, "pulse", duty=0.3, release=0.02, gain=0.35) for _ in range(5)])
    return fx


# --- music ----------------------------------------------------------------------------------------

def render_song(bpm, bars, lead, bass, arp_chords, rate=44100, wind=0.0):
    """Three SID voices: a pulse lead with vibrato, a triangle bass, a fast arpeggio pulse."""
    beat = 60.0 / bpm
    total = int(bars * 4 * beat * rate)
    out = [0.0] * total

    def place(samples, start):
        i0 = int(start * rate)
        for i, v in enumerate(samples):
            if i0 + i < total:
                out[i0 + i] += v

    t = 0.0
    for note, beats in lead:
        d = beats * beat
        if note != "-":
            place(tone(hz(note), d * 0.97, "pulse", duty=0.3, attack=0.01, decay=0.1, sustain=0.7, release=min(0.25, d / 2),
                       vibrato=0.006, vib_rate=5.5, gain=0.28, rate=rate), t)
        t += d
    t = 0.0
    for note, beats in bass:
        d = beats * beat
        if note != "-":
            place(tone(hz(note), d * 0.95, "tri", attack=0.005, decay=0.2, sustain=0.75, release=0.08, gain=0.42, rate=rate), t)
        t += d
    t = 0.0
    step = beat / 4
    for chord, beats in arp_chords:
        d = beats * beat
        k = 0
        s = 0.0
        while s < d - 1e-9:
            place(tone(hz(chord[k % len(chord)]), step * 0.9, "pulse", duty=0.125, attack=0.002, decay=0.03, sustain=0.4,
                       release=0.02, gain=0.09, rate=rate), t + s)
            k += 1
            s += step
        t += d
    if wind:
        w = lowpass([RNG.uniform(-1, 1) * wind * (0.6 + 0.4 * math.sin(2 * math.pi * i / (rate * 7.3)))
                     for i in range(total)], 400, rate)
        out = [a + b for a, b in zip(out, w)]
    # Fade the last half beat so the loop seam is clean.
    fade = int(beat * 0.5 * rate)
    for i in range(fade):
        out[total - fade + i] *= 1 - i / fade
    return out


def title_theme():
    """"The Proving Grounds": E minor, a stately 84 bpm march, 16 bars, loops."""
    lead = [
        ("E5", 2), ("G5", 1), ("B5", 1),
        ("A5", 2), ("G5", 1), ("F#5", 1),
        ("E5", 2), ("D5", 1), ("E5", 1),
        ("B4", 4),
        ("C5", 2), ("E5", 1), ("G5", 1),
        ("F#5", 2), ("E5", 1), ("D#5", 1),
        ("E5", 3), ("-", 1),
        ("B4", 2), ("D#5", 2),
        ("E5", 1), ("F#5", 1), ("G5", 1), ("A5", 1),
        ("B5", 2), ("A5", 1), ("G5", 1),
        ("A5", 1), ("G5", 1), ("F#5", 1), ("E5", 1),
        ("F#5", 4),
        ("G5", 2), ("F#5", 1), ("E5", 1),
        ("D#5", 2), ("F#5", 2),
        ("E5", 4),
        ("E5", 2), ("-", 2),
    ]
    bass = []
    for root in ["E2", "C2", "A1", "B1", "C2", "B1", "E2", "B1",
                 "E2", "G1", "A1", "B1", "C2", "B1", "E2", "E2"]:
        bass += [(root, 1.5), (root, 0.5), (root, 1), (root.replace("2", "3").replace("1", "2"), 1)]
    em, c, am, b, g = ["E4", "G4", "B4"], ["C4", "E4", "G4"], ["A3", "C4", "E4"], ["B3", "D#4", "F#4"], ["G3", "B3", "D4"]
    chords = [(ch, 4) for ch in [em, c, am, b, c, b, em, b, em, g, am, b, c, b, em, em]]
    assert sum(b for _, b in lead) == 64 and sum(b for _, b in bass) == 64
    return render_song(84, 16, lead, bass, chords, wind=0.03)


def dungeon_theme():
    """"Graph Paper": a sparse, slow A minor loop for the upper levels, 60 bpm, 8 bars."""
    lead = [("-", 4), ("E5", 2), ("D5", 1), ("C5", 1), ("B4", 4),
            ("A4", 2), ("C5", 2), ("E5", 2), ("D5", 2), ("C5", 4), ("B4", 3), ("-", 1), ("A4", 4)]
    bass = [(r, 4) for r in ["A1", "A1", "F1", "E1", "A1", "D2", "E1", "A1"]]
    chords = [(c, 4) for c in [["A3", "C4", "E4"], ["A3", "C4", "E4"], ["F3", "A3", "C4"], ["E3", "G#3", "B3"],
                               ["A3", "C4", "E4"], ["D3", "F3", "A3"], ["E3", "G#3", "B3"], ["A3", "C4", "E4"]]]
    assert sum(b for _, b in lead) == 32
    return render_song(60, 8, lead, bass, chords, wind=0.08)


def encode_ogg(samples, path, rate=44100):
    tmp = path + ".wav"
    write_wav(tmp, samples, rate)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", tmp, "-c:a", "libvorbis", "-q:a", "4", path], check=True)
    os.remove(tmp)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    fx_dir, music_dir = sys.argv[1], sys.argv[2]
    os.makedirs(fx_dir, exist_ok=True)
    os.makedirs(music_dir, exist_ok=True)
    for name, samples in effects().items():
        write_wav(os.path.join(fx_dir, name.lower() + ".wav"), samples)
    encode_ogg(title_theme(), os.path.join(music_dir, "avawiz_title.ogg"))
    encode_ogg(dungeon_theme(), os.path.join(music_dir, "avawiz_graph_paper.ogg"))
    print("done")


if __name__ == "__main__":
    main()
