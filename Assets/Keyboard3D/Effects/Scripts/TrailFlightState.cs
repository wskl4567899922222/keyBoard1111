using UnityEngine;

namespace KeyboardGame
{
    /// <summary>Independent motion model; the trail never relies on a pre-baked arc.</summary>
    public sealed class TrailFlightState
    {
        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }
        public bool Arrived { get; private set; }
        public bool IsFinished { get; private set; }
        public float Age { get; private set; }
        readonly float speed, riseDuration, turnRadians, arrivalDistance, maxTime;
        Vector3 boundsUp = Vector3.up;
        float startHeight, maxRiseHeight = float.PositiveInfinity;
        System.Func<Vector3,Vector3> constrainPosition;
        public void ConfigureBounds(Vector3 up,float maxHeight,System.Func<Vector3,Vector3> screenConstraint)
        {
            boundsUp=up.normalized;startHeight=Vector3.Dot(Position,boundsUp);
            maxRiseHeight=Mathf.Max(0,maxHeight);constrainPosition=screenConstraint;
        }

        public TrailFlightState(Vector3 position, Vector3 initialDirection, float speed,
            float riseDuration, float turnDegrees, float arrivalDistance, float maxTime)
        {
            Position = position;
            Direction = initialDirection.sqrMagnitude > 0 ? initialDirection.normalized : Vector3.up;
            this.speed = Mathf.Max(0.001f, speed);
            this.riseDuration = Mathf.Max(0, riseDuration);
            turnRadians = Mathf.Max(1, turnDegrees) * Mathf.Deg2Rad;
            this.arrivalDistance = Mathf.Max(0.0001f, arrivalDistance);
            this.maxTime = Mathf.Max(this.riseDuration + 0.1f, maxTime);
        }

        // —— 参考视频轨迹模式：竖直起飞 → 大弧度贝塞尔 → 下方切入目标，起步带衰减横向摆动 ——
        readonly bool curveMode;
        readonly Vector3 cUp;
        readonly float baseDist,wiggleAmp,wiggleFreq;
        Vector3 c0,c1,c2,c3,sideAxis;
        float curveDuration,curveLength;
        float[] curveLut;

        public TrailFlightState(Vector3 position, Vector3 targetPosition, Vector3 up, float speed,
            float wiggleAmplitude, float wiggleFrequency, float maxTime)
        {
            curveMode=true;
            Position=position;c0=position;c3=targetPosition;
            cUp=up.sqrMagnitude>0?up.normalized:Vector3.up;
            baseDist=Mathf.Max(0.001f,Vector3.Distance(c0,c3));
            sideAxis=Vector3.Cross(cUp,c3-c0);
            sideAxis=sideAxis.sqrMagnitude>.0001f?sideAxis.normalized:Vector3.right;
            this.speed=Mathf.Max(0.001f,speed);
            riseDuration=0;turnRadians=1;arrivalDistance=0.0001f;
            this.maxTime=Mathf.Max(0.1f,maxTime);
            wiggleAmp=wiggleAmplitude;wiggleFreq=Mathf.Max(1,wiggleFrequency);
            RebuildCurve(true);
            Direction=(CurvePoint(0.01f)-c0).normalized;
            curveDuration=curveLength/speed;
        }

        void RebuildCurve(bool buildLut)
        {
            // P1 竖直上升段（0.3×距离），P2 位于目标正下方（0.18×距离）→ 到达时从下方上勾切入
            c1=c0+cUp*(baseDist*0.50f);
            c2=c3-cUp*(baseDist*0.18f);
            if(!buildLut)return;
            curveLut=new float[33];
            Vector3 prev=c0;curveLength=0;
            for(int i=1;i<=32;i++)
            {
                Vector3 pt=CurvePoint(i/32f);
                curveLength+=Vector3.Distance(prev,pt);
                curveLut[i]=curveLength;prev=pt;
            }
        }

        Vector3 CurvePoint(float u)
        {
            float v=1-u;
            return v*v*v*c0+3*v*v*u*c1+3*v*u*u*c2+u*u*u*c3;
        }

        public void Advance(float deltaTime, Vector3? target)
        {
            if (IsFinished || deltaTime <= 0) return;
            if (curveMode)
            {
                if (!target.HasValue){IsFinished=true;return;}
                if(c3!=(Vector3)target){c3=target.Value;RebuildCurve(false);}
                Age+=deltaTime;
                float u=Mathf.Clamp01(Age/curveDuration);
                Vector3 next=CurvePoint(u);
                float decay=(1f-u)*(1f-u); // 摆动只在起飞段明显，末端归零
                next+=sideAxis*(Mathf.Sin(Age*wiggleFreq)*wiggleAmp*decay);
                if(constrainPosition!=null)next=constrainPosition(next);
                Vector3 segment=next-Position;
                if(segment.sqrMagnitude>.000000001f)Direction=segment.normalized;
                Position=next;
                if(Age>=curveDuration||Age>=maxTime)Land(target.Value);
                return;
            }
            if (!target.HasValue) { IsFinished = true; return; }
            float remaining = Mathf.Min(deltaTime, maxTime - Age + 0.01f);
            while (remaining > 0 && !IsFinished)
            {
                float step = Mathf.Min(remaining, 1f / 120f);
                remaining -= step;
                Age += step;
                if (Age >= maxTime) { IsFinished = true; break; }
                Vector3 toTarget = target.Value - Position;
                bool seeking = Age > riseDuration;
                if (seeking && toTarget.magnitude <= arrivalDistance) { Land(target.Value); break; }
                float currentSpeed = speed;
                if (seeking)
                {
                    Direction = Vector3.RotateTowards(Direction, toTarget.normalized, turnRadians * step, 0).normalized;
                    // Slow within the turning radius, so a nearby target cannot cause endless circling.
                    float slowDistance = Mathf.Max(arrivalDistance * 5, speed / turnRadians * 2);
                    currentSpeed *= Mathf.Clamp(toTarget.magnitude / slowDistance, 0.12f, 1);
                }
                Vector3 next = Position + Direction * (currentSpeed * step);
                float ceiling=Mathf.Max(startHeight,Vector3.Dot(target.Value,boundsUp))+maxRiseHeight;
                float excess=Vector3.Dot(next,boundsUp)-ceiling;
                if(excess>0)
                {
                    next-=boundsUp*excess;
                    Vector3 toward=target.Value-next;
                    Direction=toward.sqrMagnitude>0?toward.normalized:Direction;
                }
                if(constrainPosition!=null)next=constrainPosition(next);
                if (seeking)
                {
                    Vector3 segment = next - Position;
                    float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(toTarget, segment) / segment.sqrMagnitude) : 0;
                    if (Vector3.Distance(Position + segment * fraction, target.Value) <= arrivalDistance)
                    { Land(target.Value); break; }
                }
                Position = next;
            }
        }
        void Land(Vector3 target) { Position = target; Arrived = true; IsFinished = true; }
    }
}
