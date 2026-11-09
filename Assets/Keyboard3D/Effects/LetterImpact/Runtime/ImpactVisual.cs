using UnityEngine;
using UnityEngine.Rendering;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    /// <summary>Replaceable impact renderer. Never calls into gameplay or changes a letter.</summary>
    public sealed class ImpactVisual
    {
        readonly Transform owner;
        readonly MeshRenderer[] pieces=new MeshRenderer[20];
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        static Mesh quad,starMesh,fragmentMesh;
        readonly Material legacyMaterial,burstMaterial;
        ImpactStyle style;
        Camera camera;
        LetterView glyph;
        int bindingVersion;
        Vector3 contact,initialCenter;
        Vector3 lastCenter,lastContact;
        Vector2 size;
        Color color;
        float age,switchTime,seed,lifetime,lastDepth;
        public float Lifetime=>lifetime;
        public Vector2 CoverSize=>size;
        public ImpactVisual(Transform parent)
        {
            owner=parent;
            if(quad==null)
            {
                quad=new Mesh{name="LetterImpact_Quad",hideFlags=HideFlags.HideAndDontSave};
                quad.vertices=new[]{new Vector3(-0.5f,-0.5f),new Vector3(0.5f,-0.5f),new Vector3(0.5f,0.5f),new Vector3(-0.5f,0.5f)};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateBounds();
            }
            legacyMaterial=Resources.Load<Material>("LetterImpactVisual");
            burstMaterial=Resources.Load<Material>("LetterImpactBurstVisual");
            EnsureShapes();
            for(int i=0;i<pieces.Length;i++)
            {
                var go=new GameObject(i==0?"ContactCore":i==1?"GlyphCover":"Spark_"+(i-2));go.transform.SetParent(owner,false);
                go.AddComponent<MeshFilter>().sharedMesh=quad;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=legacyMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                pieces[i]=renderer;renderer.enabled=false;
            }
        }
        public static Vector2 Measure(LetterView view,Camera cam)
        {return ProjectBounds(view,cam,false);}
        static Vector2 ProjectBounds(LetterView view,Camera cam,bool legacy)
        {
            if(view==null)return new Vector2(0.025f,0.025f);
            var b=view.LocalBounds;Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
            // Resting bounds + letter/root transform only: entrance scale must never inflate coverage.
            for(int i=0;i<8;i++)
            {
                Vector3 p=view.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(cam!=null)p=legacy?cam.transform.InverseTransformPoint(p):Quaternion.Inverse(cam.transform.rotation)*(p-cam.transform.position);
                var v=new Vector2(p.x,p.y);min=Vector2.Min(min,v);max=Vector2.Max(max,v);
            }
            return Vector2.Max(max-min,new Vector2(0.0001f,0.0001f));
        }
        public void Play(Vector3 point,Color tint,Camera cam,LetterView view,float handoff)
        {
            style=ImpactStyle.Resolve();
            if(style==null)throw new System.InvalidOperationException("LetterImpactStyle missing");
            glyph=view;bindingVersion=view!=null?view.BindingVersion:0;camera=cam;contact=point;color=tint;switchTime=handoff;age=0;seed=Random.value*6.28f;
            switchTime=Mathf.Max(0,handoff);
            lifetime=Mathf.Max(0.1f,Mathf.Max(style.lifetime,Mathf.Max(style.sparkLifetime,Mathf.Max(style.coreLifetime,switchTime+style.coverHoldAfterSwitch+style.coverRelease))));
            if(style.visualVersion==1)
            {
                lifetime=Mathf.Max(lifetime,switchTime+style.coverHoldAfterSwitch+Mathf.Max(style.breakupDuration,style.afterglowLifetime));
                float holdEnd=switchTime+Mathf.Max(0,style.coverHoldAfterSwitch);
                lifetime=Mathf.Max(lifetime,ParticleDelay(true,holdEnd)+Mathf.Max(0.03f,style.sparkLifetime));
                lifetime=Mathf.Max(lifetime,ParticleDelay(false,holdEnd)+Mathf.Max(0.03f,style.sparkLifetime*0.72f));
            }
            for(int i=0;i<pieces.Length;i++)
            {
                pieces[i].sharedMaterial=style.visualVersion==1?burstMaterial:legacyMaterial;
                pieces[i].GetComponent<MeshFilter>().sharedMesh=style.visualVersion==1&&i>=2&&i<6?starMesh:
                    style.visualVersion==1&&i>=6&&i<14?fragmentMesh:quad;
                pieces[i].gameObject.name=i==0?"ContactCore":i==1?"GlyphCover":i==19?"Afterglow":i<6?"Star_"+(i-2):i<14?"Fragment_"+(i-6):"Spare_"+i;
                pieces[i].enabled=false;
            }
            initialCenter=view!=null?view.transform.TransformPoint(view.LocalBounds.center):point;size=ProjectBounds(view,cam,style.visualVersion==0);
            lastCenter=initialCenter;lastContact=point;
            lastDepth=Mathf.Max(size.y,0.006f)*0.12f;
            Step(0);
        }
        public void Step(float deltaTime)
        {
            if(style==null)return;age+=Mathf.Max(0,deltaTime);
            if(age>=lifetime){for(int i=0;i<pieces.Length;i++)pieces[i].enabled=false;return;}
            if(style.visualVersion==1){StepBurst();return;}
            var rotation=camera!=null?camera.transform.rotation:Quaternion.identity;
            Vector3 forward=rotation*Vector3.forward;
            float depth=Mathf.Max(0.001f,size.y*0.12f);
            if(glyph!=null&&glyph.BindingVersion==bindingVersion&&glyph.gameObject.activeInHierarchy)
            {
                lastCenter=glyph.transform.TransformPoint(glyph.LocalBounds.center);lastContact=glyph.LandingPoint.position;
            }
            else glyph=null; // Keep the last old-word pose after recycle, never follow the next binding.
            Vector3 center=lastCenter;Vector3 hit=lastContact;
            float reference=Mathf.Max(size.y,0.006f);
            float coverEnd=switchTime+style.coverHoldAfterSwitch;
            float attack=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/Mathf.Max(0.008f,switchTime*0.7f)));
            float release=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-coverEnd)/Mathf.Max(0.01f,style.coverRelease)));
            float core=1-Mathf.Clamp01(age/Mathf.Max(0.01f,style.coreLifetime));
            Color coreColor=Color.Lerp(color,Color.white,style.whiteCore)*style.brightness;coreColor.a=core;
            Draw(0,hit-forward*(depth*1.1f),rotation,Vector2.one*reference*style.coreSize*(0.8f+0.6f*(1-core)),coreColor,0);
            Color coverColor=Color.Lerp(color,Color.white,style.coverWhiteness)*Mathf.Min(style.brightness,1.2f);coverColor.a=attack*release*style.coverOpacity;
            Draw(1,center-forward*depth,rotation,Vector2.Scale(size,new Vector2(style.coverWidth,style.coverHeight))*Mathf.Lerp(0.78f,1,attack),coverColor,1);
            int count=Mathf.Clamp(style.sparkCount,0,18);
            for(int i=0;i<18;i++)
            {
                if(i>=count){pieces[i+2].enabled=false;continue;}
                float angle=seed+i*2.399963f;float variety=0.72f+0.28f*Mathf.Sin(i*4.17f+seed);
                float life=style.sparkLifetime*(0.8f+(i%3)*0.1f);float t=Mathf.Clamp01(age/Mathf.Max(0.01f,life));
                Vector3 direction=rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                Vector3 position=contact-forward*(depth*1.2f)+direction*reference*style.sparkTravel*variety*(1-Mathf.Pow(1-t,2));
                Color spark=Color.Lerp(color,Color.white,style.sparkWhiteness)*style.brightness*1.35f;spark.a=Mathf.Pow(1-t,1.5f)*style.sparkBrightness;
                Draw(i+2,position,rotation*Quaternion.Euler(0,0,angle*Mathf.Rad2Deg-90),
                    new Vector2(reference*style.sparkWidth,reference*style.sparkLength*(1-t*0.65f)*(0.7f+variety)),spark,2);
            }
        }
        static void EnsureShapes()
        {
            if(starMesh==null)
            {
                var vertices=new Vector3[11];var uv=new Vector2[11];var indices=new int[30];uv[0]=Vector2.one*0.5f;
                for(int i=0;i<10;i++)
                {
                    float a=Mathf.PI*0.5f+i*Mathf.PI/5;float r=i%2==0?0.5f:0.24f;
                    vertices[i+1]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r);uv[i+1]=(Vector2)vertices[i+1]+Vector2.one*0.5f;
                    indices[i*3]=0;indices[i*3+1]=i+1;indices[i*3+2]=(i+1)%10+1;
                }
                starMesh=new Mesh{name="ImpactBurst_SharedStar",hideFlags=HideFlags.HideAndDontSave};
                starMesh.vertices=vertices;starMesh.uv=uv;starMesh.triangles=indices;starMesh.RecalculateBounds();
            }
            if(fragmentMesh==null)
            {
                fragmentMesh=new Mesh{name="ImpactBurst_SharedFragment",hideFlags=HideFlags.HideAndDontSave};
                fragmentMesh.vertices=new[]{new Vector3(-0.42f,-0.38f),new Vector3(0.5f,-0.14f),new Vector3(-0.12f,0.5f)};
                fragmentMesh.uv=new[]{new Vector2(0.08f,0.12f),new Vector2(1,0.36f),new Vector2(0.38f,1)};
                fragmentMesh.triangles=new[]{0,1,2};fragmentMesh.RecalculateBounds();
            }
        }
        void StepBurst()
        {
            var rotation=camera!=null?camera.transform.rotation:Quaternion.identity;
            Vector3 forward=rotation*Vector3.forward;
            if(glyph!=null&&glyph.BindingVersion==bindingVersion&&glyph.gameObject.activeInHierarchy)
            {
                lastCenter=glyph.transform.TransformPoint(glyph.LocalBounds.center);lastContact=glyph.LandingPoint.position;
                size=Measure(glyph,camera);
            }
            else glyph=null;
            float reference=Mathf.Max(size.y,0.006f);
            // Place the billboard in front of the nearest resting corner, not inside the extruded glyph.
            float depth=lastDepth;
            if(glyph!=null)
            {
                depth=reference*0.12f;
                var b=glyph.LocalBounds;
                for(int i=0;i<8;i++)
                {
                    var p=glyph.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    depth=Mathf.Max(depth,Vector3.Dot(lastCenter-p,forward)+reference*0.035f);
                }
                lastDepth=depth;
            }
            float holdEnd=switchTime+Mathf.Max(0,style.coverHoldAfterSwitch);
            float attack=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/Mathf.Max(0.003f,Mathf.Min(style.attackDuration,Mathf.Max(0.003f,switchTime*0.65f)))));
            float release=Mathf.Clamp01((age-holdEnd)/Mathf.Max(0.01f,style.breakupDuration));
            float coverRelease=Mathf.Clamp01((age-holdEnd)/Mathf.Max(0.01f,style.coverRelease));
            var bodySize=Vector2.Scale(size,new Vector2(Mathf.Clamp(style.coverWidth,1.4f,1.8f),Mathf.Clamp(style.coverHeight,1.4f,1.8f)));
            bodySize.x=Mathf.Max(bodySize.x,reference*0.28f);
            Color body=Color.Lerp(color,Color.white,Mathf.Clamp01(style.coverWhiteness))*Mathf.Min(style.brightness,1.3f);
            body.a=style.coverOpacity*Mathf.Lerp(0.18f,1,attack);
            DrawBurst(1,lastCenter-forward*depth,rotation,bodySize*Mathf.Lerp(0.32f,1,attack),body,1,release,coverRelease,attack);
            float coreT=Mathf.Clamp01(age/Mathf.Max(0.01f,style.coreLifetime));
            Color core=Color.Lerp(color,Color.white,style.whiteCore)*style.brightness*1.55f;core.a=Mathf.Pow(1-coreT,1.3f);
            DrawBurst(0,lastContact-forward*(depth+reference*0.02f),rotation,Vector2.one*reference*style.coreSize*Mathf.Lerp(0.85f,1.1f,coreT),core,0,release,coverRelease,attack);
            for(int i=0;i<4;i++)
            {
                if(i>=Mathf.Clamp(style.starCount,0,4)){pieces[i+2].enabled=false;continue;}
                DrawParticle(i+2,i,true,rotation,forward,depth,reference,holdEnd);
            }
            for(int i=0;i<8;i++)
            {
                if(i>=Mathf.Clamp(style.fragmentCount,0,8)){pieces[i+6].enabled=false;continue;}
                DrawParticle(i+6,i,false,rotation,forward,depth,reference,holdEnd);
            }
            float glowAge=age-holdEnd;
            float glowT=Mathf.Clamp01(glowAge/Mathf.Max(0.01f,style.afterglowLifetime));
            Color glow=color*Mathf.Min(style.brightness,1.2f);
            // A very fast attack and monotonic release, never a second flash at the tail.
            glow.a=glowAge<0?0:style.afterglowOpacity*Mathf.SmoothStep(0,1,Mathf.Clamp01(glowT*8))*Mathf.Pow(1-glowT,2);
            DrawBurst(19,lastCenter-forward*(depth*0.98f),rotation,bodySize*Mathf.Lerp(0.8f,0.5f,glowT),glow,4,release,coverRelease,attack);
        }
        void DrawParticle(int slot,int index,bool star,Quaternion rotation,Vector3 forward,float depth,float reference,float holdEnd)
        {
            float delay=ParticleDelay(star,holdEnd);
            float life=star?style.sparkLifetime:style.sparkLifetime*0.72f;
            life=Mathf.Max(0.03f,life)*(0.88f+0.06f*(index%3));
            float t=Mathf.Clamp01((age-delay)/life);
            float angle=seed+(star?0.36f:1.2f)+index*(star?2.18f:2.399963f);
            float variety=0.78f+0.12f*Mathf.Sin(seed+index*3.17f);
            Vector3 direction=rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            float travel=reference*style.sparkTravel*variety*(star?1:1.08f);
            Vector3 position=lastCenter-forward*(depth+reference*0.025f)+direction*(reference*(star?0.25f:0.16f)+travel*(1-Mathf.Pow(1-t,2.5f)));
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.62f,1,t));
            Color tint=Color.Lerp(color,Color.white,star?0.20f:0.11f)*Mathf.Min(style.brightness,1.3f)*style.sparkBrightness;
            tint.a=age<delay?0:fade*Mathf.SmoothStep(0,1,Mathf.Clamp01((age-delay)/0.012f));
            float shrink=Mathf.Lerp(1,0.12f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.58f,1,t)));
            float s=reference*(star?style.starSize:style.fragmentSize)*(0.82f+0.12f*(index%3))*shrink;
            float spin=(index%2==0?1:-1)*style.starSpin*t*150;
            DrawBurst(slot,position,rotation*Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+spin),new Vector2(s,s*(star?1:1.35f)),tint,star?2:3,0,0,1);
        }
        static float ParticleDelay(bool star,float holdEnd)
        {return star?Mathf.Max(0.008f,holdEnd-0.055f):Mathf.Max(0.003f,holdEnd-0.060f);}
        void DrawBurst(int index,Vector3 position,Quaternion rotation,Vector2 dimensions,Color tint,int kind,float release,float coverRelease,float attack)
        {
            var r=pieces[index];r.enabled=tint.a>0.001f;
            if(kind==1&&release>=1)r.enabled=false;
            if(!r.enabled)return;
            r.transform.SetPositionAndRotation(position,rotation);r.transform.localScale=new Vector3(dimensions.x,dimensions.y,1);
            block.Clear();block.SetColor("_Color",tint);block.SetFloat("_Kind",kind);
            block.SetFloat("_Release",release);block.SetFloat("_CoverRelease",coverRelease);block.SetFloat("_Attack",attack);
            block.SetFloat("_Seed",seed);block.SetFloat("_Variation",style.lobeVariation);block.SetFloat("_Glow",style.bodyGlow);
            r.SetPropertyBlock(block);
        }
        void Draw(int index,Vector3 position,Quaternion rotation,Vector2 dimensions,Color tint,int kind)
        {
            var r=pieces[index];r.enabled=tint.a>0.001f;
            if(!r.enabled)return;
            r.transform.SetPositionAndRotation(position,rotation);r.transform.localScale=new Vector3(dimensions.x,dimensions.y,1);
            block.Clear();block.SetColor("_Color",tint);block.SetFloat("_Kind",kind);block.SetFloat("_Progress",age/Lifetime);
            block.SetFloat("_Irregularity",style.edgeIrregularity);block.SetFloat("_Seed",seed);r.SetPropertyBlock(block);
        }
        public void Clear(){foreach(var r in pieces)if(r!=null)r.enabled=false;glyph=null;style=null;}
    }
}
