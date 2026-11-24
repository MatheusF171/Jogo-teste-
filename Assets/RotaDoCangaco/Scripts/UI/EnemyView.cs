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
        const float LungeReturn = 0.22f;

        public EnemyCombatant Enemy { get; private set; }
        public Action<EnemyView> OnClicked;
        public Action<EnemyView, bool> OnHover;

        RectTransform rt;
        Image sprite;
        Image targetRing;
        Image highlightGlow;
        Image intentBg;
        Text intentText;
        Text statusText;
        Text blockText;
        GameObject blockBadge;
        UIBar hpBar;
        Vector2 targetPos;
        Vector2 spriteBase;
        float targetScale = 1f;
        float flash;
        float shake;
        bool targetable;
        bool highlighted;
        float highlightT;
        bool dying;
        float deathT;

        // Bote (ataque corpo a corpo): recua, dispara até o pico (impacto) e volta sozinho.
        Vector2 lungeDir;
        float lungeT = -1f;
        float lungeWindup;
        float lungeDistance;

        public Vector2 PopupPosition => rt.anchoredPosition + new Vector2(0f, 60f);

        /// <summary>Escala atual na arena (inimigos mais longe ficam menores).</summary>
        public float CurrentScale => rt.localScale.x;

        /// <summary>Tamanho do sprite sem escala (para dimensionar os efeitos).</summary>
        public float VisualSize { get; private set; }

        /// <summary>Centro do sprite em coordenadas de mundo (já com bote e tremida).</summary>
        public Vector3 SpriteWorldPosition => sprite.rectTransform.position;

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
            VisualSize = size;
            rt.sizeDelta = new Vector2(size + 40f, size + 40f);

            // área clicável invisível (também é onde a carta arrastada é solta)
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            // Chefe: só o personagem (sem moldura de carta), um pouco mais alto e com a sombra nos pés.
            Vector2 spriteSize = boss ? new Vector2(size * 0.95f, size * 1.08f) : new Vector2(size, size);
            spriteBase = boss ? new Vector2(0f, 4f) : Vector2.zero;

            var shadow = UIFactory.Image(rt, "Sombra", TextureFactory.Shadow, new Color(1, 1, 1, 0.9f), false);
            if (boss)
                shadow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, spriteBase.y - spriteSize.y * 0.46f), new Vector2(size * 0.75f, size * 0.2f));
            else
                shadow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.42f), new Vector2(size * 0.9f, size * 0.25f));

            targetRing = UIFactory.Image(rt, "Alvo", TextureFactory.Ring, new Color(1f, 0.85f, 0.3f, 0f), false);
            targetRing.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.15f, size * 1.15f));

            // brilho dourado atrás do sprite quando uma carta arrastada está sobre ele
            highlightGlow = UIFactory.Image(rt, "Destaque", TextureFactory.Glow, new Color(1f, 0.85f, 0.35f, 0f), false);
            highlightGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), spriteBase, new Vector2(size * 1.25f, size * 1.25f));

            sprite = UIFactory.Image(rt, "Sprite", SpriteLibrary.Get(enemy.Data.SpriteKey));
            sprite.preserveAspect = true;
            sprite.rectTransform.Place(new Vector2(0.5f, 0.5f), spriteBase, spriteSize);

            intentBg = UIFactory.Panel(rt, "Intencao", new Color(0.17f, 0.09f, 0.04f, 0.92f), TextureFactory.RoundedRectSmall);
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

        /// <summary>Destaque forte: a carta arrastada está sobre este inimigo.</summary>
        public void SetHighlighted(bool value) => highlighted = value;

        public void Hit()
        {
            flash = 1f;
            shake = 0.25f;
        }

        /// <summary>
        /// Bote em direção a um ponto (o herói): recua um pouco, dispara e chega ao pico em <paramref name="windup"/>
        /// segundos (momento do impacto) e depois volta sozinho. <paramref name="distance"/> em unidades locais.
        /// </summary>
        public void Lunge(Vector3 targetWorld, float windup, float distance)
        {
            if (dying) return;
            Vector2 local = rt.InverseTransformPoint(targetWorld);
            local -= spriteBase;
            lungeDir = local.sqrMagnitude > 1f ? local.normalized : Vector2.down;
            lungeWindup = Mathf.Max(0.05f, windup);
            lungeDistance = distance;
            lungeT = 0f;
            transform.SetAsLastSibling(); // passa por cima dos outros inimigos durante o bote
        }

        public void Die(Action onDone)
        {
            dying = true;
            deathT = 0f;
            highlighted = false;
            targetable = false;
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

        /// <summary>Quanto do bote está aplicado agora (-0,18 recuando · 1 no impacto · 0 parado).</summary>
        float LungeAmount(float dt)
        {
            if (lungeT < 0f) return 0f;
            lungeT += dt;
            float back = lungeWindup * 0.4f;
            if (lungeT < back) return -0.18f * Mathf.Sin(lungeT / back * Mathf.PI * 0.5f);
            if (lungeT < lungeWindup)
            {
                float k = (lungeT - back) / (lungeWindup - back);
                return Mathf.Lerp(-0.18f, 1f, k * k);
            }
            float r = (lungeT - lungeWindup) / LungeReturn;
            if (r >= 1f)
            {
                lungeT = -1f;
                return 0f;
            }
            return 1f - r * r * (3f - 2f * r);
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
            float lunge = LungeAmount(dt);
            rt.anchoredPosition = pos;
            sprite.rectTransform.anchoredPosition = spriteBase + offset + lungeDir * (lungeDistance * lunge);

            flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
            var col = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), flash);

            highlightT = Mathf.MoveTowards(highlightT, highlighted && !dying ? 1f : 0f, dt * 8f);
            float spriteScale = 1f + 0.08f * Mathf.Max(0f, lunge) + 0.05f * highlightT;
            if (dying)
            {
                deathT += dt;
                col.a = Mathf.Clamp01(1f - deathT / 0.6f);
                spriteScale = 1f + deathT * 0.5f;
            }
            sprite.rectTransform.localScale = new Vector3(spriteScale, spriteScale, 1f);
            sprite.color = col;

            var ring = targetRing.color;
            float pulse = targetable && !dying ? 0.55f + Mathf.Sin(Time.time * 6f) * 0.35f : 0f;
            ring.a = Mathf.Lerp(pulse, 1f, highlightT);
            targetRing.color = ring;
            float ringScale = 1f + 0.06f * highlightT;
            targetRing.rectTransform.localScale = new Vector3(ringScale, ringScale, 1f);

            var glowCol = highlightGlow.color;
            glowCol.a = 0.5f * highlightT;
            highlightGlow.color = glowCol;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => OnHover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData eventData) => OnHover?.Invoke(this, false);
    }
}
