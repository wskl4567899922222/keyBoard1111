using System.Collections;
using UnityEngine;

namespace Keyboard3D
{
    /// <summary>
    /// 按键音效：任意键盘按键按下时播放一次 Audio Source 上的音频。
    /// 挂在带 Audio Source（已指定 KeyBoardClickSound 等音频）的物体上。
    /// 鼠标按键不触发；音量用 Audio Source 的 Volume 调节。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Keyboard3D/Key Click Sound (按键音效)")]
    public class KeyClickSound : MonoBehaviour
    {
        AudioSource src;
        bool warmed; // WebGL：音频引擎在首次交互后才唤醒，头一两下按键会被吞

        void Awake() => src = GetComponent<AudioSource>();

        // 预热：解锁浏览器 autoplay 并提前建好播放声道（首建声道有延迟，会导致头几秒按键无声）
        void TryWarm(bool fromClick)
        {
            if (warmed) return;
            warmed = true;
            if (src != null && src.clip != null)
                src.PlayOneShot(src.clip, 0f);
            // 同步预热拼读发音音源（WordTrailTyping.voiceSource），否则首个音标音会被吞
            var wt = WordTrailTyping.Instance;
            if (wt != null) wt.WarmVoice(fromClick);
        }

        void Update()
        {
            // 鼠标点击不算按键（与 TypingHands 的对错判定同一口径），但同样能解锁音频 → 顺便预热
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                TryWarm(true);
                return;
            }
            if (!Input.anyKeyDown) return;

            var clip = src != null ? src.clip : null;
            if (clip == null) return;

            if (!warmed)
            {
                // 首键：零音量预热建声道，真实按键音延后 0.1s 错开 AudioContext resume 帧，保证第一键有声
                warmed = true;
                src.PlayOneShot(src.clip, 0f);
                var wt = WordTrailTyping.Instance;
                if (wt != null) wt.WarmVoice(false);
                StartCoroutine(PlayClickDelayed(clip, 0.1f));
                return;
            }
            src.PlayOneShot(clip); // PlayOneShot 可叠加：快速连按不会被截断
        }

        IEnumerator PlayClickDelayed(AudioClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (src != null) src.PlayOneShot(clip);
        }
    }
}
