using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyboardGame.Letters
{
    [CreateAssetMenu(menuName="Keyboard/3D Letter Catalog")]
    public sealed class LetterCatalog:ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            [Tooltip("One character; A and a are distinct.")] public string character;
            public Mesh mesh;
            [Min(0.00001f)] public float layoutWidth;
            [Tooltip("Original mesh-local position, slightly outside a solid front triangle. Editable per glyph.")] public Vector3 landingOffset;
            public int bevelSlot,frontSlot;
            [Tooltip("Original font advance in imported-mesh units.")] public float fontAdvance;
            [Tooltip("Original font pen to this centered mesh's origin; never derived from a guessed gap.")] public float fontOriginOffset;
            public float fontLeftBearing,fontOutlineMin,fontOutlineMax;
            public char Character=>string.IsNullOrEmpty(character)?'\0':character[0];
        }
        [SerializeField] List<Entry> entries=new List<Entry>();
        [SerializeField] float capHeight=0.116666667f;
        [SerializeField] bool hasNativeSpacing;
        [SerializeField] string sourceFont,sourceFontSha256;
        [SerializeField] float fontToMeshScale;
        [SerializeField] List<KerningPair> kerningPairs=new List<KerningPair>();
        Dictionary<char,Entry> lookup;
        Dictionary<int,float> kerning;
        [Serializable] public sealed class KerningPair{public string first,second;public float adjustment;}
        [Serializable] public sealed class GlyphMetric{public string character;public float advance,originOffset,leftBearing,outlineMin,outlineMax;}
        [Serializable] public sealed class NativeFontData{public string font,fontSha256;public int unitsPerEm;public float fontToMeshScale,capHeight;public GlyphMetric[] glyphs;public KerningPair[] pairs;}
        public IReadOnlyList<Entry> Entries=>entries;
        public float CapHeight=>capHeight;
        public bool HasNativeSpacing=>hasNativeSpacing;
        public string SourceFont=>sourceFont;
        public float FontToMeshScale=>fontToMeshScale;
        void OnEnable(){lookup=null;kerning=null;}
        void OnValidate(){lookup=null;kerning=null;}
        public float GetKerning(char first,char second)
        {
            if(kerning==null)
            {
                kerning=new Dictionary<int,float>();
                foreach(var p in kerningPairs)if(p.first.Length==1&&p.second.Length==1)kerning[(p.first[0]<<16)|p.second[0]]=p.adjustment;
            }
            return kerning.TryGetValue((first<<16)|second,out float value)?value:0;
        }
        public void InstallNativeMetrics(NativeFontData data)
        {
            if(data==null||data.font!="Barlow ExtraBold"||data.glyphs==null||data.glyphs.Length!=52||Mathf.Abs(data.capHeight-capHeight)>0.00001f)throw new ArgumentException("Font metrics do not match existing Barlow mesh scale");
            var byCharacter=new Dictionary<char,GlyphMetric>();
            foreach(var m in data.glyphs)byCharacter.Add(m.character[0],m);
            foreach(var e in entries)if(!byCharacter.ContainsKey(e.Character))throw new ArgumentException("Missing font metrics for "+e.character);
            foreach(var e in entries)
            {
                var m=byCharacter[e.Character];e.fontAdvance=m.advance;e.fontOriginOffset=m.originOffset;
                e.fontLeftBearing=m.leftBearing;e.fontOutlineMin=m.outlineMin;e.fontOutlineMax=m.outlineMax;
            }
            sourceFont=data.font;sourceFontSha256=data.fontSha256;fontToMeshScale=data.fontToMeshScale;
            kerningPairs=new List<KerningPair>(data.pairs);hasNativeSpacing=true;kerning=null;
        }
        public bool TryGet(char character,out Entry entry)
        {
            if(lookup==null)
            {
                lookup=new Dictionary<char,Entry>();
                foreach(var e in entries)
                    if(e!=null&&e.character!=null&&e.character.Length==1&&e.mesh!=null&&!lookup.ContainsKey(e.Character))lookup.Add(e.Character,e);
            }
            return lookup.TryGetValue(character,out entry);
        }
        public void Initialize(IEnumerable<Entry> values,float commonCapHeight)
        {entries=new List<Entry>(values);capHeight=Mathf.Max(commonCapHeight,0.00001f);lookup=null;}
    }
}
