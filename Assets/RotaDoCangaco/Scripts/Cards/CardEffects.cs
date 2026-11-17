using System.Linq;
using UnityEngine;

namespace RotaDoCangaco
{
    public enum EffectScope { Target, AllEnemies, Self }

    /// <summary>
    /// Efeito base de uma carta. Cada subclasse sabe se aplicar na batalha e gerar seu próprio texto.
    /// </summary>
    public abstract class CardEffect
    {
        public abstract void Apply(BattleSystem battle, EnemyCombatant target, float mult);
        public abstract string Describe(BattleSystem battle, float mult);

        public static int Scale(int value, float mult) => Mathf.Max(0, Mathf.FloorToInt(value * mult + 0.5f));

        /// <summary>Destaca em verde valores aumentados (combo, Força) e em vermelho valores reduzidos.</summary>
        protected static string Highlight(int shown, int baseValue)
        {
            if (shown > baseValue) return "<color=#9CFF8A>" + shown + "</color>";
            if (shown < baseValue) return "<color=#FF8A8A>" + shown + "</color>";
            return shown.ToString();
        }
    }

    public class DamageEffect : CardEffect
    {
        public readonly int Amount;
        public readonly int Hits;
        public readonly bool AllEnemies;

        public DamageEffect(int amount, int hits = 1, bool allEnemies = false)
        {
            Amount = amount; Hits = hits; AllEnemies = allEnemies;
        }

        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult)
        {
            for (int i = 0; i < Hits; i++)
            {
                if (AllEnemies)
                {
                    foreach (var enemy in battle.AliveEnemies.ToList())
                        battle.PlayerAttack(enemy, Amount, mult);
                }
                else if (target != null && !target.IsDead)
                {
                    battle.PlayerAttack(target, Amount, mult);
                }
            }
        }

        public override string Describe(BattleSystem battle, float mult)
        {
            int shown = battle != null ? battle.PreviewPlayerDamage(Amount, mult) : Scale(Amount, mult);
            string v = Highlight(shown, Amount);
            if (AllEnemies) return $"Causa {v} de dano a TODOS os inimigos.";
            if (Hits > 1) return $"Causa {v} de dano {Hits} vezes.";
            return $"Causa {v} de dano.";
        }
    }

    public class BlockEffect : CardEffect
    {
        public readonly int Amount;
        public BlockEffect(int amount) { Amount = amount; }

        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult)
            => battle.PlayerGainBlock(Scale(Amount, mult));

        public override string Describe(BattleSystem battle, float mult)
            => $"Ganha {Highlight(Scale(Amount, mult), Amount)} de escudo.";
    }

    public class StatusEffect : CardEffect
    {
        public readonly StatusType Status;
        public readonly int Amount;
        public readonly EffectScope Scope;
        public readonly bool ScalesWithCombo;

        public StatusEffect(StatusType status, int amount, EffectScope scope, bool scalesWithCombo = false)
        {
            Status = status; Amount = amount; Scope = scope; ScalesWithCombo = scalesWithCombo;
        }

        int Value(float mult) => ScalesWithCombo ? Scale(Amount, mult) : Amount;

        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult)
        {
            int amount = Value(mult);
            switch (Scope)
            {
                case EffectScope.Self:
                    battle.ApplyStatus(battle.Player, Status, amount);
                    break;
                case EffectScope.AllEnemies:
                    foreach (var enemy in battle.AliveEnemies.ToList()) battle.ApplyStatus(enemy, Status, amount);
                    break;
                default:
                    if (target != null && !target.IsDead) battle.ApplyStatus(target, Status, amount);
                    break;
            }
        }

        public override string Describe(BattleSystem battle, float mult)
        {
            string v = ScalesWithCombo ? Highlight(Value(mult), Amount) : Amount.ToString();
            string name = Statuses.Name(Status);
            if (Status == StatusType.Sono)
            {
                string turns = Amount == 1 ? "1 turno" : Amount + " turnos";
                return Scope == EffectScope.AllEnemies ? $"TODOS os inimigos dormem por {turns}." : $"O alvo dorme por {turns}.";
            }
            if (Scope == EffectScope.Self) return $"Ganha {v} de {name}.";
            if (Scope == EffectScope.AllEnemies) return $"Aplica {v} de {name} a TODOS.";
            return $"Aplica {v} de {name}.";
        }
    }

    public class DrawEffect : CardEffect
    {
        public readonly int Count;
        public DrawEffect(int count) { Count = count; }
        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult) => battle.DrawCards(Count);
        public override string Describe(BattleSystem battle, float mult) => Count == 1 ? "Compra 1 carta." : $"Compra {Count} cartas.";
    }

    public class EnergyEffect : CardEffect
    {
        public readonly int Amount;
        public EnergyEffect(int amount) { Amount = amount; }
        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult) => battle.GainEnergy(Amount);
        public override string Describe(BattleSystem battle, float mult) => $"Ganha {Amount} de energia.";
    }

    public class HealEffect : CardEffect
    {
        public readonly int Amount;
        public HealEffect(int amount) { Amount = amount; }
        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult) => battle.HealPlayer(Scale(Amount, mult));
        public override string Describe(BattleSystem battle, float mult) => $"Recupera {Highlight(Scale(Amount, mult), Amount)} de vida.";
    }

    public class SelfDamageEffect : CardEffect
    {
        public readonly int Amount;
        public SelfDamageEffect(int amount) { Amount = amount; }
        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult) => battle.PlayerLoseHp(Amount);
        public override string Describe(BattleSystem battle, float mult) => $"Perde {Amount} de vida.";
    }

    public class PushEffect : CardEffect
    {
        public readonly int Amount;
        public readonly bool AllEnemies;
        public PushEffect(int amount, bool allEnemies = false) { Amount = amount; AllEnemies = allEnemies; }

        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult)
        {
            if (AllEnemies)
                foreach (var enemy in battle.AliveEnemies.ToList()) battle.Push(enemy, Amount);
            else
                battle.Push(target, Amount);
        }

        public override string Describe(BattleSystem battle, float mult)
            => AllEnemies ? "Empurra TODOS os inimigos para trás." : "Empurra o alvo para trás.";
    }

    /// <summary>Carta de Personagem: fica em jogo durante a batalha concedendo bônus contínuos.</summary>
    public class CharacterEffect : CardEffect
    {
        public readonly int AttackBonus;
        public readonly int BlockPerTurn;
        public CharacterEffect(int attackBonus, int blockPerTurn) { AttackBonus = attackBonus; BlockPerTurn = blockPerTurn; }

        public override void Apply(BattleSystem battle, EnemyCombatant target, float mult)
            => battle.EnterCharacter(AttackBonus, BlockPerTurn);

        public override string Describe(BattleSystem battle, float mult)
            => $"Fica em jogo: +{AttackBonus} de dano nos ataques e +{BlockPerTurn} de escudo a cada turno.";
    }
}
