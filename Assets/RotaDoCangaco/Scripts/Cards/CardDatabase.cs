using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Todas as cartas do protótipo. A magia inicial vem do folclore da Cuca (feitiços, poções, caldeirão);
    /// as recompensas trazem elementos da cultura nordestina (benzedeira, garrafada, caldo de cana, rasga-mortalha).
    /// </summary>
    public static class CardDatabase
    {
        static readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
        static readonly List<CardData> ordered = new List<CardData>();

        public const string BossCardId = "furia_mula";

        static CardDatabase() { Build(); }

        public static CardData Get(string id) => cards[id];
        public static IEnumerable<CardData> All => ordered;

        /// <summary>Baralho inicial de 4 cartas, uma delas o próprio personagem (regra do documento).</summary>
        public static List<CardData> StartingDeck() => new List<CardData>
        {
            Get("aprendiz"), Get("feitico"), Get("amuleto"), Get("caldeirao")
        };

        /// <summary>Sorteia cartas distintas para escolha (subida de nível ou baú).</summary>
        public static List<CardData> RollChoices(System.Random rng, int count, bool rareOnly)
        {
            var commons = ordered.Where(c => c.Rarity == CardRarity.Comum).ToList();
            var rares = ordered.Where(c => c.Rarity == CardRarity.Rara).ToList();
            var result = new List<CardData>();
            int guard = 0;
            while (result.Count < count && guard++ < 200)
            {
                var pool = rareOnly || rng.NextDouble() < 0.25 ? rares : commons;
                var pick = pool[rng.Next(pool.Count)];
                if (!result.Contains(pick)) result.Add(pick);
            }
            return result;
        }

        static CardData Add(string id, string name, int cost, CardType type, CardTarget target, CardRarity rarity,
                            string icon, string tintHex, string flavor, params CardEffect[] effects)
        {
            ColorUtility.TryParseHtmlString(tintHex, out var tint);
            var card = new CardData
            {
                Id = id, Name = name, Cost = cost, Type = type, Target = target, Rarity = rarity,
                Icon = icon, Tint = tint, Flavor = flavor
            };
            card.Effects.AddRange(effects);
            cards[id] = card;
            ordered.Add(card);
            return card;
        }

        static void Build()
        {
            // ---------- Baralho inicial ----------
            Add("aprendiz", "Aprendiz da Cuca", 0, CardType.Personagem, CardTarget.Self, CardRarity.Inicial, "hero", "#3C6BFF",
                "Você! O aprendiz da velha bruxa.",
                new CharacterEffect(1, 2));
            Add("feitico", "Feitiço da Cuca", 1, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Inicial, "orb", "#9B5CFF",
                "Um raio de magia arroxeada.",
                new DamageEffect(6));
            Add("amuleto", "Amuleto de Arruda", 1, CardType.Defesa, CardTarget.Self, CardRarity.Inicial, "leaf", "#4CC46A",
                "Arruda atrás da orelha espanta o mau-olhado.",
                new BlockEffect(5));
            Add("caldeirao", "Caldeirão Borbulhante", 2, CardType.Ataque, CardTarget.AllEnemies, CardRarity.Inicial, "cauldron", "#6FD34A",
                "Respingos para todo lado!",
                new DamageEffect(4, 1, true), new StatusEffect(StatusType.Veneno, 2, EffectScope.AllEnemies, true));

            // ---------- Comuns ----------
            Add("praga", "Praga Rogada", 1, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Comum, "eye", "#B04BD6",
                "\"Que o teu caminho seque!\"",
                new DamageEffect(4), new StatusEffect(StatusType.Veneno, 3, EffectScope.Target, true));
            Add("vassoura", "Vassoura Voadora", 1, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Comum, "broom", "#C98B3A",
                "Uma vassourada de longe.",
                new DamageEffect(5), new PushEffect(1));
            Add("mau_olhado", "Mau-Olhado", 0, CardType.Habilidade, CardTarget.SingleEnemy, CardRarity.Comum, "eye", "#5E8BFF",
                "Olho gordo enfraquece qualquer um.",
                new DamageEffect(2), new StatusEffect(StatusType.Fraqueza, 2, EffectScope.Target));
            Add("chama", "Chama do Caldeirão", 2, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Comum, "flame", "#FF7A2E",
                "O fogo que ferve a poção.",
                new DamageEffect(9), new StatusEffect(StatusType.Queimadura, 3, EffectScope.Target, true));
            Add("reza", "Reza de Benzedeira", 1, CardType.Defesa, CardTarget.Self, CardRarity.Comum, "star", "#F2D35B",
                "Ramo de arruda e muita fé.",
                new BlockEffect(4), new HealEffect(3));
            Add("patua", "Patuá Protetor", 2, CardType.Defesa, CardTarget.Self, CardRarity.Comum, "shield", "#3FA7D6",
                "Saquinho de pano com ervas e orações.",
                new BlockEffect(11));
            Add("caldo", "Caldo de Cana", 0, CardType.Bonus, CardTarget.Self, CardRarity.Comum, "cup", "#E8C547",
                "Direto da moenda do engenho.",
                new EnergyEffect(1), new DrawEffect(1)).Exhaust = true;
            Add("cafe", "Café com Rapadura", 1, CardType.Bonus, CardTarget.Self, CardRarity.Comum, "cup", "#8B5A2B",
                "O combustível do sertanejo.",
                new DrawEffect(2));
            Add("gato", "Gato Preto", 1, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Comum, "cat", "#7A7A9A",
                "O fiel companheiro da bruxa.",
                new DamageEffect(4), new DrawEffect(1)).IsWildcard = true;
            Add("pocao_sono", "Poção do Sono", 1, CardType.Habilidade, CardTarget.SingleEnemy, CardRarity.Comum, "flask", "#6FA8FF",
                "Uma gota e... zzz.",
                new StatusEffect(StatusType.Sono, 1, EffectScope.Target));
            Add("garrafada", "Garrafada", 1, CardType.Habilidade, CardTarget.Self, CardRarity.Comum, "flask", "#5BC27A",
                "Remédio de raízes curtidas na garrafa.",
                new HealEffect(6)).Exhaust = true;

            // ---------- Raras ----------
            Add("cantiga", "Cantiga da Cuca", 2, CardType.Habilidade, CardTarget.AllEnemies, CardRarity.Rara, "moon", "#9AA8FF",
                "\"Nana, neném, que a Cuca vem pegar...\"",
                new StatusEffect(StatusType.Sono, 1, EffectScope.AllEnemies)).Exhaust = true;
            Add("lua_cheia", "Lua Cheia", 1, CardType.Bonus, CardTarget.Self, CardRarity.Rara, "moon", "#FFE58A",
                "A magia cresce com a lua.",
                new StatusEffect(StatusType.Forca, 2, EffectScope.Self));
            Add("rasga_mortalha", "Rasga-Mortalha", 2, CardType.Ataque, CardTarget.SingleEnemy, CardRarity.Rara, "owl", "#D9D2C3",
                "A coruja agourenta do Nordeste.",
                new DamageEffect(8), new StatusEffect(StatusType.Fraqueza, 1, EffectScope.Target)).IsWildcard = true;
            Add("tempestade", "Tempestade de Agouros", 3, CardType.Ataque, CardTarget.AllEnemies, CardRarity.Rara, "orb", "#5A3CFF",
                "Raios, trovões e mau agouro.",
                new DamageEffect(10, 1, true));
            Add("circulo_sal", "Círculo de Sal", 2, CardType.Defesa, CardTarget.Self, CardRarity.Rara, "ring", "#EDEDED",
                "Nenhuma assombração atravessa o sal.",
                new BlockEffect(7), new PushEffect(1, true));
            Add("pacto", "Pacto Sombrio", 0, CardType.Bonus, CardTarget.Self, CardRarity.Rara, "eye", "#D63B3B",
                "Todo poder tem seu preço.",
                new SelfDamageEffect(3), new EnergyEffect(2)).Exhaust = true;

            // ---------- Habilidade do chefe ----------
            Add(BossCardId, "Fúria da Mula Sem Cabeça", 2, CardType.Habilidade, CardTarget.AllEnemies, CardRarity.Chefe, "mula", "#FF2D6F",
                "As chamas que ardem onde deveria haver cabeça agora são suas.",
                new DamageEffect(10, 1, true), new StatusEffect(StatusType.Queimadura, 4, EffectScope.AllEnemies, true), new BlockEffect(5));
        }
    }
}
