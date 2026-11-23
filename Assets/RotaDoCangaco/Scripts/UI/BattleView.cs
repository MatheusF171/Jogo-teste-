using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// Tela de batalha vista de cima: inimigos em colunas que descem em direção ao herói conforme
    /// a distância (Longe → Médio → Perto), mão de cartas embaixo, energia, combo e registro.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        public event Action<CardData> CardClicked;
        public event Action<EnemyCombatant> EnemyClicked;
        public event Action EndTurnClicked;

        static readonly float[] LaneY = { 510f, 682f, 855f };
        static readonly float[] LaneScale = { 1f, 0.86f, 0.72f };
        static readonly float[] BossLaneY = { 578f, 700f, 820f };
        static readonly float[] BossLaneScale = { 0.9f, 0.8f, 0.72f };
        static readonly Vector2 HeroPos = new Vector2(210f, 455f);
        const float ArenaCenterX = 80f;
        const float SlotSpacing = 300f;

        RectTransform root, enemyLayer, handLayer, fxLayer, heroRt;
        RawImage background;
        Texture2D fieldTexture, engenhoTexture, sertaoTexture, lajedoTexture;
        Text titleText, turnText, comboText, comboHint, promptText, bannerText, logText, tooltipText;
        Text playerName, blockText, statusText, inPlayText, energyText, drawText, discardText, fatText;
        GameObject tooltip, blockBadge;
        Image heroImage;
        HeroAnimator heroAnim;
        UIBar hpBar;
        Button endTurnButton;

        readonly List<string> logLines = new List<string>();
        readonly Dictionary<EnemyCombatant, EnemyView> enemyViews = new Dictionary<EnemyCombatant, EnemyView>();
        readonly Dictionary<EnemyCombatant, float> slots = new Dictionary<EnemyCombatant, float>();
        readonly List<CardView> handViews = new List<CardView>();

        BattleSystem battle;
        CardData targeting;
        float bannerTime;
        float heroShake;
        bool busy;

        public static string Fmt(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        public static BattleView Create(Transform parent)
        {
            var rt = UIFactory.Rect(parent, "TelaBatalha").Stretch();
            var view = rt.gameObject.AddComponent<BattleView>();
            view.Build(rt);
            return view;
        }

        // ------------------------------------------------------------------ Construção

        void Build(RectTransform rootRt)
        {
            root = rootRt;
            var bg = UIFactory.Rect(root, "Arena").Stretch();
            background = bg.gameObject.AddComponent<RawImage>();
            background.color = new Color(0.85f, 0.85f, 0.9f);

            // Linhas de distância
            string[] laneNames = { "PERTO", "MÉDIO", "LONGE" };
            for (int i = 0; i < 3; i++)
            {
                var label = UIFactory.Text(root, "Faixa" + i, laneNames[i], 18, new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleLeft, FontStyle.Bold);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0f);
                label.rectTransform.sizeDelta = new Vector2(120, 30);
                label.rectTransform.anchoredPosition = new Vector2(480, LaneY[i] - 70);
                if (i < 2)
                {
                    var line = UIFactory.Panel(root, "Linha" + i, new Color(1f, 1f, 1f, 0.08f), TextureFactory.RoundedRectSmall);
                    line.raycastTarget = false;
                    var lrt = line.rectTransform;
                    lrt.anchorMin = new Vector2(0f, 0f);
                    lrt.anchorMax = new Vector2(1f, 0f);
                    lrt.offsetMin = new Vector2(430, (LaneY[i] + LaneY[i + 1]) * 0.5f - 2);
                    lrt.offsetMax = new Vector2(-40, (LaneY[i] + LaneY[i + 1]) * 0.5f + 2);
                }
            }

            enemyLayer = UIFactory.Rect(root, "Inimigos").Stretch();

            // Herói
            var heroShadow = UIFactory.Image(root, "SombraHeroi", TextureFactory.Shadow, Color.white, false);
            heroShadow.rectTransform.anchorMin = heroShadow.rectTransform.anchorMax = Vector2.zero;
            heroShadow.rectTransform.sizeDelta = new Vector2(160, 40);
            heroShadow.rectTransform.anchoredPosition = HeroPos + new Vector2(0, -78);
            heroImage = UIFactory.Image(root, "Heroi", SpriteLibrary.Get("heroi"));
            heroRt = heroImage.rectTransform;
            heroRt.anchorMin = heroRt.anchorMax = Vector2.zero;
            heroRt.sizeDelta = new Vector2(170, 170);
            heroRt.anchoredPosition = HeroPos;
            heroAnim = HeroAnimator.Attach(heroImage.gameObject);

            BuildTopBar();
            BuildPlayerPanel();
            BuildLog();
            BuildBottom();

            handLayer = UIFactory.Rect(root, "Mao");
            handLayer.anchorMin = handLayer.anchorMax = new Vector2(0.5f, 0f);
            handLayer.sizeDelta = new Vector2(10, 10);
            handLayer.anchoredPosition = Vector2.zero;

            fxLayer = UIFactory.Rect(root, "Efeitos").Stretch();

            tooltip = UIFactory.Panel(root, "Dica", new Color(0.05f, 0.04f, 0.08f, 0.94f)).gameObject;
            ((RectTransform)tooltip.transform).Place(new Vector2(1f, 0f), new Vector2(-175, 560), new Vector2(330, 250));
            tooltip.GetComponent<Image>().raycastTarget = false;
            tooltipText = UIFactory.Text(tooltip.transform, "Texto", "", 18, Color.white, TextAnchor.UpperLeft);
            tooltipText.rectTransform.Stretch(14, 10, 14, 10);
            tooltip.SetActive(false);

            promptText = UIFactory.Text(root, "Aviso", "", 22, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            promptText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(-175, 330), new Vector2(330, 100));

            bannerText = UIFactory.Text(root, "Faixa", "", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 120));
        }

        void BuildTopBar()
        {
            var bar = UIFactory.Panel(root, "BarraTopo", new Color(0.04f, 0.03f, 0.07f, 0.88f), TextureFactory.RoundedRectSmall);
            bar.raycastTarget = false;
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(10, -90);
            brt.offsetMax = new Vector2(-10, -6);

            titleText = UIFactory.Text(bar.transform, "Titulo", "", 25, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(20, 0);
            titleText.rectTransform.offsetMax = Vector2.zero;
            turnText = UIFactory.Text(bar.transform, "Turno", "", 26, UIFactory.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            turnText.rectTransform.anchorMin = new Vector2(0.8f, 0f);
            turnText.rectTransform.anchorMax = new Vector2(1f, 1f);
            turnText.rectTransform.offsetMin = Vector2.zero;
            turnText.rectTransform.offsetMax = new Vector2(-20, 0);

            var combo = UIFactory.Panel(bar.transform, "Combo", new Color(0.2f, 0.08f, 0.26f, 0.95f), TextureFactory.RoundedRectSmall);
            combo.raycastTarget = false;
            combo.rectTransform.anchorMin = new Vector2(0.3f, 0f);
            combo.rectTransform.anchorMax = new Vector2(0.8f, 1f);
            combo.rectTransform.offsetMin = new Vector2(10, 6);
            combo.rectTransform.offsetMax = new Vector2(-10, -6);
            comboText = UIFactory.Text(combo.transform, "Texto", "", 25, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            comboText.rectTransform.Stretch(10, 30, 10, 2);
            comboHint = UIFactory.Text(combo.transform, "Dica", "", 16, new Color(0.88f, 0.82f, 0.98f));
            comboHint.rectTransform.Stretch(10, 2, 10, 40);
        }

        void BuildPlayerPanel()
        {
            var panel = UIFactory.Panel(root, "PainelJogador", UIFactory.PanelColor);
            panel.raycastTarget = false;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.sizeDelta = new Vector2(390, 225);
            prt.anchoredPosition = new Vector2(210, 660);

            playerName = UIFactory.Text(panel.transform, "Nome", "", 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            playerName.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(350, 30));

            hpBar = new UIBar(panel.transform, "Vida", new Color(0.85f, 0.2f, 0.25f), 18);
            hpBar.Root.Place(new Vector2(0.5f, 1f), new Vector2(20, -64), new Vector2(300, 28));

            var badge = UIFactory.Image(panel.transform, "Escudo", TextureFactory.Circle, new Color(0.25f, 0.55f, 1f), false);
            badge.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(-155, -64), new Vector2(44, 44));
            blockText = UIFactory.Text(badge.transform, "Valor", "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            blockText.rectTransform.Stretch();
            blockBadge = badge.gameObject;

            statusText = UIFactory.Text(panel.transform, "Status", "", 18, Color.white, TextAnchor.MiddleLeft);
            statusText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(350, 28));

            inPlayText = UIFactory.Text(panel.transform, "EmJogo", "", 16, new Color(0.55f, 0.95f, 0.9f), TextAnchor.UpperLeft);
            inPlayText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(350, 48));

            fatText = UIFactory.Text(panel.transform, "Peso", "", 16, new Color(1f, 0.75f, 0.35f), TextAnchor.MiddleLeft);
            fatText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -198), new Vector2(350, 40));
        }

        void BuildLog()
        {
            var panel = UIFactory.Panel(root, "Registro", new Color(0.04f, 0.03f, 0.07f, 0.8f));
            panel.raycastTarget = false;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.sizeDelta = new Vector2(390, 220);
            prt.anchoredPosition = new Vector2(210, 885);
            logText = UIFactory.Text(panel.transform, "Texto", "", 16, new Color(0.9f, 0.9f, 0.9f), TextAnchor.LowerLeft, FontStyle.Normal, false);
            logText.rectTransform.Stretch(14, 10, 14, 10);
            logText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildBottom()
        {
            var orb = UIFactory.Image(root, "Energia", TextureFactory.Circle, new Color(1f, 0.72f, 0.18f), false);
            orb.rectTransform.anchorMin = orb.rectTransform.anchorMax = Vector2.zero;
            orb.rectTransform.sizeDelta = new Vector2(140, 140);
            orb.rectTransform.anchoredPosition = new Vector2(110, 185);
            var orbGlow = UIFactory.Image(orb.transform, "Brilho", TextureFactory.Glow, new Color(1f, 0.85f, 0.4f, 0.6f), false);
            orbGlow.rectTransform.Stretch(-40, -40, -40, -40);
            orbGlow.transform.SetAsFirstSibling();
            energyText = UIFactory.Text(orb.transform, "Valor", "", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            energyText.rectTransform.Stretch();
            var energyLabel = UIFactory.Text(root, "EnergiaRotulo", "ENERGIA", 18, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            energyLabel.rectTransform.anchorMin = energyLabel.rectTransform.anchorMax = Vector2.zero;
            energyLabel.rectTransform.sizeDelta = new Vector2(160, 30);
            energyLabel.rectTransform.anchoredPosition = new Vector2(110, 98);

            drawText = UIFactory.Text(root, "Monte", "", 19, Color.white);
            drawText.rectTransform.anchorMin = drawText.rectTransform.anchorMax = Vector2.zero;
            drawText.rectTransform.sizeDelta = new Vector2(200, 30);
            drawText.rectTransform.anchoredPosition = new Vector2(110, 60);

            endTurnButton = UIFactory.Button(root, "EncerrarTurno", "Encerrar turno\n<size=18>(Espaço)</size>", () => EndTurnClicked?.Invoke(),
                                             new Color(0.75f, 0.3f, 0.2f), 28);
            var ert = (RectTransform)endTurnButton.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(1f, 0f);
            ert.sizeDelta = new Vector2(250, 96);
            ert.anchoredPosition = new Vector2(-150, 200);

            discardText = UIFactory.Text(root, "Descarte", "", 19, Color.white);
            discardText.rectTransform.anchorMin = discardText.rectTransform.anchorMax = new Vector2(1f, 0f);
            discardText.rectTransform.sizeDelta = new Vector2(280, 60);
            discardText.rectTransform.anchoredPosition = new Vector2(-150, 110);
        }

        // ------------------------------------------------------------------ Ligação com a batalha

        public void Bind(BattleSystem b, bool sertao)
        {
            Unbind();
            battle = b;
            logLines.Clear();
            busy = false;
            targeting = null;

            if (sertao)
            {
                if (b.IsBossBattle)
                    background.texture = lajedoTexture ?? (lajedoTexture = MapPainter.PaintArena(true, 7, true));
                else
                    background.texture = sertaoTexture ?? (sertaoTexture = MapPainter.PaintArena(false, 3, true));
            }
            else if (b.IsBossBattle)
            {
                if (engenhoTexture == null) engenhoTexture = MapPainter.PaintArena(true, 7);
                background.texture = engenhoTexture;
            }
            else
            {
                if (fieldTexture == null) fieldTexture = MapPainter.PaintArena(false, 3);
                background.texture = fieldTexture;
            }

            int n = b.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                slots[b.Enemies[i]] = (4 - n) * 0.5f + i;
                CreateEnemyView(b.Enemies[i], true);
            }

            b.OnLog += AddLog;
            b.OnPopup += Popup;
            b.OnHit += Hit;
            b.OnEnemySpawned += OnEnemySpawned;
            titleText.text = (b.IsBossBattle ? "CHEFE — " : "Batalha — ") + b.Encounter.Title;
            tooltip.SetActive(false);
            heroAnim.SetFatStage(b.Data.FatStage);
            heroAnim.SetPose(HeroPose.Idle);
        }

        public void Unbind()
        {
            if (battle != null)
            {
                battle.OnLog -= AddLog;
                battle.OnPopup -= Popup;
                battle.OnHit -= Hit;
                battle.OnEnemySpawned -= OnEnemySpawned;
            }
            battle = null;
            foreach (var v in enemyViews.Values) if (v != null) Destroy(v.gameObject);
            enemyViews.Clear();
            slots.Clear();
            foreach (var cv in handViews) if (cv != null) Destroy(cv.gameObject);
            handViews.Clear();
            foreach (Transform t in fxLayer) Destroy(t.gameObject);
        }

        void CreateEnemyView(EnemyCombatant enemy, bool instant)
        {
            var view = EnemyView.Create(enemyLayer, enemy);
            view.OnClicked = v => EnemyClicked?.Invoke(v.Enemy);
            view.OnHover = (v, on) => ShowTooltip(on ? EnemyTooltip(v.Enemy) : null);
            enemyViews[enemy] = view;
            LayoutEnemy(enemy, instant);
        }

        void OnEnemySpawned(EnemyCombatant enemy)
        {
            float[] candidates = { 1.5f, 0.5f, 2.5f, 1f, 2f, 0f, 3f, 3.5f };
            var taken = slots.Where(kv => !kv.Key.IsDead).Select(kv => kv.Value).ToList();
            float slot = candidates.FirstOrDefault(c => taken.All(t => Mathf.Abs(t - c) >= 0.99f));
            if (taken.Any(t => Mathf.Abs(t - slot) < 0.99f)) slot = 3.5f;
            slots[enemy] = slot;
            CreateEnemyView(enemy, true);
            var rt = (RectTransform)enemyViews[enemy].transform;
            rt.localScale = Vector3.one * 0.2f;
        }

        void LayoutEnemy(EnemyCombatant enemy, bool instant)
        {
            if (!enemyViews.TryGetValue(enemy, out var view) || view == null) return;
            float x = ArenaCenterX + (slots[enemy] - 1.5f) * SlotSpacing;
            int d = Mathf.Clamp(enemy.Distance, 0, 2);
            bool boss = enemy.Data.IsBoss;
            view.SetLayout(new Vector2(x, boss ? BossLaneY[d] : LaneY[d]), boss ? BossLaneScale[d] : LaneScale[d], instant);
        }

        static string EnemyTooltip(EnemyCombatant e)
        {
            string extra = "";
            if (e.Data.Trait == EnemyTrait.Cardume) extra += " Cardume: +1 de dano por outra piranha viva.";
            if (e.Data.Trait == EnemyTrait.Carniceiro) extra += " Carniceiro: +50% de dano se você estiver com menos da metade da vida.";
            if (e.Data.Charges) extra += " Investida: avança e ataca no mesmo turno.";
            if (e.Data.ResistsSleep) extra += " Resiste ao Sono (fica com Fraqueza).";
            string range = e.Data.Range == 0 ? "corpo a corpo" : "à distância";
            return $"<b>{e.Name}</b> ({e.Data.Habitat}) — {e.Data.Description} Ataca {range}.{extra}";
        }

        // ------------------------------------------------------------------ Atualização

        public void Refresh()
        {
            if (battle == null) return;

            turnText.text = $"Turno {battle.Turn}";
            playerName.text = $"{battle.Data.Name}  <size=17>Nv.{battle.Data.Level}</size>";
            hpBar.Set(battle.Player.Hp, battle.Player.MaxHp, $"{battle.Player.Hp}/{battle.Player.MaxHp}");
            blockBadge.SetActive(battle.Player.Block > 0);
            blockText.text = battle.Player.Block.ToString();

            var sb = new StringBuilder();
            foreach (var kv in battle.Player.AllStatuses)
                sb.Append($"<color={Statuses.Hex(kv.Key)}>{Statuses.Name(kv.Key)} {kv.Value}</color>  ");
            statusText.text = sb.Length > 0 ? sb.ToString() : "<color=#888888>Sem efeitos</color>";
            inPlayText.text = battle.InPlay.Count > 0
                ? $"Em jogo: {string.Join(", ", battle.InPlay.Select(c => c.Name))}\n+{battle.AttackBonus} dano · +{battle.BlockPerTurn} escudo/turno"
                : "";

            energyText.text = $"{battle.Energy}/{battle.Data.EffectiveEnergy}";
            if (battle.Data.FatStage > 0)
            {
                string limit = battle.MaxCardsPerTurn > 0 ? $"  ·  Cartas: {battle.CardsPlayedThisTurn}/{battle.MaxCardsPerTurn}" : "";
                fatText.text = $"Peso {battle.Data.Peso} ({battle.Data.FatLabel}): {battle.Data.FatPenaltyText}{limit}";
            }
            else
            {
                fatText.text = battle.Data.Peso > 0 ? $"<color=#9CFF8A>Peso {battle.Data.Peso}: satisfeito, sem penalidade</color>" : "";
            }
            drawText.text = $"Monte: {battle.Deck.DrawPile.Count}";
            discardText.text = $"Descarte: {battle.Deck.Discard.Count}\nExauridas: {battle.Deck.Exhausted.Count}";

            var combo = battle.Combo;
            if (!combo.Started)
            {
                comboText.text = "COMBO x1.00";
                comboHint.text = "Jogue cartas em ordem CRESCENTE de custo (ex.: 0 » 1 » 2) para fortalecer as próximas.";
            }
            else
            {
                comboText.text = $"COMBO x{Fmt(combo.CurrentMultiplier)}   <size=20>Sequência: {string.Join(" » ", combo.History)}</size>";
                comboHint.text = $"Próxima carta com custo maior que {combo.LastCost} (ou Coringa): x{Fmt(ComboTracker.MultiplierFor(combo.Chain + 1))}";
            }

            RefreshEnemies();
            SyncHand();
            endTurnButton.interactable = !busy && !battle.IsOver;
            logText.text = string.Join("\n", logLines);
        }

        void RefreshEnemies()
        {
            foreach (var enemy in battle.Enemies.ToList())
            {
                if (!enemyViews.TryGetValue(enemy, out var view) || view == null) continue;
                if (enemy.IsDead)
                {
                    enemyViews.Remove(enemy);
                    view.Die(null);
                    continue;
                }
                view.Refresh(battle);
                view.SetTargetable(targeting != null);
                LayoutEnemy(enemy, false);
            }
        }

        void SyncHand()
        {
            var hand = battle.Deck.Hand;
            var unused = new List<CardView>(handViews);
            var result = new List<CardView>();
            foreach (var card in hand)
            {
                var view = unused.FirstOrDefault(v => v.Card == card);
                if (view != null)
                {
                    unused.Remove(view);
                }
                else
                {
                    view = CardView.Create(handLayer, card);
                    view.HoverLift = true;
                    var rt = (RectTransform)view.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(-860, 170);
                    rt.localScale = Vector3.one * 0.6f;
                    view.OnClicked = v => CardClicked?.Invoke(v.Card);
                    view.OnHover = (v, on) => ShowTooltip(on ? $"<b>{v.Card.Name}</b> — <i>{v.Card.Flavor}</i>" : null);
                }
                result.Add(view);
            }
            foreach (var v in unused) Destroy(v.gameObject);
            handViews.Clear();
            handViews.AddRange(result);

            int n = handViews.Count;
            float spacing = n <= 1 ? 0f : Mathf.Min(232f, 1150f / (n - 1));
            for (int i = 0; i < n; i++)
            {
                var view = handViews[i];
                var card = view.Card;
                view.SetBasePosition(new Vector2((i - (n - 1) * 0.5f) * spacing, 170f), false);

                float mult = battle.Combo.PreviewMultiplier(card);
                string label = null;
                bool good = false;
                if (battle.Combo.Started)
                {
                    good = battle.Combo.WouldContinue(card);
                    label = good ? "x" + Fmt(mult) : "quebra";
                }
                view.Refresh(battle, mult, label, good);
                bool playable = !busy && battle.CanPlay(card, out _);
                view.SetPlayable(playable);
                view.SetSelected(card == targeting);
            }
        }

        // ------------------------------------------------------------------ Feedback

        public void SetTargeting(CardData card)
        {
            targeting = card;
            promptText.text = card != null ? $"Escolha um alvo para {card.Name}  <size=18>(botão direito ou Esc cancela)</size>" : "";
            Refresh();
        }

        public void SetBusy(bool value)
        {
            busy = value;
            if (value) promptText.text = "Turno dos inimigos...";
            else if (targeting == null) promptText.text = "";
            Refresh();
        }

        public void Flash(string message)
        {
            promptText.text = message;
            CancelInvoke(nameof(ClearPrompt));
            Invoke(nameof(ClearPrompt), 1.6f);
        }

        void ClearPrompt()
        {
            if (targeting == null && !busy) promptText.text = "";
        }

        public void ShowBanner(string text)
        {
            bannerText.text = text;
            bannerTime = 1.2f;
        }

        void ShowTooltip(string text)
        {
            tooltip.SetActive(!string.IsNullOrEmpty(text));
            if (text != null) tooltipText.text = text;
        }

        void AddLog(string line)
        {
            logLines.Add(line);
            while (logLines.Count > 9) logLines.RemoveAt(0);
            if (logText != null) logText.text = string.Join("\n", logLines);
        }

        void Popup(Combatant target, string text, Color color)
        {
            Vector2 pos;
            if (target is EnemyCombatant enemy && enemyViews.TryGetValue(enemy, out var view) && view != null)
                pos = view.PopupPosition;
            else
                pos = new Vector2(HeroPos.x - root.rect.width * 0.5f, HeroPos.y + 80f);
            FloatingText.Spawn(fxLayer, pos, text, color);
        }

        void Hit(Combatant target)
        {
            if (target is EnemyCombatant enemy)
            {
                if (enemyViews.TryGetValue(enemy, out var view) && view != null) view.Hit();
            }
            else
            {
                heroShake = 0.3f;
                heroAnim.PlayOnce(HeroPose.Hurt, 0.4f);
            }
        }

        /// <summary>O herói ergue os braços (quadro 1) ao lançar uma carta.</summary>
        public void HeroCast() => heroAnim.PlayOnce(HeroPose.Cast, 0.35f);

        void Update()
        {
            if (bannerTime > 0f)
            {
                bannerTime -= Time.deltaTime;
                var c = bannerText.color;
                c.a = Mathf.Clamp01(bannerTime / 0.4f);
                bannerText.color = c;
            }
            else if (bannerText.text.Length > 0)
            {
                bannerText.text = "";
            }

            float bob = Mathf.Sin(Time.time * 2.4f) * 6f;
            float shakeX = 0f;
            if (heroShake > 0f)
            {
                heroShake -= Time.deltaTime;
                shakeX = Mathf.Sin(Time.time * 80f) * 12f * heroShake * 3f;
            }
            heroRt.anchoredPosition = HeroPos + new Vector2(shakeX, bob);
            heroImage.color = heroShake > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;
        }
    }
}
