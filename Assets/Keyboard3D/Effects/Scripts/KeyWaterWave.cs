using UnityEngine;
using UnityEngine.Rendering;
namespace KeyboardGame.TrailFlightStudy
{
 public sealed class KeyWaterWave:MonoBehaviour
 {
  const int Sectors=64,Rows=18;
  readonly Mesh[] meshes=new Mesh[3];readonly MeshRenderer[] renderers=new MeshRenderer[3];
  readonly Vector3[] vertices=new Vector3[(Sectors+1)*(Rows+1)];
  readonly Vector2[] uv=new Vector2[(Sectors+1)*(Rows+1)];
  Material material;MaterialPropertyBlock block;Transform key;
  KeyWaterSplash splash;
  Vector3 localCenter,fallbackCenter,up,right,forward;float radius;
  Vector3 baseColor; // 字母基准色覆盖（rgb）
  public float Radius=>radius;
  public float MinimumHeight{get;private set;}
  public float CentralHeight{get;private set;}
  public bool IsVisible{get;private set;}
  public float Age{get;private set;}
  public KeyWaterSplash Splash=>splash;
  // 本项目适配：参考项目用 KeyboardInputMapping 找最近键帽；这里由宿主（WordTrailTyping）注入解析器
  public static System.Func<Vector3,Transform> NearestKeyResolver;
  void Ensure()
  {
   if(material!=null)return;
   material=new Material(Shader.Find("Keyboard/Preview/KeyWaterWave"));block=new MaterialPropertyBlock();
   var triangles=new int[Rows*Sectors*6];int k=0;
   for(int row=0;row<Rows;row++)for(int a=0;a<Sectors;a++)
   {int n=row*(Sectors+1)+a;triangles[k++]=n;triangles[k++]=n+1;triangles[k++]=n+Sectors+1;triangles[k++]=n+1;triangles[k++]=n+Sectors+2;triangles[k++]=n+Sectors+1;}
   for(int layer=0;layer<3;layer++)
   {
    var go=new GameObject("Water sheet "+layer);go.transform.SetParent(transform,false);go.layer=gameObject.layer;
    var mesh=new Mesh{name="Reusable key water sheet "+layer};mesh.MarkDynamic();mesh.vertices=vertices;mesh.triangles=triangles;
    meshes[layer]=mesh;go.AddComponent<MeshFilter>().sharedMesh=mesh;
    var draw=go.AddComponent<MeshRenderer>();draw.sharedMaterial=material;draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=false;renderers[layer]=draw;
   }
   splash=gameObject.AddComponent<KeyWaterSplash>();splash.Initialize(material);
  }
  public void Launch(Vector3 origin,Vector3 axis,float hue,Color color)
  {
   Ensure();key=null;up=axis.normalized;right=Vector3.Cross(up,Vector3.forward).normalized;
   if(right.sqrMagnitude<.1f)right=Vector3.right;forward=Vector3.Cross(right,up).normalized;
   baseColor=new Vector3(color.r,color.g,color.b);
   if(NearestKeyResolver!=null){var resolvedKey=NearestKeyResolver(origin);if(resolvedKey!=null)key=resolvedKey;}
   fallbackCenter=origin;radius=.3f;
   if(key!=null)
   {
    var filter=key.GetComponentInChildren<MeshFilter>();
    if(filter!=null&&filter.sharedMesh!=null)
    {
     var bounds=filter.sharedMesh.bounds;float top=float.NegativeInfinity,left=top,front=top,minX=float.PositiveInfinity,minZ=minX;
     for(int n=0;n<8;n++)
     {
      Vector3 p=filter.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1)));
      top=Mathf.Max(top,Vector3.Dot(p,up));left=Mathf.Max(left,Vector3.Dot(p,right));front=Mathf.Max(front,Vector3.Dot(p,forward));minX=Mathf.Min(minX,Vector3.Dot(p,right));minZ=Mathf.Min(minZ,Vector3.Dot(p,forward));
     }
     radius=Mathf.Min(left-minX,front-minZ)*.75f;
     fallbackCenter=filter.transform.TransformPoint(bounds.center);
     fallbackCenter+=up*(top-Vector3.Dot(fallbackCenter,up));
    }
    localCenter=key.InverseTransformPoint(fallbackCenter);
   }
   block.SetFloat("_HueShift",hue);block.SetVector("_BaseColor",baseColor);Sample(0,origin);
  }
  public void Sample(float age,Vector3 head)
  {
   Age=age;IsVisible=age<.34f;
   if(!IsVisible){foreach(var draw in renderers)if(draw!=null)draw.enabled=false;splash.Clear();return;}
   Vector3 center=key!=null?key.TransformPoint(localCenter):fallbackCenter;
   float attack=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.012f,age));
   float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.34f,age));
   float press=Mathf.Exp(-Mathf.Pow((age-.016f)/.014f,2));
   float rebound=Mathf.Exp(-Mathf.Pow((age-.065f)/.038f,2))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.023f,.045f,age));
   float tether=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.022f,.045f,age))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.060f,.085f,age)));
   Vector3 lift=head-center;
   if(Vector3.Dot(lift,up)<0)lift-=up*Vector3.Dot(lift,up);
   lift=Vector3.ClampMagnitude(lift,radius*1.5f);
   MinimumHeight=float.PositiveInfinity;
   for(int layer=0;layer<3;layer++)
   {
    renderers[layer].enabled=true;
    float inner=layer==0?0:layer==1?.34f:.63f,outer=layer==0?1:layer==1?.78f:1;
    for(int row=0;row<=Rows;row++)for(int a=0;a<=Sectors;a++)
    {
     float t=row/(float)Rows,r=Mathf.Lerp(inner,outer,t),angle=a/(float)Sectors*Mathf.PI*2;
     float organic=1+.025f*Mathf.Sin(angle*5+age*9)+.018f*Mathf.Sin(angle*9-age*12);
     float movingRidge=Mathf.Exp(-Mathf.Pow((r-(.28f+age*3.5f-layer*.12f))/.13f,2));
     float crown=Mathf.Pow(r,3)*(.7f+.3f*Mathf.Sin(angle*7+layer))*press;
     float height=.00075f-.00045f*press*Mathf.Exp(-r*r*18)+.0028f*crown;
     height+=.0011f*movingRidge*attack*fade+.00045f*layer*fade;
     float returningWave=Mathf.Exp(-Mathf.Pow((r-(.2f+Mathf.Max(0,age-.12f)*4))/.14f,2));
     height+=.0015f*returningWave*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.14f,age))*fade;
     float peak=Mathf.Pow(Mathf.Clamp01(1-r/.34f),2);
     height+=peak*.0050f*rebound*(1-tether);
     Vector3 p=center+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*(radius*r*organic)+up*height;
     if(layer==0)p+=lift*(peak*tether);
     MinimumHeight=Mathf.Min(MinimumHeight,Vector3.Dot(p-center,up));
     if(layer==0&&row==0&&a==0)CentralHeight=Vector3.Dot(p-center,up);
     int index=row*(Sectors+1)+a;vertices[index]=transform.InverseTransformPoint(p);uv[index]=new Vector2(r,angle);
    }
    meshes[layer].vertices=vertices;meshes[layer].uv=uv;meshes[layer].RecalculateNormals();meshes[layer].RecalculateBounds();
    block.SetFloat("_Age",age);block.SetFloat("_Layer",layer);block.SetFloat("_Opacity",attack*fade);renderers[layer].SetPropertyBlock(block);
   }
   splash.baseColor=baseColor;splash.Sample(age,center,up,right,forward,radius,block.GetFloat("_HueShift"));
  }
  void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
 }
}
