using UnityEngine;

namespace KeyboardGame.Letters
{
    public static class LetterPopAnimation
    {
        public static float Scale(float progress,float start,float peak)
        {
            float t=Mathf.Clamp01(progress);
            return t<0.7f?Mathf.Lerp(start,peak,Mathf.SmoothStep(0,1,t/0.7f)):
                Mathf.Lerp(peak,1,Mathf.SmoothStep(0,1,(t-0.7f)/0.3f));
        }
        public static float Opacity(float progress)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/0.45f));
    }
}
