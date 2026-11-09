using UnityEditor;
using UnityEngine;

namespace ByteTyping.Editor
{
    /// <summary>
    /// 自动把 ByteTyping 目录下的贴图导入为 Sprite（UI 用），免手动在 Inspector 逐个设置。
    /// </summary>
    internal class ByteTypingImportFixer : AssetPostprocessor
    {
        private const string Root = "Assets/ByteTyping/";

        private void OnPostprocessTexture(Texture2D tex)
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        [InitializeOnLoadMethod]
        private static void FixExisting()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/'))) return;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null || ti.textureType == TextureImporterType.Sprite) continue;
                    ti.textureType = TextureImporterType.Sprite;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
            };
        }
    }
}
