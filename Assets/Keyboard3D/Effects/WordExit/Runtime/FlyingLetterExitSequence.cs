using System;
using System.Collections.Generic;
using UnityEngine;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    /// <summary>Whole-letter flight only: no light visual, particles, shader or material changes.</summary>
    public sealed class FlyingLetterExitSequence : IDisposable
    {
        sealed class Glyph
        {
            public LetterView view;
            public int binding;
            public Vector3 position,scale,center,start,end;
            public Quaternion rotation;
            public float opacity,height,delay,duration,side,variation;
            public bool hidden;
        }
        readonly List<Glyph> glyphs=new List<Glyph>();
        readonly Camera camera;
        readonly WordExitStyle style;
        readonly Vector3 up,right;
        static int serial;
        float age;
        bool disposed;
        public float Age=>age;
        public float Duration{get;private set;}
        public bool IsComplete{get;private set;}
        public int HiddenCount{get;private set;}
        public float MinimumFacingDot{get;private set;}=1;
        public bool HiddenWhileVisible{get;private set;}
        static float Variation(int i,int version)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+version*7.233f)*4375.8545f,1);
        public FlyingLetterExitSequence(IList<LetterView> letters,Camera cam,WordExitStyle config)
        {
            camera=cam;style=config;up=cam!=null?cam.transform.up:Vector3.up;right=cam!=null?cam.transform.right:Vector3.right;
            int version=++serial;float average=0;
            foreach(var v in letters)average+=v.LocalBounds.size.y*Mathf.Abs(v.transform.lossyScale.y);
            average/=Mathf.Max(1,letters.Count);float delay=0;
            for(int i=0;i<letters.Count;i++)
            {
                var v=letters[i];float variant=Variation(i,version);float side=(i-(letters.Count-1)*.5f)/Mathf.Max(1,(letters.Count-1)*.5f);
                var g=new Glyph{view=v,binding=v.BindingVersion,position=v.Model.localPosition,rotation=v.Model.localRotation,scale=v.Model.localScale,
                    opacity=v.Appearance.OpacityValue,center=v.LocalBounds.center,height=Mathf.Max(.00001f,average),delay=delay,
                    duration=style.quickJump?style.quickSquat+style.quickOutDuration*Mathf.Lerp(.96f,1.04f,variant):Mathf.Max(.35f,style.letterFlightDuration)*Mathf.Lerp(.91f,1.09f,variant),side=side,variation=variant};
                g.start=v.Model.TransformPoint(v.Mesh.bounds.center);g.end=g.start+up*average*5;
                if(cam!=null)
                {
                    var p=cam.WorldToViewportPoint(g.start);
                    float h=Mathf.Abs(cam.WorldToViewportPoint(g.start+up*g.height).y-p.y);
                    g.end=cam.ViewportToWorldPoint(new Vector3(p.x+side*style.letterFanSpread+(variant-.5f)*.008f,1.04f+Mathf.Max(.12f,h*1.1f),p.z));
                    if(style.quickJump)
                    {
                        float angle=style.quickDirectionAngle*(side*.75f+(variant-.5f)*.5f)*Mathf.Deg2Rad;
                        float distance=Vector3.Dot(g.end-g.start,up);
                        g.end=g.start+up*distance+right*(Mathf.Tan(angle)*distance);
                    }
                }
                glyphs.Add(g);delay+=Mathf.Max(0,style.quickJump?style.quickLetterDelay:style.letterDelay)*Mathf.Lerp(.85f,1.15f,Variation(i+9,version));
            }
            float last=glyphs.Count>0?glyphs[glyphs.Count-1].delay:0;
            foreach(var g in glyphs)
            {float cap=style.quickJump?style.quickMaximumStagger:style.maximumStagger;if(last>cap)g.delay*=Mathf.Max(0,cap)/last;Duration=Mathf.Max(Duration,g.delay+g.duration+(style.quickJump?.04f:.1f));}
            Step(0);
        }
        Vector3 Shape(float t)
        {
            if(style.quickJump)
            {
                var flat=new Vector3(style.squashWidth,style.squashHeight,1.04f);
                var tall=new Vector3(style.stretchWidth,style.stretchHeight,.98f);
                if(t<=style.quickSquat)return Vector3.Lerp(Vector3.one,flat,WordExitMotion.Ease(t/Mathf.Max(.01f,style.quickSquat)));
                if(t<=style.quickSquat+.055f)return Vector3.Lerp(flat,tall,WordExitMotion.Ease((t-style.quickSquat)/.055f));
                return Vector3.Lerp(tall,new Vector3(.98f,1.04f,1),WordExitMotion.Ease((t-style.quickSquat-.055f)/.12f));
            }
            // Reuse the accepted squash/stretch/recoil exactly; never enter its old gathering segment.
            if(t<=style.GatherStart)return WordExitMotion.Shape(t,style);
            float q=WordExitMotion.Ease((t-style.GatherStart)/.16f);
            return Vector3.Lerp(new Vector3(1.05f,.95f,1.01f),new Vector3(.97f,1.06f,1),q);
        }
        Vector3 Position(Glyph g,float t)
        {
            if(style.quickJump)
            {
                float q=Mathf.Clamp01((t-style.quickSquat)/Mathf.Max(.05f,g.duration-style.quickSquat));
                // One continuous launch with increasing speed: no airborne hold, recoil or second takeoff.
                float progress=.30f*q+.70f*q*q;
                return Vector3.LerpUnclamped(g.start,g.end,progress)+up*(Mathf.Max(0,t-g.duration)*g.height*8);
            }
            float launch=style.anticipation+style.stretchDuration;
            float u=Mathf.Clamp01((t-launch)/Mathf.Max(.08f,g.duration-launch));
            float travel=WordExitMotion.Flight(u);
            Vector3 jump=up*(g.height*WordExitMotion.Rise(t,style));
            Vector3 remainder=g.end-g.start-up*(g.height*style.jumpHeight);
            float side=(g.side*.8f+(g.variation-.5f)*.4f)*style.flightArc*g.height*Mathf.Sin(u*Mathf.PI);
            return g.start+jump+remainder*travel+right*side+up*(Mathf.Max(0,t-g.duration)*g.height*8);
        }
        Quaternion Rotation(Glyph g,float t,Vector3 center)
        {
            float press=WordExitMotion.Ease(t/Mathf.Max(.01f,style.anticipation));
            float release=WordExitMotion.Ease((t-style.anticipation)/Mathf.Max(.01f,style.stretchDuration));
            float angle=style.flightTilt*(press*(1-release)-.65f*Mathf.Sin(release*Mathf.PI));
            float later=WordExitMotion.Ease((t-style.GatherStart)/.22f);
            float tiltX=angle*Mathf.Lerp(-1,1,g.variation)+later*style.flightTilt*.25f;
            float tiltY=angle*(g.side*.65f+(g.variation-.5f)*.6f);
            float bank=style.flightBank*(g.side*.8f+(g.variation-.5f)*.4f)*WordExitMotion.Ease(t/.25f);
            Quaternion cameraFrame=camera!=null?camera.transform.rotation:Quaternion.identity;
            Quaternion extra=cameraFrame*Quaternion.Euler(tiltX,tiltY,-bank)*Quaternion.Inverse(cameraFrame);
            if(style.quickJump)
            {
                float phi=g.variation*Mathf.PI*2;
                Vector3 axis=new Vector3(Mathf.Sin(phi)*.48f,Mathf.Cos(phi)*.48f,g.side<0?-.72f:.72f).normalized;
                float magnitude=Mathf.Lerp(Mathf.Clamp(style.quickRotationRange.x,0,12),Mathf.Clamp(style.quickRotationRange.y,0,12),g.variation);
                Quaternion tilt=Quaternion.AngleAxis(magnitude*WordExitMotion.Ease(t/Mathf.Max(.01f,style.quickSquat)),axis);
                extra=cameraFrame*tilt*Quaternion.Inverse(cameraFrame);
            }
            Quaternion world=extra*g.view.transform.rotation*g.rotation;
            Vector3 toCamera=camera==null?Vector3.back:(camera.orthographic?-camera.transform.forward:(camera.transform.position-center).normalized);
            Vector3 normal=world*Vector3.forward;
            float limit=Mathf.Clamp(style.maximumFaceAngle,5,35)*Mathf.Deg2Rad;
            Vector3 safe=Vector3.RotateTowards(toCamera,normal,limit,0);
            world=Quaternion.FromToRotation(normal,safe)*world;
            MinimumFacingDot=Mathf.Min(MinimumFacingDot,Vector3.Dot(world*Vector3.forward,toCamera));
            return Quaternion.Inverse(g.view.transform.rotation)*world;
        }
        bool Outside(LetterView view)
        {
            if(camera==null)return true;
            var b=view.Mesh.bounds;float bottom=float.PositiveInfinity;
            for(int k=0;k<8;k++)
            {
                Vector3 p=view.Model.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                bottom=Mathf.Min(bottom,camera.WorldToViewportPoint(p).y);
            }
            return bottom>1.025f;
        }
        public void Step(float deltaTime)
        {
            if(disposed||IsComplete)return;age+=Mathf.Max(0,deltaTime);HiddenCount=0;
            foreach(var g in glyphs)
            {
                if(g.view==null||g.view.BindingVersion!=g.binding){HiddenCount++;continue;}
                if(g.hidden){HiddenCount++;continue;}
                float t=age-g.delay;if(t<0)continue;
                var view=g.view;Vector3 center=Position(g,t);Vector3 shape=Shape(t);
                Quaternion rotation=Rotation(g,t,center);view.Model.localRotation=rotation;view.Model.localScale=Vector3.Scale(g.scale,shape);
                float pivotBlend=style.quickJump?WordExitMotion.Ease((t-style.quickSquat)/.055f):WordExitMotion.Ease((t-style.anticipation)/Mathf.Max(.01f,style.stretchDuration));
                Vector3 anchor=Vector3.Lerp(new Vector3(g.center.x,view.LocalBounds.min.y,g.center.z),g.center,pivotBlend);
                Vector3 meshAnchor=Quaternion.Inverse(g.rotation)*(anchor-g.position);
                meshAnchor=new Vector3(meshAnchor.x/g.scale.x,meshAnchor.y/g.scale.y,meshAnchor.z/g.scale.z);
                view.Model.localPosition=anchor-rotation*Vector3.Scale(meshAnchor,view.Model.localScale)+view.transform.InverseTransformVector(center-g.start);
                view.Appearance.Refresh();
                if(t>=(style.quickJump?style.quickSquat:style.GatherStart)&&Outside(view))
                {g.hidden=true;view.SetOpacity(0);HiddenCount++;}
            }
            IsComplete=HiddenCount==glyphs.Count;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var g in glyphs)if(g.view!=null&&g.view.BindingVersion==g.binding)
            {
                g.view.Model.localPosition=g.position;g.view.Model.localRotation=g.rotation;g.view.Model.localScale=g.scale;
                g.view.SetOpacity(g.opacity);g.view.Appearance.Refresh();
            }
        }
    }
}
