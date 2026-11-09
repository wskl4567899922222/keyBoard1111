using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetAudit
{
    /// <summary>
    /// 一张贴图在本次扫描中需要的最小信息集合。
    /// 刻意不包含 Texture2D 本身——全程不加载贴图，避免内存与导入开销。
    /// </summary>
    [Serializable]
    public class TextureFact
    {
        public string Path;
        public long MtimeTicks;
        public long MetaMtimeTicks;
        public long FileSize;
        public int SrcWidth;
        public int SrcHeight;
        public bool HasSourceSize;
        public int MaxSize;
        public int Format;
        public bool Mipmap;
        public bool AndroidOverridden;
        public bool AppleOverridden;
    }

    [Serializable]
    public class FactCache
    {
        public List<TextureFact> Entries = new List<TextureFact>();
    }

    public static class TextureFactBuilder
    {
        /// <summary>
        /// 只读文件时间戳，供缓存命中判断，不解析图片头。
        /// .meta 的时间戳必须一起比对，否则美术改了导入设置而源文件没变时会读到脏缓存。
        /// </summary>
        public static void Stamp(string assetPath, out long mtime, out long metaMtime, out long size)
        {
            var full = Path.Combine(AuditPaths.ProjectRoot, assetPath);
            var fi = new FileInfo(full);
            size = fi.Exists ? fi.Length : 0;
            mtime = fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0;
            var mi = new FileInfo(full + ".meta");
            metaMtime = mi.Exists ? mi.LastWriteTimeUtc.Ticks : 0;
        }

        public static TextureFact Build(string assetPath)
        {
            var fact = new TextureFact { Path = assetPath };
            var full = Path.Combine(AuditPaths.ProjectRoot, assetPath);
            var fi = new FileInfo(full);
            if (fi.Exists)
            {
                fact.FileSize = fi.Length;
                fact.MtimeTicks = fi.LastWriteTimeUtc.Ticks;
            }
            var mi = new FileInfo(full + ".meta");
            if (mi.Exists) fact.MetaMtimeTicks = mi.LastWriteTimeUtc.Ticks;

            int w = 0, h = 0;
            if (TryReadImageSize(full, out w, out h))
            {
                fact.SrcWidth = w;
                fact.SrcHeight = h;
                fact.HasSourceSize = true;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return fact;

            fact.MaxSize = importer.maxTextureSize;
            fact.Mipmap = importer.mipmapEnabled;

            var android = importer.GetPlatformTextureSettings("Android");
            fact.AndroidOverridden = android.overridden;
            fact.Format = (int)android.format;

            var apple = importer.GetPlatformTextureSettings("iPhone");
            fact.AppleOverridden = apple.overridden;

            // 头部解析失败时的兜底（少数格式 Unity 能读但字节头不规范）
            if (!fact.HasSourceSize)
            {
                importer.GetSourceTextureWidthAndHeight(out w, out h); // 返回 void，尺寸经 out 带出
                fact.SrcWidth = w;
                fact.SrcHeight = h;
                fact.HasSourceSize = true;
            }
            return fact;
        }

        /// <summary>
        /// 只读文件头拿真实像素尺寸，不触发 Unity 导入。
        /// 支持 PNG / TGA / BMP / JPEG，覆盖绝大多数美术源文件。
        /// </summary>
        public static bool TryReadImageSize(string fullPath, out int w, out int h)
        {
            w = 0;
            h = 0;
            try
            {
                if (!File.Exists(fullPath)) return false;
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var head = new byte[32];
                var read = fs.Read(head, 0, head.Length);
                if (read < 24) return false;

                var ext = Path.GetExtension(fullPath).ToLowerInvariant();

                if (head[0] == 0x89 && head[1] == 0x50) // PNG
                {
                    w = (int)ReadBE32(head, 16);
                    h = (int)ReadBE32(head, 20);
                    return w > 0 && h > 0;
                }

                if (ext == ".tga") // TGA 头部固定布局，小端
                {
                    w = head[12] | (head[13] << 8);
                    h = head[14] | (head[15] << 8);
                    return w > 0 && h > 0;
                }

                if (head[0] == 0x42 && head[1] == 0x4D) // BMP
                {
                    w = BitConverter.ToInt32(head, 18);
                    h = Math.Abs(BitConverter.ToInt32(head, 22));
                    return w > 0 && h > 0;
                }

                if (head[0] == 0xFF && head[1] == 0xD8) // JPEG：扫到 SOF 段
                    return TryReadJpegSize(fs, out w, out h);
            }
            catch (Exception)
            {
                // 损坏文件、权限问题都不应该中断扫描，交给调用方降级处理
            }
            return false;
        }

        static bool TryReadJpegSize(FileStream fs, out int w, out int h)
        {
            w = 0;
            h = 0;
            fs.Seek(2, SeekOrigin.Begin);
            var buf = new byte[5];
            while (fs.Position < fs.Length)
            {
                if (fs.Read(buf, 0, 2) != 2) return false;
                if (buf[0] != 0xFF) continue;

                var marker = buf[1];
                if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0x01) continue;

                if (fs.Read(buf, 0, 2) != 2) return false;
                var len = (buf[0] << 8) | buf[1];

                // SOF0~SOF3 / SOF5~SOF7 / SOF9~SOF11 / SOF13~SOF15
                if (marker >= 0xC0 && marker <= 0xCF &&
                    marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (fs.Read(buf, 0, 5) != 5) return false;
                    h = (buf[1] << 8) | buf[2];
                    w = (buf[3] << 8) | buf[4];
                    return w > 0 && h > 0;
                }

                fs.Seek(len - 2, SeekOrigin.Current);
            }
            return false;
        }

        static long ReadBE32(byte[] buf, int offset) =>
            ((long)buf[offset] << 24) | ((long)buf[offset + 1] << 16) |
            ((long)buf[offset + 2] << 8) | buf[offset + 3];
    }
}
