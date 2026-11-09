using UnityEngine;
namespace KeyboardGame.TrailFlightStudy
{
 // Samples the EXISTING gameplay motion. It never owns input, arrival callbacks or letters.
 public sealed class EnhancedTrailGame:MonoBehaviour
 {
  public EnergyFlightPreview visual;
  const int Capacity=1024;
  readonly float[] times=new float[Capacity];
  readonly Vector3[] points=new Vector3[Capacity];
  int count;bool landed;float postAge,flightSpeed;Transform destination;
  KeyWaterWave water;float launchAge;
  Vector3 coreSize,shieldSize,outerSize;bool birthSizesStored;
  public KeyWaterWave Water=>water;
  public EnergyFlightPreview Visual=>visual;
  public bool HasLanded=>landed;
  public Color LaunchColor{get;private set;}
  public void Launch(Vector3 origin,Vector3 up,Transform target,Color color,float speed,float lifetime,Camera camera)
  {
   count=1;times[0]=0;points[0]=origin;landed=false;postAge=0;destination=target;flightSpeed=speed;
   visual.playing=false;visual.showControls=false;visual.view=0;visual.enabled=false;
   // 彗星主体=ribbon 分层拖尾（参考工程同款）。之前误判"宽度塌缩"而禁用：
   // 实际是实例缩放未跟上本工程 25 倍键盘尺度（现由 KeyboardSingleTrail.LaunchLetter 统一缩放）
   visual.origin=origin;visual.target=target.position;visual.tailLifetime=lifetime;
   LaunchColor=color;Color.RGBToHSV(color,out var hue,out var saturation,out var value);
   visual.hueShift=Mathf.Repeat(hue-.56f+.5f,1)-.5f;
   // 字母基准色覆盖：拖尾/头光晕按字母颜色重着色（palette 多色渐变无法靠 hueShift 映射到任意色）
   visual.baseColor=new Vector3(color.r,color.g,color.b);
   launchAge=0;visual.launchVisibility=0;
   if(!birthSizesStored){coreSize=visual.core.transform.localScale;shieldSize=visual.shield.transform.localScale;outerSize=visual.outerShield.transform.localScale;birthSizesStored=true;}
   SetBirthSize(0);
   if(water==null)
   {
    var go=new GameObject("Key water launch");go.transform.SetParent(transform,false);go.layer=gameObject.layer;water=go.AddComponent<KeyWaterWave>();
   }
   water.Launch(origin,up,visual.hueShift,color);
   visual.externalPath=PointAt;BindCamera(camera);
   foreach(var r in visual.tails){r.transform.position=origin;r.transform.rotation=Quaternion.identity;}
   visual.flightDuration=Mathf.Max(.2f,Vector3.Distance(origin,target.position)/Mathf.Max(.001f,speed));
   visual.Sample(0);
  }
  public float ConsumeLaunch(float deltaTime)
  {
   float previous=launchAge;launchAge+=deltaTime;
   visual.launchVisibility=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.018f,.045f,launchAge));
   SetBirthSize(launchAge);
   if(previous<.045f)
   {
    visual.Sample(0);water.Sample(launchAge,visual.head.position);
    return Mathf.Max(0,launchAge-.045f);
   }
   return deltaTime;
  }
  void SetBirthSize(float age)
  {
   float formation=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.014f,.045f,age));
   float covering=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.052f,age));
   visual.core.transform.localScale=coreSize*Mathf.Lerp(.06f,1,formation);
   visual.shield.transform.localScale=shieldSize*Mathf.Lerp(.08f,1,covering);
   visual.outerShield.transform.localScale=outerSize*Mathf.Lerp(.08f,1,covering);
  }
  public void BindCamera(Camera camera)
  {
   if(camera==null)camera=Camera.main;
   visual.previewCamera=camera;
   if(camera!=null){visual.gamePosition=camera.transform.position;visual.gameRotation=camera.transform.rotation;}
  }
  Vector3 PointAt(float t)
  {
   if(count==0)return visual.origin;
   if(t<=times[0])return points[0];
   if(t>=times[count-1])return points[count-1];
   int low=0,high=count-1;
   while(high-low>1){int mid=(low+high)/2;if(times[mid]<=t)low=mid;else high=mid;}
   return Vector3.Lerp(points[low],points[high],Mathf.InverseLerp(times[low],times[high],t));
  }
  public void Advance(TrailFlightState flight,float deltaTime)
  {
   if(visual.previewCamera==null)BindCamera(Camera.main);
   if(visual.previewCamera==null)return;
   if(!landed)
   {
    if(count<Capacity&&flight.Age>times[count-1]+.000001f)
    {times[count]=flight.Age;points[count]=flight.Position;count++;}
    else points[count-1]=flight.Position;
    visual.target=destination!=null?destination.position:flight.Position;
    if(flight.IsFinished)
    {landed=true;visual.flightDuration=Mathf.Max(.001f,flight.Age);}
    else
    {
     float remaining=Vector3.Distance(flight.Position,visual.target)/Mathf.Max(.001f,flightSpeed);
     visual.flightDuration=flight.Age+Mathf.Clamp(remaining,.08f,.5f);
    }
   }
   else postAge+=deltaTime;
   visual.Sample(landed?visual.flightDuration+postAge:flight.Age);
   water.Sample(launchAge,visual.head.position);
  }
 }
}
