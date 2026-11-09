using UnityEngine;
using UnityEngine.Rendering;
namespace KeyboardGame.TrailFlightStudy
{
 public sealed class EnergyFlightPreview:MonoBehaviour
 {
  public Renderer core,shield,outerShield,halo;
  public Transform head;
  public Camera previewCamera;
  public Vector3 gamePosition,origin,target;
  public Quaternion gameRotation;
  public Renderer[] tails;
  public float time,speed=1,flightDuration=.70f,tailLifetime=.25f;
  public float arcHeight=.035f;
  public float animationPhaseOffset;
  public float hueShift;
  /// <summary>基准色覆盖（rgb）。非零时所有彗星组件按字母颜色重着色（代替色相平移）。</summary>
  public Vector3 baseColor;
  public float launchVisibility=1;
  public bool playing=true,showControls=true;
  public int view;
  [System.NonSerialized] public System.Func<float,Vector3> externalPath;
  public const float Period=1.75f;
  public const float JoinInset=.00025f;
  public float VisibleTailLength{get;private set;}
  public float HeadVisibility{get;private set;}
  public int VisibleRibbonCount{get;private set;}
  public Vector3 TailJoin{get;private set;}
  public float MaxNeckHalfWidth{get;private set;}
  public float JoinOpacity{get;private set;}
  public float MainJoinHalfWidth{get;private set;}
  public float ArrivalGatherAt(float historyS,float seconds)
  {
   float start=flightDuration-.14f+.22f*Mathf.Clamp01(historyS);
   return Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+.14f,seconds));
  }
  const int Segments=64;
  Mesh[] meshes;
  Vector3[] vertices;
  Vector2[] uv;
  Color[] colors;
  MaterialPropertyBlock block;
  EnergyFlightDetails details;
  public int ActiveDetailCount=>details==null?0:details.ActiveCount;
  readonly float[] Widths={.0048f,.0019f,.0014f,.0009f,.0007f,.0065f};
  readonly float[] JoinWidths={.0022f,.0011f,.0009f,.00065f,.0005f,.0038f};
  readonly float[] Lengths={1,.78f,.90f,.63f,.97f,1};
  readonly float[] Offsets={0,.0029f,-.0027f,.0036f,-.0033f,0};
  readonly float[] Gains={1.02f,.58f,.54f,.40f,.36f,.045f};
  /// <summary>拖尾发光增益（全局）。参考视频的粗柔光尾 = 高增益输出冲过 Bloom 阈值泛光撑粗；
  /// 本工程 Bloom 环境不同，需提高增益才能得到同样的粗柔尾观感。</summary>
  public static float GainBoost=1f;
  /// <summary>拖尾宽度增益（全局）。参考工程 ribbon 宽度是绝对值（主层 0.0048），
  /// 观感偏细时可整体加宽；只影响拖尾飘带，不影响头部光晕。</summary>
  public static float WidthBoost=1f;
  /// <summary>头部光晕增益（全局）。参考工程靠 Bloom 2.0 撑出大而柔的头光；
  /// 本工程 Bloom 弱，用此增益补足。</summary>
  public static float HeadGlowBoost=1f;

  void EnsureMeshes()
  {
   if(meshes!=null)return;
   meshes=new Mesh[tails.Length];vertices=new Vector3[(Segments+1)*2];uv=new Vector2[vertices.Length];colors=new Color[vertices.Length];
   var triangles=new int[Segments*6];
   for(int i=0;i<=Segments;i++)
   {
    uv[i*2]=new Vector2(i/(float)Segments,0);uv[i*2+1]=new Vector2(i/(float)Segments,1);
    if(i==Segments)continue;int k=i*6,a=i*2;
    triangles[k]=a;triangles[k+1]=a+2;triangles[k+2]=a+1;
    triangles[k+3]=a+1;triangles[k+4]=a+2;triangles[k+5]=a+3;
   }
   for(int layer=0;layer<meshes.Length;layer++)
   {
    meshes[layer]=new Mesh{name="Runtime layered tail "+layer};meshes[layer].MarkDynamic();
    meshes[layer].vertices=vertices;meshes[layer].uv=uv;meshes[layer].triangles=triangles;
    tails[layer].GetComponent<MeshFilter>().sharedMesh=meshes[layer];
   }
   block=new MaterialPropertyBlock();
  }
  float Progress(float seconds)
  {
   // A short acceleration from A, then steady movement directly toward B.
   float t=Mathf.Clamp01(seconds/flightDuration);
   return t*t*(2-t);
  }
  public Vector3 FlightPoint(float seconds)
  {
   if(externalPath!=null)return externalPath(seconds);
   float u=Progress(seconds);
   return Vector3.Lerp(origin,target,u)+Vector3.up*(4*u*(1-u)*arcHeight);
  }
  public Vector3 FlightTangent(float seconds)
  {
   float t=Mathf.Clamp(seconds,0,flightDuration);
   return (FlightPoint(Mathf.Min(flightDuration,t+.002f))-FlightPoint(Mathf.Max(0,t-.002f))).normalized;
  }
  void ApplyHead(Renderer r,float gain)
  {block.Clear();block.SetFloat("_Clock",time);block.SetFloat("_Gain",gain*HeadGlowBoost*HeadVisibility*launchVisibility);block.SetFloat("_HueShift",hueShift);block.SetVector("_BaseColor",baseColor);r.SetPropertyBlock(block);r.enabled=HeadVisibility*launchVisibility>.001f;}
  public void Sample(float seconds)
  {
   EnsureMeshes();time=Mathf.Max(0,seconds);
   var direction=FlightTangent(time);
   head.position=FlightPoint(time);head.rotation=Quaternion.FromToRotation(Vector3.right,direction);
   HeadVisibility=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(flightDuration,flightDuration+.075f,time));
   ApplyHead(core,.85f);ApplyHead(shield,.70f);ApplyHead(outerShield,.10f);ApplyHead(halo,.55f);
   if(view!=0)FollowCamera();
   halo.transform.rotation=previewCamera.transform.rotation;
   float newest=Mathf.Min(time,flightDuration);
   float afterArrival=Mathf.Max(0,time-flightDuration);
   float oldest=Mathf.Max(0,newest-tailLifetime);
   float movingOldest=Mathf.Min(flightDuration,oldest+afterArrival);
   VisibleTailLength=movingOldest<newest?Vector3.Distance(FlightPoint(movingOldest),FlightPoint(newest)):0;
   TailJoin=FlightPoint(newest)-direction*JoinInset;
   MaxNeckHalfWidth=0;JoinOpacity=0;MainJoinHalfWidth=0;
   VisibleRibbonCount=0;
   for(int layer=0;layer<tails.Length;layer++)
   {
    float layerOldest=Mathf.Max(oldest,newest-tailLifetime*Lengths[layer]);
    bool visible=layerOldest<newest&&VisibleTailLength>.00002f;
    tails[layer].enabled=visible;if(!visible)continue;VisibleRibbonCount++;
    float emerging=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.035f,time));
    for(int i=0;i<=Segments;i++)
    {
     float s=i/(float)Segments,emission=Mathf.Lerp(newest,layerOldest,s);
     // Advect the fixed arrival history into B; do not redistribute its shape every frame.
     float birth=Mathf.Min(flightDuration,emission+afterArrival),age=newest-emission;
     float historyS=Mathf.Clamp01((newest-emission)/Mathf.Max(.00001f,newest-oldest));
     float arrivalGather=ArrivalGatherAt(historyS,time);
     float arrivalSpread=Mathf.Lerp(1,.16f,arrivalGather);
     float life=Mathf.Clamp01(1-age/tailLifetime);
     bool auxiliary=layer>=1&&layer<=4;
     float taperPower=layer==1?.60f:layer==2?.80f:layer==3?.85f:layer==4?.70f:.72f;
     // Leave some width while the terminal section fades, rather than drawing a needle.
     float terminalS=auxiliary?Mathf.Min(s,.90f):Mathf.Min(s,.94f);
     float taper=Mathf.Pow(1-terminalS,taperPower);
     Vector3 tangent=FlightTangent(birth);
     Vector3 across=Vector3.Cross(previewCamera.transform.forward,tangent).normalized;
     if(across.sqrMagnitude<.1f)across=previewCamera.transform.up;
     float behind=Vector3.Distance(FlightPoint(birth),FlightPoint(newest));
     float neck=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.012f,behind));
     float clock=time+animationPhaseOffset;
     float spatialRate=layer==1?155:layer==2?180:layer==3?210:layer==4?170:85;
     float temporalRate=layer==1?19:layer==2?23:layer==3?21.5f:layer==4?26:6;
     float phase=behind*spatialRate-clock*temporalRate+layer*1.7f+.22f*Mathf.Sin(behind*75-clock*3+layer);
     float amplitude=layer==1?.00065f:layer==2?.00055f:layer==3?.00045f:layer==4?.0004f:0;
     float drift=Mathf.Sin(phase)*amplitude;
     // Auxiliary bands fold toward, cross and rejoin the body instead of staying parallel.
     float wrapping=Offsets[layer]*Mathf.Sin(phase*.65f+layer);
     // A shared broad S wave bends the body and its highlights, while the narrower
     // bands open visible gaps, cross and return around that moving body.
     float bodyPhase=behind*100-clock*6+.20f*Mathf.Sin(behind*48-clock*2);
     float bodyS=auxiliary?historyS:s;
     float bodyOffset=.0011f*Mathf.Sin(bodyPhase)*Mathf.Sin(bodyS*Mathf.PI);
     float ribbonEnvelope=auxiliary?Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.22f,s))*(1-.65f*s):Mathf.Sin(s*Mathf.PI);
     // Lift only local portions of the secondary ribbons, preserving the body.
     // Their tips keep a small offset instead of collapsing to the body's last point.
     float loft=0;
     if(auxiliary)
     {
      float centerS=(layer==1?.43f:layer==2?.56f:layer==3?.35f:.65f)+.045f*Mathf.Sin(clock*3+layer);
      float lobe=Mathf.Exp(-Mathf.Pow((s-centerS)/(layer==1?.24f:layer==2?.27f:.23f),2));
      float side=layer==1||layer==3?1:-1;
      float loftSize=layer==1?.0027f:layer==2?.0022f:layer==3?.0020f:.0018f;
      loft=side*(lobe*loftSize+Mathf.Pow(s,3)*.0018f)*ribbonEnvelope;
     }
     float spread=1,localOpening=0;
     if(auxiliary)
     {
      // All layers share positions along the BODY, not their individual shorter lengths:
      // full middle, slight waist, then a small reopening of the trailing ends.
      float full=Mathf.Exp(-Mathf.Pow((bodyS-.45f)/.24f,2));
      float waist=Mathf.Exp(-Mathf.Pow((bodyS-.76f)/.075f,2));
      float reopening=Mathf.Exp(-Mathf.Pow((bodyS-.93f)/.10f,2));
      spread=1+.25f*full-.58f*waist+.34f*reopening;
      float side=layer==1||layer==3?1:-1;
      loft+=side*.00055f*reopening*ribbonEnvelope;
      // User-marked middle section only: move ribbons apart, without widening them.
      // A compact envelope leaves the head, existing waist and trailing tips unchanged.
      float opening=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.22f,bodyS))
       *Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.58f,bodyS));
      float separation=layer==1?.0032f:layer==2?.0027f:layer==3?.0022f:.0019f;
      localOpening=side*separation*opening;
     }
     // Short launch trails remain compact; restore the accepted spread as they grow.
     float launchSpread=Mathf.Lerp(.5f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.55f,time)));
     float offset=(bodyOffset+(((wrapping+drift)*ribbonEnvelope+loft)*spread+localOpening)*launchSpread*arrivalSpread)*emerging*neck;
     Vector3 depthAxis=Vector3.Cross(tangent,across).normalized;
     float depth=auxiliary?Mathf.Sin(phase*.79f+layer)*.0006f*neck*ribbonEnvelope*arrivalSpread:0;
     Vector3 center=FlightPoint(birth)-tangent*JoinInset+across*offset+depthAxis*depth;
     float width=Mathf.Lerp(JoinWidths[layer],Widths[layer],neck)*taper*emerging;
     width*=Mathf.Lerp(1,.96f+.20f*Mathf.Sin(phase+.8f)+.09f*Mathf.Sin(phase*.57f+layer),neck);
     if(auxiliary)width*=Mathf.Lerp(.87f,1.04f,Mathf.Clamp01((spread-.42f)/.83f));
     width*=WidthBoost;
     // 顶点是"世界坐标差值"，必须经渲染器 Transform 转回局部空间：
     // 本工程 EnhancedTrailGame 根被放大 N 倍（25 倍键盘尺度），
     // 直接写世界差值会把路径长度也放大 N 倍（拖尾甩出屏幕）；
     // 宽度保持局部值，随 lossyScale 放大后正好匹配键盘尺度（参考工程 N=1 两种写法等价）
     var tailTr=tails[layer].transform;
     Vector3 localCenter=tailTr.InverseTransformPoint(center);
     vertices[i*2]=localCenter-across*width;
     vertices[i*2+1]=localCenter+across*width;
     // Secondary edges remain readable almost to their own tip, then fade locally.
     // Different history lengths create staggered ends within the common envelope.
     float tipStart=auxiliary?(layer==1?.83f:layer==2?.85f:layer==3?.80f:.86f):.84f;
     float alpha=Mathf.Pow(life,auxiliary?.45f:.8f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,tipStart,s));
     // The front edge is transparent and tucked inside the core; no flat cut across it.
     alpha*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.0015f,behind));
     // Only the portion reaching B dissolves. Rear sections retain their own opacity.
     float captureFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.035f,flightDuration-birth));
     alpha*=captureFade;
     colors[i*2]=colors[i*2+1]=new Color(behind,1,1,alpha);
     if(i==0){MaxNeckHalfWidth=Mathf.Max(MaxNeckHalfWidth,width);JoinOpacity=Mathf.Max(JoinOpacity,alpha);if(layer==0)MainJoinHalfWidth=width;}
    }
    meshes[layer].vertices=vertices;meshes[layer].colors=colors;meshes[layer].RecalculateBounds();
    block.Clear();block.SetFloat("_Clock",time+animationPhaseOffset);block.SetFloat("_Layer",layer);block.SetFloat("_Gain",Gains[layer]*GainBoost);block.SetFloat("_HueShift",hueShift);block.SetVector("_BaseColor",baseColor);tails[layer].SetPropertyBlock(block);
   }
   if(details==null){details=GetComponent<EnergyFlightDetails>();if(details==null)details=gameObject.AddComponent<EnergyFlightDetails>();}
   details.Sample(this,time);
  }
  void FollowCamera()
  {
   Vector3 direction=FlightTangent(time);
   Vector3 center=head.position-direction*.024f;
   previewCamera.orthographic=true;previewCamera.orthographicSize=.045f;
   if(view==1)previewCamera.transform.SetPositionAndRotation(center-gameRotation*Vector3.forward*.15f,gameRotation);
   else
   {
    Vector3 forward=Vector3.Cross(direction,gameRotation*Vector3.up).normalized;
    if(Vector3.Dot(forward,gameRotation*Vector3.forward)<0)forward=-forward;
    previewCamera.transform.position=center-forward*.15f;previewCamera.transform.LookAt(center,gameRotation*Vector3.up);
   }
  }
  public void SetView(int index)
  {
   view=index;previewCamera.nearClipPlane=.001f;previewCamera.farClipPlane=100;
   if(view==0){previewCamera.orthographic=false;previewCamera.transform.SetPositionAndRotation(gamePosition,gameRotation);}
   Sample(time);
  }
  void Update(){if(playing)Sample(Mathf.Repeat(time+Time.unscaledDeltaTime*speed,Period));}
  void OnDestroy(){if(meshes!=null)foreach(var mesh in meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
  void OnGUI()
  {
   if(!showControls)return;
   if(view==0)
   {
    Vector3 b=previewCamera.WorldToScreenPoint(target);
    if(b.z>0){GUI.color=new Color(.5f,.65f,1,.7f);GUI.Label(new Rect(b.x-6,Screen.height-b.y-24,40,24),"B");GUI.color=Color.white;}
   }
   GUI.Box(new Rect(12,12,670,108),"");
   GUI.Label(new Rect(24,16,640,22),"STEP 2 - A to B / short tail grows into layered main tail / natural fade");
   if(GUI.Button(new Rect(24,44,100,27),"Game view"))SetView(0);
   if(GUI.Button(new Rect(132,44,100,27),"Close view"))SetView(1);
   if(GUI.Button(new Rect(240,44,90,27),"Replay")){Sample(0);playing=true;}
   if(GUI.Button(new Rect(338,44,90,27),playing?"Pause":"Play"))playing=!playing;
   if(GUI.Button(new Rect(436,44,100,27),speed<.5f?"0.35x":"1.0x"))speed=speed<.5f?1:.35f;
   float scrub=GUI.HorizontalSlider(new Rect(24,90,510,20),time,0,Period);
   if(Mathf.Abs(scrub-time)>.003f){playing=false;Sample(scrub);}
   GUI.Label(new Rect(550,82,115,24),time.ToString("F2")+"s");
  }
 }
}
