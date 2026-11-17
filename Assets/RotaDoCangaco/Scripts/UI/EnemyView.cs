using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>Representação de um inimigo na arena: sprite, intenção, vida, escudo e status.</summary>
    public class EnemyView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public EnemyCombatant Enemy { get; private set; }
        public Action<EnemyView> OnClicked;
        public Action<EnemyView, bool> OnHover;

        RectTransform rt;
        Image sprite;
        Image targetRing;
        Image intentBg;
        Text intentText;
        Text statusText;
        Text blockText;
        GameObject blockBadge;
        UIBar hpBar;
        Vector2 targetPos;
        float targetScale = 1f;
        float flash;
        float shake;
        bool targetable;
        bool dying;
        float deathT;

        public Vector2 PopupPosition => rt.anchoredPosition + new Vector2(0f, 60f);

        public static EnemyView Create(Transform parent, EnemyCombatant enemy)
        {
            var root = UIFactory.Rect(parent, "Inimigo " + enemy.Name);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            var view = root.gameObject.AddComponent<EnemyView>();
            view.Build(enemy);
            return view;
        }

        void Build(EnemyCombatant enemy)
        {
            Enemy = enemy;
            rt = (RectTransform)transform;
            bool boss = enemy.Data.IsBoss;
            float size = boss ? 320f : 220f;
            rt.sizeDelta = new Vector2(size + 40f, size + 40f);

            // área clicável invisível
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var shadow = UIFactory.Image(rt, "Sombra", TextureFactory.Shadow, new Color(1, 1, 1, 0.9f), false);
            shadow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.42f), new Vector2(size * 0.9f, size * 0.25f));

            targetRing = UIFactory.Image(rt, "Alvo", TextureFactory.Ring, new Color(1f, 0.85f, 0.3f, 0f), false);
            targetRing.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.15f, size * 1.15f));

            if (boss)
            {
                var frame = UIFactory.Panel(rt, "Moldura", new Color(1f, 0.18f, 0.44f));
                frame.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.97f + 12, size + 12));
                frame.raycastTarget = false;
            }
            sprite = UIFactory.Image(rt, "Sprite", SpriteLibrary.Get(enemy.Data.SpriteKey));
            sprite.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(boss ? size * 0.97f : size, size));

            intentBg = UIFactory.Panel(rt, "Intencao", new Color(0.05f, 0.04f, 0.08f, 0.9f), TextureFactory.RoundedRectSmall);
            intentBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 28), new Vector2(250, 60));
            intentBg.raycastTarget = false;
            intentText = UIFactory.Text(intentBg.transform, "Texto", "", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            intentText.rectTransform.Stretch(4, 2, 4, 2);

            var title = UIFactory.Text(rt, "Nome", $"{enemy.Name}  <size=16>Nv.{enemy.Data.Level}</size>", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, -6), new Vector2(300, 30));

            hpBar = new UIBar(rt, "Vida", new Color(0.85f, 0.2f, 0.25f), 16);
            hpBar.Root.Place(new Vector2(0.5f, 0f), new Vector2(0, -35), new Vector2(boss ? 300 : 200, 24));

            var badge = UIFactory.Image(rt, "Escudo", TextureFactory.Circle, new Color(0.25f, 0.55f, 1f), false);
            badge.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(boss ? -172 : -122, -35), new Vector2(40, 40));
            blockText = UIFactory.Text(badge.transform, "Valor", "", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            blockText.rectTransform.Stretch();
            blockBadge = badge.gameObject;

            statusText = UIFactory.Text(rt, "Status", "", 17, Color.white);
            statusText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, -62), new Vector2(320, 24));
        }

        public void SetLayout(Vector2 position, float scale, bool instant)
        {
            targetPos = position;
            targetScale = scale;
            if (!instant) return;
            rt.anchoredPosition = position;
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh(BattleSystem battle)
        {
            if (Enemy.IsDead) return;
            hpBar.Set(Enemy.Hp, Enemy.MaxHp, $"{Enemy.Hp}/{Enemy.MaxHp}");
            blockBadge.SetActive(Enemy.Block > 0);
            blockText.text = Enemy.Block.ToString();

            var sb = new StringBuilder();
            foreach (var kv in Enemy.AllStatuses)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"<color={Statuses.Hex(kv.Key)}>{Statuses.Name(kv.Key)} {kv.Value}</color>");
            }
            string dist = Enemy.Distance == 0 ? "Perto" : Enemy.Distance == 1 ? "Médio" : "Longe";
            if (sb.Length > 0) sb.Append("  ");
            sb.Append($"<color=#AAAAAA>[{dist}]</color>");
            statusText.text = sb.ToString();

            intentText.text = Enemy.DescribeIntent(battle, out var color);
            intentText.color = color;
        }

        public void SetTargetable(bool value) => targetable = value;

        public void Hit()
        {
            flash = 1f;
            shake = 0.25f;
        }

        public void Die(Action onDone)
        {
            dying = true;
            deathT = 0f;
            intentBg.gameObject.SetActive(false);
            OnClicked = null;
            StartCoroutine(DieRoutine(onDone));
        }

        System.Collections.IEnumerator DieRoutine(Action onDone)
        {
            while (deathT < 0.6f) yield return null;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var pos = Vector2.Lerp(rt.anchoredPosition, targetPos, 1f - Mathf.Exp(-8f * dt));
            float s = Mathf.Lerp(rt.localScale.x, targetScale, 1f - Mathf.Exp(-8f * dt));
            rt.localScale = new Vector3(s, s, 1f);

            Vector2 offset = Vector2.zero;
            if (shake > 0f)
            {
                shake -= dt;
                offset = new Vector2(Mathf.Sin(Time.time * 90f) * 10f * shake * 4f, 0f);
            }
            rt.anchoredPosition = pos;
            sprite.rectTransform.anchoredPosition = offset;

            flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
            var col = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), flash);

            if (dying)
            {
                deathT += dt;
                col.a = Mathf.Clamp01(1f - deathT / 0.6f);
                sprite.rectTransform.localScale = Vector3.one * (1f + deathT * 0.5f);
            }
            sprite.color = col;

            var ring = targetRing.color;
            ring.a = targetable && !dying ? 0.55f + Mathf.Sin(Time.time * 6f) * 0.35f : 0f;
            targetRing.color = ring;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => OnHover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData eventData) => OnHover?.Invoke(this, false);
    }
}
