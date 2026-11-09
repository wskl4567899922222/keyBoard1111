using UnityEngine;
namespace KeyboardGame
{
    [CreateAssetMenu(menuName="Keyboard/Word Dust Style")]
    public sealed class WordDustStyle:ScriptableObject
    {
        [Header("Independent word landing dust")]
        public bool enabled=true;
        [Range(.3f,.8f)] public float lifetime=.50f;
        [Range(0,.15f)] public float clearPause=.04f;
        [Range(.8f,3)] public float maximumWidthInHeights=2.2f;
        [Range(.3f,.85f)] public float maximumWordWidthFraction=.70f;
        [Range(.12f,.4f)] public float heightInHeights=.28f;
        [Range(.15f,.8f)] public float opacity=.64f;
        [Range(0,1)] public float saturation=.46f;
        [Header("Next gray word")]
        [Range(.08f,.3f)] public float promptDuration=.16f;
        [Range(0,.25f)] public float promptRiseInHeights=.12f;
    }
}
