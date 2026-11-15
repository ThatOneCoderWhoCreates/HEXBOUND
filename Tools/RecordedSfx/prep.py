"""CC0 recordings -> trimmed, faded, loudness-matched mono OGG clips named for SfxPlayer's prefixes."""
import subprocess, numpy as np, os, glob, json, sys
SR = 44100
def load(path):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)
def save(x, path):
    x = np.clip(x, -1, 1).astype(np.float32)
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libvorbis', '-q:a', '6', path], input=x.tobytes(), check=True)
def trim(x, thresh=0.01, pre=0.004, tail=0.06, maxlen=None):
    a = np.abs(x); peak = a.max() + 1e-9
    idx = np.where(a > peak * thresh)[0]
    if len(idx) == 0: return x
    s = max(0, idx[0] - int(pre * SR)); e = min(len(x), idx[-1] + int(tail * SR))
    y = x[s:e].copy()
    if maxlen: y = y[:int(maxlen * SR)]
    fi = int(0.002 * SR); fo = int(min(0.05, len(y) / SR * 0.3) * SR)
    y[:fi] *= np.linspace(0, 1, fi)
    if fo > 0: y[-fo:] *= np.linspace(1, 0, fo) ** 2
    return y
def rms_db(x):
    # loudness over the loudest 100 ms
    w = int(0.1 * SR); 
    if len(x) < w: return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)
    c = np.convolve(x ** 2, np.ones(w) / w, 'valid')
    return 10 * np.log10(c.max() + 1e-12)
def norm(x, target_db, peak=0.95):
    g = 10 ** ((target_db - rms_db(x)) / 20); y = x * g
    p = np.abs(y).max()
    if p > peak: y *= peak / p
    return y
def highpass(x, hz):
    a = np.exp(-2 * np.pi * hz / SR); y = np.zeros_like(x); prev_x = 0; prev_y = 0
    for i in range(len(x)):
        prev_y = a * (prev_y + x[i] - prev_x); prev_x = x[i]; y[i] = prev_y
    return y

SRC = 'src'
plan = []
sw = sorted(glob.glob(SRC + '/38715754/swishes/swish-*.wav'), key=lambda p: int(p.split('-')[-1].split('.')[0]))
star = sorted(glob.glob(SRC + '/2a2fbd16/*/sword.*.ogg'), key=lambda p: int(p.split('.')[-2]))
clash = sorted(glob.glob(SRC + '/44ef7341/sword_clash.*.ogg'), key=lambda p: int(p.split('.')[-2]))
rpg = SRC + '/3c127a67/Audio/'
imp = SRC + '/ad7afca9/Audio/'
for p in sw[:4]: plan.append((p, 'rx_swinglight', -20, 0.5))
for p in sw[4:]: plan.append((p, 'rx_swingheavy', -17, 0.7))
for p in star: plan.append((p, 'rx_swingheavy', -17, 0.8))
for p in clash: plan.append((p, 'rx_clash', -14, 1.2))
for p in clash[::2]: plan.append((p, 'rx_parry', -12, 1.4))
for k in range(5): plan.append((imp + 'impactMetal_heavy_%03d.ogg' % k, 'rx_block', -15, 0.8))
for k in range(1, 5): plan.append((rpg + 'cloth%d.ogg' % k, 'rx_dodge', -21, 0.6))
for k in range(5): plan.append((imp + 'impactMetal_light_%03d.ogg' % k, 'rx_drop', -18, 0.6))
plan.append((rpg + 'dropLeather.ogg', 'rx_drop', -18, 0.6))
for k in range(1, 4): plan.append((rpg + 'drawKnife%d.ogg' % k, 'rx_pickup', -18, 0.8))
for p in sw[6:10]: plan.append((p, 'rx_throw', -16, 0.7))
for k in range(5): plan.append((imp + 'footstep_concrete_%03d.ogg' % k, 'rx_step', -24, 0.35))
for k in range(10): plan.append((rpg + 'footstep%02d.ogg' % k, 'rx_step', -24, 0.35))
os.makedirs('out', exist_ok=True)
for f in glob.glob('out/*'): os.remove(f)
counts = {}
report = []
for src, prefix, db, maxlen in plan:
    x = load(src)
    x = trim(x, maxlen=maxlen)
    x = norm(x, db)
    n = counts.get(prefix, 0); counts[prefix] = n + 1
    out = 'out/%s_%02d.ogg' % (prefix, n)
    save(x, out)
    report.append((out, os.path.basename(src), round(len(x) / SR, 2)))
for r in report: print(*r)
print(counts)
