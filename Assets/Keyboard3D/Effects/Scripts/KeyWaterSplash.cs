using UnityEngine;
using UnityEngine.Rendering;
namespace KeyboardGame.TrailFlightStudy
{
 // Five unequal liquid sheets and pooled low-poly droplets: two additional draws.
 public sealed class KeyWaterSplash:MonoBehaviour
 {
  const int Petals=5,Across=12,High=8,Drops=18,Lat=6,Lon=8;
  Mesh crown,droplets;MeshRenderer crownDraw,dropDraw;MaterialPropertyBlock block;
  Vector3[] cv=new Vector3[Petals*(Across+1)*(High+1)],dv=new Vector3[Drops*(Lat+1)*(Lon+1)],dn=new Vector3[Drops*(Lat+1)*(Lon+1)];
  Vector2[] cu=new Vector2[Petals*(Across+1)*(High+1)],du=new Vector2[Drops*(Lat+1)*(Lon+1)];
  Color[] dc=new Color[Drops*(Lat+1)*(Lon+1)];
  public int ActiveDrops{get;private set;}
  public float CrownHeight{get;private set;}
  public Vector3 baseColor; // 字母基准色覆盖（rgb），由 KeyWaterWave 注入
  static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*73.27f+11.2f)*43758.5453f,1);
  Mesh Create(string name,Material material,Vector3[] vertices,int[] triangles,out MeshRenderer draw)
  {
   var go=new GameObject(name);go.transform.SetParent(transform,false);go.layer=gameObject.layer;
   var mesh=new Mesh{name=name};mesh.MarkDynamic();mesh.vertices=vertices;mesh.triangles=triangles;
   go.AddComponent<MeshFilter>().sharedMesh=mesh;draw=go.AddComponent<MeshRenderer>();draw.sharedMaterial=material;
   draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=false;return mesh;
  }
  public void Initialize(Material material)
  {
   if(crown!=null)return;block=new MaterialPropertyBlock();
   int[] ct=new int[Petals*Across*High*6];int k=0;
   for(int p=0;p<Petals;p++)for(int y=0;y<High;y++)for(int x=0;x<Across;x++)
   {int a=p*(Across+1)*(High+1)+y*(Across+1)+x;ct[k++]=a;ct[k++]=a+1;ct[k++]=a+Across+1;ct[k++]=a+1;ct[k++]=a+Across+2;ct[k++]=a+Across+1;}
   crown=Create("Uneven splash crown",material,cv,ct,out crownDraw);
   int[] dt=new int[Drops*Lat*Lon*6];k=0;
   for(int d=0;d<Drops;d++)for(int y=0;y<Lat;y++)for(int x=0;x<Lon;x++)
   {int a=d*(Lat+1)*(Lon+1)+y*(Lon+1)+x;dt[k++]=a;dt[k++]=a+1;dt[k++]=a+Lon+1;dt[k++]=a+1;dt[k++]=a+Lon+2;dt[k++]=a+Lon+1;}
   droplets=Create("Detached water droplets",material,dv,dt,out dropDraw);
  }
  public void Sample(float age,Vector3 center,Vector3 up,Vector3 right,Vector3 forward,float radius,float hue)
  {
   float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.004f,.032f,age));
   float fall=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.070f,.175f,age));
   CrownHeight=0;
   for(int p=0;p<Petals;p++)for(int y=0;y<=High;y++)for(int x=0;x<=Across;x++)
   {
    float u=x/(float)Across,v=y/(float)High,a=p*1.256637f+.25f+(u-.5f)*(.68f+Hash(p+6)*.25f);
    float lobes=.72f+.28f*Mathf.Cos((u-.5f)*Mathf.PI*4+p);
    float silhouette=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.65f)*lobes;
    float h=(.004f+.005f*Hash(p+9))*rise*fall*silhouette;
    float elevation=.0007f+h*v;
    float r=radius*(.64f+.29f*v+.12f*v*v*rise);
    Vector3 point=center+(right*Mathf.Cos(a)+forward*Mathf.Sin(a))*r+up*elevation;
    int i=p*(Across+1)*(High+1)+y*(Across+1)+x;
    cv[i]=transform.InverseTransformPoint(point);cu[i]=new Vector2(u,v);
    CrownHeight=Mathf.Max(CrownHeight,elevation-.0007f);
   }
   crown.vertices=cv;crown.uv=cu;crown.RecalculateNormals();crown.RecalculateBounds();
   block.Clear();block.SetFloat("_HueShift",hue);block.SetVector("_BaseColor",baseColor);block.SetFloat("_Layer",3);block.SetFloat("_Age",age);block.SetFloat("_Opacity",rise*fall);crownDraw.SetPropertyBlock(block);crownDraw.enabled=age<.18f;
   ActiveDrops=0;
   for(int d=0;d<Drops;d++)
   {
    float t=age-(.027f+Hash(d+4)*.018f),a=d*2.39996f+.2f;
    float initialHeight=.003f+.0035f*Hash(d+15),vertical=.095f+.045f*Hash(d+8);
    float height=initialHeight+vertical*t-.8f*t*t;
    bool alive=t>0&&t<.22f&&height>.0007f;
    float opacity=alive?Mathf.SmoothStep(0,1,t/.009f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.20f,t))):0;
    if(opacity>.01f)ActiveDrops++;
    float r=radius*(.86f+Hash(d+11)*.12f)+Mathf.Max(0,t)*(.018f+Hash(d+24)*.02f);
    Vector3 location=center+(right*Mathf.Cos(a)+forward*Mathf.Sin(a))*r+up*Mathf.Max(.0007f,height);
    float size=.00025f+.0004f*Hash(d+20);
    for(int y=0;y<=Lat;y++)for(int x=0;x<=Lon;x++)
    {
     float theta=y/(float)Lat*Mathf.PI,phi=x/(float)Lon*Mathf.PI*2;
     Vector3 normal=right*(Mathf.Sin(theta)*Mathf.Cos(phi))+forward*(Mathf.Sin(theta)*Mathf.Sin(phi))+up*Mathf.Cos(theta);
     Vector3 point=location+normal*size+up*(Mathf.Cos(theta)*size*.45f);
     int i=d*(Lat+1)*(Lon+1)+y*(Lon+1)+x;
     dv[i]=transform.InverseTransformPoint(point);dn[i]=transform.InverseTransformDirection(normal);du[i]=new Vector2(x/(float)Lon,y/(float)Lat);dc[i]=new Color(1,1,1,opacity);
    }
   }
   droplets.vertices=dv;droplets.normals=dn;droplets.uv=du;droplets.colors=dc;droplets.RecalculateBounds();
   block.SetFloat("_Layer",4);block.SetFloat("_Opacity",1);dropDraw.SetPropertyBlock(block);dropDraw.enabled=ActiveDrops>0;
  }
  public void Clear(){if(crownDraw!=null)crownDraw.enabled=false;if(dropDraw!=null)dropDraw.enabled=false;ActiveDrops=0;CrownHeight=0;}
  void OnDestroy(){if(Application.isPlaying){Destroy(crown);Destroy(droplets);}else{DestroyImmediate(crown);DestroyImmediate(droplets);}}
 }
}
