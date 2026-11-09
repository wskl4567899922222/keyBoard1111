using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KeyboardGame.Letters
{
    /// <summary>Static font-derived quads in the original Model coordinates, not a second text layout.</summary>
    public sealed class WaitingSdfCatalog:ScriptableObject
    {
        [Serializable] public sealed class Glyph
        {
            public string character;
            public Mesh mesh;
            public Bounds outlineBounds;
        }
        public string sourceSha256;
        public TMP_FontAsset font;
        public Material material;
        public List<Glyph> glyphs=new List<Glyph>();
        Dictionary<char,Glyph> lookup;
        public bool TryGet(char character,out Glyph glyph)
        {
            if(lookup==null){lookup=new Dictionary<char,Glyph>();foreach(var entry in glyphs)if(entry!=null&&!string.IsNullOrEmpty(entry.character))lookup[entry.character[0]]=entry;}
            return lookup.TryGetValue(character,out glyph);
        }
        void OnEnable(){lookup=null;}
        void OnValidate(){lookup=null;}
    }
}
