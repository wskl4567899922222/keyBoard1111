using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KeyboardGame.Letters;
namespace KeyboardGame
{
    /// <summary>One charging line, a continuous two-ended collapse and a single finishing point.</summary>
    public sealed class WordEnergySequence:IDisposable
    {
        public enum Stage{Charge,Release,Collapse,Point,Extinguish,Clear,Complete}
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly Texture2D gradient;
        readonly Color[] gradientPixels=new Color[128];
        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        readonly Vector3 right,up;
        readonly float charge,collapseStart,collapseEnd,hold,fade,pause,halfHeight,core,dotRadius,brightness;
        readonly float pressAttack,pressKick,coreBoost,flashDuration,flashGain,flashScale;
        readonly float gatherPointScale;
        const int RibbonSegments=24;
        const int PointStart=(RibbonSegments+1)*4;
        readonly Vector3[] vertices=new Vector3[PointStart+4];
        readonly Color[] colors=new Color[PointStart+4];
        readonly Vector2[] uvs=new Vector2[PointStart+4],modes=new Vector2[PointStart+4];
        readonly int[] indices=new int[RibbonSegments*12+6];
        readonly float weaveHeight,disturbance;
        readonly Color leftTint,rightTint;
        float offscreenAt=-1;
        bool disposed;
        bool flashPeakPresented;
        public float Age{get;private set;}
        public Stage Phase{get;private set;}
        public float LineFraction{get;private set;}
        public float ChargeAmount{get;private set;}
        public float PointOpacity{get;private set;}
        public float LineOpacity{get;private set;}
        public float CurrentIntensity{get;private set;}
        public float PressImpulse{get;private set;}
        public float FinishFlashAmount{get;private set;}
        public float PointGain=>1+flashGain*FinishFlashAmount;
        public float GatherAmount{get;private set;}
        public float GatherScale{get;private set;}
        public float PointVisualScale{get;private set;}
        public float WeaveAmount{get;private set;}
        public int RibbonVertexCount=>vertices.Length;
        public bool IsComplete=>disposed||Phase==Stage.Complete;
        public bool IsVisible=>renderer!=null&&renderer.enabled;
        public float Duration=>collapseEnd+hold+fade+pause;
        public float Width{get;}
        public float WordWidth{get;}
        public Vector3 Anchor{get;}
        public Color MainColor{get;}
        public WordEnergySequence(IList<LetterView> letters,Camera cam,Transform owner,WordEnergyStyle config,float squat)
        {
            if(letters==null||letters.Count==0)throw new ArgumentException("Energy needs a word",nameof(letters));
            var source=Resources.Load<Material>("WordEnergy");
            var shader=source!=null?source.shader:Shader.Find("KeyboardGame/WordEnergy");
            if(shader==null)throw new InvalidOperationException("WordEnergy shader is missing");
            right=cam!=null?cam.transform.right:Vector3.right;up=cam!=null?cam.transform.up:Vector3.up;
            Vector3 forward=Vector3.Cross(right,up);
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity,depth=0;
            var centers=new List<float>();var palette=new List<Color>();
            foreach(var v in letters)
            {
                var b=v.Mesh.bounds;
                centers.Add(Vector3.Dot(v.Model.TransformPoint(b.center),right));
                Color.RGBToHSV(v.MainColor,out var hue,out _,out _);
                palette.Add(Color.HSVToRGB(hue,Mathf.Clamp(config.saturation,.2f,1),1));
                for(int i=0;i<8;i++)
                {
                    Vector3 p=v.Model.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    minX=Mathf.Min(minX,Vector3.Dot(p,right));maxX=Mathf.Max(maxX,Vector3.Dot(p,right));
                    minY=Mathf.Min(minY,Vector3.Dot(p,up));maxY=Mathf.Max(maxY,Vector3.Dot(p,up));
                }
                depth+=Vector3.Dot(v.Model.position,forward);
            }
            WordWidth=maxX-minX;float h=Mathf.Max(.00001f,maxY-minY);
            Anchor=right*((minX+maxX)*.5f)+up*(minY-h*Mathf.Clamp(config.belowWord,.02f,.2f))+forward*(depth/letters.Count);
            float pixel=h/100;
            if(cam!=null)
            {
                Vector3 p=cam.WorldToViewportPoint(Anchor);
                pixel=Vector3.Distance(cam.ViewportToWorldPoint(p),cam.ViewportToWorldPoint(p+Vector3.up/Mathf.Max(1,cam.pixelHeight)));
            }
            Width=Mathf.Max(.000001f,WordWidth*(Mathf.Clamp(config.wordWidthFraction,.55f,1)+2*Mathf.Clamp(config.extraLetterWidthPerEnd,0,1.5f)/letters.Count));
            halfHeight=Mathf.Max(h*Mathf.Clamp(config.haloHeight,.03f,.15f),pixel*3.5f);
            core=Mathf.Max(h*Mathf.Clamp(config.coreThickness,.003f,.04f)*.5f,pixel*.55f);
            dotRadius=Mathf.Min(WordWidth*.9f*.12f,Mathf.Max(h*Mathf.Clamp(config.pointRadius,.02f,.09f),pixel*1.2f));
            charge=Mathf.Max(.01f,squat);collapseStart=charge+Mathf.Clamp(config.releaseHold,0,.12f);
            collapseEnd=collapseStart+Mathf.Clamp(config.collapseDuration,.12f,.35f);
            hold=Mathf.Clamp(config.pointHold,.02f,.1f);fade=Mathf.Clamp(config.extinguishDuration,.05f,.18f);pause=Mathf.Clamp(config.clearPause,0,.15f);
            brightness=Mathf.Clamp(config.brightness,.5f,3);
            pressAttack=Mathf.Clamp(config.pressAttack,.015f,.06f);pressKick=Mathf.Clamp(config.pressBrightnessKick,0,.5f);coreBoost=Mathf.Clamp(config.chargeCoreBoost,0,.5f);
            flashDuration=Mathf.Clamp(config.finishFlashDuration,.04f,.12f);flashGain=Mathf.Clamp(config.finishFlashGain,0,1.5f);flashScale=Mathf.Clamp(config.finishFlashScale,0,.35f);
            gatherPointScale=Mathf.Clamp(config.gatherPointScale,1,3.5f);
            weaveHeight=h*Mathf.Clamp(config.weaveHeightInHeights,0,.08f);disturbance=Mathf.Clamp(config.pointDisturbance,0,.2f);
            int stop=0;
            for(int i=0;i<gradientPixels.Length;i++)
            {
                float x=(minX+maxX)*.5f+((float)i/(gradientPixels.Length-1)-.5f)*Width;
                while(stop<centers.Count-1&&x>centers[stop+1])stop++;
                gradientPixels[i]=stop==centers.Count-1?palette[stop]:Color.Lerp(palette[stop],palette[stop+1],Mathf.InverseLerp(centers[stop],centers[stop+1],x));
            }
            MainColor=GradientColor(.5f);
            leftTint=GradientColor(.25f);rightTint=GradientColor(.75f);
            gradient=new Texture2D(128,1,TextureFormat.RGBA32,false,true){name="WordEnergy_StableGradient",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            gradient.SetPixels(gradientPixels);gradient.Apply(false,true);
            InitializeMeshLayout();
            material=source!=null?UnityEngine.Object.Instantiate(source):new Material(shader);material.hideFlags=HideFlags.HideAndDontSave;
            mesh=new Mesh{name="WordEnergy_LineAndPoint",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
            root=new GameObject("WordExit_Energy");root.transform.SetParent(owner,false);root.transform.SetPositionAndRotation(Anchor,Quaternion.identity);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Step(0,false);
        }
        public void Step(float dt,bool lettersOffscreen)
        {
            if(disposed||IsComplete)return;float previousAge=Age;Age+=Mathf.Max(0,dt);
            if(lettersOffscreen&&offscreenAt<0)offscreenAt=Age;
            float collapse=Mathf.Clamp01((Age-collapseStart)/Mathf.Max(.01f,collapseEnd-collapseStart));
            ChargeAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/charge));
            PressImpulse=Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/pressAttack))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(pressAttack,pressAttack+.04f,Age)));
            LineFraction=1-Mathf.Pow(collapse,2.2f);
            GatherAmount=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,1,collapse));
            GatherScale=Mathf.Lerp(.20f,gatherPointScale,GatherAmount);
            WeaveAmount=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.70f,collapse));
            float extinguishAt=offscreenAt<0?float.PositiveInfinity:Mathf.Max(collapseEnd,offscreenAt)+hold;
            float extinction=Mathf.Clamp01((Age-extinguishAt)/fade);
            float flashAge=Age-Mathf.Max(collapseEnd,offscreenAt);
            FinishFlashAmount=offscreenAt<0?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(flashAge/(flashDuration/3)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(flashDuration/3,flashDuration,flashAge)));
            float flashPeakAt=Mathf.Max(collapseEnd,offscreenAt)+flashDuration/3;
            // A slow frame may cross the whole short pulse. Present its peak once while the point still exists.
            if(offscreenAt>=0&&!flashPeakPresented&&previousAge<flashPeakAt&&Age>=flashPeakAt&&Age<extinguishAt+fade)
                FinishFlashAmount=1;
            if(FinishFlashAmount>=.95f)flashPeakPresented=true;
            float alpha=1-Mathf.SmoothStep(0,1,extinction);
            if(Age<charge)Phase=Stage.Charge;
            else if(Age<collapseStart)Phase=Stage.Release;
            else if(Age<collapseEnd)Phase=Stage.Collapse;
            else if(Age<extinguishAt)Phase=Stage.Point;
            else if(Age<extinguishAt+fade)Phase=Stage.Extinguish;
            else if(Age<extinguishAt+fade+pause)Phase=Stage.Clear;
            else Phase=Stage.Complete;
            renderer.enabled=alpha>0&&Phase!=Stage.Clear&&Phase!=Stage.Complete;
            float lineAlpha=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1,collapse)))*Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/.010f))*alpha;
            float pointAlpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.95f,collapse))*alpha;
            PointOpacity=pointAlpha;LineOpacity=lineAlpha;
            float lineHalo=halfHeight*(1+.08f*PressImpulse);
            DrawRibbons(lineHalo,lineAlpha);
            PointVisualScale=GatherScale*Mathf.Pow(1-extinction,1.4f)*(1+flashScale*FinishFlashAmount);
            float pointSize=dotRadius*3*PointVisualScale;
            // Begin as a small horizontal swelling, then round out as the line converges.
            AddQuad(PointStart,pointSize*Mathf.Lerp(1.6f,1,GatherAmount),pointSize,pointAlpha);
            mesh.vertices=vertices;mesh.uv=uvs;mesh.uv2=modes;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();
            float release=Mathf.SmoothStep(0,1,Mathf.Clamp01((Age-charge)/.115f));
            properties.SetColor("_Tint",MainColor);properties.SetFloat("_CoreRatio",core/lineHalo*Mathf.Lerp(.72f,1.35f,ChargeAmount)*(1+coreBoost*Mathf.Max(PressImpulse,ChargeAmount*(1-release))));
            CurrentIntensity=brightness*Mathf.Min(1,Mathf.Lerp(.32f,1,ChargeAmount)+pressKick*PressImpulse)*Mathf.Lerp(1,.67f,release);
            properties.SetFloat("_PointGain",PointGain);
            properties.SetFloat("_Gather",GatherAmount);
            properties.SetFloat("_Disturbance",disturbance*GatherAmount);properties.SetFloat("_FlowTime",Age*3);
            properties.SetColor("_LeftTint",leftTint);properties.SetColor("_RightTint",rightTint);
            properties.SetFloat("_Intensity",CurrentIntensity);properties.SetTexture("_GradientTex",gradient);renderer.SetPropertyBlock(properties);
        }
        public Color GradientColor(float u)
        {
            float p=Mathf.Clamp01(u)*(gradientPixels.Length-1);int i=Mathf.Min(gradientPixels.Length-2,Mathf.FloorToInt(p));
            return Color.Lerp(gradientPixels[i],gradientPixels[i+1],p-i);
        }
        void InitializeMeshLayout()
        {
            int at=0;
            for(int arm=0;arm<2;arm++)
            {
                int start=arm*(RibbonSegments+1)*2;
                for(int j=0;j<=RibbonSegments;j++)
                {
                    float t=(float)j/RibbonSegments;float x=arm==0?t*.5f:1-t*.5f;
                    uvs[start+j*2]=new Vector2(x,0);uvs[start+j*2+1]=new Vector2(x,1);
                    if(j==RibbonSegments)continue;
                    int a=start+j*2;indices[at++]=a;indices[at++]=a+3;indices[at++]=a+1;
                    indices[at++]=a;indices[at++]=a+2;indices[at++]=a+3;
                }
            }
            for(int k=0;k<4;k++){uvs[PointStart+k]=new Vector2(k==1||k==2?1:0,k>=2?1:0);modes[PointStart+k]=Vector2.right;}
            indices[at++]=PointStart;indices[at++]=PointStart+2;indices[at++]=PointStart+1;
            indices[at++]=PointStart;indices[at++]=PointStart+3;indices[at]=PointStart+2;
        }
        float ArmLength=>Mathf.Max(Width*.5f*LineFraction,dotRadius*.3f);
        float LocalRadius=>Mathf.Min(ArmLength,dotRadius*GatherScale*3);
        void RibbonFrame(bool left,float t,out Vector2 p,out Vector2 tangent)
        {
            float sign=left?-1:1,length=ArmLength,region=LocalRadius;
            float distance=length*(1-Mathf.Clamp01(t));
            float u=Mathf.Clamp01(1-distance/region);
            float lift=Mathf.Min(weaveHeight,region*.15f)*WeaveAmount;
            float wave=u<=0||u>=1?0:Mathf.Sin(Mathf.PI*u);
            p=new Vector2(sign*distance,sign*lift*wave*wave);
            tangent=new Vector2(-sign*length,sign*lift*Mathf.PI*Mathf.Sin(2*Mathf.PI*u)*length/region);
        }
        public Vector2 RibbonPoint(bool left,float t)
        {
            RibbonFrame(left,t,out var p,out _);return p;
        }
        void DrawRibbons(float radius,float alpha)
        {
            for(int arm=0;arm<2;arm++)
            {
                bool left=arm==0;int start=arm*(RibbonSegments+1)*2;
                for(int j=0;j<=RibbonSegments;j++)
                {
                    // Eight straight spans and sixteen local spans keep the tiny join smooth at any word length.
                    float join=1-LocalRadius/ArmLength;
                    float t=j<=8?join*j/8f:Mathf.Lerp(join,1,(j-8)/16f);
                    RibbonFrame(left,t,out var p,out var tangent);
                    if(tangent.sqrMagnitude<1e-12f)tangent=left?Vector2.right:Vector2.left;
                    var normal=new Vector2(-tangent.y,tangent.x).normalized;
                    for(int side=0;side<2;side++)
                    {
                        int n=start+j*2+side;Vector2 q=p+normal*((side==0?-1:1)*radius);
                        uvs[n]=new Vector2(left?t*.5f:1-t*.5f,side);
                        vertices[n]=root.transform.InverseTransformPoint(Anchor+right*q.x+up*q.y);colors[n]=new Color(1,1,1,alpha);
                    }
                }
            }
        }
        void AddQuad(int start,float rx,float ry,float alpha)
        {
            for(int i=0;i<4;i++)
            {
                Vector2 uv=uvs[start+i];Vector3 p=Anchor+right*((uv.x*2-1)*rx)+up*((uv.y*2-1)*ry);
                vertices[start+i]=root.transform.InverseTransformPoint(p);colors[start+i]=new Color(1,1,1,alpha);
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;if(renderer!=null)renderer.enabled=false;Destroy(root);Destroy(mesh);Destroy(material);Destroy(gradient);}
        static void Destroy(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
