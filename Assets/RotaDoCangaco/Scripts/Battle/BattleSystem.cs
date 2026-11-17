using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Regras da batalha por turnos com cartas (sem nada visual — a BattleController/BattleView desenham).
    /// Turno do jogador: recebe energia, compra a mão e joga cartas. Depois cada inimigo age em ordem:
    /// quem está fora de alcance avança, quem está em alcance executa a intenção anunciada.
    /// </summary>
    public class BattleSystem
    {
        public static readonly Color ColDamage = new Color(1f, 0.35f, 0.3f);
        public static readonly Color ColBlock = new Color(0.45f, 0.75f, 1f);
        public static readonly Color ColHeal = new Color(0.45f, 1f, 0.5f);
        public static readonly Color ColStatus = new Color(0.85f, 0.6f, 1f);
        public static readonly Color ColGold = new Color(1f, 0.85f, 0.3f);
        public static readonly Color ColNeutral = new Color(0.85f, 0.85f, 0.85f);

        public readonly PlayerData Data;
        public readonly PlayerCombatant Player;
        public readonly List<EnemyCombatant> Enemies = new List<EnemyCombatant>();
        public readonly Deck Deck;
        public readonly ComboTracker Combo = new ComboTracker();
        public readonly EncounterData Encounter;
        public readonly List<CardData> InPlay = new List<CardData>();
        public readonly System.Random Rng;

        public int Energy { get; private set; }
        public int Turn { get; private set; }
        public int CardsPlayedThisTurn { get; private set; }
        public int MaxCardsPerTurn => Data.MaxCardsPerTurn;
        public int AttackBonus { get; private set; }
        public int BlockPerTurn { get; private set; }
        public bool IsOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public int XpEarned { get; private set; }
        public int ScoreEarned { get; private set; }
        public int MonstersKilled { get; private set; }
        public bool IsBossBattle { get; }

        public event Action<string> OnLog;
        public event Action<Combatant, string, Color> OnPopup;
        public event Action<Combatant> OnHit;
        public event Action<EnemyCombatant> OnEnemySpawned;

        public IEnumerable<EnemyCombatant> AliveEnemies => Enemies.Where(e => !e.IsDead);

        public BattleSystem(PlayerData data, EncounterData encounter, System.Random rng)
        {
            Data = data;
            Encounter = encounter;
            Rng = rng;
            Player = new PlayerCombatant(data);
            if (data.BonusStrength > 0) Player.SetStatus(StatusType.Forca, data.BonusStrength);
            Deck = new Deck(data.Deck, rng);
            foreach (var slot in encounter.Enemies) SpawnEnemy(slot.Key, slot.Value, false);
            IsBossBattle = Enemies.Any(e => e.Data.IsBoss);
        }

        public void Log(string message) => OnLog?.Invoke(message);
        void Popup(Combatant c, string text, Color color) => OnPopup?.Invoke(c, text, color);

        public EnemyCombatant SpawnEnemy(string id, int distance, bool summoned)
        {
            var enemy = EnemyDatabase.Create(id);
            enemy.Distance = Mathf.Clamp(distance, 0, GameConfig.MaxDistance);
            enemy.IsSummoned = summoned;
            enemy.Init(Rng);
            Enemies.Add(enemy);
            if (summoned)
            {
                enemy.ChooseIntent(this);
                Log($"{enemy.Name} surgiu!");
                OnEnemySpawned?.Invoke(enemy);
            }
            return enemy;
        }

        public void Start()
        {
            Log($"Batalha: {Encounter.Title}!");
            StartPlayerTurn();
        }

        // ------------------------------------------------------------------ Turno do jogador

        public void StartPlayerTurn()
        {
            if (IsOver) return;
            Turn++;
            Player.Block = 0;
            TickStatuses(Player);
            if (CheckEnd()) return;

            if (BlockPerTurn > 0)
            {
                Player.GainBlock(BlockPerTurn);
                Popup(Player, $"+{BlockPerTurn} escudo", ColBlock);
            }

            int drained = Player.GetStatus(StatusType.Drenado);
            Energy = Mathf.Max(1, Data.EffectiveEnergy - drained);
            if (drained > 0)
            {
                Player.SetStatus(StatusType.Drenado, 0);
                Log($"Você foi drenado: -{drained} de energia neste turno.");
            }

            int extraCards = 0;
            if (Turn == 1)
            {
                if (Data.BonusStartBlock > 0)
                {
                    Player.GainBlock(Data.BonusStartBlock);
                    Popup(Player, $"+{Data.BonusStartBlock} escudo (cuscuz)", ColBlock);
                }
                extraCards = Data.BonusFirstHand;
                if (Data.FatStage > 0) Log($"Pesado de cuscuz ({Data.FatLabel}): {Data.FatPenaltyText}.");
            }

            CardsPlayedThisTurn = 0;
            Combo.Reset();
            Deck.Draw(Data.HandSize + extraCards);
            foreach (var enemy in AliveEnemies) enemy.ChooseIntent(this);
        }

        public bool CanPlay(CardData card, out string reason)
        {
            reason = null;
            if (IsOver) { reason = "A batalha acabou."; return false; }
            if (!Deck.Hand.Contains(card)) { reason = "A carta não está na mão."; return false; }
            if (card.Cost > Energy) { reason = "Energia insuficiente!"; return false; }
            if (MaxCardsPerTurn > 0 && CardsPlayedThisTurn >= MaxCardsPerTurn)
            {
                reason = $"Barriga cheia! Só {MaxCardsPerTurn} cartas por turno.";
                return false;
            }
            return true;
        }

        public bool PlayCard(CardData card, EnemyCombatant target)
        {
            if (!CanPlay(card, out var reason))
            {
                Log(reason);
                return false;
            }
            if (card.NeedsTarget && (target == null || target.IsDead)) target = AliveEnemies.FirstOrDefault();

            Energy -= card.Cost;
            CardsPlayedThisTurn++;
            Deck.Hand.Remove(card);
            float mult = Combo.Register(card);
            Log(mult > 1f ? $"Você usa {card.Name} (combo x{mult:0.00})" : $"Você usa {card.Name}");

            foreach (var effect in card.Effects)
            {
                if (IsOver) break;
                effect.Apply(this, target, mult);
            }

            if (card.Type == CardType.Personagem) InPlay.Add(card);
            else if (card.Exhaust) Deck.Exhausted.Add(card);
            else Deck.Discard.Add(card);

            CheckEnd();
            return true;
        }

        public int PreviewPlayerDamage(int baseAmount, float mult)
            => CardEffect.Scale(baseAmount + Player.GetStatus(StatusType.Forca) + AttackBonus, mult);

        public void PlayerAttack(EnemyCombatant enemy, int baseAmount, float mult)
        {
            if (enemy == null || enemy.IsDead) return;
            int damage = PreviewPlayerDamage(baseAmount, mult);
            int lost = enemy.TakeDamage(damage);
            Popup(enemy, lost > 0 ? $"-{lost}" : "Bloqueado", lost > 0 ? ColDamage : ColBlock);
            OnHit?.Invoke(enemy);
            if (enemy.IsDead) HandleEnemyDeath(enemy);
        }

        public void PlayerGainBlock(int amount)
        {
            Player.GainBlock(amount);
            Popup(Player, $"+{amount} escudo", ColBlock);
        }

        public void HealPlayer(int amount)
        {
            int healed = Player.Heal(amount);
            Popup(Player, $"+{healed} vida", ColHeal);
        }

        public void PlayerLoseHp(int amount)
        {
            int lost = Player.TakeDamage(amount, true);
            Popup(Player, $"-{lost}", ColDamage);
            OnHit?.Invoke(Player);
            CheckEnd();
        }

        public void GainEnergy(int amount)
        {
            Energy += amount;
            Popup(Player, $"+{amount} energia", ColGold);
        }

        public void DrawCards(int count)
        {
            if (Deck.Draw(count) < count) Log("Não há mais cartas para comprar.");
        }

        public void EnterCharacter(int attackBonus, int blockPerTurn)
        {
            AttackBonus += attackBonus;
            BlockPerTurn += blockPerTurn;
            Popup(Player, "Personagem em jogo!", ColGold);
            Log($"Bônus de personagem: +{attackBonus} de dano e +{blockPerTurn} de escudo por turno.");
        }

        public void ApplyStatus(Combatant target, StatusType status, int amount)
        {
            if (target == null || target.IsDead || amount <= 0) return;
            if (status == StatusType.Sono && target is EnemyCombatant enemy && enemy.Data.ResistsSleep)
            {
                Log($"{enemy.Name} resiste ao sono, mas fica atordoada!");
                status = StatusType.Fraqueza;
                amount += 1;
            }
            target.AddStatus(status, amount);
            Popup(target, $"{Statuses.Name(status)} +{amount}", ColStatus);
        }

        public void Push(EnemyCombatant enemy, int amount)
        {
            if (enemy == null || enemy.IsDead) return;
            int before = enemy.Distance;
            enemy.Distance = Mathf.Min(GameConfig.MaxDistance, enemy.Distance + amount);
            Popup(enemy, enemy.Distance != before ? "Empurrado!" : "Já está longe", ColNeutral);
        }

        // ------------------------------------------------------------------ Turno dos inimigos

        public void BeginEnemyPhase()
        {
            Deck.DiscardHand();
            Log("— Turno dos inimigos —");
        }

        /// <summary>Os mais próximos agem primeiro. Inimigos invocados durante a fase só agem no próximo turno.</summary>
        public List<EnemyCombatant> EnemyTurnOrder() => AliveEnemies.OrderBy(e => e.Distance).ToList();

        public void ExecuteEnemyTurn(EnemyCombatant enemy)
        {
            if (IsOver || enemy.IsDead) return;

            enemy.Block = 0;
            TickStatuses(enemy);
            if (enemy.IsDead || IsOver) return;

            if (enemy.GetStatus(StatusType.Sono) > 0)
            {
                enemy.AddStatus(StatusType.Sono, -1);
                Popup(enemy, "Zzz...", ColStatus);
                Log($"{enemy.Name} está dormindo.");
                enemy.EndTurn();
                return;
            }

            var move = enemy.Intent;
            if (move == null) return;

            bool attackLands = enemy.AttackLandsThisTurn;
            if (enemy.IsOutOfRange)
            {
                enemy.Distance = Mathf.Max(enemy.Data.Range, enemy.Distance - enemy.Data.Speed);
                Popup(enemy, "Avança!", ColNeutral);
                Log($"{enemy.Name} avança.");
                if (move.IsAttack && !attackLands)
                {
                    enemy.EndTurn();
                    return;
                }
            }

            ExecuteMove(enemy, move);
            enemy.EndTurn();
        }

        void ExecuteMove(EnemyCombatant enemy, EnemyMove move)
        {
            Log($"{enemy.Name}: {move.Name}");

            if (move.IsAttack)
            {
                for (int i = 0; i < move.Hits && !IsOver; i++) EnemyHitsPlayer(enemy, move.Damage);
            }
            if (IsOver) return;

            if (move.Block > 0)
            {
                enemy.GainBlock(move.Block);
                Popup(enemy, $"+{move.Block} escudo", ColBlock);
            }
            if (move.Cleanse) enemy.ClearDebuffs();
            if (move.Status.HasValue) ApplyStatus(move.StatusOnSelf ? (Combatant)enemy : Player, move.Status.Value, move.StatusAmount);
            if (move.SummonId != null)
            {
                if (AliveEnemies.Count() < GameConfig.MaxEnemies) SpawnEnemy(move.SummonId, GameConfig.MaxDistance, true);
                else Log("...mas ninguém respondeu ao chamado.");
            }

            enemy.OnMoveExecuted(this, move);
        }

        public int PreviewEnemyDamage(EnemyCombatant enemy, int baseDamage)
        {
            float dmg = baseDamage + enemy.GetStatus(StatusType.Forca);
            if (enemy.Data.Trait == EnemyTrait.Cardume)
                dmg += AliveEnemies.Count(o => o != enemy && o.Data.Id == enemy.Data.Id);
            if (enemy.Data.Trait == EnemyTrait.Carniceiro && Player.Hp * 2 < Player.MaxHp)
                dmg *= 1.5f;
            if (enemy.GetStatus(StatusType.Fraqueza) > 0)
                dmg *= 0.75f;
            return Mathf.Max(0, Mathf.FloorToInt(dmg));
        }

        void EnemyHitsPlayer(EnemyCombatant enemy, int baseDamage)
        {
            int lost = Player.TakeDamage(PreviewEnemyDamage(enemy, baseDamage));
            Popup(Player, lost > 0 ? $"-{lost}" : "Bloqueado!", lost > 0 ? ColDamage : ColBlock);
            OnHit?.Invoke(Player);
            CheckEnd();
        }

        // ------------------------------------------------------------------ Comum

        void TickStatuses(Combatant combatant)
        {
            int damage = combatant.TickDamageOverTime(out string source);
            if (damage > 0)
            {
                Popup(combatant, $"-{damage}", ColStatus);
                Log($"{combatant.Name} sofre {damage} de dano ({source}).");
                OnHit?.Invoke(combatant);
            }
            if (combatant is EnemyCombatant enemy && enemy.IsDead) HandleEnemyDeath(enemy);
            CheckEnd();
        }

        void HandleEnemyDeath(EnemyCombatant enemy)
        {
            if (enemy.Rewarded) return;
            enemy.Rewarded = true;
            MonstersKilled++;

            if (enemy.IsSummoned)
            {
                ScoreEarned += 10;
                Log($"{enemy.Name} se apagou.");
            }
            else
            {
                XpEarned += enemy.Data.Xp;
                ScoreEarned += enemy.Data.Score;
                Popup(enemy, $"+{enemy.Data.Xp} XP", ColGold);
                Log($"{enemy.Name} foi derrotado! +{enemy.Data.Xp} XP");
            }

            // Sem o chefe, as criaturas invocadas por ele se dissipam.
            if (enemy.Data.IsBoss)
            {
                foreach (var other in AliveEnemies.Where(e => e.IsSummoned).ToList())
                {
                    other.Hp = 0;
                    other.Rewarded = true;
                    Log($"{other.Name} se dissipa no ar.");
                }
            }
            CheckEnd();
        }

        bool CheckEnd()
        {
            if (IsOver) return true;
            if (Player.IsDead)
            {
                IsOver = true;
                PlayerWon = false;
                Log("Você caiu...");
                return true;
            }
            if (!AliveEnemies.Any())
            {
                IsOver = true;
                PlayerWon = true;
                Log("Vitória!");
                return true;
            }
            return false;
        }
    }
}
