using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Sprites gerados por código: peças de interface, ícones das cartas e objetos do mapa.
    /// Assim o protótipo não depende de arte extra além dos personagens fornecidos.
    /// </summary>
    public static class TextureFactory
    {
        static Sprite roundedRect, roundedRectSmall, circle, ring, glow, shadow, heart;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect => roundedRect != null ? roundedRect : (roundedRect = MakeRounded(64, 18, 22));
        public static Sprite RoundedRectSmall => roundedRectSmall != null ? roundedRectSmall : (roundedRectSmall = MakeRounded(32, 8, 10));

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                var c = new PixelCanvas(128, 128);
                c.Circle(64, 64, 62, Color.white);
                return circle = c.ToSprite();
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                var c = new PixelCanvas(128, 128);
                c.Ring(64, 64, 56, 7, Color.white);
                return ring = c.ToSprite();
            }
        }

        public static Sprite Glow
        {
            get
            {
                if (glow != null) return glow;
                var c = new PixelCanvas(128, 128);
                c.Glow(64, 64, 64, Color.white);
                return glow = c.ToSprite();
            }
        }

        public static Sprite Shadow
        {
            get
            {
                if (shadow != null) return shadow;
                var c = new PixelCanvas(128, 64);
                for (int i = 0; i < 6; i++) c.Ellipse(64, 32, 60 - i * 8, 28 - i * 4, new Color(0, 0, 0, 0.12f));
                return shadow = c.ToSprite();
            }
        }

        public static Sprite Heart
        {
            get
            {
                if (heart != null) return heart;
                var c = new PixelCanvas(64, 64);
                var red = new Color(0.92f, 0.2f, 0.3f);
                c.Circle(22, 40, 15, red);
                c.Circle(42, 40, 15, red);
                c.Triangle(new Vector2(8.5f, 34), new Vector2(55.5f, 34), new Vector2(32, 6), red);
                c.Circle(19, 44, 5, new Color(1, 1, 1, 0.55f));
                return heart = c.ToSprite();
            }
        }

        static Sprite MakeRounded(int size, float radius, float border)
        {
            var c = new PixelCanvas(size, size);
            c.RoundRect(0, 0, size, size, radius, Color.white);
            return c.ToSprite(100f, border);
        }

        // ------------------------------------------------------------------ Ícones das cartas

        public static Sprite CardIcon(CardData card)
        {
            if (card.Icon == "hero") return SpriteLibrary.Get("heroi");
            if (card.Icon == "mula") return SpriteLibrary.Get("mula_retrato");
            string key = "icon_" + card.Icon + "_" + ColorUtility.ToHtmlStringRGB(card.Tint);
            if (cache.TryGetValue(key, out var s)) return s;
            s = DrawIcon(card.Icon, card.Tint).ToSprite();
            cache[key] = s;
            return s;
        }

        static PixelCanvas DrawIcon(string icon, Color tint)
        {
            var c = new PixelCanvas(128, 128);
            Color dark = Color.Lerp(tint, Color.black, 0.45f);
            Color light = Color.Lerp(tint, Color.white, 0.55f);
            Color white = Color.white;
            Color ink = new Color(0.08f, 0.06f, 0.1f);

            switch (icon)
            {
                case "orb":
                    c.Glow(64, 64, 62, new Color(tint.r, tint.g, tint.b, 0.7f));
                    c.Circle(64, 64, 30, dark);
                    c.Circle(64, 64, 25, tint);
                    c.Circle(55, 74, 10, light);
                    c.Circle(100, 100, 4, white); c.Circle(26, 96, 3, white); c.Circle(96, 30, 3, white);
                    break;

                case "leaf":
                    c.Capsule(64, 12, 64, 112, 3, Color.Lerp(dark, ink, 0.3f));
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 30 + i * 22;
                        c.Ellipse(44, y + 6, 22, 9, i % 2 == 0 ? tint : light, 35);
                        c.Ellipse(84, y + 6, 22, 9, i % 2 == 0 ? light : tint, -35);
                    }
                    c.Ellipse(64, 114, 10, 16, tint);
                    break;

                case "cauldron":
                    c.Circle(40, 22, 7, new Color(1f, 0.55f, 0.15f));
                    c.Circle(64, 18, 9, new Color(1f, 0.75f, 0.2f));
                    c.Circle(88, 22, 7, new Color(1f, 0.55f, 0.15f));
                    c.Ellipse(64, 54, 44, 34, new Color(0.18f, 0.18f, 0.22f));
                    c.Ellipse(52, 62, 14, 8, new Color(1, 1, 1, 0.12f));
                    c.RoundRect(16, 76, 112, 90, 6, new Color(0.28f, 0.28f, 0.33f));
                    c.Ellipse(64, 86, 42, 7, tint);
                    c.Circle(48, 100, 9, light); c.Circle(72, 108, 6, tint); c.Circle(86, 98, 5, light);
                    break;

                case "eye":
                    c.Glow(64, 64, 62, new Color(tint.r, tint.g, tint.b, 0.45f));
                    c.Ellipse(64, 64, 54, 30, ink);
                    c.Ellipse(64, 64, 50, 26, new Color(0.95f, 0.93f, 0.88f));
                    c.Circle(64, 64, 22, tint);
                    c.Circle(64, 64, 10, ink);
                    c.Circle(57, 71, 5, white);
                    for (int i = -2; i <= 2; i++) c.Capsule(64 + i * 18, 92 - Mathf.Abs(i) * 5, 64 + i * 22, 106 - Mathf.Abs(i) * 5, 2.5f, ink);
                    break;

                case "broom":
                    c.Capsule(28, 108, 76, 44, 5, new Color(0.45f, 0.28f, 0.14f));
                    c.Triangle(new Vector2(64, 54), new Vector2(84, 70), new Vector2(118, 14), tint);
                    c.Triangle(new Vector2(64, 54), new Vector2(118, 14), new Vector2(92, 8), Color.Lerp(tint, dark, 0.4f));
                    c.Capsule(62, 54, 82, 70, 5, new Color(0.7f, 0.15f, 0.2f));
                    c.Circle(24, 30, 4, white); c.Circle(36, 18, 3, white); c.Circle(14, 48, 2.5f, white);
                    break;

                case "flame":
                    c.Glow(64, 54, 60, new Color(1f, 0.5f, 0.1f, 0.5f));
                    c.Circle(64, 46, 32, tint);
                    c.Triangle(new Vector2(33, 54), new Vector2(95, 54), new Vector2(64, 122), tint);
                    c.Circle(64, 40, 18, new Color(1f, 0.85f, 0.3f));
                    c.Triangle(new Vector2(47, 44), new Vector2(81, 44), new Vector2(64, 92), new Color(1f, 0.85f, 0.3f));
                    c.Circle(64, 36, 8, new Color(1f, 1f, 0.85f));
                    break;

                case "star":
                    c.Glow(64, 64, 60, new Color(tint.r, tint.g, tint.b, 0.6f));
                    c.Triangle(new Vector2(64, 120), new Vector2(52, 64), new Vector2(76, 64), tint);
                    c.Triangle(new Vector2(64, 8), new Vector2(52, 64), new Vector2(76, 64), tint);
                    c.Triangle(new Vector2(8, 64), new Vector2(64, 52), new Vector2(64, 76), tint);
                    c.Triangle(new Vector2(120, 64), new Vector2(64, 52), new Vector2(64, 76), tint);
                    c.Circle(64, 64, 12, white);
                    c.Circle(100, 100, 5, light); c.Circle(28, 28, 4, light);
                    break;

                case "shield":
                    c.RoundRect(24, 58, 104, 112, 12, ink);
                    c.Triangle(new Vector2(24, 64), new Vector2(104, 64), new Vector2(64, 8), ink);
                    c.RoundRect(30, 60, 98, 106, 10, tint);
                    c.Triangle(new Vector2(30, 66), new Vector2(98, 66), new Vector2(64, 17), tint);
                    c.Circle(64, 72, 14, light);
                    c.Circle(64, 72, 7, dark);
                    break;

                case "cup":
                    c.Ring(96, 62, 14, 7, Color.Lerp(tint, Color.white, 0.2f));
                    c.RoundRect(34, 22, 92, 92, 10, Color.Lerp(tint, Color.white, 0.2f));
                    c.Ellipse(63, 86, 26, 6, dark);
                    c.Capsule(50, 100, 46, 120, 3, new Color(1, 1, 1, 0.6f));
                    c.Capsule(66, 100, 70, 122, 3, new Color(1, 1, 1, 0.6f));
                    c.Capsule(80, 100, 78, 116, 3, new Color(1, 1, 1, 0.6f));
                    c.RoundRect(26, 14, 100, 24, 5, new Color(0.85f, 0.85f, 0.85f));
                    break;

                case "cat":
                    var fur = new Color(0.16f, 0.15f, 0.2f);
                    c.Triangle(new Vector2(30, 68), new Vector2(40, 116), new Vector2(62, 88), fur);
                    c.Triangle(new Vector2(98, 68), new Vector2(88, 116), new Vector2(66, 88), fur);
                    c.Circle(64, 58, 40, fur);
                    c.Ellipse(48, 64, 10, 7, new Color(1f, 0.85f, 0.2f));
                    c.Ellipse(80, 64, 10, 7, new Color(1f, 0.85f, 0.2f));
                    c.Ellipse(48, 64, 2.5f, 6, ink);
                    c.Ellipse(80, 64, 2.5f, 6, ink);
                    c.Triangle(new Vector2(58, 48), new Vector2(70, 48), new Vector2(64, 41), new Color(1f, 0.55f, 0.65f));
                    c.Capsule(20, 44, 44, 46, 1.2f, light); c.Capsule(20, 36, 44, 42, 1.2f, light);
                    c.Capsule(84, 46, 108, 44, 1.2f, light); c.Capsule(84, 42, 108, 36, 1.2f, light);
                    break;

                case "flask":
                    c.Circle(64, 46, 34, new Color(0.85f, 0.9f, 1f, 0.5f));
                    c.Circle(64, 46, 30, tint);
                    c.RoundRect(52, 70, 76, 102, 4, new Color(0.85f, 0.9f, 1f, 0.7f));
                    c.RoundRect(49, 98, 79, 114, 4, new Color(0.55f, 0.36f, 0.2f));
                    c.Circle(52, 56, 8, new Color(1, 1, 1, 0.6f));
                    c.Circle(72, 36, 5, light); c.Circle(60, 28, 3, light);
                    break;

                case "moon":
                    c.Glow(64, 64, 62, new Color(tint.r, tint.g, tint.b, 0.45f));
                    c.Circle(60, 64, 40, tint);
                    c.CutCircle(84, 78, 34);
                    c.Circle(98, 30, 4, white); c.Circle(104, 98, 3, white); c.Circle(22, 106, 3, white);
                    break;

                case "owl":
                    var body = Color.Lerp(tint, new Color(0.7f, 0.55f, 0.35f), 0.4f);
                    c.Ellipse(64, 52, 38, 46, body);
                    c.Circle(64, 74, 32, new Color(0.97f, 0.95f, 0.9f));
                    c.Circle(50, 76, 11, ink); c.Circle(78, 76, 11, ink);
                    c.Circle(47, 79, 3, white); c.Circle(75, 79, 3, white);
                    c.Triangle(new Vector2(59, 66), new Vector2(69, 66), new Vector2(64, 54), new Color(0.9f, 0.6f, 0.2f));
                    c.Ellipse(38, 40, 12, 22, Color.Lerp(body, ink, 0.3f), 20);
                    c.Ellipse(90, 40, 12, 22, Color.Lerp(body, ink, 0.3f), -20);
                    break;

                case "ring":
                    c.Glow(64, 64, 62, new Color(0.7f, 0.85f, 1f, 0.45f));
                    c.Ring(64, 64, 42, 10, tint);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI * 2f / 16f;
                        c.Circle(64 + Mathf.Cos(a) * 54, 64 + Mathf.Sin(a) * 54, 2.5f, white);
                    }
                    break;

                default:
                    c.Circle(64, 64, 40, tint);
                    break;
            }
            return c;
        }

        // ------------------------------------------------------------------ Objetos do mapa

        public static Sprite MapObject(string key)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var c = new PixelCanvas(96, 96);
            switch (key)
            {
                case "chest_card": DrawChest(c, new Color(1f, 0.82f, 0.3f)); break;
                case "chest_relic": DrawChest(c, new Color(0.75f, 0.45f, 1f)); break;
                case "chest_heal": DrawChest(c, new Color(0.45f, 0.95f, 0.5f)); break;
                case "chest_open": DrawChest(c, new Color(0.45f, 0.45f, 0.45f), true); break;
                case "campfire": DrawCampfire(c, true); break;
                case "campfire_out": DrawCampfire(c, false); break;
                case "cordel": DrawCordel(c, false); break;
                case "cordel_read": DrawCordel(c, true); break;
                case "portal": DrawPortal(c); break;
                case "cuscuz": DrawCuscuzPot(c, true); break;
                case "cuscuz_empty": DrawCuscuzPot(c, false); break;
            }
            s = c.ToSprite(96f);
            cache[key] = s;
            return s;
        }

        static readonly Color CuscuzYellow = new Color(1f, 0.8f, 0.28f);
        static readonly Color CuscuzDark = new Color(0.85f, 0.6f, 0.15f);

        /// <summary>Cuscuzeira de alumínio com o cuscuz amarelinho saindo por cima.</summary>
        static void DrawCuscuzPot(PixelCanvas c, bool full)
        {
            var metal = new Color(0.72f, 0.74f, 0.8f);
            var metalDark = new Color(0.45f, 0.47f, 0.53f);
            c.Ellipse(48, 10, 32, 7, new Color(0, 0, 0, 0.35f));
            c.Capsule(14, 38, 22, 40, 3.5f, metalDark);
            c.Capsule(74, 40, 82, 38, 3.5f, metalDark);
            c.RoundRect(20, 10, 76, 50, 8, metalDark);
            c.RoundRect(22, 12, 74, 48, 6, metal);
            c.Rect(22, 30, 74, 33, metalDark);
            c.RoundRect(26, 14, 34, 46, 3, new Color(1, 1, 1, 0.35f));
            c.RoundRect(22, 46, 74, 58, 4, metalDark);
            c.RoundRect(24, 48, 72, 56, 3, Color.Lerp(metal, Color.white, 0.2f));
            if (!full)
            {
                c.Ellipse(48, 56, 22, 4, new Color(0.15f, 0.15f, 0.18f));
                return;
            }
            c.Ellipse(48, 62, 24, 16, CuscuzDark);
            c.Ellipse(48, 63, 22, 14, CuscuzYellow);
            var rng = new System.Random(5);
            for (int i = 0; i < 26; i++)
                c.Circle(30 + (float)rng.NextDouble() * 36, 52 + (float)rng.NextDouble() * 22, 1.1f, CuscuzDark);
            c.Ellipse(42, 70, 8, 4, new Color(1f, 0.95f, 0.7f, 0.8f));
            c.Capsule(40, 80, 36, 92, 2.2f, new Color(1, 1, 1, 0.45f));
            c.Capsule(50, 80, 54, 94, 2.2f, new Color(1, 1, 1, 0.45f));
            c.Capsule(60, 78, 58, 90, 2.2f, new Color(1, 1, 1, 0.45f));
        }

        /// <summary>Prato de cuscuz com o acompanhamento de cada opção da panela.</summary>
        public static Sprite CuscuzPlate(string id)
        {
            string key = "plate_" + id;
            if (cache.TryGetValue(key, out var s)) return s;
            var c = new PixelCanvas(128, 128);
            c.Ellipse(64, 34, 58, 22, new Color(0.55f, 0.55f, 0.6f));
            c.Ellipse(64, 36, 56, 20, new Color(0.95f, 0.95f, 0.97f));
            c.Ellipse(64, 38, 40, 12, new Color(0.85f, 0.85f, 0.9f));
            c.Ellipse(64, 58, 40, 30, CuscuzDark);
            c.Ellipse(64, 60, 38, 28, CuscuzYellow);
            var rng = new System.Random(id.GetHashCode());
            for (int i = 0; i < 40; i++)
                c.Circle(32 + (float)rng.NextDouble() * 64, 40 + (float)rng.NextDouble() * 42, 1.4f, CuscuzDark);
            c.Ellipse(52, 72, 12, 6, new Color(1f, 0.95f, 0.7f, 0.8f));

            switch (id)
            {
                case "ovo":
                    c.Ellipse(70, 80, 26, 14, new Color(0.98f, 0.98f, 0.95f));
                    c.Circle(73, 82, 9, new Color(1f, 0.6f, 0.1f));
                    c.Circle(70, 85, 3, new Color(1f, 0.85f, 0.5f));
                    break;
                case "charque":
                    var meat = new Color(0.5f, 0.15f, 0.1f);
                    var meatLight = new Color(0.68f, 0.28f, 0.18f);
                    c.Capsule(42, 76, 66, 88, 4, meat);
                    c.Capsule(60, 82, 88, 74, 4, meatLight);
                    c.Capsule(50, 66, 80, 70, 3.5f, meat);
                    c.Capsule(28, 46, 44, 54, 3.5f, meatLight);
                    break;
                case "leite":
                    c.Ellipse(64, 40, 46, 12, new Color(1f, 1f, 1f, 0.85f));
                    c.Capsule(56, 86, 54, 60, 4, new Color(1f, 1f, 1f, 0.9f));
                    c.Capsule(70, 84, 74, 58, 4, new Color(1f, 1f, 1f, 0.9f));
                    c.Ellipse(64, 86, 16, 6, Color.white);
                    break;
                case "manteiga":
                    var gold = new Color(1f, 0.68f, 0.1f, 0.9f);
                    c.Ellipse(64, 84, 22, 8, gold);
                    c.Capsule(46, 82, 42, 58, 3, gold);
                    c.Capsule(80, 82, 86, 62, 3, gold);
                    c.Capsule(62, 80, 62, 54, 3, gold);
                    c.Glow(64, 84, 30, new Color(1f, 0.9f, 0.4f, 0.5f));
                    break;
            }
            c.Capsule(50, 96, 46, 116, 2.5f, new Color(1, 1, 1, 0.4f));
            c.Capsule(66, 98, 70, 120, 2.5f, new Color(1, 1, 1, 0.4f));
            c.Capsule(80, 96, 78, 112, 2.5f, new Color(1, 1, 1, 0.4f));
            s = c.ToSprite();
            cache[key] = s;
            return s;
        }

        static void DrawChest(PixelCanvas c, Color band, bool open = false)
        {
            var wood = new Color(0.5f, 0.3f, 0.15f);
            var woodDark = new Color(0.32f, 0.18f, 0.09f);
            c.Ellipse(48, 16, 34, 8, new Color(0, 0, 0, 0.35f));
            c.RoundRect(14, 16, 82, 56, 6, woodDark);
            c.RoundRect(17, 19, 79, 53, 4, wood);
            if (open)
            {
                c.RoundRect(14, 56, 82, 80, 6, woodDark);
                c.RoundRect(18, 52, 78, 58, 2, new Color(0.08f, 0.05f, 0.03f));
                return;
            }
            c.RoundRect(12, 52, 84, 74, 10, woodDark);
            c.RoundRect(15, 55, 81, 71, 8, Color.Lerp(wood, Color.white, 0.1f));
            c.Rect(14, 50, 82, 56, band);
            c.Rect(26, 18, 32, 72, Color.Lerp(band, woodDark, 0.25f));
            c.Rect(64, 18, 70, 72, Color.Lerp(band, woodDark, 0.25f));
            c.RoundRect(42, 40, 54, 58, 3, band);
            c.Circle(48, 47, 2.5f, woodDark);
            c.Glow(48, 60, 46, new Color(band.r, band.g, band.b, 0.35f));
        }

        static void DrawCampfire(PixelCanvas c, bool lit)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Circle(48 + Mathf.Cos(a) * 26, 36 + Mathf.Sin(a) * 18, 6, new Color(0.45f, 0.43f, 0.4f));
            }
            c.Capsule(28, 28, 68, 44, 5, new Color(0.4f, 0.24f, 0.12f));
            c.Capsule(28, 44, 68, 28, 5, new Color(0.35f, 0.2f, 0.1f));
            if (!lit)
            {
                c.Circle(48, 38, 9, new Color(0.3f, 0.3f, 0.3f));
                c.Capsule(46, 50, 52, 70, 3, new Color(0.6f, 0.6f, 0.6f, 0.4f));
                return;
            }
            c.Glow(48, 46, 46, new Color(1f, 0.55f, 0.15f, 0.55f));
            c.Circle(48, 44, 14, new Color(1f, 0.45f, 0.1f));
            c.Triangle(new Vector2(34, 46), new Vector2(62, 46), new Vector2(48, 86), new Color(1f, 0.45f, 0.1f));
            c.Circle(48, 42, 8, new Color(1f, 0.85f, 0.25f));
            c.Triangle(new Vector2(40, 44), new Vector2(56, 44), new Vector2(48, 68), new Color(1f, 0.85f, 0.25f));
        }

        static void DrawCordel(PixelCanvas c, bool read)
        {
            c.Capsule(6, 80, 90, 80, 1.5f, new Color(0.85f, 0.8f, 0.65f));
            var paper = read ? new Color(0.55f, 0.52f, 0.46f) : new Color(0.96f, 0.9f, 0.74f);
            c.RoundRect(24, 14, 72, 80, 3, new Color(0.25f, 0.2f, 0.15f));
            c.RoundRect(26, 16, 70, 78, 2, paper);
            // xilogravura simplificada
            c.Rect(32, 42, 64, 70, new Color(0.15f, 0.12f, 0.1f));
            c.Circle(48, 62, 5, paper);
            c.Triangle(new Vector2(36, 44), new Vector2(60, 44), new Vector2(48, 58), paper);
            c.Rect(32, 30, 64, 33, new Color(0.15f, 0.12f, 0.1f));
            c.Rect(32, 22, 56, 25, new Color(0.15f, 0.12f, 0.1f));
            c.Capsule(40, 78, 48, 86, 2, new Color(0.35f, 0.3f, 0.25f));
            c.Capsule(56, 78, 48, 86, 2, new Color(0.35f, 0.3f, 0.25f));
            if (!read) c.Glow(48, 50, 46, new Color(1f, 0.95f, 0.6f, 0.3f));
        }

        static void DrawPortal(PixelCanvas c)
        {
            c.Glow(48, 48, 48, new Color(1f, 0.85f, 0.3f, 0.8f));
            c.Ring(48, 48, 30, 6, new Color(1f, 0.85f, 0.35f));
            c.Ring(48, 48, 20, 4, new Color(1f, 0.95f, 0.7f));
            c.Circle(48, 48, 10, Color.white);
        }
    }
}
