using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Combo do documento: jogar cartas em ordem crescente de custo (ex.: 0 → 1 → 2) aumenta o multiplicador
    /// das cartas seguintes. Uma carta fora de ordem reinicia a sequência. Coringas sempre continuam a sequência
    /// sem mudar o "último custo".
    /// </summary>
    public class ComboTracker
    {
        public int Chain { get; private set; }
        public int LastCost { get; private set; } = -1;
        public bool Started { get; private set; }
        public readonly List<string> History = new List<string>();

        public float CurrentMultiplier => MultiplierFor(Chain);

        public void Reset()
        {
            Chain = 0;
            LastCost = -1;
            Started = false;
            History.Clear();
        }

        public bool WouldContinue(CardData card) => Started && (card.IsWildcard || card.Cost > LastCost);

        public int ChainAfter(CardData card)
        {
            if (!Started) return 0;
            return card.IsWildcard || card.Cost > LastCost ? Chain + 1 : 0;
        }

        public float PreviewMultiplier(CardData card) => MultiplierFor(ChainAfter(card));

        public static float MultiplierFor(int chain) => Mathf.Min(GameConfig.ComboMax, 1f + chain * GameConfig.ComboStep);

        /// <summary>Registra a carta jogada e devolve o multiplicador que ela recebe.</summary>
        public float Register(CardData card)
        {
            int chain = ChainAfter(card);
            if (chain == 0) History.Clear();
            Chain = chain;
            Started = true;
            if (!card.IsWildcard) LastCost = card.Cost;
            History.Add(card.IsWildcard ? "C" : card.Cost.ToString());
            return MultiplierFor(Chain);
        }
    }
}
