using System.Collections.Generic;
using UnityEngine;

namespace KeyboardGame
{
    /// 粒子到达字母时的音效：从 Resources/ArrivalChimes 加载音效池，
    /// 洗牌袋随机抽取（避免连续重复），每次播放带轻微音高抖动
    public sealed class ArrivalChimePlayer : MonoBehaviour
    {
        const int SourceCount = 6; // 并发声源轮转（快速连续敲键不吞音）
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.05f;

        readonly List<AudioClip> clips = new List<AudioClip>();
        readonly List<AudioClip> bag = new List<AudioClip>();
        readonly AudioSource[] sources = new AudioSource[SourceCount];
        int nextSource;

        void EnsureLoaded()
        {
            if (clips.Count > 0) return;
            var loaded = Resources.LoadAll<AudioClip>("ArrivalChimes");
            if (loaded != null)
            {
                clips.AddRange(loaded);
                clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
            for (int i = 0; i < SourceCount; i++)
            {
                if (sources[i] != null) continue;
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].loop = false;
                sources[i].spatialBlend = 0f; // 2D：固定视角下全程清晰
            }
        }

        AudioClip NextClip()
        {
            if (clips.Count == 0) return null;
            if (bag.Count == 0) { bag.AddRange(clips); Shuffle(bag); }
            int last = bag.Count - 1;
            var clip = bag[last];
            bag.RemoveAt(last);
            return clip;
        }

        static void Shuffle(List<AudioClip> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <param name="position">到达字母的世界坐标（2D 播放，仅用于未来扩展空间音效）</param>
        public void Play(Vector3 position)
        {
            EnsureLoaded();
            var clip = NextClip();
            if (clip == null) return;
            var src = sources[nextSource];
            nextSource = (nextSource + 1) % SourceCount;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.PlayOneShot(clip, volume);
        }
    }
}
