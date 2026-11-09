using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteTyping
{
    /// <summary>课程数据模型，对应 Assets/Resources/Data/typing_course.json</summary>
    [Serializable]
    public class CourseData
    {
        public List<Chapter> chapters;
    }

    [Serializable]
    public class Chapter
    {
        public string key;
        public string name;
        public List<Lesson> lessons;
    }

    [Serializable]
    public class Lesson
    {
        public string id;
        public string title;
        public string text;
        public string span;   // 学习新键的键帽，如 "FJ"
        public string type;   // video / learn / review / practice
        public string icon;
    }

    public static class CourseLoader
    {
        public static CourseData Load()
        {
            var asset = Resources.Load<TextAsset>("Data/typing_course");
            if (asset == null)
            {
                Debug.LogError("[ByteTyping] 找不到 Resources/Data/typing_course.json");
                return null;
            }
            var data = JsonUtility.FromJson<CourseData>(asset.text);
            if (data == null || data.chapters == null || data.chapters.Count == 0)
            {
                Debug.LogError("[ByteTyping] 课程数据解析失败");
                return null;
            }
            return data;
        }
    }
}
