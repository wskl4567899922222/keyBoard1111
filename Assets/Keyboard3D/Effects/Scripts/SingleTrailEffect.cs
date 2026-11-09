using System;
using UnityEngine;

namespace KeyboardGame
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SingleTrailEffect : MonoBehaviour
    {
        ParticleSystem particles;
        TrailFlightState flight;
        Transform target;
        Action<bool> resolved;
        Action<SingleTrailEffect> recycle;
        float remainingTail;
        bool notified;
        [SerializeField] bool enhancedVisual=true;
        /// <summary>运行时开关增强视觉（EnhancedTrailGame 圆圈光罩）。参考视频的彗星拖尾走的是基础粒子 trail 路径，设为 false。</summary>
        public bool EnhancedVisualEnabled { get => enhancedVisual; set => enhancedVisual = value; }
        TrailFlightStudy.EnhancedTrailGame enhanced;
        bool enhancedActive;
        public bool EnhancedVisualActive=>enhancedActive;
        public TrailFlightStudy.EnhancedTrailGame EnhancedVisual=>enhanced;
        public bool IsFlying => flight != null && !flight.IsFinished;
        public TrailFlightState Flight => flight;
        public Transform Destination => target;
        Camera flightCamera;
        float minX,maxX,minY,maxY;
        public void ConfigureBounds(Vector3 up,float height,Camera camera,float margin)
        {
            flightCamera=camera;
            if(camera!=null&&target!=null)
            {
                var a=camera.WorldToViewportPoint(flight.Position);var b=camera.WorldToViewportPoint(target.position);
                minX=Mathf.Min(margin,a.x,b.x);maxX=Mathf.Max(1-margin,a.x,b.x);
                minY=Mathf.Min(margin,a.y,b.y);maxY=Mathf.Max(1-margin,a.y,b.y);
            }
            flight.ConfigureBounds(up,height,camera!=null?(Func<Vector3,Vector3>)ConstrainToView:null);
            if(enhancedActive)enhanced.BindCamera(camera);
        }
        Vector3 ConstrainToView(Vector3 position)
        {
            if(flightCamera==null)return position;
            var p=flightCamera.WorldToViewportPoint(position);
            if(p.z<=flightCamera.nearClipPlane)return position;
            var b=target!=null?flightCamera.WorldToViewportPoint(target.position):p;
            p.x=Mathf.Clamp(p.x,Mathf.Min(minX,b.x),Mathf.Max(maxX,b.x));
            p.y=Mathf.Clamp(p.y,Mathf.Min(minY,b.y),Mathf.Max(maxY,b.y));
            return flightCamera.ViewportToWorldPoint(p);
        }

        public void Launch(Vector3 origin, Vector3 up, Transform target, Color color,
            float speed, float riseTime, float turnDegrees, float arrivalDistance,
            float maxFlightTime, float tailDuration, Action<bool> onResolved, Action<SingleTrailEffect> onRecycle,
            bool curveFlight=false, float wiggleAmplitude=0f, float wiggleFrequency=22f)
        {
            if(particles==null)particles=GetComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.Billboard;
            transform.position=origin;
            transform.rotation=Quaternion.identity;
            this.target=target;resolved=onResolved;recycle=onRecycle;notified=false;
            remainingTail=Mathf.Max(0.05f,tailDuration)+0.05f;
            Vector3 targetPoint=target!=null?target.position:origin+Vector3.up;
            flight=curveFlight
                ?new TrailFlightState(origin,targetPoint,up,speed,wiggleAmplitude,wiggleFrequency,maxFlightTime)
                :new TrailFlightState(origin,up,speed,riseTime,turnDegrees,arrivalDistance,maxFlightTime);
            var main=particles.main;
            main.startColor=color;
            float life=Mathf.Max(maxFlightTime+remainingTail+1,1);
            main.startLifetime=life;
            // Trail vertex lifetime is relative to the owning particle lifetime.
            var trails=particles.trails;trails.lifetime=Mathf.Clamp01(tailDuration/life);
            enhancedActive=false;
            if(enhancedVisual)
            {
                if(enhanced==null)
                {
                    var prefab=Resources.Load<GameObject>("EnhancedTrailGame");
                    if(prefab!=null)
                    {
                        enhanced=Instantiate(prefab,transform.parent).GetComponent<TrailFlightStudy.EnhancedTrailGame>();
                        // 必须在任何世界坐标写入（tail 摆位/路径记录）之前继承飞行器缩放：
                        // 本工程键盘是参考工程的 25 倍尺度，若先摆位后缩放，
                        // 父级放大会把 ribbon 顶点/位置一起乘 25，整条拖尾飞出屏幕
                        enhanced.transform.localScale=transform.localScale;
                    }
                }
                if(enhanced!=null)
                {
                    enhanced.gameObject.SetActive(true);
                    foreach(var child in enhanced.GetComponentsInChildren<Transform>(true))child.gameObject.layer=gameObject.layer;
                    enhanced.Launch(origin,up,target,color,speed,tailDuration,Camera.main);enhancedActive=true;
                    // 参考工程行为：彗星外观完全由 EnhancedTrailGame（ribbon 分层拖尾+光头）承担，
                    // 基础粒子渲染器关闭；仅当 EnhancedTrailGame 资源缺失时才走粒子拖尾兜底
                    GetComponent<ParticleSystemRenderer>().enabled=false;
                    return;
                }
            }
            // 层级：彗星永远盖在单词文本上方。两个手段缺一不可：
            // 1) sortingOrder=20 + renderQueue 4000（TMP 文本是 3000，队列低永远垫底）
            // 2) ZTest Always——单词 3D 字母离相机更近时会深度遮挡彗星，仅调顺序救不了
            orderApplied=false;ApplyHighlightOrder();
            GetComponent<ParticleSystemRenderer>().enabled=true;
            particles.Play(true);
            particles.Emit(new ParticleSystem.EmitParams {position=Vector3.zero,velocity=Vector3.zero,startColor=color,startLifetime=life},1);
        }

        bool orderApplied; // 碎点/星星渲染器是首帧懒创建的，第二帧需补挂层级
        void ApplyHighlightOrder()
        {
            var particleRenderer=GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingOrder=20;
            var particleMat=particleRenderer.material;
            if(particleMat!=null)ForceOnTop(particleMat);
            if(enhanced==null)return;
            foreach(var r in enhanced.GetComponentsInChildren<Renderer>(true))
            {
                r.sortingOrder=20;
                var mats=r.materials; // 首次访问生成每渲染器实例，之后复用，不产生累积
                bool changed=false;
                for(int m=0;m<mats.Length;m++)
                    if(mats[m]!=null&&ForceOnTop(mats[m]))changed=true;
                if(changed)r.sharedMaterials=mats;
            }
        }
        static bool ForceOnTop(Material mat)
        {
            bool changed=false;
            if(mat.renderQueue<4000){mat.renderQueue=4000;changed=true;}
            if(mat.HasProperty("_ZTest")){mat.SetInt("_ZTest",8);changed=true;}     // 8=Always
            if(mat.HasProperty("_ZWrite")&&mat.GetFloat("_ZWrite")!=0f){mat.SetFloat("_ZWrite",0f);changed=true;}
            return changed;
        }

        void Update() { Advance(Time.deltaTime); }
        public void Advance(float deltaTime)
        {
            if(flight==null || deltaTime<=0) return;
            if(enhancedActive){deltaTime=enhanced.ConsumeLaunch(deltaTime);if(deltaTime<=0)return;}
            if(enhancedActive&&!orderApplied){orderApplied=true;ApplyHighlightOrder();} // 第二帧补挂懒创建的碎点渲染器层级
            if(!flight.IsFinished)
            {
                flight.Advance(deltaTime,target!=null ? (Vector3?)target.position : null);
                // Local stationary particle follows this root; world-space trail vertices stay behind.
                transform.position=flight.Position;
            }
            if(enhancedActive)enhanced.Advance(flight,deltaTime);
            if(!flight.IsFinished)return;
            if(!notified)
            {
                notified=true;
                var callback=resolved;resolved=null;
                callback?.Invoke(flight.Arrived);
                GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.None;
                // No movement means no new trail segments. Existing vertices fade naturally.
            }
            remainingTail-=deltaTime;
            if(remainingTail>0)return;
            var returnToPool=recycle;
            recycle=null;flight=null;target=null;resolved=null;
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(enhanced!=null)enhanced.gameObject.SetActive(false);
            enhancedActive=false;
            returnToPool?.Invoke(this);
        }
        public void ResetForPool()
        {
            flight=null;target=null;resolved=null;recycle=null;
            if(particles==null)particles=GetComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(enhanced!=null)enhanced.gameObject.SetActive(false);
            enhancedActive=false;
        }
        void OnDestroy(){if(enhanced!=null)Destroy(enhanced.gameObject);}
    }
}
