using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RotaDoCangaco
{
    public enum CardType { Ataque, Defesa, Habilidade, Bonus, Personagem }
    public enum CardTarget { Self, SingleEnemy, AllEnemies }
    public enum CardRarity { Inicial, Comum, Rara, Chefe }

    /// <summary>
    /// Definição de uma carta. As cartas são imutáveis, então cópias no baralho compartilham a mesma instância.
    /// </summary>
    public class CardData
    {
        public string Id;
        public string Name;
        public string Flavor;
        public int Cost;
        public CardType Type;
        public CardTarget Target;
        public CardRarity Rarity;
        public bool IsWildcard;   // Coringa: conta como "custo crescente" e mantém o combo
        public bool Exhaust;      // Sai da batalha depois de usada
        public string Icon;       // ícone procedural (TextureFactory) ou chave de sprite
        public Color Tint = Color.white;
        public readonly List<CardEffect> Effects = new List<CardEffect>();

        public bool NeedsTarget => Target == CardTarget.SingleEnemy;

        public string TypeLabel
        {
            get
            {
                switch (Type)
                {
                    case CardType.Ataque: return "ATAQUE";
                    case CardType.Defesa: return "DEFESA";
                    case CardType.Habilidade: return "HABILIDADE";
                    case CardType.Bonus: return "BÔNUS";
                    default: return "PERSONAGEM";
                }
            }
        }

        /// <param name="battle">Batalha atual (null fora de combate), usada para mostrar valores já modificados.</param>
        /// <param name="mult">Multiplicador de combo que a carta receberia se fosse jogada agora.</param>
        public string Describe(BattleSystem battle, float mult)
        {
            var sb = new StringBuilder();
            foreach (var effect in Effects)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(effect.Describe(battle, mult));
            }
            if (IsWildcard) sb.Append("\n<b>Coringa:</b> mantém o combo.");
            if (Exhaust) sb.Append("\n<i>Exaurir.</i>");
            return sb.ToString();
        }
    }
}
