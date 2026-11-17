using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>Desenho de uma carta (na mão, na escolha de recompensa ou no baralho).</summary>
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float Width = 220f;
        public const float Height = 310f;

        public CardData Card { get; private set; }
        public Action<CardView> OnClicked;
        public Action<CardView, bool> OnHover;
        public bool HoverLift;

        RectTransform rt;
        CanvasGroup group;
        Image glow;
        Text descText;
        Text comboText;
        GameObject comboBadge;
        Vector2 basePos;
        float hoverT;
        bool hovered;
        int restoreSibling = -1;

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

            var border = UIFactory.Panel(rt, "Borda", typeColor);
            border.rectTransform.Stretch();

            var body = UIFactory.Panel(border.transform, "Corpo", new Color(0.1f, 0.08f, 0.13f));
            body.rectTransform.Stretch(6, 6, 6, 6);
            body.raycastTarget = false;

            var title = UIFactory.Text(body.transform, "Nome", card.Name, 21, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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
            var cost = UIFactory.Text(costBg.transform, "Valor", card.Cost.ToString(), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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

        public void SetPlayable(bool playable) => group.alpha = playable ? 1f : 0.5f;

        public void SetSelected(bool selected) => glow.color = selected ? new Color(1f, 0.85f, 0.3f, 0.95f) : Color.clear;

        public void SetBasePosition(Vector2 pos, bool instant)
        {
            basePos = pos;
            if (instant) rt.anchoredPosition = pos;
        }

        void Update()
        {
            if (!HoverLift) return;
            hoverT = Mathf.MoveTowards(hoverT, hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            var target = basePos + new Vector2(0f, 60f * hoverT);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            float s = 1f + 0.12f * hoverT;
            rt.localScale = new Vector3(s, s, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (HoverLift)
            {
                restoreSibling = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
            }
            OnHover?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (HoverLift && restoreSibling >= 0 && restoreSibling < transform.parent.childCount)
                transform.SetSiblingIndex(restoreSibling);
            restoreSibling = -1;
            OnHover?.Invoke(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnClicked?.Invoke(this);
        }
    }
}
