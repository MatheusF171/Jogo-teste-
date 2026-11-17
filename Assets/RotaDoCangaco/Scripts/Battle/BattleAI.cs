using System.Linq;

namespace RotaDoCangaco
{
    /// <summary>
    /// Jogador automático simples, usado pela simulação de balanceamento e pelo teste automático.
    /// Joga cartas em ordem crescente de custo (gerando combo) e mira o inimigo com menos vida.
    /// </summary>
    public static class BattleAI
    {
        public static void PlayTurn(BattleSystem battle)
        {
            int guard = 0;
            while (!battle.IsOver && guard++ < 30)
            {
                var playable = battle.Deck.Hand.Where(c => c.Cost <= battle.Energy).ToList();
                if (playable.Count == 0) break;

                var pick = playable.FirstOrDefault(c => c.Type == CardType.Personagem)
                           ?? playable.Where(c => !battle.Combo.Started || battle.Combo.WouldContinue(c))
                                      .OrderBy(c => c.IsWildcard ? 99 : c.Cost)
                                      .FirstOrDefault()
                           ?? playable.OrderBy(c => c.Cost).First();

                var target = battle.AliveEnemies.OrderBy(e => e.Hp).FirstOrDefault();
                if (!battle.PlayCard(pick, target)) break;
            }
        }

        /// <summary>Executa a fase inimiga inteira de uma vez (sem animação).</summary>
        public static void RunEnemyPhase(BattleSystem battle)
        {
            battle.BeginEnemyPhase();
            foreach (var enemy in battle.EnemyTurnOrder())
            {
                battle.ExecuteEnemyTurn(enemy);
                if (battle.IsOver) return;
            }
            battle.StartPlayerTurn();
        }
    }
}
