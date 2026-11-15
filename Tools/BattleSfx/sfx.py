import numpy as np
from scipy import signal
from scipy.io import wavfile
SR = 44100

def T(d): return np.arange(int(d * SR)) / SR
def sos_f(kind, f, order=2): return signal.butter(order, f, kind, fs=SR, output='sos')
def filt(x, kind, f, order=2): return signal.sosfilt(sos_f(kind, f, order), x)
def noise(d, rng): return rng.standard_normal(int(d * SR))
def pad(x, n): return np.concatenate([x, np.zeros(max(0, n - len(x)))])[:n]
def mix(*xs):
    n = max(len(x) for x in xs); return sum(pad(x, n) for x in xs)

def sweep_bp(x, f_from, f_mid, f_to, q=1.8, frames=64):
    """Bandpass with a moving center frequency (time-varying, block-wise)."""
    n = len(x); out = np.zeros(n); edges = np.linspace(0, n, frames + 1).astype(int)
    zi = None
    for k in range(frames):
        t = (k + 0.5) / frames
        f = np.interp(t, [0, 0.5, 1], [f_from, f_mid, f_to])
        lo, hi = max(40, f / (1 + 1 / q)), min(SR / 2 - 100, f * (1 + 1 / q))
        sos = signal.butter(2, [lo, hi], 'bandpass', fs=SR, output='sos')
        if zi is None: zi = signal.sosfilt_zi(sos) * 0
        seg, zi = signal.sosfilt(sos, x[edges[k]:edges[k + 1]], zi=zi)
        out[edges[k]:edges[k + 1]] = seg
    return out

def sweep_lp(x, f_from, f_to, frames=80):
    n = len(x); out = np.zeros(n); edges = np.linspace(0, n, frames + 1).astype(int); zi = None
    for k in range(frames):
        f = f_from * (f_to / f_from) ** ((k + 0.5) / frames)
        sos = signal.butter(2, min(f, SR / 2 - 100), 'lowpass', fs=SR, output='sos')
        if zi is None: zi = signal.sosfilt_zi(sos) * 0
        seg, zi = signal.sosfilt(sos, x[edges[k]:edges[k + 1]], zi=zi); out[edges[k]:edges[k + 1]] = seg
    return out

def env_ar(n, attack, release_shape=1.0):
    t = np.linspace(0, 1, n); a = np.clip(t / max(attack, 1e-4), 0, 1)
    r = np.clip((1 - t) / max(1 - attack, 1e-4), 0, 1) ** release_shape
    return np.minimum(a, r)

def tailfade(n, frac=0.25):
    k = max(1, int(n * frac)); w = np.ones(n); w[-k:] = 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k)); return w

def expdec(d, tau): t = T(d); return np.exp(-t / tau)

def boom(d, f0, f1, tau, drive=1.5):
    t = T(d); f = f1 + (f0 - f1) * np.exp(-t / 0.06)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.tanh(np.sin(ph) * drive) * np.exp(-t / tau) * tailfade(len(t))

def partials(d, f0, ratios, taus, amps, rng, detune=0.002):
    t = T(d); x = np.zeros(len(t))
    for r, tau, a in zip(ratios, taus, amps):
        f = f0 * r * (1 + rng.uniform(-detune, detune))
        x += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / tau)
    return x * tailfade(len(t))

def reverb(x, length, mix_, rng, predelay=0.012, bright=6000):
    n = int(length * SR); ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 6.5, n))
    ir = filt(ir, 'lowpass', bright); ir /= np.sqrt((ir ** 2).sum())
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    wet = signal.fftconvolve(x, ir)
    return mix(x * (1 - mix_ * 0.5), wet * mix_ * 0.7)

def finish(x, peak_db, fade=0.02):
    x = x - np.mean(x)
    a = np.abs(x); thr = a.max() * 10 ** (-58 / 20)
    last = np.nonzero(a > thr)[0]
    if len(last): x = x[:min(len(x), last[-1] + int(0.02 * SR))]
    f = int(fade * SR); x[-f:] *= np.linspace(1, 0, f)
    x = x * (10 ** (peak_db / 20) / (np.abs(x).max() + 1e-12))
    return x

def write(name, x):
    wavfile.write(name + '.wav', SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))

# ---------------------------------------------------------------- designs
def swing_light(rng):
    d = rng.uniform(0.2, 0.26); x = noise(d, rng)
    w = sweep_bp(x, rng.uniform(500, 700), rng.uniform(2200, 2900), rng.uniform(700, 1000), q=1.6)
    air = filt(noise(d, rng), 'highpass', 5000) * 0.15
    e = env_ar(len(w), 0.55, 1.6)
    return finish(reverb((w + air) * e, 0.35, 0.18, rng), -5)

def swing_heavy(rng):
    d = rng.uniform(0.34, 0.42); x = noise(d, rng)
    w = sweep_bp(x, 220, rng.uniform(1100, 1400), 350, q=1.3)
    low = filt(noise(d, rng), 'lowpass', 220) * 0.9
    e = env_ar(len(w), 0.6, 1.4)
    return finish(reverb(np.tanh((w + low) * e * 2.0), 0.45, 0.2, rng), -4)

def dodge(rng):
    d = 0.24; x = noise(d, rng)
    w = sweep_bp(x, 1400, 3600, 1800, q=1.4)
    flutter = 1 + 0.25 * np.sin(2 * np.pi * rng.uniform(22, 32) * T(d)[:len(w)])
    return finish(w * flutter * env_ar(len(w), 0.35, 1.8), -7)

def jump(rng):
    d = 0.16; x = noise(d, rng)
    puff = filt(x, 'bandpass', [250, 1600]) * env_ar(len(x), 0.08, 2.5)
    scuff = filt(noise(0.05, rng), 'bandpass', [1500, 5000]) * expdec(0.05, 0.012) * 0.6
    return finish(mix(puff, scuff), -9)

def parry(rng):
    f0 = rng.uniform(560, 640)
    ring = partials(1.4, f0, [1.0, 2.71, 5.15, 8.42, 12.2], [0.55, 0.32, 0.18, 0.1, 0.06], [1.0, 0.7, 0.5, 0.35, 0.25], rng, 0.004)
    hit = filt(noise(0.06, rng), 'highpass', 2500) * expdec(0.06, 0.008) * 2.5
    shing = sweep_bp(noise(0.35, rng), 7000, 5000, 3500, q=4) * expdec(0.35, 0.12) * 0.6
    thump = boom(0.15, 180, 110, 0.04, 1.2) * 0.5
    return finish(reverb(np.tanh(mix(ring, hit, shing, thump) * 1.4), 1.0, 0.3, rng), -3)

def clash(rng):
    f0 = rng.uniform(420, 520)
    ring = partials(0.6, f0, [1.0, 2.4, 4.6, 7.3], [0.22, 0.14, 0.08, 0.05], [1, 0.6, 0.4, 0.3], rng, 0.01)
    crack = filt(noise(0.08, rng), 'highpass', 1200) * expdec(0.08, 0.015) * 2
    thump = boom(0.25, 140, 70, 0.07, 1.6)
    return finish(reverb(np.tanh(mix(ring * 0.7, crack, thump) * 1.3), 0.7, 0.25, rng), -4)

def guard_break(rng):
    sub = boom(1.0, 120, 45, 0.28, 2.5)
    crack = np.tanh(filt(noise(0.25, rng), 'bandpass', [600, 7000]) * expdec(0.25, 0.04) * 6) * 0.8
    clang = partials(0.7, rng.uniform(300, 360), [1, 2.3, 3.9, 6.1], [0.25, 0.15, 0.1, 0.06], [0.8, 0.6, 0.4, 0.3], rng, 0.01) * 0.6
    crackle = np.zeros(int(0.8 * SR))
    for _ in range(40):
        p = rng.integers(int(0.05 * SR), len(crackle) - 400); crackle[p:p + 300] += filt(noise(300 / SR, rng), 'highpass', 2000)[:300] * np.exp(-np.arange(300) / 60) * rng.uniform(0.1, 0.4)
    crackle *= np.exp(-np.arange(len(crackle)) / (0.25 * SR))
    return finish(reverb(np.tanh(mix(sub, crack, clang, crackle) * 1.2), 1.2, 0.3, rng), -1)

def projectile_hit(rng):
    burst = filt(noise(0.12, rng), 'bandpass', [400, 5000]) * expdec(0.12, 0.025) * 1.5
    thump = boom(0.25, 160, 80, 0.06, 1.4)
    sizzle = filt(noise(0.45, rng), 'highpass', 4000) * expdec(0.45, 0.12) * 0.35 * (1 + 0.5 * rng.standard_normal(int(0.45 * SR)) * 0)
    return finish(reverb(np.tanh(mix(burst, thump, sizzle) * 1.3), 0.6, 0.22, rng), -4)

def explosion(rng):
    d = 2.0
    sub = boom(d, 95, 32, 0.45, 2.2)
    body = sweep_lp(noise(d, rng), 7000, 180) * np.exp(-T(d) / 0.35) * 1.6 * tailfade(int(d * SR))
    crackle = np.zeros(int(d * SR))
    for _ in range(120):
        p = rng.integers(0, int(1.2 * SR)); L = 250
        crackle[p:p + L] += filt(noise(L / SR, rng), 'highpass', 1500)[:L] * np.exp(-np.arange(L) / 40) * rng.uniform(0.05, 0.3)
    crackle *= np.exp(-T(d) / 0.5)
    x = np.tanh(mix(sub * 1.2, body, crackle) * 1.4)
    return finish(reverb(x, 1.8, 0.35, rng, bright=3500), -1)

def cast(rng):
    d = 0.7
    swell = sweep_bp(noise(d, rng), 400, 2500, 1200, q=1.2) * env_ar(int(d * SR), 0.3, 1.5)
    t = T(d); shimmer = np.zeros(len(t))
    for k in range(6):
        f = rng.uniform(1400, 3800); shimmer += np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(8, 14) * t))
    shimmer *= env_ar(len(t), 0.25, 2.0) * 0.06
    thoom = boom(0.5, 120, 70, 0.15, 1.2) * 0.5
    thoom = np.concatenate([np.zeros(int(0.18 * SR)), thoom])
    return finish(reverb(mix(swell, shimmer, thoom), 1.0, 0.35, rng), -6)

def magic(rng):
    d = 1.1; t = T(d)
    swell = filt(noise(d, rng), 'bandpass', [600, 4000]) * (np.clip(t / 0.5, 0, 1) ** 2) * np.exp(-np.clip(t - 0.5, 0, None) / 0.2)
    pad_ = np.zeros(len(t))
    for f in [220, 277.2, 329.6, 440]:
        for det in (-0.6, 0.6):
            pad_ += np.sin(2 * np.pi * (f + det) * t)
    pad_ = filt(pad_, 'lowpass', 1500) * env_ar(len(t), 0.45, 1.5) * 0.06
    return finish(reverb(mix(swell * 0.7, pad_), 1.3, 0.4, rng), -8)

def teleport(rng):
    d = 0.3; t = T(d)
    rise = sweep_bp(noise(d, rng), 300, 1500, 5000, q=1.5) * (t / d) ** 2
    pop = mix(boom(0.3, 200, 90, 0.05, 2), filt(noise(0.05, rng), 'highpass', 2000) * expdec(0.05, 0.01))
    tail = filt(noise(0.5, rng), 'bandpass', [2000, 8000]) * expdec(0.5, 0.15) * 0.3
    x = np.concatenate([rise, mix(pop, tail)])
    return finish(reverb(x, 0.9, 0.3, rng), -5)

def buff(rng):
    d = 1.7; t = T(d)
    f = 38 + 34 * np.clip(t / 1.1, 0, 1)
    drone = np.tanh(np.sin(2 * np.pi * np.cumsum(f) / SR) * 3) * env_ar(len(t), 0.6, 2.0)
    drone = filt(drone, 'lowpass', 900)
    swell = sweep_lp(noise(d, rng), 300, 6000) * (np.clip(t / 1.05, 0, 1) ** 3) * (t < 1.1)
    hit = np.concatenate([np.zeros(int(1.05 * SR)), mix(boom(0.65, 110, 40, 0.25, 2.5), filt(noise(0.2, rng), 'bandpass', [500, 6000]) * expdec(0.2, 0.05))])
    return finish(reverb(np.tanh(mix(drone * 0.8, swell * 0.6, hit * 1.2) * 1.2), 1.6, 0.35, rng, bright=4000), -2)

def ko(rng):
    sub = boom(2.2, 110, 36, 0.6, 2.6)
    slap = filt(noise(0.2, rng), 'bandpass', [300, 6000]) * expdec(0.2, 0.03) * 2
    gong = partials(2.2, 72, [1, 1.52, 2.3, 3.1, 4.4], [1.2, 0.9, 0.6, 0.4, 0.3], [1, 0.6, 0.45, 0.3, 0.2], rng, 0.003) * 0.5
    return finish(reverb(np.tanh(mix(sub, slap, gong) * 1.3), 2.2, 0.4, rng, bright=3000), -1)

def war_drum(rng):
    """Big drum hit with a low gong under it: round start, fight, round end."""
    sub = boom(1.4, rng.uniform(78, 86), 52, 0.35, 2.0)
    skin = partials(0.6, 160, [1, 1.59, 2.14], [0.12, 0.08, 0.05], [1, 0.5, 0.3], rng) * 0.6
    slap = filt(noise(0.1, rng), 'bandpass', [400, 4000]) * expdec(0.1, 0.018) * 1.6
    gong = partials(2.6, rng.uniform(98, 104), [1, 1.48, 2.02, 2.76, 3.9], [1.6, 1.1, 0.8, 0.5, 0.35], [0.6, 0.45, 0.35, 0.22, 0.15], rng, 0.004)
    gong *= np.clip(T(2.6) / 0.05, 0, 1) * 0.55
    return finish(reverb(np.tanh(mix(sub * 1.1, skin, slap, gong) * 1.2), 2.4, 0.4, rng, bright=3500), -2)

designs = {'wx_swinglight': swing_light, 'wx_swingheavy': swing_heavy, 'wx_dodge': dodge, 'wx_jump': jump, 'wx_parry': parry,
           'wx_clash': clash, 'wx_guardbreak': guard_break, 'wx_projectilehit': projectile_hit, 'wx_explosion': explosion,
           'wx_cast': cast, 'wx_magic': magic, 'wx_teleport': teleport, 'wx_buff': buff, 'wx_ko': ko, 'wx_drum': war_drum}
if __name__ == '__main__':
    import os; os.makedirs('out', exist_ok=True)
    for name, fn in designs.items():
        for v in range(3):
            x = fn(np.random.default_rng(sum(map(ord, name)) + v * 101))
            write('out/%s_%d' % (name, v + 1), x)
        print(name, '%.2f s' % (len(x) / SR))

# ---------------------------------------------------------------- weapons (Vex and the throw system)
def rocket(rng):
    d = 1.2; t = T(d)
    thump = boom(0.4, 130, 55, 0.09, 2.2)
    ignite = filt(noise(0.15, rng), 'bandpass', [800, 7000]) * expdec(0.15, 0.03) * 1.6
    hiss = filt(noise(d, rng), 'bandpass', [1500, 6000]) * np.exp(-t / 0.35) * (1 - np.exp(-t / 0.02)) * 0.45 * tailfade(len(t), 0.4)
    roar = filt(noise(d, rng), 'lowpass', 500) * np.exp(-t / 0.3) * 0.8 * tailfade(len(t), 0.4)
    whoosh = sweep_bp(noise(0.7, rng), 2500, 900, 400, q=1.2) * env_ar(int(0.7 * SR), 0.1, 2.0) * 0.5
    whoosh = np.concatenate([np.zeros(int(0.05 * SR)), whoosh])
    return finish(reverb(np.tanh(mix(thump * 1.2, ignite, hiss, roar, whoosh) * 1.3), 0.9, 0.3, rng, bright=5000), -2)

def pistol(rng):
    crack = filt(noise(0.03, rng), 'highpass', 1500) * expdec(0.03, 0.004) * 3
    body = boom(0.18, 260, 120, 0.035, 2.5)
    mech = filt(noise(0.02, rng), 'bandpass', [3000, 9000]) * expdec(0.02, 0.003) * 0.5
    mech = np.concatenate([np.zeros(int(0.05 * SR)), mech])
    tail = filt(noise(0.5, rng), 'bandpass', [300, 3000]) * expdec(0.5, 0.08) * 0.25
    return finish(reverb(np.tanh(mix(crack, body, mech, tail) * 1.6), 0.8, 0.28, rng, bright=6000), -2)

def throw(rng):
    d = 0.55; t = T(d)
    w = sweep_bp(noise(d, rng), 500, 1800, 700, q=1.6)
    spin = 0.55 + 0.45 * np.sin(2 * np.pi * rng.uniform(13, 17) * t)
    grunt = filt(noise(0.12, rng), 'bandpass', [150, 700]) * env_ar(int(0.12 * SR), 0.2, 2) * 0.4
    return finish(reverb(mix(w * spin * env_ar(len(t), 0.25, 1.5), grunt), 0.4, 0.2, rng), -5)

def weapon_drop(rng):
    out = np.zeros(int(0.9 * SR))
    for k, (at, amp) in enumerate([(0.0, 1.0), (0.11, 0.55), (0.19, 0.35), (0.25, 0.2)]):
        f0 = rng.uniform(380, 520)
        hit = partials(0.35, f0, [1, 2.6, 4.3, 6.8], [0.09, 0.06, 0.04, 0.03], [1, 0.7, 0.5, 0.3], rng, 0.02)
        click = filt(noise(0.015, rng), 'highpass', 2000) * expdec(0.015, 0.003) * 1.5
        thud = boom(0.12, 160, 90, 0.03, 1.5) * 0.6
        s = mix(hit, click, thud) * amp
        p = int(at * SR); out[p:p + len(s)] += s[:len(out) - p]
    return finish(reverb(out, 0.5, 0.2, rng), -5)

def pickup(rng):
    slide = sweep_bp(noise(0.25, rng), 2500, 5000, 3500, q=3) * env_ar(int(0.25 * SR), 0.6, 1.5) * 0.6
    click = partials(0.2, rng.uniform(1100, 1300), [1, 2.4, 3.9], [0.05, 0.03, 0.02], [1, 0.5, 0.3], rng) * 0.8
    click = np.concatenate([np.zeros(int(0.2 * SR)), click])
    return finish(reverb(mix(slide, click), 0.4, 0.2, rng), -6)

designs2 = {'wx_rocket': rocket, 'wx_pistol': pistol, 'wx_throw': throw, 'wx_weapondrop': weapon_drop, 'wx_pickup': pickup}
