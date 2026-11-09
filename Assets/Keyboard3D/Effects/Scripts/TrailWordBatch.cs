using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KeyboardGame
{
    /// <summary>Input-order tickets, independent arrivals, one Enter-controlled fade.</summary>
    public sealed class TrailWordBatch
    {
        sealed class Letter { public char character; public Color color; public float width, offset; public int state; }
        readonly List<Letter> letters = new List<Letter>();
        float readyTime;
        public int Id { get; }
        public int Count => letters.Count;
        public bool Submitted { get; private set; }
        public bool IsComplete { get; private set; }
        public float Alpha { get; private set; } = 1;
        public bool AllResolved { get { foreach(var l in letters) if(l.state == 0) return false; return true; } }
        public string Text { get { var s=new StringBuilder();foreach(var l in letters)s.Append(l.character);return s.ToString(); } }
        public TrailWordBatch(int id) { Id = id; }
        public int Add(char character, Color color, float width)
        {
            if (Submitted) throw new InvalidOperationException("Cannot append a letter after Enter.");
            width = Mathf.Max(0.00001f, width);
            float offset = letters.Count == 0 ? 0 : letters[letters.Count-1].offset + letters[letters.Count-1].width * (4f/3f);
            letters.Add(new Letter {character=character,color=color,width=width,offset=offset});
            return letters.Count-1;
        }
        public float GetOffset(int index) => letters[index].offset;
        /// <summary>Use an external font shaper's exact position while retaining existing task/fade state.</summary>
        public int AddPositioned(char character,Color color,float width,float left)
        {
            if(Submitted)throw new InvalidOperationException("Cannot append a letter after Enter.");
            letters.Add(new Letter{character=character,color=color,width=Mathf.Max(0.00001f,width),offset=left});return letters.Count-1;
        }
        public Color GetColor(int index) => letters[index].color;
        public void SetColor(int index, Color color) => letters[index].color = color; // 音节拆分双色（2026-11-09）：不打乱索引的原地改色
        public char GetCharacter(int index) => letters[index].character;
        public bool HasArrived(int index) => letters[index].state == 1;
        public bool MarkArrived(int index) { if(letters[index].state!=0)return false; letters[index].state=1;return true; }
        public bool MarkFailed(int index) { if(letters[index].state!=0)return false; letters[index].state=2;return true; }
        public void Submit() { if(Count>0) Submitted=true; }
        public bool IsReadyForExit(float hold) => Submitted && AllResolved && readyTime >= Mathf.Max(0,hold);
        public void CompleteExit() { if(Submitted && AllResolved){Alpha=0;IsComplete=true;} }
        public void Advance(float deltaTime, float hold, float fade)
        {
            if(!Submitted || !AllResolved || IsComplete || deltaTime<=0) return;
            readyTime += deltaTime;
            Alpha = 1f - Mathf.Clamp01((readyTime-Mathf.Max(0,hold))/Mathf.Max(0.01f,fade));
            IsComplete = Alpha <= 0;
        }
    }
}
