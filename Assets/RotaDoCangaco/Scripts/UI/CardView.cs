using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>Desenho de uma carta (na mão, na escolha de recompensa ou no baralho).</summary>
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
                            IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 220f;
        public const float Height = 310f;
        const float DragScale = 0.82f;
        const float LeaveTime = 0.22f;

        /// <summary>Carta sendo arrastada agora (enquanto isso, as outras não levantam no hover).</summary>
        static CardView activeDrag;

        public CardData Card { get; private set; }
        public Action<CardView> OnClicked;
        public Action<CardView, bool> OnHover;
        public bool HoverLift;

        /// <summary>Só as cartas da mão na batalha podem ser arrastadas.</summary>
        public bool Draggable;
        /// <summary>Chamado ao começar a arrastar; retornar false cancela o arrasto.</summary>
        public Func<CardView, bool> OnDragStart;
        public Action<CardView, PointerEventData> OnDragMove;
        public Action<CardView, PointerEventData> OnDragEnd;
        /// <summary>A ordem de desenho mudou (hover entrou/saiu, arrasto começou/terminou): quem monta a mão reordena.</summary>
        public Action<CardView> OnOrderChanged;

        public bool IsDragging => dragging;
        public bool IsHovered => hovered;

        RectTransform rt;
        CanvasGroup group;
        Image glow;
        Image border;           // única área clicável da carta
        Text descText;
        Text comboText;
        GameObject comboBadge;
        Vector2 basePos;
        float hoverT;
        bool hovered;
        bool pendingHover;      // o ponteiro entrou enquanto outra carta era arrastada

        bool dragging;
        bool dragOverTarget;    // sobre um inimigo: encolhe e fica translúcida para o alvo aparecer
        Vector2 dragPos;
        float tilt;
        bool leaving;
        float leaveT;
        Vector2 leaveFrom;
        float leaveScale = 1f;

        public static Color TypeColor(CardData card)
        {
            if (card.Rarity == CardRarity.Chefe) return new Color(1f, 0.18f, 0.44f);
            switch (card.Type)
            {
                case CardType.Ataque: return new Color(0.85f, 0.28f, 0.37f);
                case CardType.Defesa: return new Color(0.24f, 0.55f, 0.99f);
                case CardType.Habilidade: return new Color(0.61f, 0.35f, 0.82f);
                case CardType.Bonus: return new Color(0.88f, 0.65f, 0.15f);
                default: return new Color(0.18f, 0.77f, 0.71f);
            }
        }

        public static CardView Create(Transform parent, CardData card)
        {
            var root = UIFactory.Rect(parent, "Carta " + card.Name);
            root.sizeDelta = new Vector2(Width, Height);
            var view = root.gameObject.AddComponent<CardView>();
            view.Build(card);
            return view;
        }

        void Build(CardData card)
        {
            Card = card;
            rt = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            Color typeColor = TypeColor(card);

            glow = UIFactory.Panel(rt, "Brilho", Color.clear);
            glow.rectTransform.Stretch(-10, -10, -10, -10);
            glow.raycastTarget = false;

            border = UIFactory.Panel(rt, "Borda", typeColor);
            border.rectTransform.Stretch();

            var body = UIFactory.Panel(border.transform, "Corpo", new Color(0.16f, 0.1f, 0.06f));
            body.rectTransform.Stretch(6, 6, 6, 6);
            body.raycastTarget = false;
            // Leve brilho no topo do corpo (couro), sem poluir a carta.
            var sheen = UIFactory.Image(body.transform, "Brilho", TextureFactory.VerticalFade, new Color(1f, 0.8f, 0.5f, 0.07f), false);
            sheen.rectTransform.Stretch();
            sheen.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            var title = UIFactory.Text(body.transform, "Nome", card.Name, 21, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle();
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(10, -30), new Vector2(170, 48));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 21;
            title.verticalOverflow = VerticalWrapMode.Truncate;

            var artBg = UIFactory.Panel(body.transform, "FundoArte", Color.Lerp(card.Tint, Color.black, 0.72f));
            artBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(190, 104));
            artBg.raycastTarget = false;
            var art = UIFactory.Image(artBg.transform, "Arte", TextureFactory.CardIcon(card));
            art.rectTransform.Stretch(6, 6, 6, 6);

            string typeLabel = card.TypeLabel;
            if (card.IsWildcard) typeLabel += " · CORINGA";
            if (card.Rarity == CardRarity.Rara) typeLabel += " · RARA";
            if (card.Rarity == CardRarity.Chefe) typeLabel = "HABILIDADE DE CHEFE";
            var type = UIFactory.Text(body.transform, "Tipo", typeLabel, 14, Color.Lerp(typeColor, Color.white, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
            type.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -175), new Vector2(200, 22));

            descText = UIFactory.Text(body.transform, "Descricao", "", 17, new Color(0.95f, 0.93f, 0.88f), TextAnchor.UpperCenter);
            descText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(194, 108));
            descText.resizeTextForBestFit = true;
            descText.resizeTextMinSize = 12;
            descText.resizeTextMaxSize = 17;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            var costBg = UIFactory.Image(rt, "Custo", TextureFactory.Circle,
                card.IsWildcard ? new Color(1f, 0.55f, 0.85f) : new Color(1f, 0.78f, 0.22f), false);
            costBg.rectTransform.Place(new Vector2(0f, 1f), new Vector2(16, -16), new Vector2(54, 54));
            var costRing = UIFactory.Image(costBg.transform, "Aro", TextureFactory.Ring, new Color(0.25f, 0.12f, 0.02f), false);
            costRing.rectTransform.Stretch();
            var cost = UIFactory.Text(costBg.transform, "Valor", card.Cost.ToString(), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).SignStyle(Color.white);
            cost.rectTransform.Stretch();

            var badge = UIFactory.Panel(rt, "Combo", new Color(0.05f, 0.25f, 0.08f, 0.95f), TextureFactory.RoundedRectSmall);
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-30, 8), new Vector2(92, 30));
            badge.raycastTarget = false;
            comboText = UIFactory.Text(badge.transform, "Texto", "", 17, new Color(0.6f, 1f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
            comboText.rectTransform.Stretch();
            comboBadge = badge.gameObject;
            comboBadge.SetActive(false);

            Refresh(null, 1f, null, false);
        }

        /// <summary>Atualiza texto (valores com combo) e o selo de combo ("x1.25" / "quebra").</summary>
        public void Refresh(BattleSystem battle, float mult, string comboLabel, bool comboGood)
        {
            descText.text = Card.Describe(battle, mult);
            comboBadge.SetActive(!string.IsNullOrEmpty(comboLabel));
            if (comboLabel == null) return;
            comboText.text = comboLabel;
            comboText.color = comboGood ? new Color(0.6f, 1f, 0.6f) : new Color(0.85f, 0.85f, 0.85f);
            comboBadge.GetComponent<Image>().color = comboGood ? new Color(0.05f, 0.3f, 0.1f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.9f);
        }

        public void SetPlayable(bool playable)
        {
            if (!leaving) group.alpha = playable ? 1f : 0.5f;
        }

        public void SetSelected(bool selected) => glow.color = selected ? new Color(1f, 0.85f, 0.3f, 0.95f) : Color.clear;

        public void SetBasePosition(Vector2 pos, bool instant)
        {
            basePos = pos;
            if (instant && !dragging && !leaving) rt.anchoredPosition = pos;
        }

        /// <summary>A carta jogada sobe, encolhe e some enquanto o herói ataca (quem a destrói é a BattleView).</summary>
        public void PlayOut()
        {
            if (dragging) StopDrag();
            leaving = true;
            leaveT = 0f;
            leaveFrom = rt.anchoredPosition;
            leaveScale = rt.localScale.x;
            hovered = false;
            pendingHover = false;
            Draggable = false;
            OnClicked = null;
            OnDragStart = null;
            OnDragMove = null;
            OnDragEnd = null;
            OnOrderChanged = null;
            group.blocksRaycasts = false;
            border.raycastPadding = Vector4.zero;
            SetSelected(false);
            transform.SetAsLastSibling();
        }

        /// <summary>Cancela um arrasto em andamento (a carta volta para a mão).</summary>
        public void CancelDrag()
        {
            if (dragging) StopDrag();
        }

        void Update()
        {
            if (!HoverLift && !dragging && !leaving) return;
            float dt = Time.unscaledDeltaTime;

            if (leaving)
            {
                leaveT += dt;
                float k = Mathf.Clamp01(leaveT / LeaveTime);
                float e = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = leaveFrom + new Vector2(0f, 150f * e);
                float ls = Mathf.Lerp(leaveScale, leaveScale * 0.55f, e);
                rt.localScale = new Vector3(ls, ls, 1f);
                group.alpha = 1f - k;
                return;
            }

            if (dragging)
            {
                var prev = rt.anchoredPosition;
                rt.anchoredPosition = Vector2.Lerp(prev, dragPos, 1f - Mathf.Exp(-30f * dt));
                float vx = dt > 0f ? (rt.anchoredPosition.x - prev.x) / dt : 0f;
                tilt = Mathf.Lerp(tilt, Mathf.Clamp(-vx * 0.012f, -14f, 14f), 1f - Mathf.Exp(-12f * dt));
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
                float ds = Mathf.Lerp(rt.localScale.x, dragOverTarget ? 0.5f : DragScale, 1f - Mathf.Exp(-16f * dt));
                rt.localScale = new Vector3(ds, ds, 1f);
                group.alpha = Mathf.Lerp(group.alpha, dragOverTarget ? 0.5f : 1f, 1f - Mathf.Exp(-16f * dt));
                // fica por cima das outras cartas da mão
                var parent = transform.parent;
                if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1) transform.SetAsLastSibling();
                return;
            }

            // O ponteiro ficou sobre esta carta durante o arrasto de outra: aplica o hover assim que o arrasto acaba.
            if (pendingHover && activeDrag == null && !hovered) EnterHover();

            hoverT = Mathf.MoveTowards(hoverT, hovered ? 1f : 0f, dt * 8f);
            var target = basePos + new Vector2(0f, 60f * hoverT);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, 1f - Mathf.Exp(-18f * dt));
            float s = Mathf.Lerp(rt.localScale.x, 1f + 0.12f * hoverT, 1f - Mathf.Exp(-20f * dt));
            rt.localScale = new Vector3(s, s, 1f);
            // Enquanto sobe, a área clicável se estende para baixo: o ponteiro perto da base da carta não "escapa"
            // e a carta não fica subindo e descendo sem parar.
            border.raycastPadding = new Vector4(0f, -80f * hoverT, 0f, 0f);
            if (tilt != 0f)
            {
                tilt = Mathf.Lerp(tilt, 0f, 1f - Mathf.Exp(-14f * dt));
                if (Mathf.Abs(tilt) < 0.05f) tilt = 0f;
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }
        }

        // ------------------------------------------------------------------ Arrastar

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Draggable || leaving) return; // fora da mão: ignora (o clique continua valendo)
            if (eventData.button != PointerEventData.InputButton.Left || (OnDragStart != null && !OnDragStart(this)))
            {
                eventData.pointerDrag = null; // cancela o arrasto (e o clique desta vez)
                return;
            }
            dragging = true;
            activeDrag = this;
            pendingHover = false;
            group.blocksRaycasts = false; // o raycast passa a enxergar o inimigo embaixo da carta
            transform.SetAsLastSibling();
            UpdateDragPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            UpdateDragPosition(eventData);
            OnDragMove?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            StopDrag();
            OnDragEnd?.Invoke(this, eventData);
        }

        void UpdateDragPosition(PointerEventData eventData)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var local))
                return;
            var r = parent.rect;
            var anchorPoint = new Vector2(Mathf.Lerp(r.xMin, r.xMax, rt.anchorMin.x), Mathf.Lerp(r.yMin, r.yMax, rt.anchorMin.y));
            dragPos = local - anchorPoint;
        }

        /// <summary>A carta arrastada está sobre um alvo válido (a tela de batalha avisa a cada movimento).</summary>
        public void SetDragOverTarget(bool value) => dragOverTarget = value && dragging;

        void StopDrag()
        {
            dragging = false;
            dragOverTarget = false;
            if (activeDrag == this) activeDrag = null;
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.alpha = 1f; // o próximo Refresh reaplica o "não jogável" se for o caso
            }
            hovered = false;
            OnOrderChanged?.Invoke(this);
        }

        void OnDisable()
        {
            // Sem mexer na hierarquia aqui (pode ser chamado durante a desativação/destruição do pai).
            pendingHover = false;
            if (!dragging) return;
            dragging = false;
            if (activeDrag == this) activeDrag = null;
            if (group != null) group.blocksRaycasts = true;
            hovered = false;
        }

        void OnDestroy()
        {
            if (activeDrag == this) activeDrag = null;
        }

        // ------------------------------------------------------------------ Ponteiro

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (leaving || dragging) return;
            if (activeDrag != null && activeDrag != this)
            {
                pendingHover = true; // aplicado no Update quando o arrasto da outra carta terminar
                return;
            }
            EnterHover();
        }

        void EnterHover()
        {
            pendingHover = false;
            hovered = true;
            if (HoverLift) OnOrderChanged?.Invoke(this); // a mão coloca a carta com hover por cima
            OnHover?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pendingHover = false;
            if (dragging)
            {
                OnHover?.Invoke(this, false); // durante o arrasto continua por cima das outras
                return;
            }
            hovered = false;
            if (HoverLift) OnOrderChanged?.Invoke(this);
            OnHover?.Invoke(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragging || leaving) return;
            if (Draggable && eventData.dragging) return; // soltou depois de arrastar: não abre a mira
            if (eventData.button == PointerEventData.InputButton.Left) OnClicked?.Invoke(this);
        }
    }
}
