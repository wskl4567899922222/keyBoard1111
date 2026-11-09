using UnityEngine;

namespace KeyboardGame
{
    /// <summary>Deterministic visual curves. No gameplay state and no accumulated frame-dependent integration.</summary>
    public static class WordExitMotion
    {
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        public static Vector3 Shape(float age,WordExitStyle s)
        {
            if(age<=0)return Vector3.one;
            Vector3 flat=new Vector3(s.squashWidth,s.squashHeight,1.04f);
            Vector3 tall=new Vector3(s.stretchWidth,s.stretchHeight,.98f);
            Vector3 recoil=new Vector3(1.05f,.95f,1.01f);
            if(age<s.anticipation)return Vector3.LerpUnclamped(Vector3.one,flat,Ease(age/s.anticipation));
            if(age<s.anticipation+s.stretchDuration)return Vector3.LerpUnclamped(flat,tall,Ease((age-s.anticipation)/s.stretchDuration));
            if(age<s.GatherStart)return Vector3.LerpUnclamped(tall,recoil,Ease((age-s.anticipation-s.stretchDuration)/s.recoilDuration));
            float q=Ease((age-s.GatherStart)/s.gatherDuration);
            return Vector3.LerpUnclamped(recoil,Vector3.one*.018f,q);
        }
        public static float Rise(float age,WordExitStyle s)
        {
            if(age<=s.anticipation)return 0;
            float t=Mathf.Clamp01((age-s.anticipation)/Mathf.Max(.01f,s.LaunchTime-s.anticipation));
            return s.jumpHeight*Mathf.Sin(t*Mathf.PI*.5f);
        }
        public static float Flight(float t){t=Mathf.Clamp01(t);return t*t*(.72f+.28f*t);}
        public static Vector3 FollowDisplacement(Vector3 velocity,float age,float drag)
        {return velocity*((1-Mathf.Exp(-Mathf.Max(.001f,drag)*Mathf.Max(0,age)))/Mathf.Max(.001f,drag));}
    }
}
