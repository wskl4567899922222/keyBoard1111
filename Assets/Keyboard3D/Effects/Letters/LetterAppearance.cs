using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace KeyboardGame.Letters
{
    /// <summary>Per-letter task color and fade, without material cloning or shared-material edits.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class LetterAppearance:MonoBehaviour
    {
        [SerializeField] bool overrideMainColor;
        [SerializeField,ColorUsage(false,false)] Color mainColor=new Color(0.08f,0.85f,1f);
        [SerializeField,Range(0,1)] float opacity=1;
        [SerializeField] int frontMaterialSlot=2;
        const string DepthNode="__LetterFadeDepth";
        readonly Dictionary<Renderer,MeshRenderer> depthRenderers=new Dictionary<Renderer,MeshRenderer>();
        static Material depthMaterial;
        Renderer[] targets;
        MaterialPropertyBlock block;
        static readonly int Top=Shader.PropertyToID("_TopColor"),Opacity=Shader.PropertyToID("_Opacity");
        static readonly int MinY=Shader.PropertyToID("_Baseline"),MaxY=Shader.PropertyToID("_TopY"),GradientY=Shader.PropertyToID("_GradientY");
        public Color MainColor=>mainColor;
        public float OpacityValue=>opacity;
        public Vector2 GradientRange{get;private set;}
        void OnEnable(){Refresh();}
        void OnValidate(){Refresh();}
        void OnDisable()
        {
            foreach(var pair in depthRenderers)if(pair.Value!=null)
            {
                pair.Value.enabled=false;
                if(Application.isPlaying)Destroy(pair.Value.gameObject);else DestroyImmediate(pair.Value.gameObject);
            }
            depthRenderers.Clear();
        }
        void RefreshDepth(Renderer source)
        {
            var filter=source.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null)return;
            if(depthMaterial==null)depthMaterial=Resources.Load<Material>("LetterFadeDepth");
            if(depthMaterial==null)return;
            if(!depthRenderers.TryGetValue(source,out var renderer)||renderer==null)
            {
                var go=new GameObject(DepthNode){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};
                go.transform.SetParent(source.transform,false);go.AddComponent<MeshFilter>();
                renderer=go.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                depthRenderers[source]=renderer;
            }
            renderer.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            if(renderer.sharedMaterials.Length!=filter.sharedMesh.subMeshCount)
            {
                var materials=new Material[filter.sharedMesh.subMeshCount];
                for(int i=0;i<materials.Length;i++)materials[i]=depthMaterial;
                renderer.sharedMaterials=materials;
            }
            renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder;
            renderer.enabled=source.enabled&&opacity>0;
            renderer.SetPropertyBlock(block);
        }
        public void SetFrontMaterialSlot(int slot){frontMaterialSlot=slot;Refresh();}
        public void SetMainColor(Color color){mainColor=color;overrideMainColor=true;Refresh();}
        public void UseMaterialColors(){overrideMainColor=false;Refresh();}
        public void SetOpacity(float value){opacity=Mathf.Clamp01(value);Refresh();}
        public void Refresh()
        {
            // The component owns the letter renderer's property block; material tuning stays live.
            if(targets==null||targets.Length==0)targets=System.Array.FindAll(GetComponentsInChildren<Renderer>(true),r=>r.gameObject.name!=DepthNode&&r.gameObject.name!="__WaitingGlyph");
            bool hasBounds=false;Bounds bounds=new Bounds();
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null||filter.gameObject.name=="__WaitingGlyph"||filter.gameObject.name==DepthNode)continue;
                var b=filter.sharedMesh.bounds;
                var matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int corner=0;corner<8;corner++)
                {
                    var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                    if(hasBounds)bounds.Encapsulate(p);else{bounds=new Bounds(p,Vector3.zero);hasBounds=true;}
                }
            }
            // Includes the complete glyph: descenders and disconnected dots share one range.
            GradientRange=hasBounds?new Vector2(bounds.min.y,bounds.max.y):new Vector2(0,1);
            if(block==null)block=new MaterialPropertyBlock();
            block.Clear();
            if(overrideMainColor)block.SetColor(Top,mainColor);
            block.SetFloat(Opacity,opacity);
            block.SetFloat(MinY,GradientRange.x);block.SetFloat(MaxY,GradientRange.y);
            foreach(var target in targets)if(target!=null)
            {
                var matrix=transform.worldToLocalMatrix*target.transform.localToWorldMatrix;
                block.SetVector(GradientY,matrix.GetRow(1));
                block.SetFloat("_Region",0);
                target.SetPropertyBlock(block);
                RefreshDepth(target);
                // Indexed blocks replace the renderer block, so copy all color/fade/height values.
                var materials=target.sharedMaterials;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    bool bevel=materials[slot]!=null&&materials[slot].HasProperty("_Region")&&Mathf.Approximately(materials[slot].GetFloat("_Region"),1);
                    block.SetFloat("_Region",bevel?1:(materials.Length==1||slot==frontMaterialSlot?0:2));
                    target.SetPropertyBlock(block,slot);
                }
            }
        }
    }
}
