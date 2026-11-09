using UnityEngine;

namespace KeyboardGame
{
    [CreateAssetMenu(menuName="Keyboard/Word Exit Style")]
    public sealed class WordExitStyle : ScriptableObject
    {
        public enum ExitMode { JellyLight, OriginalFade, FlyingLetters }
        [Header("Version selection")]
        public ExitMode mode=ExitMode.FlyingLetters;
        [Header("Whole word bounce")]
        public bool wholeWordJump=true;
        [Range(.06f,.12f)] public float wholeWordSquat=.085f;
        [Range(.18f,.32f)] public float wholeWordOutDuration=.25f;
        public Vector2 wholeWordSquash=new Vector2(1.14f,.70f);
        public Vector2 wholeWordStretch=new Vector2(.88f,1.33f);
        [Header("Quick squash and jump (Flying Letters)")]
        public bool quickJump=true;
        [Range(.04f,.09f)] public float quickSquat=.06f;
        [Range(.14f,.35f)] public float quickOutDuration=.23f;
        [Range(0,.025f)] public float quickLetterDelay=.012f;
        [Range(0,.1f)] public float quickMaximumStagger=.06f;
        [Range(0,12)] public float quickDirectionAngle=8f;
        public Vector2 quickRotationRange=new Vector2(6,10);
        [Header("Complete letter flight")]
        [Range(.4f,.9f)] public float letterFlightDuration=.62f;
        [Range(0,12)] public float flightTilt=7f;
        [Range(0,20)] public float flightBank=11f;
        [Range(10,35)] public float maximumFaceAngle=24f;
        [Range(0,.12f)] public float letterFanSpread=.055f;
        [Range(0,.6f)] public float flightArc=.28f;
        [Header("Jelly / seconds")]
        [Range(.03f,.15f)] public float anticipation=.07f;
        [Range(.05f,.25f)] public float stretchDuration=.10f;
        [Range(.02f,.15f)] public float recoilDuration=.055f;
        [Range(.04f,.2f)] public float gatherDuration=.095f;
        [Range(.6f,1)] public float squashHeight=.80f;
        [Range(1,1.3f)] public float squashWidth=1.12f;
        [Range(1,1.4f)] public float stretchHeight=1.20f;
        [Range(.7f,1)] public float stretchWidth=.90f;
        [Range(.1f,1)] public float jumpHeight=.48f;
        [Header("Word rhythm")]
        [Range(0,.08f)] public float letterDelay=.03f;
        [Range(0,.3f)] public float maximumStagger=.15f;
        [Range(.18f,.8f)] public float flyDuration=.34f;
        [Range(0,.12f)] public float fanOut=.025f;
        [Header("Light cluster")]
        [Range(.1f,.45f)] public float headRadius=.25f;
        [Range(.5f,3)] public float brightness=1.25f;
        [Range(0,.16f)] public float disturbance=.055f;
        [Range(4,16)] public int smallPointCount=12;
        [Range(32,512)] public int totalSmallPointBudget=240;
        [Range(.015f,.08f)] public float smallPointRadius=.035f;
        [Range(.15f,.7f)] public float clusterSpread=.40f;
        [Range(.08f,.5f)] public float tailLifetime=.22f;
        [Range(0,.8f)] public float inheritedVelocity=.38f;
        [Range(1,14)] public float drag=5f;
        public float GatherStart=>anticipation+stretchDuration+recoilDuration;
        public float LaunchTime=>GatherStart+gatherDuration;
        public float Delay(int index,int count)=>index*Mathf.Min(Mathf.Max(0,letterDelay),Mathf.Max(0,maximumStagger)/Mathf.Max(1,count-1));
    }
}
