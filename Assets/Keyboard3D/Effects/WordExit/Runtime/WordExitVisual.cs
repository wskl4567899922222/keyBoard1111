using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeyboardGame
{
    /// <summary>All head/satellite quads in one dynamic mesh and one material. Separate from letter Appearance.</summary>
    public sealed class WordExitVisual : IDisposable
    {
        public const float OpaqueCoreFraction=.36f;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>(),detail=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        readonly MeshRenderer renderer;
        public int PointCount=>vertices.Count/4;
        public WordExitVisual(Transform parent,Material material)
        {
            root=new GameObject("WordExit_LightClusters");root.transform.SetParent(parent,false);
            // Vertices are in world coordinates, independent of letter transforms/pooling.
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            mesh=new Mesh{name="WordExit_CombinedPoints",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        public void Begin(float age,float disturbance,float brightness)
        {
            vertices.Clear();uv.Clear();detail.Clear();colors.Clear();triangles.Clear();
            block.SetFloat("_Phase",age);block.SetFloat("_Disturbance",disturbance);block.SetFloat("_Brightness",brightness);renderer.SetPropertyBlock(block);
        }
        public void Add(Vector3 center,Vector3 right,Vector3 up,float radius,Color color,float alpha,float seed)
        {
            if(radius<=0||alpha<=0)return;int n=vertices.Count;
            vertices.Add(center+(-right-up)*radius);vertices.Add(center+(right-up)*radius);
            vertices.Add(center+(right+up)*radius);vertices.Add(center+(-right+up)*radius);
            uv.Add(new Vector2(-1,-1));uv.Add(new Vector2(1,-1));uv.Add(new Vector2(1,1));uv.Add(new Vector2(-1,1));
            color.a=Mathf.Clamp01(alpha);for(int i=0;i<4;i++){colors.Add(color);detail.Add(new Vector2(seed,0));}
            triangles.Add(n);triangles.Add(n+2);triangles.Add(n+1);triangles.Add(n);triangles.Add(n+3);triangles.Add(n+2);
        }
        public void End()
        {
            mesh.Clear();renderer.enabled=vertices.Count>0;if(vertices.Count==0)return;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,detail);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            if(root!=null)root.SetActive(false);
            if(Application.isPlaying){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);}
            else{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
