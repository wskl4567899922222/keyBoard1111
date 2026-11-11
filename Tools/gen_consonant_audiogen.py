# -*- coding: utf-8 -*-
"""Meta AudioGen (facebook/audiogen-medium) 生成辅音 hit 音效。

7 类提示词与元音 hit 音效一一对应（同风格 + soft/gentle/muffled 弱化措辞），
生成后统一后处理：裁剪静音 → 峰值归一（比元音弱，约 -9dB 量级）→ 淡入淡出 →
重采样 44.1kHz → 立体声 16bit WAV，输出到 Unity ConsonantHits 文件夹。
"""
import os
os.environ.setdefault("HF_ENDPOINT", "https://hf-mirror.com")  # 国内镜像拉模型

import sys
import wave as wavemod
import numpy as np
import torch
import torchaudio

OUT_DIR = r"D:/UnityProjects/KeyBoard1015/Assets/Keyboard3D/Effects/Resources/ConsonantHits"
TARGET_PEAK = 0.32     # 目标峰值（元音 DSP 版峰值约 0.2~0.28，此处对齐"更弱"观感）
FADE_MS = 8
DURATION = 3.0         # AudioGen 生成时长（后处理裁掉静音尾）

# (输出名, 提示词)——风格对应元音 cannon_01/02、card、crossbow、electronic_01/02、heal
SPECS = [
    ("cannon_01", "a soft distant cannon boom, muffled low artillery blast, short game hit sound"),
    ("cannon_02", "a gentle cannon fire thump, low muffled explosion rumble, short game impact sound"),
    ("card", "a soft card flick whoosh, gentle paper swish, short game ui hit sound"),
    ("crossbow", "a soft crossbow shot, gentle arrow release thunk, short game weapon hit sound"),
    ("electronic_01", "a soft electronic beep blip, gentle sci-fi ui ping, short game hit sound"),
    ("electronic_02", "a mellow electronic chirp, soft digital synth zap, short game sound"),
    ("heal", "a gentle magical healing chime, soft sparkle shimmer, calm game buff sound"),
]

SR_OUT = 44100


def save_wav(path, y_int, sr, ch):
    with wavemod.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(y_int.tobytes())


def postprocess(x: torch.Tensor, sr_in: int) -> np.ndarray:
    """x: [B, C, T] → squeeze 成 [T] → 44.1k 立体声 int16"""
    y = x.detach().cpu().squeeze().numpy().astype(np.float64)
    if y.ndim != 1:
        y = y.reshape(-1)
    # 裁剪首尾静音（留 10ms 头 / 60ms 尾余量）
    active = np.where(np.abs(y) > 1e-3)[0]
    if len(active) == 0:
        return None
    pad_head, pad_tail = int(0.01 * sr_in), int(0.06 * sr_in)
    a = max(0, active[0] - pad_head)
    b = min(len(y), active[-1] + pad_tail)
    y = y[a:b]
    # 去直流
    y = y - y.mean()
    # 峰值归一
    peak = float(np.max(np.abs(y)))
    if peak > 0:
        y = y / peak * TARGET_PEAK
    # 淡入淡出
    n = len(y)
    fade = min(n // 2, int(FADE_MS * sr_in / 1000))
    if fade > 1:
        env = np.ones(n)
        env[:fade] = np.linspace(0, 1, fade)
        env[-fade:] = np.linspace(1, 0, fade)
        y *= env
    # 重采样 44.1k（torchaudio 高质量重采样）
    if sr_in != SR_OUT:
        t = torchaudio.transforms.Resample(sr_in, SR_OUT, dtype=torch.float64)
        y = t(torch.from_numpy(y)).numpy()
    # 立体声
    st = np.stack([y, y], axis=1)
    return np.clip(st * 32767.0, -32768, 32767).astype(np.int16)


def main():
    from audiocraft.models import AudioGen
    print("loading facebook/audiogen-medium ...", flush=True)
    model = AudioGen.get_pretrained("facebook/audiogen-medium")
    model.set_generation_params(duration=DURATION)
    sr = model.sample_rate
    print("model ready, sample_rate =", sr, flush=True)

    for name, prompt in SPECS:
        out_path = os.path.join(OUT_DIR, f"辅音AI_{name}.wav")
        print(f"[gen] {name}: {prompt}", flush=True)
        t0 = __import__("time").time()
        with torch.no_grad():
            wav = model.generate([prompt])  # [1, T]
        dt = __import__("time").time() - t0
        data = postprocess(wav.float(), sr)
        if data is None or len(data) < SR_OUT // 10:  # 不足 0.1s 视为无效
            print(f"[skip] {name}: 全静音/过短", flush=True)
            continue
        save_wav(out_path, data, SR_OUT, 2)
        dur = len(data) / SR_OUT
        print(f"[done] {out_path}  {dur:.2f}s  gen={dt:.1f}s", flush=True)
    print("ALL-DONE", flush=True)


if __name__ == "__main__":
    main()
