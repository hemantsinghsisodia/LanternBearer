#!/usr/bin/env python
"""Lantern Keeper Phase G audio pipeline.

Reads the raw CC0 downloads in ArtSource/Audio/raw/, edits them (trim, loop, mix, pitch), normalises
loudness with a two-pass ffmpeg loudnorm per category, and writes OGG (libvorbis q6) into
Assets/Game/Audio/{Music,Stingers,Ambience,Sfx,UI}/ named <Cue_with_underscores>_<n>.ogg.

Run:  python -I ArtSource/Audio/process_audio.py [--only SUBSTR] [--list]

Loudness targets (integrated LUFS): music and ambience -23, effects -18, UI and stingers -20.
True peak is kept at or below -1 dBTP (checked on the encoded OGG).
Clips shorter than 1.2 s are measured as a tiled loop of the clip, because BS.1770 gating needs 400 ms blocks.
Requires ffmpeg and numpy.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
RAW = HERE / "raw"
WORK = HERE / "work"
OUT = REPO / "Assets" / "Game" / "Audio"
SR = 48000
TP_LIMIT = -1.0          # dBTP the shipped files must stay under
TP_AIM = -1.5            # loudnorm aims lower to leave room for the Vorbis encode
SILENCE_DB = -60.0
TARGETS = {"music": -23.0, "ambience": -23.0, "sfx": -18.0, "ui": -20.0, "stinger": -20.0}
FOLDERS = {"music": "Music", "ambience": "Ambience", "sfx": "Sfx", "ui": "UI", "stinger": "Stingers"}
MIN_MEASURE_S = 1.2

K1 = RAW / "K1/x/Audio"
K2 = RAW / "K2/x/Audio"
K3 = RAW / "K3/x/OGG"
R1 = RAW / "R1/x"
R2 = RAW / "R2/x"
R3 = RAW / "R3/x"
R4 = RAW / "R4/x"
F1 = RAW / "F1/x/Fantozzi-footsteps/flac"
W1 = RAW / "W1"


# ---------------------------------------------------------------- ffmpeg helpers
def run(cmd, data=None):
    p = subprocess.run(cmd, input=data, capture_output=True)
    if p.returncode != 0:
        raise RuntimeError("ffmpeg failed: %s\n%s" % (" ".join(map(str, cmd)), p.stderr.decode("utf8", "replace")[-1500:]))
    return p


def read(path, ch=2):
    """Decode any file to float32 (n, ch) at SR."""
    p = run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-"])
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, ch).copy()


def ffx(x, af):
    """Run an ffmpeg audio filter chain over an array."""
    ch = x.shape[1]
    p = run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", "-", "-af", af,
             "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-"], np.ascontiguousarray(x, dtype=np.float32).tobytes())
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, ch).copy()


def pitch(x, ratio):
    return ffx(x, "asetrate=%d,aresample=%d" % (round(SR * ratio), SR))


def lowpass(x, hz):
    return ffx(x, "lowpass=f=%d" % hz)


def write_wav(path, x):
    ch = x.shape[1]
    run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", "-", "-c:a", "pcm_f32le", str(path)],
        np.ascontiguousarray(x, dtype=np.float32).tobytes())


# ---------------------------------------------------------------- editing helpers
def secs(x):
    return len(x) / SR


def seg(x, t0, t1=None):
    a = int(t0 * SR)
    b = len(x) if t1 is None else int(t1 * SR)
    return x[a:b].copy()


def to_mono(x):
    return x.mean(axis=1, keepdims=True).astype(np.float32)


def trim_silence(x, db=SILENCE_DB):
    thr = 10 ** (db / 20)
    loud = np.where(np.abs(x).max(axis=1) > thr)[0]
    if len(loud) == 0:
        return x
    return x[loud[0]:loud[-1] + 1].copy()


def fade(x, fi=0.0, fo=0.0):
    x = x.copy()
    n = len(x)
    a = min(int(fi * SR), n)
    b = min(int(fo * SR), n)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a, dtype=np.float32)[:, None]
    if b > 0:
        x[n - b:] *= np.linspace(1, 0, b, dtype=np.float32)[:, None]
    return x


def cap(x, max_s, fo):
    if secs(x) > max_s:
        x = x[:int(max_s * SR)]
    return fade(x, 0.002, fo)


def rising_zero(x, lo, hi):
    """Index in [lo, hi) where the mono signal crosses zero upward, nearest the middle of the window."""
    m = x.mean(axis=1)
    lo = max(lo, 1)
    hi = min(hi, len(m))
    if hi <= lo:
        return None
    idx = np.where((m[lo - 1:hi - 1] <= 0) & (m[lo:hi] > 0))[0]
    if len(idx) == 0:
        return None
    return int(idx[0] + lo)


def make_loop(x, xf_s):
    """Snap start and end to upward zero crossings, then equal-power crossfade the tail into the head."""
    win = int(0.02 * SR)
    s = rising_zero(x, 1, win) or 0
    e_idx = rising_zero(x, len(x) - win, len(x))
    e = e_idx if e_idx else len(x)
    y = x[s:e]
    X = int(xf_s * SR)
    n = len(y)
    if n < 4 * X:
        raise ValueError("clip too short for a %.3f s crossfade" % xf_s)
    t = np.linspace(0, 1, X, dtype=np.float32)[:, None]
    out = y[:n - X].copy()
    out[:X] = y[:X] * np.sin(t * np.pi / 2) + y[n - X:] * np.cos(t * np.pi / 2)
    return out


def tile_to(x, min_s):
    need = int(min_s * SR)
    if len(x) >= need:
        return x
    reps = int(np.ceil(need / len(x)))
    return np.tile(x, (reps, 1))


def add_circular(buf, clip, start):
    n = len(buf)
    i = start % n
    k = len(clip)
    first = min(k, n - i)
    buf[i:i + first] += clip[:first]
    if first < k:
        rest = clip[first:]
        buf[:len(rest)] += rest[:n]


def db(g):
    return 10 ** (g / 20)


def texture(clip, length_s, seg_s, xf_s, rng):
    """Random segments of a short clip, equal-power crossfaded, closed into a seamless loop."""
    n = int(length_s * SR)
    X = int(xf_s * SR)
    L = int(seg_s * SR)
    parts = []
    total = 0
    while total < n + X:
        start = int(rng.integers(0, max(1, len(clip) - L)))
        part = clip[start:start + L]
        if len(part) < L:
            part = np.vstack([part, clip[:L - len(part)]])
        parts.append(part)
        total += L - X
    out = parts[0].copy()
    t = np.linspace(0, 1, X, dtype=np.float32)[:, None]
    for p in parts[1:]:
        head = out[len(out) - X:] * np.cos(t * np.pi / 2) + p[:X] * np.sin(t * np.pi / 2)
        out = np.vstack([out[:len(out) - X], head, p[X:]])
    out = out[:n + X]
    # close the loop: crossfade the extra tail into the head
    body = out[:n].copy()
    body[:X] = out[:X] * np.sin(t * np.pi / 2) + out[n:n + X] * np.cos(t * np.pi / 2)
    return body


def swell(x, rng, cycles=(3, 5), depth=0.25):
    """Slow seamless level movement: integer cycles per loop so the ends meet."""
    n = len(x)
    ph = np.arange(n) / n
    g = np.ones(n, dtype=np.float32)
    for c in cycles:
        g += (depth / len(cycles)) * np.sin(2 * np.pi * (c * ph + rng.random()))
    return x * g[:, None]


def surf(length_s, waves, gap, gain_db, rng, fade_in=0.35):
    """Waves placed at random times, wrapping round the loop end, so no seam exists."""
    n = int(length_s * SR)
    buf = np.zeros((n, 2), dtype=np.float32)
    t = rng.uniform(0, gap[1])
    while t < length_s:
        w = waves[int(rng.integers(0, len(waves)))]
        w = fade(w, fade_in, min(0.9, secs(w) / 2)) * db(rng.uniform(*gain_db))
        add_circular(buf, w, int(t * SR))
        t += rng.uniform(*gap)
    return buf


def mix(*layers):
    n = max(len(l[0]) for l in layers)
    out = np.zeros((n, 2), dtype=np.float32)
    for arr, gdb in layers:
        out[:len(arr)] += arr * db(gdb)
    return out


# ---------------------------------------------------------------- loudness
def measure(x):
    """Return (integrated LUFS, true peak dBTP). Short clips are tiled for the loudness read only."""
    ch = x.shape[1]
    def ebur(arr):
        p = run(["ffmpeg", "-nostats", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", "-",
                 "-af", "ebur128=peak=true", "-f", "null", "-"], np.ascontiguousarray(arr, dtype=np.float32).tobytes())
        txt = p.stderr.decode("utf8", "replace")
        txt = txt[txt.rfind("Summary:"):]
        i = re.search(r"I:\s+(-?[\d.]+|-inf)\s+LUFS", txt)
        tp = re.search(r"Peak:\s+(-?[\d.]+|-inf)\s+dBFS", txt)
        f = lambda m: float("-inf") if m is None or m.group(1) == "-inf" else float(m.group(1))
        return f(i), f(tp)
    tiled = tile_to(x, MIN_MEASURE_S)
    lufs, tp = ebur(tiled)
    if tiled is not x:
        _, tp = ebur(x if len(x) > 4800 else tile_to(x, 0.2))
    return lufs, tp


def encode(x, name, target, tp_aim, method, gain=0.0):
    """One encode to OGG. method 'loudnorm' is the two-pass linear loudnorm; 'gain+limiter' applies a gain
    then a fast brickwall limiter (used for transient clips whose peaks would stop loudnorm reaching target)."""
    ch = x.shape[1]
    wav = WORK / (name + ".wav")
    ogg = WORK / (name + ".ogg")
    write_wav(wav, x)
    if method == "loudnorm":
        tiled = tile_to(x, MIN_MEASURE_S)
        p = run(["ffmpeg", "-nostats", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", "-", "-af",
                 "loudnorm=I=%g:TP=%g:LRA=20:print_format=json" % (target, tp_aim), "-f", "null", "-"],
                np.ascontiguousarray(tiled, dtype=np.float32).tobytes())
        txt = p.stderr.decode("utf8", "replace")
        m = json.loads(txt[txt.rfind("{"):txt.rfind("}") + 1])
        af = ("loudnorm=I=%g:TP=%g:LRA=20:measured_I=%s:measured_LRA=%s:measured_TP=%s:measured_thresh=%s:offset=%s:linear=true,aresample=%d"
              % (target, tp_aim, m["input_i"], m["input_lra"], m["input_tp"], m["input_thresh"], m["target_offset"], SR))
    else:
        af = "volume=%.3fdB,alimiter=limit=%.4f:attack=0.1:release=1.5:level=0" % (gain, db(tp_aim))
    run(["ffmpeg", "-v", "error", "-y", "-i", str(wav), "-af", af, "-c:a", "libvorbis", "-q:a", "6", "-ar", str(SR), "-ac", str(ch), str(ogg)])
    return ogg


def normalise(x, name, cat):
    """Returns (ogg path, LUFS, true peak, method). Tries loudnorm first, then an iterated gain+limiter."""
    target = TARGETS[cat]
    ch = x.shape[1]
    def score(l, tp):
        return max(abs(l - target) - 1.0, 0) + max(tp - TP_LIMIT, 0) * 4

    def check(ogg):
        y = read(ogg, ch)
        return measure(y)

    ogg = encode(x, name, target, TP_AIM, "loudnorm")
    lufs, tp = check(ogg)
    best = (score(lufs, tp), ogg, lufs, tp, "loudnorm")
    if best[0] == 0:
        return best[1:]
    in_lufs, _ = measure(x)
    gain = target - in_lufs
    aim = -2.0
    for _ in range(6):
        ogg = encode(x, name + "_g", target, aim, "gain+limiter", gain)
        lufs, tp = check(ogg)
        s = score(lufs, tp)
        if s < best[0] or best[4] == "loudnorm":
            best = (s, ogg, lufs, tp, "gain+limiter")
            (WORK / (name + "_best.ogg")).write_bytes(ogg.read_bytes())
        if s == 0:
            break
        if tp > TP_LIMIT:
            aim -= 0.5
        gain += min(max(target - lufs, -3.0), 3.0) * 0.9
    return (WORK / (name + "_best.ogg") if best[4] == "gain+limiter" else best[1], best[2], best[3], best[4])


# ---------------------------------------------------------------- job table
class Job:
    def __init__(self, out, cue, cat, build, kind="oneshot", xf=0.005, folder=None, ch="mono", note=""):
        self.out = out
        self.cue = cue
        self.cat = cat
        self.build = build
        self.kind = kind          # oneshot | loop (trim, crossfade xf) | loopready (crossfade 5 ms only) | built (already seamless)
        self.xf = xf
        self.folder = folder or FOLDERS[cat]
        self.ch = 1 if ch == "mono" else 2
        self.note = note


def jobs():
    J = []
    rng = lambda seed: np.random.default_rng(seed)

    def one(out, cue, cat, path, fo=0.02, max_s=None, pitch_r=None, lp=None, ch="mono", t0=None, t1=None, folder=None, note=""):
        def build():
            x = read(path, 1 if ch == "mono" else 2)
            if t0 is not None or t1 is not None:
                x = seg(x, t0 or 0, t1)
            x = trim_silence(x)
            if pitch_r:
                x = pitch(x, pitch_r)
            if lp:
                x = lowpass(x, lp)
            return cap(x, max_s or 99, fo)
        J.append(Job(out, cue, cat, build, "oneshot", ch=ch, folder=folder, note=note))

    # --- footsteps
    for i, n in enumerate(["SandL1", "SandR1", "SandL2", "SandR2"], 1):
        one("Footstep_Grass_%d" % i, "Footstep.Grass", "sfx", F1 / ("Fantozzi-%s.flac" % n), max_s=0.45, note="F1 sand step")
    for i, n in enumerate(["footstep00", "footstep02", "footstep06", "footstep07"], 1):
        one("Footstep_Dirt_%d" % i, "Footstep.Dirt", "sfx", K3 / (n + ".ogg"), max_s=0.35, note="K3 " + n)
    for i, n in enumerate(["StoneL1", "StoneR1", "StoneL2", "StoneR2"], 1):
        one("Footstep_Rock_%d" % i, "Footstep.Rock", "sfx", F1 / ("Fantozzi-%s.flac" % n), max_s=0.45, note="F1 stone step")
    for i, n in enumerate(["splash_09", "splash_10", "splash_15", "splash_06"], 1):
        one("Footstep_Water_%d" % i, "Footstep.Water", "sfx", R4 / (n + ".ogg"), max_s=0.42, fo=0.06, pitch_r=0.85, note="R4 %s, pitched x0.85, shortened" % n)

    # --- keeper
    for i, n in enumerate(["impactSoft_heavy_000", "impactSoft_heavy_002", "impactSoft_heavy_004"], 1):
        one("Keeper_Hit_%d" % i, "Keeper.Hit", "sfx", K2 / (n + ".ogg"), fo=0.05, max_s=0.5, note="K2 " + n)
    for i, n in enumerate(["cloth1", "cloth2", "cloth3"], 1):
        one("Keeper_Interact_%d" % i, "Keeper.Interact", "sfx", K3 / (n + ".ogg"), fo=0.05, max_s=0.6, note="K3 " + n)

    # --- lantern
    J.append(Job("Lantern_Crackle_1", "Lantern.Crackle", "sfx",
                 lambda: trim_silence(read(RAW / "F2/fire-1_0.ogg", 1)),
                 "loop", xf=0.3, note="F2 fire-1, loop with 0.3 s crossfade"))
    for i, n in enumerate(["spell_fire_06", "spell_fire_07"], 1):
        one("Lantern_Refuel_%d" % i, "Lantern.Refuel", "sfx", R3 / (n + ".ogg"), fo=0.12, note="R3 " + n)
    J.append(Job("Lantern_Sputter_1", "Lantern.Sputter", "sfx",
                 lambda: cap(seg(read(RAW / "F2/fire-1_0.ogg", 1), 0.9, 1.5), 0.6, 0.15), note="F2 0.9-1.5 s, fast fade"))
    one("Lantern_Sputter_2", "Lantern.Sputter", "sfx", R3 / "spell_fire_02.ogg", fo=0.2, max_s=0.7, note="R3 spell_fire_02, 0.7 s")
    for i, n in enumerate(["spell_fire_05", "spell_fire_01"], 1):
        one("Lantern_DeathGutter_%d" % i, "Lantern.DeathGutter", "sfx", R3 / (n + ".ogg"), fo=0.3, pitch_r=0.75, note="R3 %s slowed x0.75" % n)

    # --- creatures
    for i, r in enumerate([1.0, 1.122, 0.891], 1):
        one("Firefly_Chime_%d" % i, "Firefly.Chime", "sfx", RAW / "B1/pleasing-bell.wav", fo=0.1, pitch_r=None if r == 1.0 else r,
            note="B1 bell, pitch x%.3f" % r)
    for i, n in enumerate(["bug_01", "bug_02", "bug_03"], 1):
        one("Moth_Flutter_%d" % i, "Moth.Flutter", "sfx", R2 / (n + ".ogg"), fo=0.06, note="R2 " + n + " (guess: papery quality not auditioned)")
    for i, n in enumerate(["weird_01", "weird_03"], 1):
        one("Shade_Steal_%d" % i, "Shade.Steal", "sfx", R2 / (n + ".ogg"), fo=0.08, note="R2 " + n + " (guess)")

    # --- beacons
    for i, n in enumerate(["spell_fire_03", "spell_fire_04"], 1):
        one("Beacon_Ignite_%d" % i, "Beacon.Ignite", "sfx", R3 / (n + ".ogg"), fo=0.2, note="R3 " + n)
    J.append(Job("Beacon_Fire_1", "Beacon.Fire", "sfx",
                 lambda: pitch(trim_silence(read(RAW / "F2/fire-1_0.ogg", 1)), 0.88), "loop", xf=0.3, note="F2 fire-1 pitched x0.88, loop"))
    one("Beacon_Fizzle_1", "Beacon.Fizzle", "sfx", R4 / "bubble_02.ogg", fo=0.1, note="R4 bubble_02")
    one("Beacon_Fizzle_2", "Beacon.Fizzle", "sfx", R1 / "sfx100v2_air_02.ogg", fo=0.2, max_s=0.9, note="R1 air_02 hiss")

    # --- water and weather one-shots
    for i, n in enumerate(["splash_02", "splash_04", "splash_08", "splash_13"], 1):
        one("Water_Splash_%d" % i, "Water.Splash", "sfx", R4 / (n + ".ogg"), fo=0.1, max_s=1.0, note="R4 " + n)
    th = R1 / "sfx100v2_thunder_01.ogg"
    one("Thunder_Crack_1", "Thunder.Crack", "sfx", th, fo=0.5, t0=0.6, t1=2.4, ch="stereo", note="R1 thunder_01, 0.6-2.4 s")
    one("Thunder_Crack_2", "Thunder.Crack", "sfx", th, fo=0.6, t0=0.6, t1=2.4, pitch_r=0.8, ch="stereo", note="R1 thunder_01 crack, pitched x0.8")
    one("Thunder_Rumble_1", "Thunder.Rumble", "sfx", th, fo=0.5, t0=0.7, lp=700, ch="stereo", note="R1 thunder_01 from 0.7 s, lowpass 700 Hz")
    one("Thunder_Rumble_2", "Thunder.Rumble", "sfx", th, fo=0.6, t0=0.7, lp=500, pitch_r=0.8, ch="stereo", note="R1 thunder_01, lowpass 500 Hz, pitched x0.8")
    one("Ambience_Owl_1", "Ambience.Owl", "ambience", RAW / "N3/1763.mp3", fo=0.15, folder="Sfx", note="N3 Tawny Owl #1, one-shot (Sfx folder, ambience loudness)")

    # --- UI
    for i, n in enumerate(["select_008", "select_007"], 1):
        one("UI_Hover_%d" % i, "UI.Hover", "ui", K1 / (n + ".ogg"), fo=0.01, max_s=0.2, note="K1 " + n)
    for i, n in enumerate(["click_001", "click_004"], 1):
        one("UI_Click_%d" % i, "UI.Click", "ui", K1 / (n + ".ogg"), fo=0.01, max_s=0.25, note="K1 " + n)
    for i, n in enumerate(["back_001", "back_003"], 1):
        one("UI_Back_%d" % i, "UI.Back", "ui", K1 / (n + ".ogg"), fo=0.02, max_s=0.3, note="K1 " + n)
    one("UI_PauseOpen_1", "UI.PauseOpen", "ui", K1 / "open_002.ogg", fo=0.03, note="K1 open_002")
    one("UI_PauseClose_1", "UI.PauseClose", "ui", K1 / "close_002.ogg", fo=0.03, note="K1 close_002")
    for i, n in enumerate(["book_01", "book_02", "book_03"], 1):
        one("UI_PageTurn_%d" % i, "UI.PageTurn", "ui", R3 / (n + ".ogg"), fo=0.08, max_s=0.9, note="R3 " + n)
    for i, n in enumerate(["toggle_001", "toggle_003"], 1):
        one("UI_Toggle_%d" % i, "UI.Toggle", "ui", K1 / (n + ".ogg"), fo=0.02, max_s=0.2, note="K1 " + n)

    # --- stingers (2-4 s, 150 ms fade-out)
    one("Stinger_BeaconLit_1", "Stinger.BeaconLit", "stinger", RAW / "Stinger_BeaconLit/Win sound.wav", fo=0.15, max_s=2.5, ch="stereo", note="Listener win sound, first 2.5 s")
    one("Stinger_Win_1", "Stinger.Win", "stinger", RAW / "Stinger_Win/Heavy_ConceptB.wav", fo=0.15, max_s=4.0, ch="stereo", note="cynicmusic fanfare, first 4 s")
    one("Stinger_Lose_1", "Stinger.Lose", "stinger", RAW / "Stinger_Lose/game_over_iv_0.mp3", fo=0.15, max_s=4.0, ch="stereo", note="Kistol Game Over IV, first 4 s")

    # --- music (stereo, streaming)
    def music(out, path, kind, xf, note):
        J.append(Job(out, out.rsplit("_", 1)[0].replace("_", "."), "music", lambda p=path: read(p, 2), kind, xf=xf, ch="stereo", note=note))
    music("Music_Menu_1", RAW / "Music_Menu/first_light_particles_0.wav", "loop", 2.0, "Yoiyami First Light Particles; not stated as loop: 2 s crossfade")
    music("Music_Island1_1", RAW / "Music_Island1/Ambient-Loop-isaiah658_0.ogg", "loopready", 0.005, "isaiah658 Ambient Relaxing Loop (seamless loop)")
    music("Music_Island2_1", RAW / "Music_Island2/song21_0.mp3", "loop", 2.0, "cynicmusic song21; not a loop: 2 s crossfade")
    music("Music_Island3_1", RAW / "Music_Island3/Cleyton RX - Underwater_0.mp3", "loopready", 0.005, "Cleyton Kauffman Underwater Theme (loop)")
    music("Music_Island4_1", RAW / "Music_Island4/the_world_fell_silent_loop.flac", "loopready", 0.005, "Tsorthan Grove loop version")
    music("Tension_Island1_1", RAW / "Tension_Island1/airy_0.mp3", "loop", 2.0, "SRG774 Airy; crossfade advised on the page: 2 s")
    music("Tension_Island2_1", RAW / "Tension_Island2/x/Paranoid Truth.mp3", "loop", 3.0, "Tsorthan Grove Paranoid Truth; not a loop: 3 s crossfade (Island2 and Island4 share it)")
    music("Tension_Island3_1", RAW / "Tension_Island3/Juhani Junkala - Post Apocalyptic Wastelands [Loop Ready].ogg", "loopready", 0.005, "SubspaceAudio Horror Atmosphere (seamless loop)")
    # the tension track for Island4 is the Island2 file; no separate output

    # --- ambience beds (90 s, stereo, built to loop)
    def waves(*nums):
        return [read(W1 / ("wave_0%d_cc0-18363__jasinski__alkaibeach.flac" % n), 2) for n in nums]

    def bed1():
        r = rng(11)
        w = waves(1, 2, 3, 4)
        sf = surf(90, [w[2], w[1]], (5.0, 9.0), (-4, 0), r, 0.5)
        wash = texture(lowpass(np.vstack(w), 500), 90, 6.0, 1.5, r)
        wind = swell(texture(lowpass(read(RAW / "N4/short wind sound.wav", 2), 1800), 90, 3.0, 1.0, r), r, (3, 7), 0.5)
        cr = texture(read(RAW / "N1/crickets_1.mp3", 2), 90, 8.0, 1.5, r)
        return mix((sf, -2), (wash, -14), (wind, -8), (cr, -9))

    def bed2():
        r = rng(22)
        w = waves(1, 2, 3, 4)
        sf = surf(90, [w[2], w[1]], (8.0, 14.0), (-8, -4), r, 0.5)
        wash = texture(lowpass(np.vstack(w), 400), 90, 6.0, 1.5, r)
        cr = swell(texture(read(RAW / "N1/crickets_1.mp3", 2), 90, 8.0, 1.5, r), r, (2, 5), 0.2)
        return mix((cr, -2), (sf, -9), (wash, -22))

    def bed3():
        r = rng(33)
        w = waves(1, 2, 3, 4)
        a = surf(90, [w[0], w[3]], (2.5, 4.5), (-2, 0), r, 0.4)
        b = surf(90, [w[0], w[3], w[1]], (3.0, 5.5), (-5, -2), r, 0.4)
        wash = texture(lowpass(np.vstack(w), 700), 90, 6.0, 1.5, r)
        return mix((a, 0), (b, -2), (wash, -10))

    def tide():
        r = rng(44)
        w = waves(1, 2, 3, 4)
        a = surf(60, w, (3.5, 6.5), (-3, 0), r, 0.45)
        wash = texture(lowpass(np.vstack(w), 600), 60, 6.0, 1.5, r)
        return mix((a, 0), (wash, -12))

    J.append(Job("Ambience_Island1_1", "Ambience.Island1", "ambience", bed1, "built", ch="stereo", note="W1 gentle surf + N4 wind + N1 crickets"))
    J.append(Job("Ambience_Island2_1", "Ambience.Island2", "ambience", bed2, "built", ch="stereo", note="N1 crickets + quiet W1 surf"))
    J.append(Job("Ambience_Island3_1", "Ambience.Island3", "ambience", bed3, "built", ch="stereo", note="W1 stronger layered surf"))
    J.append(Job("Ambience_Tide_1", "Ambience.Tide", "ambience", tide, "built", ch="stereo", note="W1 four waves, 60 s tide loop"))
    J.append(Job("Ambience_Island4_1", "Ambience.Island4", "ambience", lambda: read(RAW / "N5/0625.mp3", 2), "loop", xf=2.0, ch="stereo",
                 note="N5 Strong Wind in a Village, 2 s crossfade"))
    J.append(Job("Ambience_Rain_1", "Ambience.Rain", "ambience", lambda: read(RAW / "RL/x/3.ogg", 2), "loopready", ch="stereo", note="RL loop 3 (45 s)"))
    J.append(Job("Ambience_Rain_2", "Ambience.Rain", "ambience", lambda: read(RAW / "RL/x/1.ogg", 2), "loopready", ch="stereo", note="RL loop 1 (27 s)"))
    J.append(Job("Ambience_Gust_1", "Ambience.Gust", "ambience", lambda: read(RAW / "N6/wind woosh loop.ogg", 2), "loopready", ch="stereo", note="N6 wind whoosh loop"))
    return J


# ---------------------------------------------------------------- main
def process(job):
    x = job.build()
    if job.kind == "loop":
        x = make_loop(trim_silence(x), job.xf)
    elif job.kind == "loopready":
        x = make_loop(x, job.xf)
    ogg, lufs, tp, method = normalise(x, job.out, job.cat)
    dest = OUT / job.folder
    dest.mkdir(parents=True, exist_ok=True)
    final = dest / (job.out + ".ogg")
    final.write_bytes(ogg.read_bytes())
    return {"file": job.folder + "/" + job.out + ".ogg", "cue": job.cue, "cat": job.cat, "target": TARGETS[job.cat],
            "lufs": round(lufs, 2), "tp": round(tp, 2), "method": method, "dur": round(secs(x), 2),
            "bytes": final.stat().st_size, "note": job.note}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    rows = []
    for job in jobs():
        if a.only and a.only not in job.out:
            continue
        if a.list:
            print(job.out, job.cue, job.cat)
            continue
        r = process(job)
        rows.append(r)
        flag = "" if abs(r["lufs"] - r["target"]) <= 1.0 and r["tp"] <= TP_LIMIT else "  <-- OUT OF TOLERANCE"
        print("%-28s %-22s %6.1f LUFS (target %5.1f)  TP %5.1f  %-12s %6.2fs%s" % (
            Path(r["file"]).name, r["cue"], r["lufs"], r["target"], r["tp"], r["method"], r["dur"], flag), flush=True)
    if rows:
        part = WORK / ("report-%s.json" % (a.only or "all"))
        part.write_text(json.dumps(rows, indent=1))
        bad = [r for r in rows if abs(r["lufs"] - r["target"]) > 1.0 or r["tp"] > TP_LIMIT]
        print("\n%d files, %d out of tolerance, total %.1f MB" % (len(rows), len(bad), sum(r["bytes"] for r in rows) / 1e6))
    return 0


if __name__ == "__main__":
    sys.exit(main())
