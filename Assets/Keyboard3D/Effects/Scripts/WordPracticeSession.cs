using System;
using System.Collections.Generic;

namespace KeyboardGame
{
    /// <summary>One Play session owns its word-list snapshot; no Unity lifecycle dependencies.</summary>
    public sealed class WordPracticeSession
    {
        public enum InputResult { Ignored, Accepted, Wrong, Blocked }
        readonly List<string> words=new List<string>();
        readonly List<int> order=new List<int>();
        readonly Random random;
        readonly int mode;
        int cursor;
        public int Position { get; private set; }
        public int CompletedWords { get; private set; } // 已打完单词数（循环词库跨轮累计，顶部进度条用）
        public int TotalWords => words.Count;           // 总单词数
        public bool HasError { get; private set; }
        public bool Submitted { get; private set; }
        public bool Finished { get; private set; }
        public string CurrentWord => Finished ? "" : words[order[cursor]];
        public bool CanSubmit => !Finished&&!HasError&&!Submitted&&Position==CurrentWord.Length;
        public int WordCount => words.Count;

        public WordPracticeSession(string[] configured,int mode,int seed)
        {
            this.mode=mode;random=new Random(seed);
            var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string raw in configured??Array.Empty<string>())
            {
                string word=raw?.Trim();if(string.IsNullOrEmpty(word))continue;
                bool valid=true;foreach(char c in word)if(!IsLetter(c)){valid=false;break;}
                if(!valid||(mode==1&&!unique.Add(word)))continue;
                words.Add(word);
            }
            for(int i=0;i<words.Count;i++)order.Add(i);
            Finished=words.Count==0;
            if(mode==1)Shuffle();
        }
        static bool IsLetter(char c)=>(c>='a'&&c<='z')||(c>='A'&&c<='Z');
        void Shuffle(){for(int i=order.Count-1;i>0;i--){int j=random.Next(i+1);int n=order[i];order[i]=order[j];order[j]=n;}}
        public InputResult Input(char c)
        {
            if(!IsLetter(c))return InputResult.Ignored;
            if(Finished||Submitted||Position>=CurrentWord.Length)return InputResult.Blocked;
            if(char.ToUpperInvariant(c)!=char.ToUpperInvariant(CurrentWord[Position]))
            {HasError=true;return InputResult.Wrong;}
            HasError=false;
            Position++;return InputResult.Accepted;
        }
        public void Backspace() { } // Retained for callers; no longer performs correction.
        public void ClearError(){if(!Submitted)HasError=false;}
        public bool Submit(){if(!CanSubmit)return false;Submitted=true;return true;}
        public bool AdvanceWord()
        {
            if(!Submitted||Finished)return false;
            CompletedWords++; // 打完一个词（顶部进度条按此计数推进）
            cursor++;Position=0;HasError=false;Submitted=false;
            if(cursor>=order.Count)
            {
                if(mode==2){Finished=true;return false;}
                cursor=0;if(mode==1)Shuffle();
            }
            return true;
        }
    }
}
