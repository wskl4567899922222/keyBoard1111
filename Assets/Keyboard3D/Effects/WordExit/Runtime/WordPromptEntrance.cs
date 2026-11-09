using UnityEngine;
namespace KeyboardGame
{
    /// <summary>Presentation-only: no changes to Model, layout, endpoints or input position.</summary>
    public sealed class WordPromptEntrance
    {
        readonly float duration,rise;
        float age;
        public bool IsComplete=>age>=duration;
        public float Alpha=>Mathf.SmoothStep(0,1,Mathf.Clamp01(age/duration));
        public float Offset=>-rise*Mathf.Pow(1-Mathf.Clamp01(age/duration),3);
        public WordPromptEntrance(float seconds,float distance){duration=Mathf.Max(.001f,seconds);rise=Mathf.Max(0,distance);}
        public void Step(float deltaTime){age=Mathf.Min(duration,age+Mathf.Max(0,deltaTime));}
        public void Complete(){age=duration;}
    }
}
