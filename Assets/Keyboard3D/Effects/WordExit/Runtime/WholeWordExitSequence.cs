using System;
using System.Collections.Generic;
using UnityEngine;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    /// <summary>One clock and vertical displacement, individual glyph pivots. No exit rotation.</summary>
    public sealed class WholeWordExitSequence : IDisposable
    {
        sealed class Glyph
        {
            public LetterView view;
            public int binding;
            public Vector3 position,scale,pivot,meshPivot;
            public Quaternion rotation;
            public float opacity;
        }
        readonly List<Glyph> glyphs=new List<Glyph>();
        readonly Camera camera;
        readonly WordExitStyle style;
        readonly Vector3 travel;
        bool disposed;
        public float Age{get;private set;}
        public float Duration=>style.wholeWordSquat+style.wholeWordOutDuration+.04f;
        public bool IsComplete{get;private set;}
        public int HiddenCount{get;private set;}
        public float MinimumFacingDot{get;private set;}=1;
        public WholeWordExitSequence(IList<LetterView> letters,Camera cam,WordExitStyle config)
        {
            camera=cam;style=config;Vector3 up=cam!=null?cam.transform.up:Vector3.up;float distance=0;
            foreach(var v in letters)
            {
                var b=v.LocalBounds;
                var g=new Glyph{view=v,binding=v.BindingVersion,position=v.Model.localPosition,rotation=v.Model.localRotation,scale=v.Model.localScale,
                    opacity=v.Appearance.OpacityValue,pivot=new Vector3(b.center.x,b.min.y,b.center.z)};
                Vector3 p=Quaternion.Inverse(g.rotation)*(g.pivot-g.position);
                g.meshPivot=new Vector3(p.x/g.scale.x,p.y/g.scale.y,p.z/g.scale.z);glyphs.Add(g);
                var mesh=v.Mesh.bounds;
                for(int k=0;k<8;k++)
                {
                    Vector3 point=v.Model.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                    if(cam==null){distance=Mathf.Max(distance,b.size.y*Mathf.Abs(v.transform.lossyScale.y)*6);continue;}
                    var screen=cam.WorldToViewportPoint(point);
                    var target=cam.ViewportToWorldPoint(new Vector3(screen.x,1.09f,screen.z));
                    distance=Mathf.Max(distance,Vector3.Dot(target-point,up));
                }
            }
            travel=up*Mathf.Max(.001f,distance);Step(0);
        }
        Vector3 Shape(float time)
        {
            Vector3 flat=new Vector3(style.wholeWordSquash.x,style.wholeWordSquash.y,1.04f);
            Vector3 tall=new Vector3(style.wholeWordStretch.x,style.wholeWordStretch.y,.98f);
            float squat=Mathf.Max(.01f,style.wholeWordSquat);
            if(time<=squat)return Vector3.Lerp(Vector3.one,flat,WordExitMotion.Ease(time/squat));
            if(time<=squat+.06f)return Vector3.Lerp(flat,tall,WordExitMotion.Ease((time-squat)/.06f));
            return Vector3.Lerp(tall,new Vector3(.99f,1.06f,1),WordExitMotion.Ease((time-squat-.06f)/.16f));
        }
        bool Outside()
        {
            if(camera==null)return Age>=Duration;
            foreach(var g in glyphs)
            {
                var v=g.view;var b=v.Mesh.bounds;
                for(int k=0;k<8;k++)
                {
                    var p=v.Model.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                    if(camera.WorldToViewportPoint(p).y<=1.025f)return false;
                }
            }
            return true;
        }
        public void Step(float deltaTime)
        {
            if(disposed||IsComplete)return;
            foreach(var g in glyphs)if(g.view==null||g.view.BindingVersion!=g.binding){IsComplete=true;return;}
            Age+=Mathf.Max(0,deltaTime);
            float q=Mathf.Clamp((Age-style.wholeWordSquat)/Mathf.Max(.05f,style.wholeWordOutDuration),0,2);
            Vector3 motion=travel*(.04f*q+.96f*q*q*q);Vector3 shape=Shape(Age);
            foreach(var g in glyphs)
            {
                var v=g.view;v.Model.localScale=Vector3.Scale(g.scale,shape);
                v.Model.localRotation=g.rotation;
                v.Model.localPosition=g.pivot-g.rotation*Vector3.Scale(g.meshPivot,v.Model.localScale)+v.transform.InverseTransformVector(motion);
                Vector3 toward=camera==null?Vector3.back:(camera.orthographic?-camera.transform.forward:(camera.transform.position-v.Model.TransformPoint(v.Mesh.bounds.center)).normalized);
                MinimumFacingDot=Mathf.Min(MinimumFacingDot,Vector3.Dot(v.Model.forward,toward));v.Appearance.Refresh();
            }
            if(Age>=style.wholeWordSquat&&Outside())
            {foreach(var g in glyphs)g.view.SetOpacity(0);HiddenCount=glyphs.Count;IsComplete=true;}
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
