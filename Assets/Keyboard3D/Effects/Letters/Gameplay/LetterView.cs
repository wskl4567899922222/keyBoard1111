using UnityEngine;

namespace KeyboardGame.Letters
{
    /// <summary>One reusable display, with immutable glyph geometry and its own solid-surface endpoint.</summary>
    [DisallowMultipleComponent]
    public sealed class LetterView:MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] Transform landingPoint;
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] LetterAppearance appearance;
        LetterCatalog.Entry entry;
        bool entrancePlaying;
        bool fadeEntrance=true;
        public bool IsEntrancePlaying=>entrancePlaying;
        public int FrontSlot=>entry!=null?entry.frontSlot:0;
        public int BindingVersion{get;private set;}
        public LetterActivation Activation=>GetComponent<LetterActivation>();
        float entranceElapsed,entranceDuration,entranceStart,entrancePeak;
        float entranceOpacity=1,wordOpacity=1;
        public Transform Model=>model;
        public Transform LandingPoint=>landingPoint;
        public LetterAppearance Appearance=>appearance;
        public char Character=>entry!=null?entry.Character:'\0';
        public Mesh Mesh=>meshFilter.sharedMesh;
        public Color MainColor=>appearance.MainColor;
        public Bounds LocalBounds
        {
            get
            {
                var b=meshFilter.sharedMesh.bounds;var matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,180,0),Vector3.one);
                var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
                for(int i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                return result;
            }
        }
        public void Initialize(Transform modelNode,Transform endpoint,MeshFilter filter,MeshRenderer renderer,LetterAppearance colorControl)
        {model=modelNode;landingPoint=endpoint;meshFilter=filter;meshRenderer=renderer;appearance=colorControl;}
        public void Bind(LetterCatalog.Entry glyph,Material body,Material bevel,Color color)
        {
            BindingVersion++;Activation?.ResetPresentation();meshRenderer.enabled=true;fadeEntrance=true;
            entry=glyph;meshFilter.sharedMesh=glyph.mesh;
            var materials=new Material[glyph.mesh.subMeshCount];
            for(int i=0;i<materials.Length;i++)materials[i]=i==glyph.bevelSlot?bevel:body;
            meshRenderer.sharedMaterials=materials;
            model.localPosition=Vector3.zero;model.localRotation=Quaternion.Euler(0,180,0);model.localScale=Vector3.one;
            landingPoint.localPosition=model.localRotation*glyph.landingOffset;
            appearance.SetFrontMaterialSlot(glyph.frontSlot);
            entrancePlaying=false;entranceOpacity=1;wordOpacity=1;
            appearance.SetMainColor(color);appearance.SetOpacity(1);
        }
        public void BeginEntrance(float startScale,float overshoot,float duration)
        {
            entranceElapsed=0;entranceDuration=Mathf.Max(0.01f,duration);
            entranceStart=Mathf.Clamp(startScale,0.1f,1);entrancePeak=Mathf.Clamp(overshoot,1,1.2f);
            fadeEntrance=true;entrancePlaying=true;ApplyEntrance(0);
        }
        public void BeginOpaqueEntrance(float startScale,float overshoot,float duration)
        {
            entranceElapsed=0;entranceDuration=Mathf.Max(0.01f,duration);
            entranceStart=Mathf.Clamp(startScale,0.1f,1);entrancePeak=Mathf.Clamp(overshoot,1,1.2f);
            fadeEntrance=false;entrancePlaying=true;ApplyEntrance(0);
        }
        public void SetDisplayScale(float scale)
        {
            var center=model.localRotation*meshFilter.sharedMesh.bounds.center;
            model.localScale=Vector3.one*scale;model.localPosition=center-center*scale;
        }
        public void AdvanceEntrance(float deltaTime)
        {
            if(!entrancePlaying||deltaTime<=0)return;
            entranceElapsed=Mathf.Min(entranceElapsed+deltaTime,entranceDuration);
            ApplyEntrance(entranceElapsed/entranceDuration);
            if(entranceElapsed>=entranceDuration)entrancePlaying=false;
        }
        void ApplyEntrance(float progress)
        {
            float scale=LetterPopAnimation.Scale(progress,entranceStart,entrancePeak);
            // Model alone pops around the glyph center; sibling LandingPoint and layout stay fixed.
            var center=model.localRotation*meshFilter.sharedMesh.bounds.center;
            model.localScale=Vector3.one*scale;model.localPosition=center-center*scale;
            entranceOpacity=fadeEntrance?LetterPopAnimation.Opacity(progress):1;
            float alpha=wordOpacity*entranceOpacity;
            if(alpha!=appearance.OpacityValue)appearance.SetOpacity(alpha);
            else appearance.Refresh(); // Keep local gradient coordinates aligned while size changes.
        }
        public void SetOpacity(float alpha)
        {
            wordOpacity=Mathf.Clamp01(alpha);Activation?.SetWordOpacity(wordOpacity);float combined=wordOpacity*entranceOpacity;
            if(combined!=appearance.OpacityValue)appearance.SetOpacity(combined);
        }
    }
}
