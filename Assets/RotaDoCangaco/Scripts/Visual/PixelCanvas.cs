using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Pequena "tela de pintura" em memória para gerar texturas por código (formas suavizadas via SDF).
    /// Coordenadas com origem no canto inferior esquerdo, como nas texturas da Unity.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color[] px;

        public delegate float Sdf(float x, float y);

        public PixelCanvas(int width, int height) : this(width, height, Color.clear) { }

        public PixelCanvas(int width, int height, Color fill)
        {
            Width = width;
            Height = height;
            px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
        }

        public Color Get(int x, int y) => px[y * Width + x];

        public void Set(int x, int y, Color c)
        {
            if ((uint)x < (uint)Width && (uint)y < (uint)Height) px[y * Width + x] = c;
        }

        /// <summary>Composição "over" com cobertura (0..1).</summary>
        public void Blend(int x, int y, Color c, float coverage)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            float a = c.a * coverage;
            if (a <= 0f) return;
            int i = y * Width + x;
            Color d = px[i];
            float k = d.a * (1f - a);
            float outA = a + k;
            if (outA <= 0.0001f) return;
            px[i] = new Color((c.r * a + d.r * k) / outA, (c.g * a + d.g * k) / outA, (c.b * a + d.b * k) / outA, outA);
        }

        public void Fill(float minX, float minY, float maxX, float maxY, Sdf sdf, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - 1);
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX) + 1);
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY) + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float cov = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f));
                if (cov > 0f) Blend(x, y, c, cov);
            }
        }

        /// <summary>Apaga (torna transparente) a área da forma.</summary>
        public void Cut(float minX, float minY, float maxX, float maxY, Sdf sdf)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - 1);
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX) + 1);
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY) + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float cov = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f));
                if (cov <= 0f) continue;
                int i = y * Width + x;
                var c = px[i];
                c.a *= 1f - cov;
                px[i] = c;
            }
        }

        public void Circle(float cx, float cy, float r, Color c)
            => Fill(cx - r, cy - r, cx + r, cy + r, (x, y) => Dist(x - cx, y - cy) - r, c);

        public void CutCircle(float cx, float cy, float r)
            => Cut(cx - r, cy - r, cx + r, cy + r, (x, y) => Dist(x - cx, y - cy) - r);

        public void Ring(float cx, float cy, float r, float thickness, Color c)
        {
            float o = r + thickness;
            Fill(cx - o, cy - o, cx + o, cy + o, (x, y) => Mathf.Abs(Dist(x - cx, y - cy) - r) - thickness * 0.5f, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float angleDeg = 0f)
        {
            float rad = angleDeg * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            float m = Mathf.Max(rx, ry);
            Fill(cx - m, cy - m, cx + m, cy + m, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float lx = (dx * cs + dy * sn) / rx, ly = (-dx * sn + dy * cs) / ry;
                return (Mathf.Sqrt(lx * lx + ly * ly) - 1f) * Mathf.Min(rx, ry);
            }, c);
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c) => RoundRect(x0, y0, x1, y1, 0f, c);

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
            Fill(x0, y0, x1, y1, (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - (hx - r), qy = Mathf.Abs(y - cy) - (hy - r);
                return Dist(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            }, c);
        }

        public void Capsule(float ax, float ay, float bx, float by, float r, Color c)
        {
            Fill(Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r, (x, y) =>
            {
                float pxv = x - ax, pyv = y - ay, dx = bx - ax, dy = by - ay;
                float h = Mathf.Clamp01((pxv * dx + pyv * dy) / Mathf.Max(0.0001f, dx * dx + dy * dy));
                return Dist(pxv - dx * h, pyv - dy * h) - r;
            }, c);
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 cpt, Color c)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, cpt.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, cpt.x));
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, cpt.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, cpt.y));
            Fill(minX, minY, maxX, maxY, (x, y) => SdTriangle(new Vector2(x, y), a, b, cpt), c);
        }

        /// <summary>Brilho radial suave (alpha cai com o quadrado da distância).</summary>
        public void Glow(float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(Width - 1, (int)(cx + r));
            int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(Height - 1, (int)(cy + r));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Dist(x + 0.5f - cx, y + 0.5f - cy) / r;
                if (d < 1f) Blend(x, y, c, (1f - d) * (1f - d));
            }
        }

        public void Multiply(Color tint)
        {
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }
        }

        public Texture2D ToTexture(FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filter
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        public Sprite ToSprite(float pixelsPerUnit = 100f, float border = 0f)
        {
            var tex = ToTexture();
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static float Dist(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float SdTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }
    }
}
