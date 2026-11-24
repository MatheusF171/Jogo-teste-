using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Carrega os sprites de Assets/RotaDoCangaco/Resources/Sprites. Se a textura não estiver importada
    /// como Sprite, cria o sprite em tempo de execução para o jogo nunca ficar sem imagem.
    /// Imagens grandes com aspecto de foto (o fundo do menu) ficam como JPEG puro (arquivo .bytes) e são
    /// decodificadas ao carregar: ocupam ~10x menos no executável do que uma textura sem compressão.
    /// </summary>
    public static class SpriteLibrary
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;

            sprite = Resources.Load<Sprite>("Sprites/" + key);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("Sprites/" + key);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (sprite == null)
            {
                var bytes = Resources.Load<TextAsset>("Sprites/" + key);
                if (bytes != null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = key };
                    if (tex.LoadImage(bytes.bytes, true))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f,
                                               0, SpriteMeshType.FullRect);
                    }
                    Resources.UnloadAsset(bytes);
                }
            }
            if (sprite == null)
            {
                Debug.LogWarning($"[RotaDoCangaco] Sprite não encontrado: {key}");
                sprite = TextureFactory.Circle;
            }
            cache[key] = sprite;
            return sprite;
        }
    }
}
