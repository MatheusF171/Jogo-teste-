using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Inimigo vivo em uma batalha. Segue um padrão de ações e mostra sua intenção antes de agir.
    /// </summary>
    public class EnemyCombatant : Combatant
    {
        public readonly EnemyData Data;
        public int Distance;
        public bool Rewarded;
        public bool IsSummoned;
        public EnemyMove Intent { get; protected set; }

        protected int patternIndex;

        public EnemyCombatant(EnemyData data)
        {
            Data = data;
            Name = data.Name;
            MaxHp = Hp = data.MaxHp;
        }

        public void Init(System.Random rng)
        {
            patternIndex = Data.RandomStart && Data.Moves.Count > 1 ? rng.Next(Data.Moves.Count) : 0;
        }

        protected virtual List<EnemyMove> CurrentMoves => Data.Moves;

        public virtual void ChooseIntent(BattleSystem battle)
        {
            var moves = CurrentMoves;
            Intent = moves[patternIndex % moves.Count];
            patternIndex++;
        }

        public virtual void OnMoveExecuted(BattleSystem battle, EnemyMove move) { }

        public bool IsOutOfRange => Distance > Data.Range;

        /// <summary>O ataque planejado acontece neste turno? (Fora de alcance só ataca quem tem investida e alcança.)</summary>
        public bool AttackLandsThisTurn => !IsOutOfRange || (Data.Charges && Distance - Data.Speed <= Data.Range);

        public void EndTurn()
        {
            if (GetStatus(StatusType.Fraqueza) > 0) AddStatus(StatusType.Fraqueza, -1);
        }

        public string DescribeIntent(BattleSystem battle, out Color color)
        {
            if (GetStatus(StatusType.Sono) > 0)
            {
                color = new Color(0.62f, 0.72f, 1f);
                return "Dormindo\nZzz...";
            }
            var move = Intent;
            if (move == null)
            {
                color = Color.gray;
                return "";
            }
            if (move.IsAttack && !AttackLandsThisTurn)
            {
                color = new Color(0.8f, 0.8f, 0.8f);
                return "Avançando\n(sem ataque)";
            }

            var parts = new List<string>();
            if (move.IsAttack)
            {
                int dmg = battle.PreviewEnemyDamage(this, move.Damage);
                parts.Add(move.Hits > 1 ? $"Ataque {dmg}x{move.Hits}" : $"Ataque {dmg}");
            }
            if (move.Block > 0) parts.Add($"Escudo {move.Block}");
            if (move.Status.HasValue)
                parts.Add(move.StatusOnSelf
                    ? $"{Statuses.Name(move.Status.Value)} +{move.StatusAmount}"
                    : $"{Statuses.Name(move.Status.Value)} {move.StatusAmount}");
            if (move.SummonId != null) parts.Add("Invocar");
            if (move.Cleanse) parts.Add("Purificar");

            if (move.IsAttack) color = new Color(1f, 0.45f, 0.4f);
            else if (move.Block > 0) color = new Color(0.45f, 0.75f, 1f);
            else color = new Color(0.85f, 0.6f, 1f);

            string prefix = IsOutOfRange ? (move.IsAttack ? "Investida! " : "Avança + ") : "";
            return prefix + move.Name + "\n" + string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// Chefe com duas fases: ao cair para 50% da vida, entra em fúria e troca de padrão de ataques.
    /// </summary>
    public class BossCombatant : EnemyCombatant
    {
        public bool Enraged { get; private set; }

        public BossCombatant(EnemyData data) : base(data) { }

        protected override List<EnemyMove> CurrentMoves =>
            Enraged && Data.EnragedMoves.Count > 0 ? Data.EnragedMoves : Data.Moves;

        public override void ChooseIntent(BattleSystem battle)
        {
            if (!Enraged && Data.EnrageMove != null && Hp * 2 <= MaxHp)
            {
                Intent = Data.EnrageMove;
                return;
            }
            base.ChooseIntent(battle);
        }

        public override void OnMoveExecuted(BattleSystem battle, EnemyMove move)
        {
            if (move != Data.EnrageMove) return;
            Enraged = true;
            patternIndex = 0;
            battle.Log($"{Name} entra em FÚRIA!");
        }
    }
}
