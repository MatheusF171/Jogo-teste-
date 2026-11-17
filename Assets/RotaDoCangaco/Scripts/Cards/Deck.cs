using System.Collections.Generic;

namespace RotaDoCangaco
{
    /// <summary>
    /// Pilhas de cartas de uma batalha: compra, mão, descarte e exauridas.
    /// Quando o monte de compra acaba, o descarte é embaralhado e vira o novo monte.
    /// </summary>
    public class Deck
    {
        public const int MaxHandSize = 10;

        public readonly List<CardData> DrawPile = new List<CardData>();
        public readonly List<CardData> Hand = new List<CardData>();
        public readonly List<CardData> Discard = new List<CardData>();
        public readonly List<CardData> Exhausted = new List<CardData>();

        readonly System.Random rng;

        public Deck(IEnumerable<CardData> cards, System.Random rng)
        {
            this.rng = rng;
            DrawPile.AddRange(cards);
            Shuffle(DrawPile);

            // Cartas de Personagem são "inatas": sempre vêm na primeira mão.
            for (int i = DrawPile.Count - 1; i >= 0; i--)
            {
                if (DrawPile[i].Type != CardType.Personagem) continue;
                var card = DrawPile[i];
                DrawPile.RemoveAt(i);
                DrawPile.Add(card); // o topo do monte é o final da lista
            }
        }

        /// <summary>Compra até <paramref name="count"/> cartas. Retorna quantas foram compradas.</summary>
        public int Draw(int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (Hand.Count >= MaxHandSize) break;
                if (DrawPile.Count == 0)
                {
                    if (Discard.Count == 0) break;
                    DrawPile.AddRange(Discard);
                    Discard.Clear();
                    Shuffle(DrawPile);
                }
                int top = DrawPile.Count - 1;
                Hand.Add(DrawPile[top]);
                DrawPile.RemoveAt(top);
                drawn++;
            }
            return drawn;
        }

        public void DiscardHand()
        {
            Discard.AddRange(Hand);
            Hand.Clear();
        }

        void Shuffle(List<CardData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
