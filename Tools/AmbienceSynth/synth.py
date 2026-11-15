import numpy as np
from scipy import signal
from scipy.io import wavfile
SR = 44100
rng = np.random.default_rng(7)

def write(name, x, peak_db=None, rms_db=None):
    x = x - x.mean()
    if rms_db is not None: x = x * (10 ** (rms_db / 20) / (np.sqrt((x ** 2).mean()) + 1e-12))
    if peak_db is not None: x = x * (10 ** (peak_db / 20) / (np.abs(x).max() + 1e-12))
    x = np.clip(x, -1, 1)
    wavfile.write(name, SR, (x * 32767).astype(np.int16))
    print(name, '%.1f s' % (len(x) / SR), 'peak %.1f dB' % (20 * np.log10(np.abs(x).max())), 'rms %.1f dB' % (20 * np.log10(np.sqrt((x ** 2).mean()))))

def bp(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'); return signal.sosfilt(sos, x)
def lp(x, f, order=2):
    sos = signal.butter(order, f, 'lowpass', fs=SR, output='sos'); return signal.sosfilt(sos, x)

def reverb(x, length=0.6, mix=0.25, seed=1):
    r = np.random.default_rng(seed); n = int(length * SR)
    ir = r.standard_normal(n) * np.exp(-np.linspace(0, 7, n)); ir = lp(ir, 5000); ir /= np.sqrt((ir ** 2).sum())
    wet = signal.fftconvolve(x, ir); wet = np.concatenate([wet, np.zeros(len(x) + n)])[:len(x) + n]
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - mix) + wet * mix * 0.6

# ---------------- stream: soft water flowing over stones, seamless loop ----------------
def stream(seconds=24, fade=3.0):
    n = int((seconds + fade) * SR); t = np.arange(n) / SR
    white = rng.standard_normal(n)
    brown = np.cumsum(white); brown = signal.sosfilt(signal.butter(1, 40, 'highpass', fs=SR, output='sos'), brown); brown /= np.abs(brown).max()
    body = bp(brown, 60, 600) * 0.7
    hiss = bp(white, 500, 2000, 3)
    lfo = 0.7 + 0.18 * np.sin(2 * np.pi * 0.07 * t) + 0.12 * np.sin(2 * np.pi * 0.17 * t + 1.3)
    hiss *= lfo * 0.08
    # babbling: many tiny resonant bubbles
    bub = np.zeros(n)
    count = int((seconds + fade) * 9)
    for _ in range(count):
        st = rng.integers(0, n - 6000); dur = rng.uniform(0.03, 0.09); m = int(dur * SR)
        f0 = rng.uniform(280, 750); tt = np.arange(m) / SR
        f = f0 * (1 + 0.8 * tt / dur)
        ph = 2 * np.pi * np.cumsum(f) / SR
        env = np.exp(-tt / (dur * 0.4)) * (1 - np.exp(-tt / 0.008))
        bub[st:st + m] += np.sin(ph) * env * rng.uniform(0.03, 0.12)
    x = body + hiss + lp(bub, 1800)
    x = lp(x, 2500)
    # seamless loop: crossfade the tail into the head
    L = int(seconds * SR); F = int(fade * SR)
    head = x[:F].copy(); tail = x[L:L + F]
    w = np.linspace(0, 1, F)
    x = x[:L].copy(); x[:F] = head * w + tail * (1 - w)
    return x

# ---------------- birds: short distant phrases ----------------
def tone(freqs, dur, amp=1.0, fm=None):
    m = int(dur * SR); tt = np.arange(m) / SR
    f = np.interp(tt, np.linspace(0, dur, len(freqs)), freqs)
    if fm: f = f + fm[1] * np.sin(2 * np.pi * fm[0] * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    env = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 1.5
    return (np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.05 * np.sin(3 * ph)) * env * amp

def phrase(parts):
    out = []
    for p in parts:
        if p[0] == 'gap': out.append(np.zeros(int(p[1] * SR)))
        else: out.append(tone(*p[1:]) if p[0] == 't' else None)
    return np.concatenate(out)

birds = [
    phrase([('t', [3600, 4600], 0.07), ('gap', 0.06), ('t', [3700, 4700], 0.07)]),
    phrase([('t', [5200, 5000], 0.38, 0.8, (28, 450))]),
    phrase([('t', [4300, 2900], 0.26), ('gap', 0.05), ('t', [2900, 3600], 0.09, 0.7)]),
    phrase([('t', [3000, 3500], 0.05, 0.7), ('gap', 0.05), ('t', [3300, 3900], 0.05, 0.8), ('gap', 0.05), ('t', [3600, 4300], 0.05, 0.9), ('gap', 0.05), ('t', [4000, 4800], 0.06)]),
    phrase([('t', [2600, 3400, 2800], 0.22), ('gap', 0.12), ('t', [2700, 3500, 2900], 0.2, 0.8)]),
]

if __name__ == '__main__':
    write('stream.wav', stream(), rms_db=-30)
    for i, b in enumerate(birds):
        b = bp(b, 1500, 9000)
        write('bird_%d.wav' % (i + 1), reverb(b, 0.5, 0.3, i), peak_db=-6)
