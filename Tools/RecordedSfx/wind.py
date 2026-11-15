"""Seamless 40 s mountain wind loop: brown and pink noise, slow gusts, a faint hollow whistle. Stereo, quiet."""
import numpy as np, subprocess
SR = 44100; L = 40.0; N = int(SR * L)
rng = np.random.RandomState(5)
def lp(x, hz):
    a = 1 - np.exp(-2 * np.pi * hz / SR); y = np.empty_like(x); s = 0.0
    for i in range(len(x)): s += a * (x[i] - s); y[i] = s
    return y
def band(x, lo, hi): return lp(x, hi) - lp(x, lo)
def smooth_noise(n_points, seed):
    r = np.random.RandomState(seed); pts = r.rand(n_points)
    pts = np.append(pts, pts[0])  # loop
    t = np.linspace(0, n_points, N, endpoint=False); i = t.astype(int); f = t - i
    f = f * f * (3 - 2 * f)
    return pts[i] * (1 - f) + pts[i + 1] * f
out = []
for ch in range(2):
    w = rng.randn(N)
    brown = lp(w, 120); brown /= np.abs(brown).max()
    pinkish = band(rng.randn(N), 200, 1400); pinkish /= np.abs(pinkish).max()
    gust = smooth_noise(14, 10 + ch) ** 1.6
    slow = 0.55 + 0.45 * smooth_noise(5, 20 + ch)
    whistle_env = np.clip(smooth_noise(9, 30 + ch) - 0.55, 0, 1) * 2
    whistle = band(rng.randn(N), 650, 900); whistle /= np.abs(whistle).max()
    x = brown * 0.55 * slow + pinkish * (0.12 + 0.5 * gust) * slow + whistle * whistle_env * 0.08
    out.append(x)
x = np.stack(out, 1)
# seamless loop: crossfade the last 2 s into the start
cf = int(2 * SR); ramp = np.linspace(0, 1, cf)[:, None]
x[:cf] = x[:cf] * ramp + x[-cf:] * (1 - ramp); x = x[:-cf]
x = x / np.abs(x).max() * 0.5
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', '-c:a', 'libvorbis', '-q:a', '5', 'out_amb/wind.ogg'], input=x.astype(np.float32).tobytes(), check=True)
print('ok', x.shape[0] / SR)
