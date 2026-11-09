using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KeyboardGame.Letters;
namespace KeyboardGame
{
    /// <summary>One bounded, detached dust cluster for an entire word. Six soft billboards, one draw.</summary>
    public sealed class WordDustSequence:IDisposable
    {
        const int Count=6;
        static int serial,lastVariant=-1;
        static float lastHue=-1;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer renderer;
        readonly Vector3 right,up,anchor;
        readonly float delay,lifetime,pause,width,height,opacity;
        readonly Vector2[] spots=new Vector2[Count];
        readonly float[] sizes=new float[Count],seeds=new float[Count];
        readonly Vector3[] vertices=new Vector3[Count*4];
        readonly Color[] colors=new Color[Count*4];
        readonly Vector2[] uvs=new Vector2[Count*4],data=new Vector2[Count*4];
        readonly int[] indices=new int[Count*6];
        bool disposed;
        public int Variant{get;}
        public Color MainColor{get;}
        public int ClusterCount=>disposed?0:1;
        public int PuffCount=>Count;
        public float Width=>width;
        public float WordWidth{get;}
        public float WordHeight{get;}
        public Vector3 Anchor=>anchor;
        public float Duration=>delay+lifetime+pause;
        public float Age{get;private set;}
        public bool IsComplete=>disposed||Age>=Duration;
        public bool IsVisible=>renderer!=null&&renderer.enabled;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetVariation(){serial=0;lastVariant=-1;lastHue=-1;}
        public WordDustSequence(IList<LetterView> letters,Camera cam,Transform owner,WordDustStyle style,float launchDelay)
        {
            right=cam!=null?cam.transform.right:Vector3.right;up=cam!=null?cam.transform.up:Vector3.up;
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity,depth=0;
            var palette=new List<Color>();int hash=17;
            foreach(var v in letters)
            {
                hash=unchecked(hash*31+v.Character);palette.Add(v.MainColor);
                var b=v.Mesh.bounds;
                for(int k=0;k<8;k++)
                {
                    var p=v.Model.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                    float x=Vector3.Dot(p,right),y=Vector3.Dot(p,up);
                    minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
                }
                depth+=Vector3.Dot(v.Model.position,Vector3.Cross(right,up));
            }
            Vector3 forward=Vector3.Cross(right,up);float cap=Mathf.Max(.00001f,maxY-minY);
            WordWidth=maxX-minX;WordHeight=cap;
            anchor=right*((minX+maxX)*.5f)+up*(minY-cap*.035f)+forward*(depth/Mathf.Max(1,letters.Count));
            width=Mathf.Max(.00001f,Mathf.Min((maxX-minX)*Mathf.Clamp(style.maximumWordWidthFraction,.3f,.85f),cap*Mathf.Clamp(style.maximumWidthInHeights,.8f,3)));
            height=cap*Mathf.Clamp(style.heightInHeights,.12f,.4f);
            delay=Mathf.Max(0,launchDelay);lifetime=Mathf.Clamp(style.lifetime,.3f,.8f);pause=Mathf.Clamp(style.clearPause,0,.15f);opacity=Mathf.Clamp01(style.opacity);
            var rng=new System.Random(unchecked(hash+(++serial)*7919)&int.MaxValue);
            Variant=lastVariant<0?rng.Next(5):(lastVariant+1+rng.Next(4))%5;lastVariant=Variant;
            Color chosen=palette.Count>0?palette[rng.Next(palette.Count)]:Color.cyan;
            for(int i=0;i<palette.Count;i++)
            {Color.RGBToHSV(chosen,out var ch,out _,out _);if(lastHue<0||HueDistance(ch,lastHue)>.10f)break;chosen=palette[(i+serial)%palette.Count];}
            Color.RGBToHSV(chosen,out var hue,out _,out _);
            if(lastHue>=0&&HueDistance(hue,lastHue)<.10f)hue=Mathf.Repeat(lastHue+.17f,1);
            lastHue=hue;MainColor=Color.HSVToRGB(hue,Mathf.Clamp01(style.saturation),.76f);
            for(int i=0;i<Count;i++)
            {
                float x=(i%3-1)*.26f+(i/3==0?-.045f:.045f);
                float y=i/3==0?-.10f:.24f;
                float lean=(Variant==0?-.12f:Variant==1?.12f:0);
                if(Variant==2)y+=(i%3==1?.23f:-.06f);
                if(Variant==3)y+=(i%3==1?-.17f:.12f);
                if(Variant==4)x+=(i/3==0?-.07f:.07f);
                spots[i]=new Vector2(x+lean*y+(float)(rng.NextDouble()-.5)*.08f,y+(float)(rng.NextDouble()-.5)*.20f);
                sizes[i]=.82f+(float)rng.NextDouble()*.27f;seeds[i]=(float)rng.NextDouble()*19+1;
                int a=i*4,t=i*6;uvs[a]=new Vector2(0,0);uvs[a+1]=new Vector2(1,0);uvs[a+2]=new Vector2(1,1);uvs[a+3]=new Vector2(0,1);
                indices[t]=a;indices[t+1]=a+2;indices[t+2]=a+1;indices[t+3]=a;indices[t+4]=a+3;indices[t+5]=a+2;
            }
            var source=Resources.Load<Material>("WordDust");var shader=source!=null?source.shader:Shader.Find("KeyboardGame/WordDust");
            if(shader==null)throw new InvalidOperationException("Word dust shader is missing");
            material=source!=null?UnityEngine.Object.Instantiate(source):new Material(shader);material.hideFlags=HideFlags.HideAndDontSave;
            mesh=new Mesh{name="WordDust_6Puffs",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
            root=new GameObject("WordExit_Dust");root.transform.SetParent(owner,false);
            root.transform.SetPositionAndRotation(anchor,Quaternion.identity);root.transform.localScale=Vector3.one;
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Step(0);
        }
        static float HueDistance(float a,float b){float d=Mathf.Abs(a-b);return Mathf.Min(d,1-d);}
        public void Step(float dt)
        {
            if(disposed)return;Age+=Mathf.Max(0,dt);float t=Age-delay;
            renderer.enabled=t>0&&t<lifetime;if(!renderer.enabled)return;
            float p=Mathf.Clamp01(t/lifetime),expand=1-Mathf.Pow(1-p,3);
            for(int i=0;i<Count;i++)
            {
                float local=Mathf.Clamp01((p-(i%2)*.035f)/(.86f+(i%3)*.05f));
                float alpha=Mathf.SmoothStep(0,1,local/.12f)*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((local-.28f)/.72f)))*opacity;
                float x=spots[i].x*(.55f+.45f*expand),y=spots[i].y+expand*(i/3==0?.10f:.24f);
                float rx=width*.125f*sizes[i]*(.60f+.40f*expand),ry=height*.43f*sizes[i]*(.55f+.45f*expand);
                x=Mathf.Clamp(x,-.5f+rx/width,.5f-rx/width);
                Vector3 center=anchor+right*(x*width)+up*(y*height);
                Color c=Color.Lerp(MainColor,Color.white,(i%3)*.055f);c.a=alpha;
                for(int k=0;k<4;k++)
                {
                    int n=i*4+k;Vector2 uv=uvs[n];var world=center+right*((uv.x*2-1)*rx)+up*((uv.y*2-1)*ry);
                    vertices[n]=root.transform.InverseTransformPoint(world);colors[n]=c;data[n]=new Vector2(seeds[i],p);
                }
            }
            mesh.vertices=vertices;mesh.uv=uvs;mesh.uv2=data;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(renderer!=null)renderer.enabled=false;
            Destroy(root);Destroy(mesh);Destroy(material);
        }
        static void Destroy(UnityEngine.Object item){if(item==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
    }
}
