using System;
using UnityEditor;
using UnityEngine;

namespace Insectum.Game.Editor
{
    internal sealed class ForestTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/PixelArt/", StringComparison.Ordinal))
                return;

            var texture = (TextureImporter)assetImporter;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.alphaIsTransparency = true;
        }
    }
}
