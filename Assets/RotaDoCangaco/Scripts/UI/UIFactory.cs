using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>Visual dos botões padrão: placas de madeira no estilo do logo (sertão / faroeste).</summary>
    public enum ButtonStyle
    {
        Primary,    // madeira clara com aro dourado (ação principal)
        Secondary   // madeira escura com aro de bronze
    }

    /// <summary>
    /// Atalhos para montar a interface (UGUI) por código, sem prefabs.
    /// Coordenadas no espaço de referência 1920x1080 (CanvasScaler).
    /// </summary>
    public static class UIFactory
    {
        static Font font;

        public static readonly Color PanelColor = new Color(0.15f, 0.09f, 0.05f, 0.92f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.25f);
        public static readonly Color SignText = new Color(1f, 0.91f, 0.72f);
        public static readonly Color SignTextOutline = new Color(0.16f, 0.07f, 0.02f, 1f);
        /// <summary>Texto corrido sobre os painéis de couro (creme suave).</summary>
        public static readonly Color BodyText = new Color(0.96f, 0.9f, 0.8f);

        /// <summary>Painel de couro com aro de bronze (tema do menu principal).</summary>
        public static Image FramePanel(Transform parent, string name)
        {
            var img = Panel(parent, name, Color.white, TextureFactory.FramePanel);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Texto no estilo das placas: creme com contorno marrom.</summary>
        public static Text SignStyle(this Text text, Color? color = null)
        {
            text.color = color ?? SignText;
            var o = text.GetComponent<Outline>();
            if (o == null) o = text.gameObject.AddComponent<Outline>();
            o.effectColor = SignTextOutline;
            o.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font = null; }
                }
                return font;
            }
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Posiciona pelo ponto de âncora (0..1) com posição e tamanho em pixels de referência.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Estica para preencher o pai, com margens.</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : TextureFactory.RoundedRect;
            img.type = img.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            return img;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color? color = null, bool preserveAspect = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(Transform parent, string name, string content, int size, Color color,
                                TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, bool outline = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.8f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Action onClick, Color background, int fontSize = 30)
        {
            var img = Panel(parent, name, background);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = Text(img.transform, "Texto", label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.Stretch(10, 4, 10, 4);
            return btn;
        }

        /// <summary>Botão padrão do jogo: placa de madeira com aro metálico e texto creme.</summary>
        public static Button Button(Transform parent, string name, string label, Action onClick, ButtonStyle style = ButtonStyle.Primary, int fontSize = 30)
        {
            var btn = Button(parent, name, label, onClick, Color.white, fontSize);
            var img = (Image)btn.targetGraphic;
            img.sprite = style == ButtonStyle.Primary ? TextureFactory.WoodButton : TextureFactory.WoodButtonDark;
            img.type = UnityEngine.UI.Image.Type.Sliced;

            var colors = btn.colors;
            colors.normalColor = new Color(0.94f, 0.94f, 0.94f);
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            btn.colors = colors;

            var text = btn.GetComponentInChildren<Text>();
            text.color = SignText;
            text.rectTransform.Stretch(22, 6, 22, 8);
            var outline = text.GetComponent<Outline>();
            outline.effectColor = SignTextOutline;
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);

            btn.gameObject.AddComponent<ButtonHover>();
            return btn;
        }

        public static void SetLabel(this Button button, string label)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }
    }

    /// <summary>A placa cresce um pouco quando o mouse passa por cima (só se o botão estiver ativo).</summary>
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float HoverScale = 1.05f;
        Button button;
        bool hovered;

        void Awake() => button = GetComponent<Button>();

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        void OnDisable()
        {
            hovered = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float target = hovered && (button == null || button.interactable) ? HoverScale : 1f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Barra de progresso simples (vida, XP).</summary>
    public class UIBar
    {
        public readonly RectTransform Root;
        readonly RectTransform fill;
        readonly Text label;

        public UIBar(Transform parent, string name, Color fillColor, int fontSize)
        {
            Root = UIFactory.Panel(parent, name, new Color(0.1f, 0.05f, 0.02f, 0.8f), TextureFactory.RoundedRectSmall).rectTransform;
            var f = UIFactory.Panel(Root, "Preenchimento", fillColor, TextureFactory.RoundedRectSmall);
            f.raycastTarget = false;
            fill = f.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(3, 3);
            fill.offsetMax = new Vector2(-3, -3);
            label = UIFactory.Text(Root, "Texto", "", fontSize, Color.white);
            label.rectTransform.Stretch();
            Root.GetComponent<Image>().raycastTarget = false;
        }

        public void Set(float value, float max, string text)
        {
            float ratio = max <= 0 ? 0f : Mathf.Clamp01(value / max);
            fill.anchorMax = new Vector2(ratio, 1f);
            fill.gameObject.SetActive(ratio > 0.001f);
            label.text = text;
        }
    }

    /// <summary>Número flutuante (dano, cura, XP) que sobe e some.</summary>
    public class FloatingText : MonoBehaviour
    {
        Text text;
        RectTransform rt;
        float age;
        const float Life = 1.1f;

        public static void Spawn(Transform parent, Vector2 anchoredPos, string content, Color color, int size = 34)
        {
            var t = UIFactory.Text(parent, "Flutuante", content, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            t.rectTransform.sizeDelta = new Vector2(320, 60);
            t.rectTransform.anchoredPosition = anchoredPos + new Vector2(UnityEngine.Random.Range(-30f, 30f), 0f);
            var ft = t.gameObject.AddComponent<FloatingText>();
            ft.text = t;
            ft.rt = t.rectTransform;
        }

        void Update()
        {
            age += Time.deltaTime;
            rt.anchoredPosition += new Vector2(0f, 70f * Time.deltaTime);
            var c = text.color;
            c.a = Mathf.Clamp01(1f - (age - Life * 0.5f) / (Life * 0.5f));
            text.color = c;
            float s = 1f + Mathf.Max(0f, 0.25f - age) * 2f;
            rt.localScale = new Vector3(s, s, 1f);
            if (age >= Life) Destroy(gameObject);
        }
    }
}
