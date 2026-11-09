using UnityEngine;

namespace KeyboardGame
{
    /// <summary>Small camera-relative pitch/yaw, independent of glyph layout and material state.</summary>
    public sealed class WordMouseTilt
    {
        Vector2 angles,velocity;
        public Vector2 Angles=>angles;
        public Quaternion Initialize(Vector2 pointer,bool pointerActive,float yawLimit,float pitchLimit)
        {
            angles=Target(pointer,pointerActive,yawLimit,pitchLimit);velocity=Vector2.zero;
            return Quaternion.Euler(angles.x,angles.y,0);
        }
        static Vector2 Target(Vector2 pointer,bool pointerActive,float yawLimit,float pitchLimit)
        {
            return pointerActive?new Vector2(Mathf.Clamp(pointer.y,-1,1)*Mathf.Max(0,pitchLimit),-Mathf.Clamp(pointer.x,-1,1)*Mathf.Max(0,yawLimit)):Vector2.zero;
        }
        public Quaternion Step(Vector2 pointer,bool pointerActive,float deltaTime,float yawLimit,float pitchLimit,float smoothTime)
        {
            // Glyph Model is turned 180 degrees: its visible front is local -Z.
            // Positive pitch / negative yaw turn that front toward screen up / right.
            Vector2 target=Target(pointer,pointerActive,yawLimit,pitchLimit);
            if(deltaTime>0)angles=Vector2.SmoothDamp(angles,target,ref velocity,Mathf.Max(0.01f,smoothTime),Mathf.Infinity,deltaTime);
            return Quaternion.Euler(angles.x,angles.y,0);
        }
        public static void Apply(Transform root,Vector3 neutralPosition,Quaternion neutralRotation,Vector3 localPivot,Quaternion tilt)
        {
            // Rebuild from the neutral pose each frame, so pivot compensation never accumulates drift.
            root.SetPositionAndRotation(neutralPosition,neutralRotation);
            Vector3 center=root.TransformPoint(localPivot);
            root.rotation=neutralRotation*tilt;
            root.position+=center-root.TransformPoint(localPivot);
        }
    }
}
