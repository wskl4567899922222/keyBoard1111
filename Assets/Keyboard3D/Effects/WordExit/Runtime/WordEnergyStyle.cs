using UnityEngine;
namespace KeyboardGame
{
    [CreateAssetMenu(menuName="Keyboard/Word Energy Style")]
    public sealed class WordEnergyStyle:ScriptableObject
    {
        public bool enabled=true;
        [Header("Line / relative to displayed word")]
        [Range(.55f,1)] public float wordWidthFraction=1f;
        [Range(0,1.5f)] public float extraLetterWidthPerEnd=1.20f;
        [Range(.02f,.2f)] public float belowWord=.075f;
        [Range(.003f,.04f)] public float coreThickness=.015f;
        [Range(.03f,.15f)] public float haloHeight=.082f;
        [Range(.5f,3)] public float brightness=2.23f;
        [Range(.2f,1)] public float saturation=.82f;
        [Header("Charge follows existing word squash")]
        [Range(0,.12f)] public float releaseHold=.055f;
        [Range(.12f,.35f)] public float collapseDuration=.22f;
        [Range(.02f,.10f)] public float pointHold=.045f;
        [Range(.05f,.18f)] public float extinguishDuration=.09f;
        [Range(0,.15f)] public float clearPause=.05f;
        [Range(.02f,.09f)] public float pointRadius=.035f;
        [Header("Short press impact / single finishing flash")]
        [Range(.015f,.06f)] public float pressAttack=.03f;
        [Range(0,.5f)] public float pressBrightnessKick=.30f;
        [Range(0,.5f)] public float chargeCoreBoost=.20f;
        [Range(.04f,.12f)] public float finishFlashDuration=.075f;
        [Range(0,1.5f)] public float finishFlashGain=.70f;
        [Range(0,.35f)] public float finishFlashScale=.08f;
        [Header("Gathering point / relative to previous point")]
        [Range(1,3.5f)] public float gatherPointScale=2.25f;
        [Header("Opposed flowing arms / organic point")]
        [Range(0,.08f)] public float weaveHeightInHeights=0f;
        [Range(0,.2f)] public float pointDisturbance=.10f;
        [Header("Next gray word")]
        [Range(.08f,.3f)] public float promptDuration=.16f;
        [Range(0,.25f)] public float promptRiseInHeights=.12f;
    }
}

