using System;
using UnityEngine;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SingleTrailImpact:MonoBehaviour
    {
        ParticleSystem particles;
        ImpactVisual visual;
        float remaining;
        Action<SingleTrailImpact> recycle;
        public bool IsPlaying=>remaining>0;
        public void PlayAt(Vector3 position,Color color,Camera camera,Action<SingleTrailImpact> onRecycle)
        {PlayForLetter(position,color,camera,null,0.04f,onRecycle);}
        public void PlayForLetter(Vector3 position,Color color,Camera camera,LetterView glyph,float handoffDuration,Action<SingleTrailImpact> onRecycle)
        {
            particles=GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.SetPositionAndRotation(position,Quaternion.identity);transform.localScale=Vector3.one;
            if(visual==null)visual=new ImpactVisual(transform);
            visual.Play(position,color,camera,glyph,handoffDuration);remaining=visual.Lifetime;recycle=onRecycle;
        }
        void Update()
        {
            if(remaining<=0)return;
            visual.Step(Time.deltaTime);remaining-=Time.deltaTime;
            if(remaining>0)return;
            visual.Clear();var callback=recycle;recycle=null;callback?.Invoke(this);
        }
        void OnDisable(){remaining=0;recycle=null;visual?.Clear();}
    }
}
