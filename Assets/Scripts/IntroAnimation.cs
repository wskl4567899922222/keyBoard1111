using UnityEngine;

namespace ByteTyping
{
    /// <summary>
    /// 入场动画：键盘从下方升起到场景摆放位置（ease-out 缓出），到位后手部淡入并
    /// 沿键盘法线轻微浮起。挂在键盘根物体上（与 KeyboardModelInput / TypingHands 同物体）。
    /// 动画只在启动时播一次，完成后自动禁用自身；Inspector 参数可调节奏与幅度。
    /// </summary>
    [RequireComponent(typeof(TypingHands))]
    public class IntroAnimation : MonoBehaviour
    {
        [Header("键盘滑入")]
        [Tooltip("键盘起始位置相对最终位置的偏移（米）。默认 (0,0,-2)：从世界 Z 负方向（相机侧/屏幕下方）滑入；方向不对就在 Inspector 改符号")]
        public Vector3 riseOffset = new Vector3(0f, 0f, -2f);
        [Tooltip("键盘滑入总时长（秒）")]
        public float riseDuration = 1.1f;
        [Tooltip("缓出强度：2=二次 3=三次（越大前段越快、收尾越柔）")]
        [Range(1f, 5f)] public float riseEasePower = 3f;

        [Header("手部出现")]
        [Tooltip("键盘到位后再等多久手开始出现（秒）")]
        public float handDelay = 0.15f;
        [Tooltip("手部淡入时长（秒）")]
        public float handFadeDuration = 0.7f;
        [Tooltip("手淡入时沿键盘法线浮起的高度（米），0=原地淡入")]
        public float handLift = 0.12f;

        Vector3 homePos;
        float t;
        TypingHands hands;

        void Start()
        {
            hands = GetComponent<TypingHands>();
            homePos = transform.position;
            transform.position = homePos + riseOffset; // 从屏幕下方（世界 Z-）滑入
            hands.introFade = 0f;                      // 动画前整手隐藏
            hands.introLift = -handLift;               // 手初始压向键盘面
            t = 0f;
        }

        void Update()
        {
            t += Time.deltaTime;

            // —— 键盘滑入（ease-out：起手快、到位缓停）——
            float kr = riseDuration > 0f ? Mathf.Clamp01(t / riseDuration) : 1f;
            float e = 1f - Mathf.Pow(1f - kr, riseEasePower);
            transform.position = homePos + riseOffset * (1f - e);

            // —— 手部淡入（键盘到位 + 延迟后开始，smoothstep）——
            float th = t - riseDuration - handDelay;
            if (th >= 0f)
            {
                float kh = handFadeDuration > 0f ? Mathf.Clamp01(th / handFadeDuration) : 1f;
                float eh = kh * kh * (3f - 2f * kh);
                hands.introFade = eh;
                hands.introLift = -handLift * (1f - eh);
            }

            // 键盘到位且手部完全显现后收工（introFade=1 / introLift=0 即常态值）
            if (kr >= 1f && th >= handFadeDuration)
            {
                transform.position = homePos;
                hands.introFade = 1f;
                hands.introLift = 0f;
                enabled = false;
            }
        }
    }
}
