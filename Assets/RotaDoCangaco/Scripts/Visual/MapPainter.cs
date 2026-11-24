using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Pinta o mapa do Sertão visto de cima, tile por tile, numa única textura: rochas, trilhas de terra,
    /// chão de caatinga, mandacarus, o açude com sua ponte e o lajedo de pedra do Saci.
    /// </summary>
    public static class MapPainter
    {
        public const int TilePx = 32;

        public static bool IsSolid(char t) => t == '#' || t == '~' || t == 'W' || t == 'T' || t == 'K';

        /// <param name="terrain">Grade [x, y] com y = 0 na linha de cima, já sem os objetos.</param>
        /// <param name="layout">Layout original (com objetos), para desenhar fogueiras, tochas e margens.</param>
        public static Texture2D PaintSertao(char[,] terrain, int seed, string[] layout = null)
        {
            int width = terrain.GetLength(0), height = terrain.GetLength(1);
            var c = new PixelCanvas(width * TilePx, height * TilePx, Color.black);
            var stone = Hex("#514041");
            var ochre = Hex("#BC8454");
            var sand = Hex("#9B6748");
            var water = Hex("#23626D");
            var yard = Hex("#A08B78");

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char tile = terrain[x, y];
                var ground = tile == '#' ? Color.Lerp(stone, Hex("#684B46"), Hash(x / 2, y / 2, seed) * 0.7f)
                    : tile == 'W' ? stone : tile == '~' || tile == '=' ? water
                    : tile == '_' ? yard : tile == '.' ? ochre : sand;
                FillNoise(c, x * TilePx, (height - 1 - y) * TilePx, ground, 0.16f, seed);
            }

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int left = x * TilePx, bottom = (height - 1 - y) * TilePx;
                float cx = left + 16, cy = bottom + 16;
                char tile = terrain[x, y];
                var rng = new System.Random(seed + x * 131 + y * 977);
                if (!IsSolid(tile)) PaintEdgeShadows(c, terrain, x, y, width, height);

                switch (tile)
                {
                    case '#':
                        bool cliffEdge = (x > 0 && terrain[x - 1, y] != '#')
                            || (x + 1 < width && terrain[x + 1, y] != '#')
                            || (y > 0 && terrain[x, y - 1] != '#')
                            || (y + 1 < height && terrain[x, y + 1] != '#');
                        if (cliffEdge)
                        {
                            c.Ellipse(cx, cy, 17, 13, Hex("#765649"), rng.Next(-28, 29));
                            c.Capsule(cx - 10, cy + 4, cx + 8, cy + 8, 0.9f, Hex("#927061"));
                        }
                        else if (rng.NextDouble() < 0.45)
                            c.Capsule(left, cy, left + 30, cy + rng.Next(-12, 13), 1.4f, Hex("#5D4543"));
                        break;
                    case 'W':
                        var ridge = Color.Lerp(Hex("#6E4F45"), Hex("#866050"), Hash(x, y, seed));
                        c.RoundRect(left + 1, bottom + 4, left + 31, bottom + 30, 5, ridge);
                        c.Capsule(left + 5, bottom + 26, left + 26, bottom + 27, 1, Hex("#967061"));
                        c.Capsule(cx - 3, cy + 8, cx + 3, cy - 7, 0.75f, stone);
                        break;
                    case 'K':
                        PaintMandacaru(c, cx, cy);
                        break;
                    case 'T':
                        PaintTree(c, left, bottom, rng);
                        break;
                    case '~':
                        c.Capsule(cx - 11, cy + rng.Next(-8, 8), cx + 8, cy, 0.8f, Hex("#387E81"));
                        break;
                    case '=':
                        PaintBridge(c, terrain, x, y, left, bottom, rng);
                        break;
                    case '_':
                        c.Rect(left, bottom, left + TilePx, bottom + 1, Hex("#827367"));
                        c.Rect(left, bottom, left + 1, bottom + TilePx, Hex("#827367"));
                        break;
                    default:
                        for (int i = 0; i < 5; i++)
                        {
                            float px = left + rng.Next(4, 28), py = bottom + rng.Next(4, 28);
                            c.Capsule(px, py, px + rng.Next(1, 5), py + 1, 0.7f,
                                tile == ',' ? Hex("#777045") : Hex("#D8A473"));
                        }
                        break;
                }
            }

            c.Multiply(new Color(1f, 0.87f, 0.89f));
            if (layout != null) PaintLandmarks(c, terrain, layout);
            return c.ToTexture(FilterMode.Bilinear);
        }

        /// <summary>Ponte de tábuas com corrimão; as tábuas ficam atravessadas no sentido da travessia.</summary>
        static void PaintBridge(PixelCanvas c, char[,] terrain, int x, int y, int left, int bottom, System.Random rng)
        {
            int width = terrain.GetLength(0), height = terrain.GetLength(1);
            bool Water(int tx, int ty) => tx < 0 || ty < 0 || tx >= width || ty >= height || terrain[tx, ty] == '~';
            bool vertical = Water(x - 1, y) && Water(x + 1, y);
            var plank = Hex("#8B5E34");
            var rail = Hex("#5A3A1E");
            for (int p = 0; p < TilePx; p += 6)
            {
                float shade = 0.85f + (float)rng.NextDouble() * 0.25f;
                var col = new Color(plank.r * shade, plank.g * shade, plank.b * shade);
                if (vertical) c.Rect(left + 3, bottom + p + 1, left + TilePx - 3, bottom + p + 5, col);
                else c.Rect(left + p + 1, bottom + 3, left + p + 5, bottom + TilePx - 3, col);
            }
            if (vertical)
            {
                c.Rect(left, bottom, left + 3, bottom + TilePx, rail);
                c.Rect(left + TilePx - 3, bottom, left + TilePx, bottom + TilePx, rail);
            }
            else
            {
                c.Rect(left, bottom, left + TilePx, bottom + 3, rail);
                c.Rect(left, bottom + TilePx - 3, left + TilePx, bottom + TilePx, rail);
            }
        }

        static void PaintMandacaru(PixelCanvas c, float x, float y)
        {
            var green = Hex("#4E794C");
            c.Ellipse(x + 2, y - 8, 12, 5, new Color(0.14f, 0.1f, 0.08f, 0.5f));
            c.Capsule(x, y - 9, x, y + 11, 3.5f, green);
            c.Capsule(x - 8, y - 1, x - 8, y + 6, 2.4f, green);
            c.Capsule(x - 8, y - 1, x, y - 1, 2.3f, green);
            c.Capsule(x + 8, y + 1, x + 8, y + 8, 2.4f, green);
            c.Capsule(x, y + 1, x + 8, y + 1, 2.3f, green);
            c.Capsule(x - 1, y - 7, x - 1, y + 10, 0.8f, Hex("#83A867"));
            c.Circle(x, y + 13, 1.9f, Hex("#F2BF80"));
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
                    bool Wet(char t) => t == '~' || t == '=';
                    if (y > 0 && !Wet(terrain[x, y - 1]))
                        c.Capsule(left, bottom + 29, left + TilePx, bottom + 29, 1.4f, bank);
                    if (y < height - 1 && !Wet(terrain[x, y + 1]))
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

        /// <summary>Clareira de caatinga cercada de rocha (ou o lajedo de pedra, na luta contra o chefe).</summary>
        public static Texture2D PaintArena(bool lajedo, int seed)
        {
            const int w = 30, h = 17;
            var grid = new char[w, h];
            var rng = new System.Random(seed);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int border = 2 + (rng.NextDouble() < 0.35 ? 1 : 0);
                bool edge = x < border || x >= w - border || y < border;
                if (lajedo) grid[x, y] = edge ? 'W' : '_';
                else grid[x, y] = edge ? '#' : (rng.NextDouble() < 0.25 ? '.' : ',');
            }
            if (!lajedo)
                for (int y = 2; y < h; y++)
                for (int x = 12; x <= 17; x++)
                    grid[x, y] = '.';
            return PaintSertao(grid, seed);
        }
    }
}
