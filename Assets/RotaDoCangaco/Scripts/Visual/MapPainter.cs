using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Pinta o mapa visto de cima, tile por tile, numa única textura: canavial denso, trilhas de terra,
    /// clareiras, rio, ponte e o velho engenho de tijolos. Tudo com tom noturno (a Mula só aparece à noite).
    /// </summary>
    public static class MapPainter
    {
        public const int TilePx = 32;
        static readonly Color NightTint = new Color(0.74f, 0.8f, 0.97f);

        public static bool IsSolid(char t) => t == '#' || t == '~' || t == 'W' || t == 'T';

        /// <param name="terrain">Grade [x, y] com y = 0 na linha de cima, já sem os objetos.</param>
        public static Texture2D Paint(char[,] terrain, int seed, bool night = true, string[] layout = null)
        {
            int w = terrain.GetLength(0), h = terrain.GetLength(1);
            var c = new PixelCanvas(w * TilePx, h * TilePx, Color.black);
            bool exploration = layout != null;

            for (int ty = 0; ty < h; ty++)
            for (int tx = 0; tx < w; tx++)
                PaintGround(c, terrain[tx, ty], tx * TilePx, (h - 1 - ty) * TilePx, tx, ty, seed, exploration);

            for (int ty = 0; ty < h; ty++)
            for (int tx = 0; tx < w; tx++)
            {
                char t = terrain[tx, ty];
                if (IsSolid(t) && t != 'T') continue;
                PaintEdgeShadows(c, terrain, tx, ty, w, h);
            }

            for (int ty = 0; ty < h; ty++)
            for (int tx = 0; tx < w; tx++)
            {
                char t = terrain[tx, ty];
                int ox = tx * TilePx, oy = (h - 1 - ty) * TilePx;
                var rng = new System.Random(seed * 7919 + tx * 131 + ty * 977);
                if (t == '#') PaintCane(c, ox, oy, rng, exploration);
                else if (t == 'T') PaintTree(c, ox, oy, rng);
            }

            if (night) c.Multiply(NightTint);
            if (exploration) PaintLandmarks(c, terrain, layout);
            return c.ToTexture(FilterMode.Bilinear);
        }

        static void PaintLandmarks(PixelCanvas c, char[,] terrain, string[] layout)
        {
            int width = terrain.GetLength(0), height = terrain.GetLength(1);
            var bank = new Color(0.28f, 0.59f, 0.57f, 0.4f);
            var fire = new Color(1f, 0.6f, 0.26f, 0.7f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int left = x * TilePx;
                int bottom = (height - 1 - y) * TilePx;
                float cx = left + TilePx * 0.5f, cy = bottom + TilePx * 0.5f;

                if (terrain[x, y] == '~')
                {
                    if (y > 0 && terrain[x, y - 1] != '~')
                        c.Capsule(left, bottom + 29, left + TilePx, bottom + 29, 1.4f, bank);
                    if (y < height - 1 && terrain[x, y + 1] != '~')
                        c.Capsule(left, bottom + 3, left + TilePx, bottom + 3, 1.4f, bank);
                }

                if (layout[y][x] == 'F')
                    c.Ring(cx, cy, 12, 2.5f, Hex("#65534B"));

                if (terrain[x, y] == 'W' && x > 0 && x < width - 1 &&
                    ((terrain[x - 1, y] == 'W' && terrain[x + 1, y] == '_') ||
                     (terrain[x + 1, y] == 'W' && terrain[x - 1, y] == '_')))
                {
                    c.Glow(cx, cy - 7, 36, fire);
                    c.Rect(cx - 2, cy - 11, cx + 2, cy - 4, Hex("#392A2A"));
                    c.Circle(cx, cy - 4, 2.5f, Hex("#FFBB69"));
                }
            }
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint hsh = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                hsh ^= hsh >> 16;
                return (hsh & 0xFFFFFF) / 16777215f;
            }
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var col);
            return col;
        }

        static void FillNoise(PixelCanvas c, int ox, int oy, Color baseCol, float noise, int seed)
        {
            for (int y = 0; y < TilePx; y++)
            for (int x = 0; x < TilePx; x++)
            {
                float n = 1f + (Hash(ox + x, oy + y, seed) - 0.5f) * noise;
                c.Set(ox + x, oy + y, new Color(baseCol.r * n, baseCol.g * n, baseCol.b * n, 1f));
            }
        }

        static void PaintGround(PixelCanvas c, char t, int ox, int oy, int tx, int ty, int seed, bool exploration)
        {
            var rng = new System.Random(seed * 31 + tx * 7477 + ty * 3331);
            switch (t)
            {
                case '#':
                    FillNoise(c, ox, oy, Hex(exploration ? "#182F28" : "#1E4424"), 0.35f, seed);
                    break;

                case '.':
                    FillNoise(c, ox, oy, Hex(exploration ? "#A47C55" : "#8A6A44"), 0.22f, seed);
                    for (int i = 0; i < 4; i++)
                        c.Circle(ox + (float)rng.NextDouble() * TilePx, oy + (float)rng.NextDouble() * TilePx, 0.8f + (float)rng.NextDouble() * 1.2f,
                                 rng.NextDouble() < 0.5 ? Hex("#A88A62") : Hex("#6B5034"));
                    break;

                case ',':
                case 'T':
                    FillNoise(c, ox, oy, Hex(exploration ? "#547047" : "#4F6E33"), 0.3f, seed);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = ox + (float)rng.NextDouble() * TilePx, y = oy + (float)rng.NextDouble() * TilePx;
                        c.Capsule(x, y, x + (float)rng.NextDouble() * 3f - 1.5f, y + 3f + (float)rng.NextDouble() * 3f, 0.7f,
                                  rng.NextDouble() < 0.5 ? Hex("#6E9244") : Hex("#3E5A28"));
                    }
                    if (rng.NextDouble() < 0.15)
                        c.Circle(ox + 6 + (float)rng.NextDouble() * 20, oy + 6 + (float)rng.NextDouble() * 20, 1.6f, Hex("#E8D36A"));
                    break;

                case '~':
                    FillNoise(c, ox, oy, Hex(exploration ? "#246274" : "#1B4E7A"), 0.12f, seed);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = ox + (float)rng.NextDouble() * TilePx, y = oy + (float)rng.NextDouble() * TilePx;
                        c.Capsule(x, y, x + 6 + (float)rng.NextDouble() * 6, y, 0.8f, new Color(0.55f, 0.75f, 0.95f, 0.45f));
                    }
                    break;

                case '=':
                    FillNoise(c, ox, oy, Hex(exploration ? "#246274" : "#1B4E7A"), 0.12f, seed);
                    for (int py = 0; py < TilePx; py += 6)
                    {
                        float shade = 0.85f + (float)rng.NextDouble() * 0.25f;
                        var plank = Hex("#8B5E34");
                        c.Rect(ox + 3, oy + py + 1, ox + TilePx - 3, oy + py + 5, new Color(plank.r * shade, plank.g * shade, plank.b * shade));
                    }
                    c.Rect(ox, oy, ox + 3, oy + TilePx, Hex("#5A3A1E"));
                    c.Rect(ox + TilePx - 3, oy, ox + TilePx, oy + TilePx, Hex("#5A3A1E"));
                    break;

                case 'W':
                    FillNoise(c, ox, oy, Hex("#3E3029"), 0.15f, seed);
                    for (int row = 0; row < 4; row++)
                    {
                        int offset = (row + ty) % 2 == 0 ? 0 : 5;
                        for (int col = -1; col < 4; col++)
                        {
                            float bx = ox + col * 10 + offset, by = oy + row * 8;
                            var brick = Color.Lerp(Hex("#8C4A33"), Hex("#A8664A"), (float)rng.NextDouble());
                            float x0 = Mathf.Max(ox, bx + 1), x1 = Mathf.Min(ox + TilePx, bx + 9);
                            if (x1 > x0) c.Rect(x0, by + 1, x1, by + 7, brick);
                        }
                    }
                    break;

                case '_':
                    FillNoise(c, ox, oy, Hex("#6E6253"), 0.18f, seed);
                    c.Rect(ox, oy, ox + TilePx, oy + 1, Hex("#4E4539"));
                    c.Rect(ox, oy, ox + 1, oy + TilePx, Hex("#4E4539"));
                    c.Rect(ox + 16, oy, ox + 17, oy + TilePx, new Color(0.3f, 0.27f, 0.22f, 0.5f));
                    c.Rect(ox, oy + 16, ox + TilePx, oy + 17, new Color(0.3f, 0.27f, 0.22f, 0.5f));
                    break;

                default:
                    FillNoise(c, ox, oy, Hex("#8A6A44"), 0.22f, seed);
                    break;
            }
        }

        static void PaintEdgeShadows(PixelCanvas c, char[,] terrain, int tx, int ty, int w, int h)
        {
            int ox = tx * TilePx, oy = (h - 1 - ty) * TilePx;
            bool Solid(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || (IsSolid(terrain[x, y]) && terrain[x, y] != '~');
            const int size = 7;
            var shadow = new Color(0, 0, 0, 1f);
            // y da textura cresce para cima; tile de cima no mapa = ty - 1
            if (Solid(tx, ty - 1)) for (int i = 0; i < size; i++) ShadeRow(c, ox, oy + TilePx - 1 - i, shadow, 0.5f * (1f - i / (float)size));
            if (Solid(tx, ty + 1)) for (int i = 0; i < size; i++) ShadeRow(c, ox, oy + i, shadow, 0.3f * (1f - i / (float)size));
            if (Solid(tx - 1, ty)) for (int i = 0; i < size; i++) ShadeCol(c, ox + i, oy, shadow, 0.4f * (1f - i / (float)size));
            if (Solid(tx + 1, ty)) for (int i = 0; i < size; i++) ShadeCol(c, ox + TilePx - 1 - i, oy, shadow, 0.4f * (1f - i / (float)size));
        }

        static void ShadeRow(PixelCanvas c, int ox, int y, Color col, float a) { for (int x = 0; x < TilePx; x++) c.Blend(ox + x, y, col, a); }
        static void ShadeCol(PixelCanvas c, int x, int oy, Color col, float a) { for (int y = 0; y < TilePx; y++) c.Blend(x, oy + y, col, a); }

        static readonly Color[] CaneGreens =
        {
            new Color(0.17f, 0.4f, 0.18f), new Color(0.25f, 0.53f, 0.23f), new Color(0.33f, 0.63f, 0.28f),
            new Color(0.14f, 0.33f, 0.16f), new Color(0.45f, 0.7f, 0.32f), new Color(0.55f, 0.72f, 0.3f)
        };

        static void PaintCane(PixelCanvas c, int ox, int oy, System.Random rng, bool exploration)
        {
            var shade = new Color(0.08f, 0.17f, 0.16f);
            var seedColor = new Color(0.55f, 0.6f, 0.3f);
            if (exploration) seedColor = Color.Lerp(seedColor, shade, 0.55f);
            for (int i = 0; i < 3; i++)
                c.Circle(ox + 4 + (float)rng.NextDouble() * 24, oy + 4 + (float)rng.NextDouble() * 24, 2.2f, seedColor);
            for (int i = 0; i < 12; i++)
            {
                float x = ox + (float)rng.NextDouble() * TilePx, y = oy + (float)rng.NextDouble() * TilePx;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float len = 6f + (float)rng.NextDouble() * 9f;
                float radius = 1.1f + (float)rng.NextDouble() * 1.1f;
                var leaf = CaneGreens[rng.Next(CaneGreens.Length)];
                if (exploration) leaf = Color.Lerp(leaf, shade, 0.45f);
                c.Capsule(x, y, x + Mathf.Cos(ang) * len, y + Mathf.Sin(ang) * len, radius, leaf);
            }
        }

        static void PaintTree(PixelCanvas c, int ox, int oy, System.Random rng)
        {
            float cx = ox + 16, cy = oy + 16;
            c.Ellipse(cx + 3, cy - 4, 17, 13, new Color(0, 0, 0, 0.35f));
            c.Circle(cx - 6, cy + 2, 11, new Color(0.13f, 0.3f, 0.13f));
            c.Circle(cx + 7, cy + 4, 10, new Color(0.15f, 0.34f, 0.15f));
            c.Circle(cx, cy - 5, 11, new Color(0.12f, 0.28f, 0.12f));
            c.Circle(cx - 2, cy + 3, 9, new Color(0.22f, 0.45f, 0.2f));
            c.Circle(cx - 5, cy + 7, 4, new Color(0.35f, 0.58f, 0.28f));
            if (rng.NextDouble() < 0.6) c.Circle(cx + 6, cy - 2, 2, new Color(0.95f, 0.75f, 0.2f));
        }

        // ------------------------------------------------------------------ Arena de batalha

        /// <summary>Clareira cercada de cana (ou o pátio do engenho, na luta contra o chefe).</summary>
        public static Texture2D PaintArena(bool engenho, int seed)
        {
            const int w = 30, h = 17;
            var grid = new char[w, h];
            var rng = new System.Random(seed);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int border = 2 + (rng.NextDouble() < 0.35 ? 1 : 0);
                bool edge = x < border || x >= w - border || y < border;
                if (engenho) grid[x, y] = edge ? 'W' : '_';
                else grid[x, y] = edge ? '#' : (rng.NextDouble() < 0.25 ? '.' : ',');
            }
            if (!engenho)
                for (int y = 2; y < h; y++)
                for (int x = 12; x <= 17; x++)
                    grid[x, y] = '.';
            return Paint(grid, seed, true);
        }
    }
}
