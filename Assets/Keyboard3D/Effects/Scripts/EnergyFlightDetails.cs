using UnityEngine;
using UnityEngine.Rendering;
namespace KeyboardGame.TrailFlightStudy
{
 public sealed class EnergyFlightDetails:MonoBehaviour
 {
  const int Slots=168;
  Mesh mesh;Material material;MeshRenderer draw;
  MaterialPropertyBlock properties;
  readonly Vector3[] vertices=new Vector3[Slots*4];
  readonly Vector2[] uv=new Vector2[Slots*4],kind=new Vector2[Slots*4];
  readonly Color[] colors=new Color[Slots*4];
  public int ActiveCount{get;private set;}
  public readonly int[] ActiveByTypeAndSide=new int[6];
  /// <summary>碎点/星星增益（全局）：尺寸与亮度同乘。参考工程靠 Bloom 撑亮闪点，
  /// 本工程 Bloom 弱时调高此值可还原参考视频的亮闪拖尾碎光。</summary>
  public static float DetailBoost=1f;
  static float Hash(int n){return Mathf.Repeat(Mathf.Sin(n*127.1f+17.7f)*43758.5453f,1);}
  void Ensure()
  {
   if(mesh!=null)return;
   var shader=Shader.Find("Keyboard/Preview/EnergyFlightDetails");
   if(shader==null)throw new System.InvalidOperationException("Energy flight detail shader missing");
   material=new Material(shader){name="Procedural speed lines fragments and blue stars"};
   var filter=GetComponent<MeshFilter>();if(filter==null)filter=gameObject.AddComponent<MeshFilter>();
   draw=GetComponent<MeshRenderer>();if(draw==null)draw=gameObject.AddComponent<MeshRenderer>();
   draw.sharedMaterial=material;draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=false;
   mesh=new Mesh{name="Pooled flight detail quads"};mesh.MarkDynamic();
   int[] triangles=new int[Slots*6];
   for(int n=0;n<Slots;n++)
   {
    int v=n*4,k=n*6;
    uv[v]=new Vector2(-1,-1);uv[v+1]=new Vector2(1,-1);uv[v+2]=new Vector2(-1,1);uv[v+3]=new Vector2(1,1);
    triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;
    triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;
   }
   mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;filter.sharedMesh=mesh;properties=new MaterialPropertyBlock();
  }
  public void Sample(EnergyFlightPreview owner,float seconds)
  {
   // ribbon 层可能被整体清空（tails=new Renderer[0]），此时退回 owner 自身层级
   if(owner.tails!=null&&owner.tails.Length>0)gameObject.layer=owner.tails[0].gameObject.layer;
   else gameObject.layer=owner.gameObject.layer;
   Ensure();ActiveCount=0;System.Array.Clear(ActiveByTypeAndSide,0,6);
   for(int n=0;n<Slots;n++)
   {
    int pair=n/2,category=pair%8;
    int type=category<2?0:category==2||category==5?2:1; // line, fragment, star
    // 速度线(type 0)在手部精灵之上渲染，看起来像手被细线割裂；默认只保留碎点与星星
    if(type==0){int f=n*4;for(int k=0;k<4;k++){vertices[f+k]=Vector3.zero;colors[f+k]=Color.clear;}continue;}
    float born=.035f+pair*.0075f+Hash(n+11)*.003f;
    float duration=type==0?.20f:type==1?.24f:.28f;
    float age=seconds-born;
    bool alive=born<owner.flightDuration-.035f&&age>0&&age<duration;
    int first=n*4;
    if(!alive){for(int k=0;k<4;k++){vertices[first+k]=Vector3.zero;colors[first+k]=Color.clear;}continue;}
    float progress=age/duration;
    // Each detail keeps its own trajectory/lifetime, including after the head arrives.
    // Emit into staggered positions along the travelled history, not just near the head.
    float lag=type==0?.015f+(pair/8%3)*.080f+Hash(n+37)*.025f:Hash(n+37)*(type==2?.085f:.11f);
    float pathTime=Mathf.Max(Mathf.Max(0,seconds-owner.tailLifetime*.96f),born+age*(type==0?.55f:.30f)-lag);
    if(pathTime>=owner.flightDuration){for(int k=0;k<4;k++)colors[first+k]=Color.clear;continue;}
    ActiveCount++;
    ActiveByTypeAndSide[type*2+n%2]++;
    Vector3 tangent=owner.FlightTangent(pathTime);
    Vector3 side=Vector3.Cross(owner.previewCamera.transform.forward,tangent).normalized;
    // Each emission group covers both sides; positions are jittered independently.
    float sign=n%2==0?-1:1;
    float spread=((type==0?.004f:.0018f)+Hash(n+21)*(type==0?.004f:.0048f))*sign;
    float emergence=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.16f,born));
    Vector3 center=owner.FlightPoint(pathTime)+side*spread*emergence;
    center+=owner.previewCamera.transform.forward*((Hash(n+77)-.5f)*.002f);
    float a=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,progress))*Mathf.Pow(1-progress,1.05f);
    Vector3 along=type==2?owner.previewCamera.transform.right:tangent;
    Vector3 up=type==2?owner.previewCamera.transform.up:side;
    if(type==1)
    {
     float angle=(Hash(n+95)-.5f)*1.1f;
     along=tangent*Mathf.Cos(angle)+side*Mathf.Sin(angle);
     up=side*Mathf.Cos(angle)-tangent*Mathf.Sin(angle);
    }
    float starSize=(.0010f+Mathf.Pow(Hash(n+18),1.4f)*.0022f)*DetailBoost;
    float length=(type==0?.0045f+Hash(n+18)*.0035f:type==1?.0006f+Hash(n+18)*.0010f:starSize)*DetailBoost;
    float width=(type==0?.00028f:type==1?.00020f+Hash(n+60)*.00035f:starSize)*DetailBoost;
    float gain=(type==0?2.0f:type==1?.9f+Hash(n+72)*.9f:1.5f+Hash(n+72)*1.1f)*DetailBoost;
    Color tint=type==2?Color.Lerp(new Color(.32f,.70f,1),new Color(.65f,.40f,1),Hash(n+43))
     :Color.Lerp(new Color(.15f,.65f,1),new Color(.65f,.30f,1),Hash(n+43));
    tint.a=a*gain;
    for(int k=0;k<4;k++)
    {
     Vector2 p=uv[first+k];
     vertices[first+k]=transform.InverseTransformPoint(center+along*(p.x*length)+up*(p.y*width));
     kind[first+k]=new Vector2(type,Hash(n+99));colors[first+k]=tint;
    }
   }
   mesh.vertices=vertices;mesh.colors=colors;mesh.uv2=kind;mesh.RecalculateBounds();draw.enabled=ActiveCount>0;
   properties.Clear();properties.SetFloat("_HueShift",owner.hueShift);properties.SetVector("_BaseColor",owner.baseColor);draw.SetPropertyBlock(properties);
  }
  void OnDestroy()
  {
   if(Application.isPlaying){Destroy(mesh);Destroy(material);}
   else{DestroyImmediate(mesh);DestroyImmediate(material);}
  }
 }
}
