using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-11-05 WebGL 包体瘦身：音频统一重导入（走 AudioImporter API，绕开手改 meta 被资产数据库缓存覆盖的问题）。
/// 构建报告实测 Sounds 20.1MB：defaultSettings.quality=1（Vorbis 最高码率）把低码率源 mp3 重编码吹大 2~3 倍。
/// 语音（ByteTyping/Resources/Audio）：Vorbis q0.35 + 24kHz 采样（人声足够）。
/// 音效（ShootingSound）：Vorbis q0.4。
/// 用法：Unity 菜单 Tools → 音频瘦身重导入（跑完约 1~2 分钟，再 Build）。
/// </summary>
public static class AudioSlimMenu
{
    [MenuItem("Tools/音频瘦身重导入")]
    public static void SlimAudio()
    {
        int voice = 0, sfx = 0;
        var folders = new[]
        {
            "Assets/ByteTyping/Resources/Audio",
            "Assets/ShootingSound",
            "Assets/Audio", // KeyBoardClickSound 等按键音效
        };
        foreach (var folder in folders)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                bool isVoice = path.StartsWith("Assets/ByteTyping");
                var s = imp.defaultSampleSettings;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = isVoice ? 0.35f : 0.4f;
                if (isVoice)
                {
                    s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    s.sampleRateOverride = 24000;
                }
                // WebGL 修复：preload=0 时首次 PlayOneShot 数据未加载 → 静默无声；
                // DecompressOnLoad 在 WebGL 主线程同步解压 → 首播卡顿。
                // 语音改 CompressedInMemory（体积小、即播即解），并全部开启预加载。
                //（新 API：loadType / preloadAudioData 都在 SampleSettings 里，AudioImporter 上的旧属性已废弃）
                s.preloadAudioData = true;
                s.loadType = isVoice
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
                if (isVoice) voice++; else sfx++;
                if ((voice + sfx) % 50 == 0)
                    Debug.Log($"[音频瘦身] {voice + sfx} / 475 ...");
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[音频瘦身] 完成：语音 {voice} 个（Vorbis q0.35 / 24kHz），音效 {sfx} 个（q0.4）。现在可以重新 Build。");
    }
}
