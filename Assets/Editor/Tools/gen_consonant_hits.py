# -*- coding: utf-8 -*-
"""把 ShootingSound 的元音 hit 音效加工成"同款风格但更弱"的辅音版。

加工链：FFT 低通（4k~8kHz 平滑滚降，去冲击感）→ 起振软化（8ms 淡入，去爆音）→ 整体降增益 -9dB。
输出：Assets/Keyboard3D/Effects/Resources/ConsonantHits/辅音_<name>.wav（16bit/44.1k 立体声，同源格式）。
"""
import wave, contextlib, os
import numpy as np

SRC_DIR = r"D:/UnityProjects/KeyBoard1015/Assets/ShootingSound"
OUT_DIR = r"D:/UnityProjects/KeyBoard1015/Assets/Keyboard3D/Effects/Resources/ConsonantHits"

NAMES = ["cannon_01", "cannon_02", "card", "crossbow",
         "electronic_01", "electronic_02", "heal"]

GAIN_DB = -9.0        # 整体比元音版弱 9dB
FADE_MS = 8           # 起振淡入
LOW_HZ = 4000.0       # 低通起始
HIGH_HZ = 8000.0      # 低通截止（之间余弦滚降）


def fft_lowpass(x, sr):
    X = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(x.shape[0], 1.0 / sr)
    gain = np.ones_like(freqs)
    roll = (freqs >= LOW_HZ) & (freqs <= HIGH_HZ)
    gain[roll] = 0.5 * (1 + np.cos(np.pi * (freqs[roll] - LOW_HZ) / (HIGH_HZ - LOW_HZ)))
    gain[freqs > HIGH_HZ] = 0.0
    return np.fft.irfft(X * gain, n=x.shape[0])


def process(path_in, path_out):
    with contextlib.closing(wave.open(path_in, "rb")) as w:
        sr, ch, sw = w.getframerate(), w.getnchannels(), w.getsampwidth()
        n = w.getnframes()
        raw = w.readframes(n)
    assert sw == 2, "expect 16bit"
    x = np.frombuffer(raw, dtype=np.int16).astype(np.float64) / 32768.0
    if ch == 2:
        x = x.reshape(-1, 2)
    else:
        x = x[:, None]

    # 低通逐通道
    y = np.stack([fft_lowpass(x[:, c], sr) for c in range(ch)], axis=1)

    # 起振软化
    fade = int(sr * FADE_MS / 1000)
    if fade > 1:
        env = np.ones(len(y))
        env[:fade] = np.linspace(0.0, 1.0, fade)
        y *= env[:, None]

    # 降增益
    y *= 10 ** (GAIN_DB / 20)

    # 防削波（等比例缩，不改变相对响度）
    peak = np.max(np.abs(y))
    if peak > 0.999:
        y /= peak

    out = np.clip(y * 32768.0, -32768, 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path_out), exist_ok=True)
    with contextlib.closing(wave.open(path_out, "wb")) as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(out.tobytes())
    print("OK", os.path.basename(path_out), f"{len(y)/sr:.3f}s peak={np.max(np.abs(y)):.3f}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for name in NAMES:
        src = os.path.join(SRC_DIR, name + ".wav")
        dst = os.path.join(OUT_DIR, "辅音_" + name + ".wav")
        process(src, dst)
    # 已有的 辅音_C5 一并纳入选项目录
    src_c5 = os.path.join(SRC_DIR, "辅音_C5.wav")
    dst_c5 = os.path.join(OUT_DIR, "辅音_C5.wav")
    if os.path.exists(src_c5):
        with open(src_c5, "rb") as f:
            data = f.read()
        with open(dst_c5, "wb") as f:
            f.write(data)
        print("OK 辅音_C5.wav（原样拷贝）")
    print("DONE ->", OUT_DIR)


if __name__ == "__main__":
    main()
