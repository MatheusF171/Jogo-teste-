using UnityEditor;
using UnityEngine;

namespace RotaDoCangaco.EditorTools
{
    /// <summary>
    /// Garante que as imagens de Resources/Sprites sejam importadas como Sprite, sem compressão e sem mipmaps
    /// (exceto o logo, em BC7).
    /// </summary>
    public class SpriteImportProcessor : AssetPostprocessor
    {
        // Mudar a versão faz o Unity reimportar as imagens com as regras novas (logo em BC7).
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("RotaDoCangaco/Resources/Sprites/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            // O logo é grande e cheio de detalhes: sem compressão ocupava ~1,5 MB no zip do jogo.
            // BC7 (alta qualidade) fica visualmente igual e cai para cerca de um terço.
            if (System.IO.Path.GetFileNameWithoutExtension(assetPath) == "logo_titulo")
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
            }
        }
    }
}
