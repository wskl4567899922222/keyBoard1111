using UnityEngine;

namespace KeyboardGame
{
    /// <summary>Visual-only tuning. Does not control input, arrival, handoff or pop timing.</summary>
    [CreateAssetMenu(menuName="Keyboard/Letter Impact Style")]
    public sealed class ImpactStyle:ScriptableObject
    {
        [Header("Version (0 legacy / 1 burst)")]
        [Range(0,1)] public int visualVersion;
        public static ImpactStyle Resolve()
        {
            var selection=Resources.Load<ImpactStyle>("LetterImpactSelection");
            if(selection!=null&&selection.visualVersion==1)
            {
                var burst=Resources.Load<ImpactStyle>("LetterImpactBurstStyle");
                if(burst!=null)return burst;
            }
            return Resources.Load<ImpactStyle>("LetterImpactStyle");
        }
        [Header("Overall finish")]
        [Min(0.1f)] public float lifetime=0.30f;
        [Range(0,4)] public float brightness=1.35f;
        [Range(0,1)] public float whiteCore=0.65f;
        [Header("Glyph-adapted cover")]
        [Range(1,1.8f)] public float coverWidth=1.18f;
        [Range(1,1.8f)] public float coverHeight=1.12f;
        [Range(0,1)] public float coverOpacity=0.96f;
        [Range(0,1)] public float coverWhiteness=0.48f;
        [Min(0.01f)] public float coverRelease=0.075f;
        [Range(0,0.2f)] public float coverHoldAfterSwitch=0.018f;
        [Range(0,1)] public float edgeIrregularity=0.10f;
        [Header("Contact core")]
        [Range(0.1f,1)] public float coreSize=0.42f;
        [Min(0.01f)] public float coreLifetime=0.09f;
        [Header("Sparks (independent from cover)")]
        [Range(0,18)] public int sparkCount=8;
        [Range(0,3)] public float sparkTravel=0.62f;
        [Range(0.02f,0.3f)] public float sparkLength=0.16f;
        [Range(0.005f,0.10f)] public float sparkWidth=0.035f;
        [Range(0.05f,0.5f)] public float sparkLifetime=0.24f;
        [Range(0,1)] public float sparkBrightness=0.85f;
        [Range(0,1)] public float sparkWhiteness=0.65f;
        [Header("Burst silhouette (new version only)")]
        [Range(0,0.35f)] public float lobeVariation=0.14f;
        [Range(0.01f,0.06f)] public float attackDuration=0.018f;
        [Range(0.02f,0.15f)] public float breakupDuration=0.09f;
        [Range(0,0.5f)] public float bodyGlow=0.12f;
        [Header("Stars and fragments (new version only)")]
        [Range(0,4)] public int starCount=3;
        [Range(0,8)] public int fragmentCount=4;
        [Range(0.06f,0.3f)] public float starSize=0.18f;
        [Range(0.02f,0.15f)] public float fragmentSize=0.075f;
        [Range(0,2)] public float starSpin=0.65f;
        [Header("Afterglow (new version only)")]
        [Range(0,0.5f)] public float afterglowOpacity=0.14f;
        [Range(0.02f,0.25f)] public float afterglowLifetime=0.11f;
    }
}
