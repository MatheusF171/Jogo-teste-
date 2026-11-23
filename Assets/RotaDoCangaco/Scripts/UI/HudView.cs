using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// HUD da exploração (RF03): vida, nível/XP, vidas, pontuação, objetivo e minimapa com tudo visível.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        GameManager game;
        RectTransform root;
        Text nameText, scoreText, title, objectiveText, progressText, hoverText, toastText, overviewText, deckInfoText;
        UIBar hpBar, xpBar;
        readonly List<Image> hearts = new List<Image>();
        GameObject hoverPanel;
        HeroAnimator portraitAnim;
        RawImage minimap;
        RectTransform markerLayer;
        Image playerMarker;
        float toastTime;

        const float MiniTile = 8f;

        public static HudView Create(Transform parent, GameManager game)
        {
            var rt = UIFactory.Rect(parent, "HUD").Stretch();
            var hud = rt.gameObject.AddComponent<HudView>();
            hud.game = game;
            hud.Build(rt);
            return hud;
        }

        void Build(RectTransform rt)
        {
            root = rt;

            // Painel do jogador (canto superior esquerdo)
            var panel = UIFactory.Panel(root, "Jogador", UIFactory.PanelColor);
            panel.raycastTarget = false;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(470, 200);
            prt.anchoredPosition = new Vector2(250, -115);

            var portraitBg = UIFactory.Image(panel.transform, "FundoRetrato", TextureFactory.Circle, new Color(0.2f, 0.3f, 0.6f), false);
            portraitBg.rectTransform.Place(new Vector2(0f, 1f), new Vector2(70, -70), new Vector2(110, 110));
            var portrait = UIFactory.Image(portraitBg.transform, "Retrato", HeroAnimator.Frame(3));
            portrait.rectTransform.Stretch(8, 8, 8, 8);
            portraitAnim = HeroAnimator.Attach(portrait.gameObject);

            nameText = UIFactory.Text(panel.transform, "Nome", "", 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            nameText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(285, -30), new Vector2(320, 30));

            hpBar = new UIBar(panel.transform, "Vida", new Color(0.85f, 0.2f, 0.25f), 18);
            hpBar.Root.Place(new Vector2(0f, 1f), new Vector2(285, -66), new Vector2(320, 28));
            xpBar = new UIBar(panel.transform, "XP", new Color(0.35f, 0.75f, 1f), 15);
            xpBar.Root.Place(new Vector2(0f, 1f), new Vector2(285, -98), new Vector2(320, 22));

            for (int i = 0; i < GameConfig.StartLives; i++)
            {
                var heart = UIFactory.Image(panel.transform, "Vida" + i, TextureFactory.Heart);
                heart.rectTransform.Place(new Vector2(0f, 1f), new Vector2(148 + i * 40, -138), new Vector2(36, 36));
                hearts.Add(heart);
            }
            scoreText = UIFactory.Text(panel.transform, "Pontos", "", 21, UIFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            scoreText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(375, -138), new Vector2(200, 30));

            deckInfoText = UIFactory.Text(panel.transform, "InfoBaralho", "", 15, new Color(0.8f, 0.8f, 0.85f), TextAnchor.MiddleLeft);
            deckInfoText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(235, -178), new Vector2(420, 24));

            // Título e objetivo (topo central)
            title = UIFactory.Text(root, "Fase", "", 30, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(900, 80));
            objectiveText = UIFactory.Text(root, "Objetivo", "", 19, UIFactory.Accent, TextAnchor.UpperCenter);
            objectiveText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(900, 30));

            toastText = UIFactory.Text(root, "Aviso", "", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(1100, 50));

            // Minimapa (canto superior direito)
            var mapFrame = UIFactory.Panel(root, "Minimapa", UIFactory.PanelColor);
            mapFrame.raycastTarget = false;
            var mrt = mapFrame.rectTransform;
            float mw = MapData.Width * MiniTile, mh = MapData.Height * MiniTile;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.sizeDelta = new Vector2(mw + 20, mh + 92);
            mrt.anchoredPosition = new Vector2(-(mw + 20) * 0.5f - 15, -(mh + 92) * 0.5f - 15);
            var mapLabel = UIFactory.Text(mapFrame.transform, "Rotulo", "MAPA  <size=15>(M: ver inteiro)</size>", 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            mapLabel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(mw, 26));
            var mapImg = UIFactory.Rect(mapFrame.transform, "Imagem");
            mapImg.Place(new Vector2(0.5f, 1f), new Vector2(0, -40 - mh * 0.5f), new Vector2(mw, mh));
            minimap = mapImg.gameObject.AddComponent<RawImage>();
            minimap.raycastTarget = false;
            markerLayer = UIFactory.Rect(mapImg, "Marcadores").Stretch();
            progressText = UIFactory.Text(mapFrame.transform, "Progresso", "", 15, Color.white, TextAnchor.MiddleLeft);
            progressText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(mw, 44));

            // Dica do que está sob o mouse
            hoverPanel = UIFactory.Panel(root, "DicaMouse", new Color(0.04f, 0.03f, 0.07f, 0.9f)).gameObject;
            hoverPanel.GetComponent<Image>().raycastTarget = false;
            ((RectTransform)hoverPanel.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1000, 60));
            hoverText = UIFactory.Text(hoverPanel.transform, "Texto", "", 20, Color.white);
            hoverText.rectTransform.Stretch(14, 4, 14, 4);
            hoverPanel.SetActive(false);

            var controls = UIFactory.Text(root, "Controles",
                "WASD/Setas ou clique: andar   ·   B: baralho   ·   M: mapa inteiro   ·   Esc: pausa", 18,
                new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            controls.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1200, 30));

            var deckButton = UIFactory.Button(root, "BotaoBaralho", "Baralho (B)", () => game.ShowDeck(), UIFactory.ButtonColor, 22);
            ((RectTransform)deckButton.transform).Place(new Vector2(1f, 0f), new Vector2(-120, 60), new Vector2(200, 60));

            overviewText = UIFactory.Text(root, "VisaoGeral", "Mapa inteiro — pressione M para voltar", 22, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            overviewText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(800, 30));
            overviewText.gameObject.SetActive(false);
        }

        public void Bind(MapController map)
        {
            minimap.texture = map.MapTexture;
            title.text = $"{map.Phase.Title}\n<size=18>{map.Phase.Region}</size>";
            SetOverview(false);
            SetHover(null);
            map.Changed -= Refresh;
            map.Changed += Refresh;
            Refresh();
        }

        public void Refresh()
        {
            var p = game.Player;
            var map = game.Map;
            if (p == null || map == null || !map.IsBuilt) return;

            nameText.text = $"{p.Name}  <size=18>Nv.{p.Level}</size>";
            hpBar.Set(p.Hp, p.MaxHp, $"Vida {p.Hp}/{p.MaxHp}");
            xpBar.Set(p.Xp, p.XpToNext, $"XP {p.Xp}/{p.XpToNext}");
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].color = i < p.Lives ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
            scoreText.text = $"{p.Score} pts";
            string energy = p.FatEnergyPenalty > 0 ? $"{p.EffectiveEnergy} <color=#FF9A9A>(-{p.FatEnergyPenalty})</color>" : p.EffectiveEnergy.ToString();
            string peso = p.Peso > 0 ? $"  ·  Peso {p.Peso}: <color=#{ColorUtility.ToHtmlStringRGB(CuscuzMenu.PesoColor(p.Peso))}>{p.FatLabel}</color>" : "";
            deckInfoText.text = $"Baralho: {p.Deck.Count}  ·  Energia: {energy}{peso}";
            portraitAnim.SetFatStage(p.FatStage);

            bool sertao = map.Phase == SertaoMap.Phase;
            if (p.BossDefeated)
                objectiveText.text = sertao ? "Objetivo: chegue à saída do lajedo"
                    : $"Objetivo: chegue à porteira para o Sertão (meta: {GameConfig.MinScoreToWin} pts)";
            else
                objectiveText.text = sertao ? "Objetivo: derrote o Saci na Pedra do Redemoinho"
                    : "Objetivo: derrote a Mula Sem Cabeça no velho engenho";

            int groupsLeft = map.All<EncounterEntity>().Count(e => !(e is BossEntity));
            progressText.text = $"Monstros derrotados: {p.MonstersDefeated}  ·  Grupos restantes: {groupsLeft}\n" +
                                $"Chefe: {(p.BossDefeated ? "<color=#8CFF8C>derrotado</color>" : "<color=#FF7AA8>aguardando</color>")}";

            RebuildMarkers(map);
        }

        void RebuildMarkers(MapController map)
        {
            foreach (Transform t in markerLayer) Destroy(t.gameObject);
            foreach (var e in map.Entities)
            {
                if (e.Removed || !e.Visible) continue;
                float size = e is BossEntity ? 16f : e is ExitEntity ? 14f : 8f;
                AddMarker(map, e.Pos, e.MinimapColor, size);
            }
            playerMarker = AddMarker(map, map.PlayerPos, Color.white, 11f);
        }

        Image AddMarker(MapController map, Vector2Int tile, Color color, float size)
        {
            var img = UIFactory.Image(markerLayer, "Marcador", TextureFactory.Circle, color, false);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.anchoredPosition = new Vector2((tile.x + 0.5f) * MiniTile, (map.Height - 1 - tile.y + 0.5f) * MiniTile);
            return img;
        }

        public void SetHover(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (hoverPanel.activeSelf != show) hoverPanel.SetActive(show);
            if (show && hoverText.text != text) hoverText.text = text;
        }

        public void SetOverview(bool on) => overviewText.gameObject.SetActive(on);

        public void Toast(string message, float duration = 2.5f)
        {
            toastText.text = message;
            toastTime = duration;
        }

        void Update()
        {
            if (toastTime > 0f)
            {
                toastTime -= Time.deltaTime;
                var c = toastText.color;
                c.a = Mathf.Clamp01(toastTime / 0.5f);
                toastText.color = c;
            }
            else if (toastText.text.Length > 0)
            {
                toastText.text = "";
            }

            if (playerMarker != null)
            {
                float s = 1f + Mathf.Sin(Time.time * 6f) * 0.25f;
                playerMarker.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
