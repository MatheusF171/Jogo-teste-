using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    public enum StatusType { Veneno, Queimadura, Sono, Fraqueza, Forca, Drenado }

    public static class Statuses
    {
        public static string Name(StatusType s)
        {
            switch (s)
            {
                case StatusType.Veneno: return "Veneno";
                case StatusType.Queimadura: return "Queimadura";
                case StatusType.Sono: return "Sono";
                case StatusType.Fraqueza: return "Fraqueza";
                case StatusType.Forca: return "Força";
                default: return "Drenado";
            }
        }

        public static string Hex(StatusType s)
        {
            switch (s)
            {
                case StatusType.Veneno: return "#8BE15B";
                case StatusType.Queimadura: return "#FF8A3D";
                case StatusType.Sono: return "#9DB4FF";
                case StatusType.Fraqueza: return "#C79BFF";
                case StatusType.Forca: return "#FF6B6B";
                default: return "#FFD24D";
            }
        }

        public static bool IsDebuff(StatusType s) => s != StatusType.Forca;
    }

    /// <summary>
    /// Base comum para jogador e inimigos: vida, escudo e efeitos de status.
    /// </summary>
    public abstract class Combatant
    {
        public string Name;
        public int MaxHp;
        public int Hp;
        public int Block;

        readonly Dictionary<StatusType, int> statuses = new Dictionary<StatusType, int>();

        public bool IsDead => Hp <= 0;
        public IEnumerable<KeyValuePair<StatusType, int>> AllStatuses => statuses;

        public int GetStatus(StatusType type) => statuses.TryGetValue(type, out var v) ? v : 0;

        public void SetStatus(StatusType type, int value)
        {
            if (value <= 0) statuses.Remove(type);
            else statuses[type] = value;
        }

        public void AddStatus(StatusType type, int amount) => SetStatus(type, GetStatus(type) + amount);

        public void ClearDebuffs()
        {
            foreach (StatusType t in System.Enum.GetValues(typeof(StatusType)))
                if (Statuses.IsDebuff(t)) statuses.Remove(t);
        }

        /// <summary>Aplica dano (o escudo absorve primeiro). Retorna o dano que chegou à vida.</summary>
        public int TakeDamage(int amount, bool ignoreBlock = false)
        {
            if (amount <= 0 || IsDead) return 0;
            int toHp = amount;
            if (!ignoreBlock && Block > 0)
            {
                int absorbed = Mathf.Min(Block, amount);
                Block -= absorbed;
                toHp -= absorbed;
            }
            Hp = Mathf.Max(0, Hp - toHp);
            return toHp;
        }

        public void GainBlock(int amount) => Block += Mathf.Max(0, amount);

        public int Heal(int amount)
        {
            int before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + Mathf.Max(0, amount));
            return Hp - before;
        }

        /// <summary>Veneno (diminui 1 por turno) e Queimadura (cai pela metade) no início do turno.</summary>
        public int TickDamageOverTime(out string description)
        {
            int total = 0;
            var parts = new List<string>();
            int poison = GetStatus(StatusType.Veneno);
            if (poison > 0)
            {
                total += TakeDamage(poison, true);
                SetStatus(StatusType.Veneno, poison - 1);
                parts.Add("Veneno");
            }
            int burn = GetStatus(StatusType.Queimadura);
            if (burn > 0 && !IsDead)
            {
                total += TakeDamage(burn, true);
                SetStatus(StatusType.Queimadura, burn / 2);
                parts.Add("Queimadura");
            }
            description = string.Join(" + ", parts);
            return total;
        }
    }

    public class PlayerCombatant : Combatant
    {
        public PlayerCombatant(PlayerData data)
        {
            Name = data.Name;
            MaxHp = data.MaxHp;
            Hp = data.Hp;
        }
    }
}
