using UnityEngine;
using UnityEngine.Rendering;

namespace KeyboardGame.Letters
{
    /// <summary>Exclusive waiting/colored presentation. Owns no input, flight or arrival state.</summary>
    [DisallowMultipleComponent]
    public sealed class LetterActivation:MonoBehaviour
    {
        LetterView view;
        MeshRenderer waiting,colored;
        MaterialPropertyBlock block;
        bool contracting;
        float elapsed,duration,target;
        float wordOpacity=1;
        Color feedbackColor;
        public bool IsWaiting=>waiting!=null&&waiting.enabled;
        public bool IsContracting=>contracting;
        public MeshRenderer WaitingRenderer=>waiting;
        public void ShowWaiting(LetterView owner,Color color)
        {
            view=owner;colored=view.Model.GetComponent<MeshRenderer>();
            if(waiting==null)
            {
                var go=new GameObject("__WaitingGlyph");go.transform.SetParent(view.Model,false);
                go.AddComponent<MeshFilter>();waiting=go.AddComponent<MeshRenderer>();
                waiting.shadowCastingMode=ShadowCastingMode.Off;waiting.receiveShadows=false;
                waiting.lightProbeUsage=LightProbeUsage.Off;waiting.reflectionProbeUsage=ReflectionProbeUsage.Off;
                block=new MaterialPropertyBlock();
            }
            var sdf=Resources.Load<WaitingSdfCatalog>("WaitingSdfCatalog");
            if(sdf==null||!sdf.TryGet(view.Character,out var glyph))
                throw new System.InvalidOperationException("Waiting SDF glyph missing: "+view.Character);
            waiting.GetComponent<MeshFilter>().sharedMesh=glyph.mesh;
            waiting.sharedMaterial=sdf.material;waiting.enabled=true;colored.enabled=false;
            contracting=false;elapsed=0;wordOpacity=1;view.SetDisplayScale(1);SetFeedback(color,Vector3.zero);
            view.Appearance.Refresh(); // Also disables any depth helper from the colored renderer.
        }
        public void SetFeedback(Color color,Vector3 localOffset)
        {
            if(!IsWaiting)return;
            feedbackColor=color;ApplyColor();
            waiting.transform.localPosition=localOffset;
        }
        public void SetWordOpacity(float alpha){wordOpacity=Mathf.Clamp01(alpha);if(IsWaiting)ApplyColor();}
        void ApplyColor()
        {
            Color color=feedbackColor;color.a*=wordOpacity;
            block.Clear();block.SetColor("_Color",color);waiting.SetPropertyBlock(block);
        }
        public void BeginContraction(float startScale,float seconds)
        {
            if(!IsWaiting)return;
            target=Mathf.Clamp(startScale,0.1f,1);duration=Mathf.Max(0.001f,seconds);elapsed=0;contracting=true;
            waiting.transform.localPosition=Vector3.zero;
        }
        public void AdvanceContraction(float deltaTime)
        {
            if(!contracting)return;
            elapsed=Mathf.Min(duration,elapsed+Mathf.Max(0,deltaTime));
            view.SetDisplayScale(Mathf.Lerp(1,target,Mathf.SmoothStep(0,1,elapsed/duration)));
            if(elapsed>=duration)contracting=false;
        }
        public void Reveal()
        {
            contracting=false;wordOpacity=1;
            if(waiting!=null){waiting.enabled=false;waiting.transform.localPosition=Vector3.zero;}
            if(colored!=null)colored.enabled=true;
            if(view!=null)view.Appearance.Refresh();
        }
        public void ResetPresentation()
        {
            contracting=false;
            if(waiting!=null){waiting.enabled=false;waiting.transform.localPosition=Vector3.zero;}
            if(colored!=null)colored.enabled=true;
        }
    }
}
