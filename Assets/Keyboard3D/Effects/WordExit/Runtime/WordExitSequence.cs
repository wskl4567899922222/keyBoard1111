using System;
using System.Collections.Generic;
using UnityEngine;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    public sealed class WordExitSequence : IDisposable
    {
        sealed class Glyph
        {
            public LetterView view;
            public int binding;
            public Vector3 savedPosition,savedScale,center,start,end;
            public float height,delay,seed;
            public bool hidden;
            public float hideAt=-1,coveredRadius,coverageMargin;
        }
        readonly List<Glyph> glyphs=new List<Glyph>();
        readonly WordExitStyle style;
        readonly FlyingLetterExitSequence flying;
        readonly WholeWordExitSequence whole;
        readonly WordEnergySequence energy;
        readonly WordExitVisual visual;
        readonly Camera camera;
        readonly Vector3 up,right,forward;
        readonly int smallCount;
        float age;
        bool disposed;
        public bool IsComplete{get;private set;}
        public int HiddenCount{get;private set;}
        public int PointCount=>visual!=null?visual.PointCount:0;
        public float Age=>age;
        public float Duration{get;private set;}
        public float FlightDuration=>whole!=null?whole.Duration:Duration;
        public bool LettersOffscreen=>whole!=null&&whole.IsComplete;
        public bool DustVisible=>false; // Historical editor probes only; dust is retired.
        public int DustPuffCount=>0;
        public bool EnergyVisible=>energy!=null&&energy.IsVisible;
        public bool HasEnergy=>energy!=null;
        public WordEnergySequence.Stage EnergyPhase=>energy!=null?energy.Phase:WordEnergySequence.Stage.Complete;
        public float MinimumCoverageMargin{get;private set;}=float.PositiveInfinity;
        public WordExitSequence(IList<LetterView> letters,Camera cam,Transform owner,WordExitStyle config,Material material)
        {
            style=UnityEngine.Object.Instantiate(config);style.hideFlags=HideFlags.HideAndDontSave;camera=cam;
            if(style.mode==WordExitStyle.ExitMode.FlyingLetters&&style.wholeWordJump)
            {
                var energyStyle=Resources.Load<WordEnergyStyle>("WordEnergyStyle");
                if(energyStyle!=null&&energyStyle.enabled&&letters.Count>0)
                    energy=new WordEnergySequence(letters,cam,owner,energyStyle,style.wholeWordSquat);
                whole=new WholeWordExitSequence(letters,cam,style);
                Duration=Mathf.Max(whole.Duration,energy!=null?energy.Duration:0);return;
            }
            if(style.mode==WordExitStyle.ExitMode.FlyingLetters)
            {flying=new FlyingLetterExitSequence(letters,cam,style);Duration=flying.Duration;return;}
            up=cam!=null?cam.transform.up:Vector3.up;right=cam!=null?cam.transform.right:Vector3.right;forward=cam!=null?cam.transform.forward:Vector3.forward;
            smallCount=Mathf.Min(Mathf.Clamp(style.smallPointCount,0,16),Mathf.Max(0,style.totalSmallPointBudget)/Mathf.Max(1,letters.Count));
            visual=new WordExitVisual(owner,material);
            float average=0;foreach(var view in letters)average+=ProjectedHeight(view);
            average/=Mathf.Max(1,letters.Count);
            for(int i=0;i<letters.Count;i++)
            {
                var v=letters[i];var g=new Glyph{view=v,binding=v.BindingVersion,savedPosition=v.Model.localPosition,savedScale=v.Model.localScale,
                    center=v.LocalBounds.center,height=Mathf.Max(.00001f,average),delay=style.Delay(i,letters.Count),seed=i*2.399963f};
                g.start=v.transform.TransformPoint(g.center);g.end=g.start+up*average*4;
                if(cam!=null)
                {
                    var p=cam.WorldToViewportPoint(g.start);
                    float fan=(i-(letters.Count-1)*.5f)/Mathf.Max(1,(letters.Count-1)*.5f)*style.fanOut;
                    g.end=cam.ViewportToWorldPoint(new Vector3(Mathf.Clamp(p.x+fan,.04f,.96f),1.12f,p.z));
                }
                glyphs.Add(g);Duration=Mathf.Max(Duration,g.delay+style.LaunchTime+style.flyDuration+style.tailLifetime);
            }
            Step(0);
        }
        float ProjectedHeight(LetterView view)
        {
            var b=view.LocalBounds;float min=float.PositiveInfinity,max=float.NegativeInfinity;
            for(int k=0;k<8;k++)
            {
                Vector3 p=view.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                float y=Vector3.Dot(p,up);min=Mathf.Min(min,y);max=Mathf.Max(max,y);
            }
            return max-min;
        }
        Vector3 Head(Glyph g,float time)
        {
            float lift=g.height*WordExitMotion.Rise(time,style);
            if(time<=style.LaunchTime)return g.start+up*lift;
            return Vector3.LerpUnclamped(g.start+up*g.height*style.jumpHeight,g.end,WordExitMotion.Flight((time-style.LaunchTime)/style.flyDuration));
        }
        Vector3 Foreground(Vector3 point,float depth,out float radiusScale)
        {
            radiusScale=1;
            if(camera==null)return point-forward*depth;
            Vector3 p=camera.WorldToViewportPoint(point);float z=Mathf.Max(camera.nearClipPlane*1.1f,p.z-depth);
            // Move along the view ray: forward-only offsets shift off-center glyphs on screen.
            if(!camera.orthographic)radiusScale=z/Mathf.Max(.00001f,p.z);
            p.z=z;return camera.ViewportToWorldPoint(p);
        }
        void Pose(Glyph g,float t)
        {
            var shape=WordExitMotion.Shape(t,style);
            g.view.Model.localScale=Vector3.Scale(g.savedScale,shape);
            float pivotBlend=WordExitMotion.Ease((t-style.anticipation)/Mathf.Max(.01f,style.stretchDuration));
            Vector3 anchor=Vector3.Lerp(new Vector3(g.center.x,g.view.LocalBounds.min.y,g.center.z),g.center,pivotBlend);
            // Shape is diagonal in the letter's local axes; preserve center/bottom independently of Y=180 model rotation.
            Vector3 correction=anchor-Vector3.Scale(anchor,shape);
            g.view.Model.localPosition=g.savedPosition+correction+g.view.transform.InverseTransformVector(Head(g,t)-g.start);
            g.view.Appearance.Refresh();
        }
        float ProjectedRadius(Glyph g,Vector3 head)
        {
            var b=g.view.Mesh.bounds;float radius=0;
            Vector3 center=camera!=null?camera.WorldToScreenPoint(head):head;
            for(int k=0;k<8;k++)
            {
                Vector3 p=g.view.Model.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                if(camera!=null)p=camera.WorldToScreenPoint(p);
                radius=Mathf.Max(radius,Vector2.Distance(new Vector2(p.x,p.y),new Vector2(center.x,center.y)));
            }
            return radius;
        }
        public void Step(float deltaTime)
        {
            if(disposed||IsComplete)return;
            if(whole!=null)
            {
                age+=Mathf.Max(0,deltaTime);whole.Step(deltaTime);energy?.Step(deltaTime,whole.IsComplete);
                HiddenCount=whole.HiddenCount;
                IsComplete=whole.IsComplete&&(energy==null||energy.IsComplete);return;
            }
            if(flying!=null){flying.Step(deltaTime);age=flying.Age;HiddenCount=flying.HiddenCount;IsComplete=flying.IsComplete;return;}
            age+=Mathf.Max(0,deltaTime);visual.Begin(age,style.disturbance,style.brightness);
            HiddenCount=0;
            foreach(var g in glyphs)
            {
                if(g.view==null||g.view.BindingVersion!=g.binding)continue;
                float t=age-g.delay;if(t<0)continue;
                float gather=Mathf.Clamp01((t-style.GatherStart)/Mathf.Max(.01f,style.gatherDuration));
                float birth=WordExitMotion.Ease((gather-.28f)/.72f);
                Vector3 head=Head(g,t);float radius=g.height*style.headRadius*birth;
                if(!g.hidden)
                {
                    Pose(g,t);
                    if(birth>=1)
                    {
                        float core=radius*WordExitVisual.OpaqueCoreFraction;
                        if(camera!=null)core=Vector2.Distance(camera.WorldToScreenPoint(head),camera.WorldToScreenPoint(head+up*core));
                        float remaining=ProjectedRadius(g,head);g.coverageMargin=core-remaining;
                        if(remaining<=core*.94f)
                        {
                            g.hidden=true;g.hideAt=t;g.coveredRadius=remaining;g.view.SetOpacity(0);
                            MinimumCoverageMargin=Mathf.Min(MinimumCoverageMargin,g.coverageMargin);
                        }
                    }
                }
                if(g.hidden)HiddenCount++;
                float flight=(t-style.LaunchTime)/Mathf.Max(.01f,style.flyDuration);
                Vector3 drawHead=Foreground(head,g.height*.7f,out var headScale);
                Color color=g.view.MainColor;
                if(flight<=1)visual.Add(drawHead,right,up,radius*headScale,color,birth,g.seed);
                for(int j=0;j<smallCount;j++)
                {
                    float f=(j+.5f)/Mathf.Max(1,smallCount);
                    float angle=g.seed+j*2.399963f;
                    float spread=g.height*style.clusterSpread*(.32f+.68f*Mathf.Sqrt(Mathf.Repeat((j+1)*.618034f+g.seed*.117f,1)));
                    float releaseAt=style.LaunchTime+style.flyDuration*(.10f+.78f*f);
                    float tailAge=Mathf.Max(0,t-releaseAt);
                    if(tailAge>style.tailLifetime)continue;
                    Vector3 jitter=(right*Mathf.Cos(angle+t*3+j*.3f)+up*Mathf.Sin(angle+t*2.6f))*spread*.10f;
                    Vector3 offset=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*spread;
                    Vector3 pos=head+offset+jitter;
                    if(t>releaseAt)
                    {
                        Vector3 velocity=(Head(g,releaseAt+.001f)-Head(g,releaseAt-.001f))/.002f*style.inheritedVelocity;
                        Vector3 releaseJitter=(right*Mathf.Cos(angle+releaseAt*3+j*.3f)+up*Mathf.Sin(angle+releaseAt*2.6f))*spread*.10f;
                        pos=Head(g,releaseAt)+offset+releaseJitter+WordExitMotion.FollowDisplacement(velocity,tailAge,style.drag);
                        pos+=right*Mathf.Sin(angle+tailAge*4)*g.height*.07f*tailAge;
                    }
                    float life=1-Mathf.Clamp01(tailAge/Mathf.Max(.01f,style.tailLifetime));
                    float size=g.height*style.smallPointRadius*(.65f+.35f*Mathf.Sin(j*1.71f+g.seed)*Mathf.Sin(j*1.71f+g.seed))*Mathf.Sqrt(life)*birth;
                    Vector3 drawPoint=Foreground(pos,g.height*.72f,out var pointScale);
                    visual.Add(drawPoint,right,up,size*pointScale,color,life*birth*.83f,angle);
                }
            }
            visual.End();
            IsComplete=age>=Duration;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;energy?.Dispose();whole?.Dispose();flying?.Dispose();visual?.Dispose();
            foreach(var g in glyphs)if(g.view!=null&&g.view.BindingVersion==g.binding)
            {g.view.Model.localPosition=g.savedPosition;g.view.Model.localScale=g.savedScale;g.view.Appearance.Refresh();}
            if(Application.isPlaying)UnityEngine.Object.Destroy(style);else UnityEngine.Object.DestroyImmediate(style);
        }
    }
}
