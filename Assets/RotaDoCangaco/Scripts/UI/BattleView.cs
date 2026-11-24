using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// Tela de batalha vista de cima: inimigos em colunas que descem em direção ao herói conforme
    /// a distância (Longe → Médio → Perto), mão de cartas embaixo, energia, combo e registro.
    /// Também desenha as animações rápidas de ataque (herói e inimigos) e o arrastar de cartas.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        public event Action<CardData> CardClicked;
        public event Action<EnemyCombatant> EnemyClicked;
        public event Action EndTurnClicked;
        /// <summary>Carta solta depois de arrastada: (carta, inimigo sob o ponteiro ou null, soltou na arena).</summary>
        public event Action<CardData, EnemyCombatant, bool> CardDropped;
        /// <summary>Começou a arrastar uma carta (o controlador cancela a mira por clique).</summary>
        public event Action<CardData> CardDragStarted;

        static readonly float[] LaneY = { 510f, 682f, 855f };
        static readonly float[] LaneScale = { 1f, 0.86f, 0.72f };
        static readonly float[] BossLaneY = { 578f, 700f, 820f };
        static readonly float[] BossLaneScale = { 0.9f, 0.8f, 0.72f };
        static readonly Vector2 HeroPos = new Vector2(210f, 455f);
        const float ArenaCenterX = 80f;
        const float SlotSpacing = 300f;
        const float ArenaMinY = 360f;          // acima disso (a partir da base da tela) é a arena; abaixo é a mão
        const float ScreenShakeTime = 0.22f;
        const string IdleHint = "<size=17><color=#E6DCC8>Arraste uma carta até o alvo\nou clique nela (teclas 1–9)</color></size>";

        RectTransform root, enemyLayer, handLayer, fxLayer, heroRt;
        RawImage background;
        Texture2D sertaoTexture, lajedoTexture;
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

        // Animação da carta jogada e arrasto
        bool animating;
        CardView playingView;   // carta que está "saindo" da mão enquanto o herói ataca
        CardView droppedView;   // carta recém-solta (para a animação sair do lugar certo)
        CardView dragView;      // carta sendo arrastada agora
        EnemyView dragHover;    // inimigo destacado sob a carta arrastada

        // Bote do herói
        Vector2 heroLungeDir = Vector2.right;
        float heroLungeT = -1f;
        float heroLungeDuration = 0.3f;
        float heroLungePeak = 0.3f;
        float heroLungeDistance;

        // Tremor leve da tela em golpes fortes
        float screenShake;
        float screenShakeStrength;
        float pendingShakeAt = -1f;
        float pendingShakeStrength;

        public static string Fmt(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Há uma carta sendo arrastada?</summary>
        public bool IsDragging => dragView != null;

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
            background.color = new Color(0.96f, 0.86f, 0.74f); // tom de fim de tarde, como o fundo do menu

            // Linhas de distância
            string[] laneNames = { "PERTO", "MÉDIO", "LONGE" };
            for (int i = 0; i < 3; i++)
            {
                var label = UIFactory.Text(root, "Faixa" + i, laneNames[i], 18, new Color(1f, 0.9f, 0.72f, 0.55f), TextAnchor.MiddleLeft, FontStyle.Bold);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0f);
                label.rectTransform.sizeDelta = new Vector2(120, 30);
                label.rectTransform.anchoredPosition = new Vector2(480, LaneY[i] - 70);
                if (i < 2)
                {
                    var line = UIFactory.Panel(root, "Linha" + i, new Color(1f, 0.85f, 0.6f, 0.1f), TextureFactory.RoundedRectSmall);
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

            tooltip = UIFactory.FramePanel(root, "Dica").gameObject;
            ((RectTransform)tooltip.transform).Place(new Vector2(1f, 0f), new Vector2(-175, 560), new Vector2(330, 250));
            tooltipText = UIFactory.Text(tooltip.transform, "Texto", "", 18, UIFactory.BodyText, TextAnchor.UpperLeft);
            tooltipText.rectTransform.Stretch(18, 14, 18, 14);
            tooltip.SetActive(false);

            promptText = UIFactory.Text(root, "Aviso", "", 22, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle(UIFactory.Accent);
            promptText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(-175, 330), new Vector2(330, 100));

            bannerText = UIFactory.Text(root, "Faixa", "", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle();
            bannerText.GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 120));
        }

        void BuildTopBar()
        {
            var bar = UIFactory.FramePanel(root, "BarraTopo");
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(10, -92);
            brt.offsetMax = new Vector2(-10, -6);

            titleText = UIFactory.Text(bar.transform, "Titulo", "", 25, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).SignStyle();
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(24, 0);
            titleText.rectTransform.offsetMax = Vector2.zero;
            turnText = UIFactory.Text(bar.transform, "Turno", "", 26, UIFactory.Accent, TextAnchor.MiddleRight, FontStyle.Bold).SignStyle(UIFactory.Accent);
            turnText.rectTransform.anchorMin = new Vector2(0.8f, 0f);
            turnText.rectTransform.anchorMax = new Vector2(1f, 1f);
            turnText.rectTransform.offsetMin = Vector2.zero;
            turnText.rectTransform.offsetMax = new Vector2(-24, 0);

            // Combo numa placa de madeira, como os botões do menu.
            var combo = UIFactory.Panel(bar.transform, "Combo", Color.white, TextureFactory.WoodButtonDark);
            combo.raycastTarget = false;
            combo.rectTransform.anchorMin = new Vector2(0.3f, 0f);
            combo.rectTransform.anchorMax = new Vector2(0.8f, 1f);
            combo.rectTransform.offsetMin = new Vector2(10, 4);
            combo.rectTransform.offsetMax = new Vector2(-10, -4);
            comboText = UIFactory.Text(combo.transform, "Texto", "", 25, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle();
            comboText.rectTransform.Stretch(24, 32, 24, 6);
            comboHint = UIFactory.Text(combo.transform, "Dica", "", 16, Color.white).SignStyle(new Color(1f, 0.86f, 0.62f));
            comboHint.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            comboHint.rectTransform.Stretch(24, 8, 24, 42);
        }

        void BuildPlayerPanel()
        {
            var panel = UIFactory.FramePanel(root, "PainelJogador");
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.sizeDelta = new Vector2(390, 225);
            prt.anchoredPosition = new Vector2(210, 660);

            playerName = UIFactory.Text(panel.transform, "Nome", "", 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).SignStyle();
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
            var panel = UIFactory.FramePanel(root, "Registro");
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.sizeDelta = new Vector2(390, 220);
            prt.anchoredPosition = new Vector2(210, 885);
            logText = UIFactory.Text(panel.transform, "Texto", "", 16, UIFactory.BodyText, TextAnchor.LowerLeft, FontStyle.Normal, false);
            logText.rectTransform.Stretch(18, 14, 18, 14);
            logText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildBottom()
        {
            var orb = UIFactory.Image(root, "Energia", TextureFactory.Circle, new Color(1f, 0.72f, 0.18f), false);
            orb.rectTransform.anchorMin = orb.rectTransform.anchorMax = Vector2.zero;
            orb.rectTransform.sizeDelta = new Vector2(140, 140);
            orb.rectTransform.anchoredPosition = new Vector2(110, 185);
            var orbGlow = UIFactory.Image(orb.transform, "Brilho", TextureFactory.Glow, new Color(1f, 0.75f, 0.35f, 0.45f), false);
            orbGlow.rectTransform.Stretch(-40, -40, -40, -40);
            orbGlow.transform.SetAsFirstSibling();
            // Aro de bronze em volta, como as placas do menu.
            var orbRim = UIFactory.Image(orb.transform, "Aro", TextureFactory.Ring, new Color(0.45f, 0.24f, 0.08f), false);
            orbRim.rectTransform.Stretch(-6, -6, -6, -6);
            var orbRimLight = UIFactory.Image(orb.transform, "AroClaro", TextureFactory.Ring, new Color(0.88f, 0.62f, 0.27f), false);
            orbRimLight.rectTransform.Stretch(-1, -1, -1, -1);
            energyText = UIFactory.Text(orb.transform, "Valor", "", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle(Color.white);
            energyText.GetComponent<Outline>().effectDistance = new Vector2(2.5f, -2.5f);
            energyText.rectTransform.Stretch();
            var energyLabel = UIFactory.Text(root, "EnergiaRotulo", "ENERGIA", 18, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle(UIFactory.Accent);
            energyLabel.rectTransform.anchorMin = energyLabel.rectTransform.anchorMax = Vector2.zero;
            energyLabel.rectTransform.sizeDelta = new Vector2(160, 30);
            energyLabel.rectTransform.anchoredPosition = new Vector2(110, 98);

            drawText = UIFactory.Text(root, "Monte", "", 19, Color.white).SignStyle();
            drawText.rectTransform.anchorMin = drawText.rectTransform.anchorMax = Vector2.zero;
            drawText.rectTransform.sizeDelta = new Vector2(200, 30);
            drawText.rectTransform.anchoredPosition = new Vector2(110, 60);

            endTurnButton = UIFactory.Button(root, "EncerrarTurno", "Encerrar turno\n<size=18>(Espaço)</size>", () => EndTurnClicked?.Invoke(),
                                             ButtonStyle.Primary, 28);
            var ert = (RectTransform)endTurnButton.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(1f, 0f);
            ert.sizeDelta = new Vector2(250, 96);
            ert.anchoredPosition = new Vector2(-150, 200);

            discardText = UIFactory.Text(root, "Descarte", "", 19, Color.white).SignStyle();
            discardText.rectTransform.anchorMin = discardText.rectTransform.anchorMax = new Vector2(1f, 0f);
            discardText.rectTransform.sizeDelta = new Vector2(280, 60);
            discardText.rectTransform.anchoredPosition = new Vector2(-150, 110);
        }

        // ------------------------------------------------------------------ Ligação com a batalha

        public void Bind(BattleSystem b)
        {
            Unbind();
            battle = b;
            logLines.Clear();
            busy = false;
            animating = false;
            targeting = null;
            heroLungeT = -1f;
            screenShake = 0f;
            pendingShakeAt = -1f;
            root.anchoredPosition = Vector2.zero;

            if (b.IsBossBattle)
                background.texture = lajedoTexture != null ? lajedoTexture : (lajedoTexture = MapPainter.PaintArena(true, 7));
            else
                background.texture = sertaoTexture != null ? sertaoTexture : (sertaoTexture = MapPainter.PaintArena(false, 3));

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
            ClearDrag();
            battle = null;
            animating = false;
            foreach (var v in enemyViews.Values) if (v != null) Destroy(v.gameObject);
            enemyViews.Clear();
            slots.Clear();
            foreach (var cv in handViews) if (cv != null) Destroy(cv.gameObject);
            handViews.Clear();
            if (playingView != null) Destroy(playingView.gameObject);
            playingView = null;
            droppedView = null;
            foreach (Transform t in fxLayer) Destroy(t.gameObject);
            promptText.text = "";
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
            // brilho de invocação no lugar onde a criatura surge
            BattleFx.Aura(fxLayer, rt, new Color(0.78f, 0.55f, 1f), 200f);
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
            bool aim = targeting != null || (dragView != null && dragView.Card.NeedsTarget);
            foreach (var enemy in battle.Enemies.ToList())
            {
                if (!enemyViews.TryGetValue(enemy, out var view) || view == null) continue;
                if (enemy.IsDead)
                {
                    enemyViews.Remove(enemy);
                    if (view == dragHover) SetDragHover(null);
                    view.Die(null);
                    continue;
                }
                view.Refresh(battle);
                view.SetTargetable(aim);
                LayoutEnemy(enemy, false);
            }
        }

        void SyncHand()
        {
            var hand = battle.Deck.Hand;
            var unused = new List<CardView>(handViews);
            var result = new List<CardView>();
            bool skipPlaying = playingView != null;
            foreach (var card in hand)
            {
                // A carta que está sendo jogada (animação) já saiu da mão visualmente.
                if (skipPlaying && card == playingView.Card)
                {
                    skipPlaying = false;
                    continue;
                }
                var view = unused.FirstOrDefault(v => v != null && v.Card == card);
                if (view != null)
                {
                    unused.Remove(view);
                }
                else
                {
                    view = CardView.Create(handLayer, card);
                    view.HoverLift = true;
                    view.Draggable = true;
                    var rt = (RectTransform)view.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(-860, 170);
                    rt.localScale = Vector3.one * 0.6f;
                    view.OnClicked = v => CardClicked?.Invoke(v.Card);
                    view.OnHover = (v, on) => ShowTooltip(on ? $"<b>{v.Card.Name}</b> — <i>{v.Card.Flavor}</i>" : null);
                    view.OnDragStart = BeginCardDrag;
                    view.OnDragMove = MoveCardDrag;
                    view.OnDragEnd = EndCardDrag;
                    view.OnOrderChanged = _ => ApplyHandOrder();
                }
                result.Add(view);
            }
            foreach (var v in unused)
            {
                if (v == null) continue;
                if (v == dragView) ClearDrag(); // a mão mudou no meio do arrasto (ex.: fim de turno)
                Destroy(v.gameObject);
            }
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
            ApplyHandOrder();
        }

        /// <summary>
        /// Ordem de desenho da mão: da esquerda para a direita, com a carta sob o mouse, a arrastada e a que está
        /// sendo jogada por cima. Recalculada sempre que a mão muda ou o hover/arrasto muda.
        /// </summary>
        void ApplyHandOrder()
        {
            for (int i = 0; i < handViews.Count; i++)
                if (handViews[i] != null) handViews[i].transform.SetSiblingIndex(i);
            var hovered = handViews.FirstOrDefault(v => v != null && v.IsHovered && !v.IsDragging);
            if (hovered != null) hovered.transform.SetAsLastSibling();
            if (dragView != null) dragView.transform.SetAsLastSibling();
            if (playingView != null) playingView.transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ Feedback

        public void SetTargeting(CardData card)
        {
            targeting = card;
            if (card != null)
            {
                CancelInvoke(nameof(ClearPrompt));
                promptText.text = $"Escolha um alvo para {card.Name}  <size=18>(botão direito ou Esc cancela)</size>";
            }
            else
            {
                ResetPrompt();
            }
            Refresh();
        }

        public void SetBusy(bool value)
        {
            busy = value;
            if (value) promptText.text = "Turno dos inimigos...";
            else ResetPrompt();
            Refresh();
        }

        public void Flash(string message)
        {
            promptText.text = message;
            CancelInvoke(nameof(ClearPrompt));
            Invoke(nameof(ClearPrompt), 1.6f);
        }

        // Chamado só por nome (Invoke): [Preserve] impede que a remoção de código não usado do build o apague.
        [UnityEngine.Scripting.Preserve]
        void ClearPrompt() => ResetPrompt();

        /// <summary>Volta o aviso ao estado normal (mira, arrasto e turno dos inimigos têm prioridade).</summary>
        void ResetPrompt()
        {
            if (targeting != null || dragView != null) return;
            if (busy) promptText.text = "Turno dos inimigos...";
            else promptText.text = battle != null && !battle.IsOver ? IdleHint : "";
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

        /// <summary>Tremor leve da tela (golpes fortes). Com atraso, acontece junto com o impacto.</summary>
        public void ShakeScreen(float strength, float delay = 0f)
        {
            if (delay <= 0f)
            {
                screenShake = ScreenShakeTime;
                screenShakeStrength = strength;
                return;
            }
            pendingShakeAt = Time.time + delay;
            pendingShakeStrength = strength;
        }

        // ------------------------------------------------------------------ Animações de ataque

        /// <summary>
        /// Começa a animação da carta jogada pelo herói: a carta sai da mão, o herói dá um bote / ergue os braços
        /// e o efeito (projétil, escudo, brilho) vai até o alvo. Retorna em quantos segundos acontece o impacto —
        /// o controlador só aplica a carta nesse momento, para o clarão e os números de dano saírem juntos.
        /// </summary>
        public float BeginCardPlay(CardData card, EnemyCombatant target)
        {
            animating = true;
            if (dragView != null) ClearDrag();
            targeting = null;
            CancelInvoke(nameof(ClearPrompt));
            ShowTooltip(null);
            DetachPlayingCard(card);
            ResetPrompt();
            if (battle == null || card == null) return 0f;

            heroAnim.PlayOnce(HeroPose.Cast, 0.35f);
            Color tint = card.Tint;
            tint.a = 1f;
            float impact;

            var targets = new List<EnemyView>();
            if (card.Target == CardTarget.AllEnemies)
            {
                foreach (var enemy in battle.AliveEnemies)
                    if (enemyViews.TryGetValue(enemy, out var ev) && ev != null) targets.Add(ev);
            }
            else if (card.NeedsTarget)
            {
                var enemy = target != null && !target.IsDead ? target : battle.AliveEnemies.FirstOrDefault();
                if (enemy != null && enemyViews.TryGetValue(enemy, out var ev) && ev != null) targets.Add(ev);
            }

            if (targets.Count > 0)
            {
                // Bote curto na direção do alvo + orbe(s) com rastro que explodem no impacto.
                const float launch = 0.07f, flight = 0.17f;
                impact = launch + flight;
                Vector3 aim = Vector3.zero;
                foreach (var ev in targets) aim += ev.transform.position;
                aim /= targets.Count;
                StartHeroLunge(aim, 0.32f, 0.28f, 46f);

                bool area = card.Target == CardTarget.AllEnemies;
                Vector3 hand = heroRt.TransformPoint(new Vector3(30f, 25f, 0f));
                BattleFx.Charge(fxLayer, hand, tint, 110f, 0.2f);
                Color trail = Color.Lerp(tint, Color.white, 0.25f);
                for (int i = 0; i < targets.Count; i++)
                {
                    float arc = area ? 35f + 30f * (i % 3) : 70f;
                    BattleFxProjectile.Launch(fxLayer, hand, targets[i].transform, tint, trail, launch, flight,
                                              area ? 36f : 46f, arc, area ? 0.85f : 1.1f);
                }
                if (PredictedDamage(card) >= 12) ShakeScreen(area ? 7f : 6f, impact);
            }
            else
            {
                // Cartas sem alvo: pulinho no lugar + escudo azul, brilhos verdes ou brilho dourado.
                impact = 0.16f;
                StartHeroLunge(heroRt.position + root.TransformVector(new Vector3(0f, 100f, 0f)), 0.3f, 0.4f, 16f);
                bool block = card.Effects.Any(e => e is BlockEffect);
                bool heal = card.Effects.Any(e => e is HealEffect);
                bool pain = card.Effects.Any(e => e is SelfDamageEffect);
                if (block) BattleFx.Shield(fxLayer, heroRt, BattleSystem.ColBlock, 200f);
                if (heal) BattleFx.Sparkles(fxLayer, heroRt, BattleSystem.ColHeal, 70f, 10);
                if (!block && !heal)
                {
                    Color gold = pain ? new Color(1f, 0.4f, 0.35f) : Color.Lerp(BattleSystem.ColGold, tint, 0.3f);
                    BattleFx.Sparkles(fxLayer, heroRt, gold, 70f, 9);
                    if (card.Type == CardType.Personagem) BattleFx.Aura(fxLayer, heroRt, BattleSystem.ColGold, 220f);
                }
            }

            Refresh();
            return impact;
        }

        /// <summary>Fim da animação da carta (o controlador já aplicou a carta): remove a carta que estava saindo.</summary>
        public void EndCardPlay()
        {
            if (playingView != null) Destroy(playingView.gameObject);
            playingView = null;
            animating = false;
        }

        /// <summary>Tira a carta jogada da mão (a que foi solta, a que está sob o mouse ou a primeira igual).</summary>
        void DetachPlayingCard(CardData card)
        {
            if (playingView != null) Destroy(playingView.gameObject);
            playingView = null;
            if (card == null) return;

            CardView view = null;
            if (droppedView != null && droppedView.Card == card && handViews.Contains(droppedView)) view = droppedView;
            if (view == null) view = handViews.FirstOrDefault(v => v != null && v.Card == card && v.IsHovered);
            if (view == null) view = handViews.FirstOrDefault(v => v != null && v.Card == card);
            droppedView = null;
            if (view == null) return;
            handViews.Remove(view);
            playingView = view;
            view.PlayOut();
        }

        int PredictedDamage(CardData card)
        {
            if (battle == null) return 0;
            float mult = battle.Combo.PreviewMultiplier(card);
            int total = 0;
            foreach (var effect in card.Effects)
                if (effect is DamageEffect d) total += battle.PreviewPlayerDamage(d.Amount, mult) * Mathf.Max(1, d.Hits);
            return total;
        }

        void StartHeroLunge(Vector3 targetWorld, float duration, float peak, float distance)
        {
            Vector2 delta = root.InverseTransformPoint(targetWorld) - root.InverseTransformPoint(heroRt.position);
            heroLungeDir = delta.sqrMagnitude > 1f ? delta.normalized : Vector2.right;
            heroLungeDuration = Mathf.Max(0.05f, duration);
            heroLungePeak = Mathf.Clamp(peak, 0.05f, 0.95f);
            heroLungeDistance = distance;
            heroLungeT = 0f;
        }

        Vector2 HeroLungeOffset(float dt)
        {
            if (heroLungeT < 0f) return Vector2.zero;
            heroLungeT += dt;
            float k = heroLungeT / heroLungeDuration;
            if (k >= 1f)
            {
                heroLungeT = -1f;
                return Vector2.zero;
            }
            float p = heroLungePeak;
            float a = k < p
                ? 1f - (1f - k / p) * (1f - k / p)
                : 1f - Mathf.SmoothStep(0f, 1f, (k - p) / (1f - p));
            return heroLungeDir * (heroLungeDistance * a);
        }

        /// <summary>
        /// Preparação do inimigo antes de agir, conforme a intenção: bote (corpo a corpo), projétil (à distância),
        /// escudo, aura de fortalecer ou brilho de invocação. Retorna em quantos segundos acontece o impacto —
        /// o controlador executa o turno do inimigo nesse momento. 0 = sem animação (dormindo, só avançando...).
        /// </summary>
        public float PlayEnemyWindup(EnemyCombatant enemy)
        {
            if (battle == null || battle.IsOver || enemy == null || enemy.IsDead) return 0f;
            if (!enemyViews.TryGetValue(enemy, out var ev) || ev == null) return 0f;
            if (enemy.GetStatus(StatusType.Sono) > 0) return 0f;
            if (enemy.GetStatus(StatusType.Veneno) + enemy.GetStatus(StatusType.Queimadura) >= enemy.Hp) return 0f; // cai antes de agir
            var move = enemy.Intent;
            if (move == null) return 0f;

            bool boss = enemy.Data.IsBoss;
            Color col = EnemyFxColor(enemy);
            float size = ev.VisualSize * ev.CurrentScale;

            if (move.IsAttack)
            {
                if (!enemy.AttackLandsThisTurn) return 0f; // só avança neste turno
                int total = battle.PreviewEnemyDamage(enemy, move.Damage) * Mathf.Max(1, move.Hits);
                bool ranged = boss ? move.Status.HasValue : enemy.Data.Range > 0;
                float impact;
                if (ranged)
                {
                    const float charge = 0.07f, flight = 0.18f;
                    impact = charge + flight;
                    Vector3 mouth = ev.SpriteWorldPosition;
                    BattleFx.Charge(fxLayer, mouth, col, size * 0.6f, charge + 0.1f);
                    ev.Lunge(heroRt.position, charge, 28f);
                    BattleFxProjectile.Launch(fxLayer, mouth, heroRt, col, EnemyTrailColor(enemy), charge, flight,
                                              boss ? 56f : 40f, boss ? 30f : 55f, boss ? 1.25f : 0.9f);
                }
                else
                {
                    impact = boss ? 0.23f : 0.2f;
                    ev.Lunge(heroRt.position, impact, boss ? 150f : 110f);
                    BattleFx.Burst(fxLayer, heroRt.position, col, boss ? 1.2f : 0.85f, impact);
                }
                if (boss || total >= 12) ShakeScreen(boss ? 9f : 6f, impact);
                return impact;
            }
            if (move.SummonId != null)
            {
                BattleFx.Aura(fxLayer, ev.transform, new Color(0.78f, 0.55f, 1f), size * 1.05f);
                return 0.22f;
            }
            if (move.Status.HasValue && move.StatusOnSelf)
            {
                BattleFx.Aura(fxLayer, ev.transform, new Color(1f, 0.32f, 0.28f), size * 1.05f);
                if (move.Block > 0) BattleFx.Shield(fxLayer, ev.transform, BattleSystem.ColBlock, size * 0.95f, 0.05f);
                if (boss) ShakeScreen(6f, 0.22f);
                return 0.22f;
            }
            if (move.Block > 0)
            {
                BattleFx.Shield(fxLayer, ev.transform, BattleSystem.ColBlock, size * 0.95f);
                return 0.2f;
            }
            if (move.Status.HasValue)
            {
                const float charge = 0.07f, flight = 0.18f;
                Vector3 mouth = ev.SpriteWorldPosition;
                BattleFx.Charge(fxLayer, mouth, BattleSystem.ColStatus, size * 0.5f, charge + 0.1f);
                BattleFxProjectile.Launch(fxLayer, mouth, heroRt, BattleSystem.ColStatus, BattleSystem.ColStatus, charge, flight, 36f, 50f, 0.8f);
                return charge + flight;
            }
            return 0f;
        }

        /// <summary>Cor dos efeitos de cada inimigo (gosma verde, fogo azulado, poeira do Saci...).</summary>
        static Color EnemyFxColor(EnemyCombatant e)
        {
            switch (e.Data.Id)
            {
                case "sapo": return new Color(0.45f, 0.92f, 0.3f);
                case "fogo": return new Color(0.45f, 0.8f, 1f);
                case "saci": return new Color(0.8f, 0.58f, 0.34f);
                case "morcego": return new Color(0.72f, 0.45f, 1f);
                case "urubu": return new Color(0.9f, 0.42f, 0.35f);
                case "piranha": return new Color(1f, 0.32f, 0.32f);
                case "escorpiao": return new Color(1f, 0.72f, 0.25f);
                case "queixada": return new Color(0.9f, 0.58f, 0.3f);
                default: return new Color(1f, 0.45f, 0.4f);
            }
        }

        static Color EnemyTrailColor(EnemyCombatant e)
        {
            switch (e.Data.Id)
            {
                case "fogo": return new Color(1f, 0.6f, 0.2f);   // fogo-fátuo: núcleo azul, rastro laranja
                case "saci": return new Color(0.62f, 0.47f, 0.32f); // poeira
                case "sapo": return new Color(0.3f, 0.7f, 0.2f);
                default: return EnemyFxColor(e);
            }
        }

        // ------------------------------------------------------------------ Arrastar cartas

        bool BeginCardDrag(CardView view)
        {
            if (battle == null || battle.IsOver || busy || animating || view == null || !handViews.Contains(view)) return false;
            if (!battle.CanPlay(view.Card, out var reason))
            {
                Flash(reason);
                return false;
            }
            dragView = view;
            CardDragStarted?.Invoke(view.Card); // cancela uma mira por clique ativa
            if (dragView != view) return false;
            bool needsTarget = view.Card.NeedsTarget;
            foreach (var ev in enemyViews.Values) if (ev != null) ev.SetTargetable(needsTarget);
            CancelInvoke(nameof(ClearPrompt));
            promptText.text = needsTarget ? "Solte sobre um inimigo" : "Solte na arena para jogar";
            ShowTooltip(null);
            return true;
        }

        void MoveCardDrag(CardView view, PointerEventData e)
        {
            if (view != dragView || battle == null) return;
            bool needsTarget = view.Card.NeedsTarget;
            var enemy = EnemyUnderPointer(e);
            SetDragHover(needsTarget ? enemy : null);
            view.SetDragOverTarget(needsTarget && enemy != null);
            bool arena = IsArenaPoint(e);
            if (needsTarget)
                promptText.text = enemy != null ? $"Soltar em {enemy.Enemy.Name}"
                                : arena ? "Solte sobre um inimigo"
                                : "Solte sobre um inimigo\n<size=17>(soltar na mão cancela)</size>";
            else
                promptText.text = arena || enemy != null ? $"Solte para usar {view.Card.Name}"
                                : "Solte na arena para jogar\n<size=17>(soltar na mão cancela)</size>";
        }

        void EndCardDrag(CardView view, PointerEventData e)
        {
            if (view != dragView) return;
            var enemy = EnemyUnderPointer(e);
            bool arena = IsArenaPoint(e);
            var card = view.Card;
            ClearDrag();
            if (battle == null || battle.IsOver || card == null) return;
            if (enemy == null && !arena) return; // soltou de volta na mão: cancela
            droppedView = view;
            CardDropped?.Invoke(card, enemy != null ? enemy.Enemy : null, arena);
            droppedView = null;
        }

        /// <summary>Cancela o arrasto atual (botão direito / Esc): a carta volta para a mão.</summary>
        public void CancelCardDrag()
        {
            if (dragView != null) ClearDrag();
        }

        void ClearDrag()
        {
            if (dragView != null) dragView.CancelDrag();
            dragView = null;
            SetDragHover(null);
            bool aim = targeting != null;
            foreach (var ev in enemyViews.Values) if (ev != null) ev.SetTargetable(aim);
            if (promptText != null) ResetPrompt();
        }

        void SetDragHover(EnemyView ev)
        {
            if (dragHover == ev) return;
            if (dragHover != null) dragHover.SetHighlighted(false);
            dragHover = ev;
            if (dragHover != null) dragHover.SetHighlighted(true);
        }

        EnemyView EnemyUnderPointer(PointerEventData e)
        {
            var go = e.pointerCurrentRaycast.gameObject;
            if (go == null) return null;
            var ev = go.GetComponentInParent<EnemyView>();
            if (ev == null || ev.Enemy == null || ev.Enemy.IsDead) return null;
            return enemyViews.TryGetValue(ev.Enemy, out var current) && current == ev ? ev : null;
        }

        bool IsArenaPoint(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, e.pressEventCamera, out var local)) return false;
            return local.y - root.rect.yMin >= ArenaMinY;
        }

        // ------------------------------------------------------------------ Quadro a quadro

        void Update()
        {
            float dt = Time.deltaTime;
            if (bannerTime > 0f)
            {
                bannerTime -= dt;
                var c = bannerText.color;
                c.a = Mathf.Clamp01(bannerTime / 0.4f);
                bannerText.color = c;
            }
            else if (bannerText.text.Length > 0)
            {
                bannerText.text = "";
            }

            // Tremor de tela (golpes fortes)
            if (pendingShakeAt >= 0f && Time.time >= pendingShakeAt)
            {
                screenShake = ScreenShakeTime;
                screenShakeStrength = pendingShakeStrength;
                pendingShakeAt = -1f;
            }
            Vector2 screenOffset = Vector2.zero;
            if (screenShake > 0f)
            {
                screenShake -= dt;
                float amount = screenShakeStrength * Mathf.Clamp01(screenShake / ScreenShakeTime);
                screenOffset = new Vector2(Mathf.Sin(Time.time * 97f), Mathf.Cos(Time.time * 83f)) * amount;
            }
            root.anchoredPosition = screenOffset;

            float bob = Mathf.Sin(Time.time * 2.4f) * 6f;
            float shakeX = 0f;
            if (heroShake > 0f)
            {
                heroShake -= dt;
                shakeX = Mathf.Sin(Time.time * 80f) * 12f * heroShake * 3f;
            }
            heroRt.anchoredPosition = HeroPos + new Vector2(shakeX, bob) + HeroLungeOffset(dt);
            heroImage.color = heroShake > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;

            // A carta arrastada foi destruída sem avisar (ex.: mão reconstruída): limpa o estado.
            if (!ReferenceEquals(dragView, null) && dragView == null) ClearDrag();
        }
    }

    /// <summary>
    /// Pedacinho de efeito visual da batalha (brilho, faísca, anel): anda, cresce/encolhe e some sozinho.
    /// Fica na camada de efeitos e nunca bloqueia cliques. Os atalhos estáticos montam efeitos prontos.
    /// </summary>
    public class BattleFx : MonoBehaviour
    {
        public Vector2 Velocity;     // px de referência por segundo
        public Vector2 Offset;       // deslocamento em relação à origem (ou ao alvo seguido)
        public float Drag;           // freio (por segundo)
        public float Gravity;        // px/s² (positivo puxa para baixo)
        public float Life = 0.4f;
        public float Delay;          // espera escondido antes de aparecer
        public float StartScale = 1f;
        public float EndScale = 1f;
        public float Spin;           // graus por segundo
        public Transform Follow;     // acompanha um alvo que se move (herói, inimigo)

        RectTransform rt;
        Image image;
        Color color;
        Vector3 origin;
        float age;

        public static BattleFx Spawn(RectTransform layer, Sprite sprite, Vector3 worldPos, float size, Color color, float life)
        {
            var img = UIFactory.Image(layer, "Efeito", sprite, new Color(color.r, color.g, color.b, 0f), false);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.position = worldPos;
            var fx = img.gameObject.AddComponent<BattleFx>();
            fx.rt = rt;
            fx.image = img;
            fx.color = color;
            fx.origin = worldPos;
            fx.Life = Mathf.Max(0.02f, life);
            return fx;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age < Delay) return;
            float t = (age - Delay) / Life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Velocity.y -= Gravity * dt;
            if (Drag > 0f) Velocity *= Mathf.Exp(-Drag * dt);
            Offset += Velocity * dt;
            if (Follow != null) origin = Follow.position;
            rt.position = origin;
            rt.anchoredPosition += Offset;

            float e = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(StartScale, EndScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            if (Spin != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, Spin * (age - Delay));

            float fadeIn = Mathf.Clamp01(t / 0.12f);
            float fadeOut = 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);
            image.color = new Color(color.r, color.g, color.b, color.a * fadeIn * fadeOut);
        }

        static float Rand(float min, float max) => UnityEngine.Random.Range(min, max);

        /// <summary>Explosão de impacto: clarão, anel que se abre e faíscas.</summary>
        public static void Burst(RectTransform layer, Vector3 world, Color color, float scale, float delay = 0f)
        {
            if (layer == null) return;
            var flash = Spawn(layer, TextureFactory.Glow, world, 180f * scale, new Color(color.r, color.g, color.b, 0.95f), 0.3f);
            flash.StartScale = 0.45f; flash.EndScale = 1.5f; flash.Delay = delay;
            var core = Spawn(layer, TextureFactory.Glow, world, 95f * scale, new Color(1f, 1f, 0.92f, 0.95f), 0.17f);
            core.StartScale = 0.6f; core.EndScale = 1.25f; core.Delay = delay;
            var ring = Spawn(layer, TextureFactory.Ring, world, 140f * scale, new Color(color.r, color.g, color.b, 0.9f), 0.3f);
            ring.StartScale = 0.3f; ring.EndScale = 1.6f; ring.Delay = delay;
            var sparkColor = Color.Lerp(color, Color.white, 0.45f);
            for (int i = 0; i < 8; i++)
            {
                float ang = (i + Rand(-0.3f, 0.3f)) / 8f * Mathf.PI * 2f;
                var spark = Spawn(layer, TextureFactory.Circle, world, Rand(9f, 15f) * scale, sparkColor, Rand(0.22f, 0.34f));
                spark.Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Rand(260f, 460f) * scale);
                spark.Drag = 5f;
                spark.Gravity = 320f;
                spark.EndScale = 0.25f;
                spark.Delay = delay;
            }
        }

        /// <summary>Escudo: anel que se fecha em volta do alvo, brilho e um segundo anel que se abre.</summary>
        public static void Shield(RectTransform layer, Transform follow, Color color, float size, float delay = 0f)
        {
            if (layer == null || follow == null) return;
            var pos = follow.position;
            var glow = Spawn(layer, TextureFactory.Glow, pos, size * 1.35f, new Color(color.r, color.g, color.b, 0.6f), 0.45f);
            glow.Follow = follow; glow.StartScale = 0.7f; glow.EndScale = 1.1f; glow.Delay = delay;
            var ring = Spawn(layer, TextureFactory.Ring, pos, size, new Color(color.r, color.g, color.b, 0.95f), 0.4f);
            ring.Follow = follow; ring.StartScale = 1.6f; ring.EndScale = 0.92f; ring.Delay = delay;
            var echo = Spawn(layer, TextureFactory.Ring, pos, size, new Color(1f, 1f, 1f, 0.75f), 0.32f);
            echo.Follow = follow; echo.StartScale = 0.92f; echo.EndScale = 1.45f; echo.Delay = delay + 0.16f;
        }

        /// <summary>Brilhos subindo (cura, bônus, personagem) com um clarão suave no alvo.</summary>
        public static void Sparkles(RectTransform layer, Transform follow, Color color, float radius, int count, float delay = 0f)
        {
            if (layer == null || follow == null) return;
            var pos = follow.position;
            var glow = Spawn(layer, TextureFactory.Glow, pos, radius * 2.8f, new Color(color.r, color.g, color.b, 0.55f), 0.5f);
            glow.Follow = follow; glow.StartScale = 0.6f; glow.EndScale = 1.2f; glow.Delay = delay;
            var light = Color.Lerp(color, Color.white, 0.35f);
            for (int i = 0; i < count; i++)
            {
                var s = Spawn(layer, TextureFactory.Glow, pos, Rand(20f, 34f), light, Rand(0.45f, 0.7f));
                s.Follow = follow;
                s.Offset = new Vector2(Rand(-radius, radius), Rand(-radius * 0.7f, radius * 0.2f));
                s.Velocity = new Vector2(Rand(-25f, 25f), Rand(120f, 230f));
                s.Drag = 1.5f;
                s.EndScale = 0.3f;
                s.Delay = delay + Rand(0f, 0.15f);
            }
        }

        /// <summary>Aura que pulsa no alvo (fortalecer, invocar): brilho, anel que se abre e fagulhas subindo.</summary>
        public static void Aura(RectTransform layer, Transform follow, Color color, float size, float delay = 0f)
        {
            if (layer == null || follow == null) return;
            var pos = follow.position;
            var glow = Spawn(layer, TextureFactory.Glow, pos, size * 1.4f, new Color(color.r, color.g, color.b, 0.7f), 0.45f);
            glow.Follow = follow; glow.StartScale = 0.6f; glow.EndScale = 1.25f; glow.Delay = delay;
            var ring = Spawn(layer, TextureFactory.Ring, pos, size, new Color(color.r, color.g, color.b, 0.9f), 0.35f);
            ring.Follow = follow; ring.StartScale = 0.5f; ring.EndScale = 1.4f; ring.Delay = delay;
            var light = Color.Lerp(color, Color.white, 0.3f);
            for (int i = 0; i < 6; i++)
            {
                var s = Spawn(layer, TextureFactory.Circle, pos, Rand(8f, 14f), light, Rand(0.35f, 0.5f));
                s.Follow = follow;
                s.Offset = new Vector2(Rand(-size * 0.35f, size * 0.35f), Rand(-size * 0.3f, 0f));
                s.Velocity = new Vector2(0f, Rand(140f, 240f));
                s.Drag = 2f;
                s.EndScale = 0.3f;
                s.Delay = delay + Rand(0f, 0.1f);
            }
        }

        /// <summary>Clarão rápido de "carga" antes de um disparo.</summary>
        public static void Charge(RectTransform layer, Vector3 world, Color color, float size, float life, float delay = 0f)
        {
            if (layer == null) return;
            var g = Spawn(layer, TextureFactory.Glow, world, size, new Color(color.r, color.g, color.b, 0.9f), life);
            g.StartScale = 0.3f;
            g.EndScale = 1.15f;
            g.Delay = delay;
        }
    }

    /// <summary>Projétil (orbe com brilho e rastro) que voa até um alvo e explode no impacto.</summary>
    public class BattleFxProjectile : MonoBehaviour
    {
        RectTransform layer, rt;
        Transform target;
        Vector3 start, to;
        Color color, trail;
        float delay, duration, size, arc, burstScale, age, trailTimer;

        public static BattleFxProjectile Launch(RectTransform layer, Vector3 start, Transform target, Color color, Color trail,
                                                float delay, float duration, float size, float arc, float burstScale)
        {
            if (layer == null) return null;
            var rt = UIFactory.Rect(layer, "Projetil");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.position = start;
            rt.localScale = Vector3.zero;
            var glow = UIFactory.Image(rt, "Brilho", TextureFactory.Glow, new Color(color.r, color.g, color.b, 0.9f), false);
            glow.rectTransform.Stretch(-size * 0.8f, -size * 0.8f, -size * 0.8f, -size * 0.8f);
            var core = UIFactory.Image(rt, "Nucleo", TextureFactory.Circle, Color.Lerp(color, Color.white, 0.6f), false);
            core.rectTransform.Stretch(size * 0.18f, size * 0.18f, size * 0.18f, size * 0.18f);

            var p = rt.gameObject.AddComponent<BattleFxProjectile>();
            p.layer = layer;
            p.rt = rt;
            p.target = target;
            p.start = start;
            p.to = target != null ? target.position : start;
            p.color = color;
            p.trail = trail;
            p.delay = delay;
            p.duration = Mathf.Max(0.05f, duration);
            p.size = size;
            p.arc = arc;
            p.burstScale = burstScale;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age < delay) return;
            if (target != null) to = target.position; // se o alvo sumir, segue para a última posição conhecida
            float t = Mathf.Clamp01((age - delay) / duration);
            float e = t * (0.55f + 0.45f * t); // acelera um pouco até o alvo
            Vector3 pos = Vector3.Lerp(start, to, e);
            if (layer != null) pos += layer.TransformVector(new Vector3(0f, arc * 4f * e * (1f - e), 0f));
            rt.position = pos;
            float s = Mathf.Clamp01((age - delay) / 0.05f);
            rt.localScale = new Vector3(s, s, 1f);

            trailTimer -= dt;
            if (trailTimer <= 0f && layer != null)
            {
                trailTimer = 0.016f;
                var tr = BattleFx.Spawn(layer, TextureFactory.Glow, pos, size * 1.4f, new Color(trail.r, trail.g, trail.b, 0.75f), 0.22f);
                tr.StartScale = 0.9f;
                tr.EndScale = 0.15f;
                rt.SetAsLastSibling(); // o orbe fica por cima do próprio rastro
            }

            if (t >= 1f)
            {
                if (layer != null) BattleFx.Burst(layer, to, color, burstScale);
                Destroy(gameObject);
            }
        }
    }
}
