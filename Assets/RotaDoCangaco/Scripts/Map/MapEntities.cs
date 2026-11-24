using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Qualquer coisa que ocupa um tile do mapa e reage quando o jogador pisa nele.
    /// Cada tipo decide sua aparência, sua dica e o que acontece na interação (polimorfismo).
    /// </summary>
    public abstract class MapEntity
    {
        public Vector2Int Pos;
        public GameObject View;
        public SpriteRenderer Renderer;
        public bool Removed;

        public abstract string Tooltip { get; }
        public abstract Color MinimapColor { get; }
        public abstract Sprite Sprite { get; }
        public virtual float SpriteSize => 0.95f;
        public virtual bool Visible => true;
        public virtual bool IsUsed => false;
        public virtual Color GlowColor => Color.clear;

        public abstract void Interact(GameManager game);
    }

    public class EncounterEntity : MapEntity
    {
        public readonly char Key;
        public readonly EncounterData Data;
        public Vector2Int Origin;
        public int StunSteps;   // tonto depois que o herói pulou por cima (não persegue)

        public int MaxLevel
        {
            get
            {
                int max = 0;
                foreach (var e in Data.Enemies) max = Mathf.Max(max, EnemyDatabase.Get(e.Key).Level);
                return max;
            }
        }

        public EncounterEntity(char key, EncounterData data, Vector2Int pos)
        {
            Key = key;
            Data = data;
            Pos = Origin = pos;
        }

        public override string Tooltip =>
            $"<b>{Data.Title}</b>: {Data.Summary()}" + (Data.Roams ? "  <color=#FFB0B0>(ronda a área e persegue você!)</color>" : "");

        public override Color MinimapColor => new Color(1f, 0.3f, 0.3f);
        public override Sprite Sprite => SpriteLibrary.Get(Data.Leader.SpriteKey);
        public override Color GlowColor => new Color(1f, 0.2f, 0.2f, 0.25f);

        public override void Interact(GameManager game) => game.OfferEncounter(this, false);
    }

    public class BossEntity : EncounterEntity
    {
        public BossEntity(char key, EncounterData data, Vector2Int pos) : base(key, data, pos) { }

        public override string Tooltip => $"<b>CHEFE: {Data.Leader.Name}</b> — {Data.Title}";
        public override Color MinimapColor => new Color(1f, 0.2f, 0.6f);
        public override float SpriteSize => 1.7f;
        public override Color GlowColor => new Color(1f, 0.25f, 0.35f, 0.45f);

        public override void Interact(GameManager game) => game.ShowBossIntro(this);
    }

    public enum ChestKind { Card, Relic, Heal }

    public class ChestEntity : MapEntity
    {
        public readonly ChestKind Kind;
        public bool Opened;

        public ChestEntity(ChestKind kind, Vector2Int pos)
        {
            Kind = kind;
            Pos = pos;
        }

        public override bool IsUsed => Opened;

        public override string Tooltip
        {
            get
            {
                if (Opened) return "Baú vazio.";
                switch (Kind)
                {
                    case ChestKind.Card: return "<b>Baú do Sertão</b>: contém uma carta rara.";
                    case ChestKind.Relic: return "<b>Baú Antigo</b>: guarda uma relíquia poderosa.";
                    default: return "<b>Baú da Benzedeira</b>: remédios para a viagem.";
                }
            }
        }

        public override Color MinimapColor => Opened ? new Color(0.4f, 0.4f, 0.4f) : new Color(1f, 0.85f, 0.25f);

        public override Sprite Sprite
        {
            get
            {
                if (Opened) return TextureFactory.MapObject("chest_open");
                switch (Kind)
                {
                    case ChestKind.Card: return TextureFactory.MapObject("chest_card");
                    case ChestKind.Relic: return TextureFactory.MapObject("chest_relic");
                    default: return TextureFactory.MapObject("chest_heal");
                }
            }
        }

        public override void Interact(GameManager game) => game.OpenChest(this);
    }

    public class QuizEntity : MapEntity
    {
        public bool Read;
        public QuizEntity(Vector2Int pos) { Pos = pos; }

        public override bool IsUsed => Read;
        public override string Tooltip => Read ? "Cordel já lido." : "<b>Folheto de Cordel</b>: responda a pergunta e ganhe pontos!";
        public override Color MinimapColor => Read ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.95f, 0.9f, 0.7f);
        public override Sprite Sprite => TextureFactory.MapObject(Read ? "cordel_read" : "cordel");
        public override void Interact(GameManager game) => game.OpenQuiz(this);
    }

    public class CampfireEntity : MapEntity
    {
        public bool Used;
        public CampfireEntity(Vector2Int pos) { Pos = pos; }

        public override bool IsUsed => Used;
        public override string Tooltip => Used ? "Fogueira apagada." : "<b>Fogueira</b>: área de descanso. Recupere 50% da vida (uma vez).";
        public override Color MinimapColor => Used ? new Color(0.4f, 0.4f, 0.4f) : new Color(1f, 0.55f, 0.15f);
        public override Sprite Sprite => TextureFactory.MapObject(Used ? "campfire_out" : "campfire");
        public override Color GlowColor => Used ? Color.clear : new Color(1f, 0.6f, 0.2f, 0.35f);
        public override void Interact(GameManager game) => game.UseCampfire(this);
    }

    /// <summary>Panela de cuscuz ao lado da fogueira: bônus a cada porção, mas comer demais deixa o herói pesado.</summary>
    public class CuscuzPotEntity : MapEntity
    {
        public int Servings = GameConfig.CuscuzServingsPerPot;
        public CuscuzPotEntity(Vector2Int pos) { Pos = pos; }

        public override bool IsUsed => Servings <= 0;
        public override string Tooltip => Servings > 0
            ? $"<b>Panela de Cuscuz</b> ({Servings} porções): cada porção dá um bônus, mas comer demais deixa você pesado!"
            : "Panela de cuscuz vazia.";
        public override Color MinimapColor => Servings > 0 ? new Color(1f, 0.9f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
        public override Sprite Sprite => TextureFactory.MapObject(Servings > 0 ? "cuscuz" : "cuscuz_empty");
        public override Color GlowColor => Servings > 0 ? new Color(1f, 0.85f, 0.3f, 0.3f) : Color.clear;
        public override void Interact(GameManager game) => game.OpenCuscuz(this);
    }

    public class ExitEntity : MapEntity
    {
        public bool Active;
        readonly string title;
        public ExitEntity(Vector2Int pos, string title)
        {
            Pos = pos;
            this.title = title;
        }

        public override bool Visible => Active;
        public override string Tooltip => $"<b>{title}</b>: conclua a fase!";
        public override Color MinimapColor => new Color(1f, 0.95f, 0.5f);
        public override Sprite Sprite => TextureFactory.MapObject("portal");
        public override float SpriteSize => 1.2f;
        public override Color GlowColor => new Color(1f, 0.85f, 0.3f, 0.5f);
        public override void Interact(GameManager game) => game.TryExit(this);
    }
}
