using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Progresso persistente do jogador durante a partida (fora das batalhas).
    /// </summary>
    public class PlayerData
    {
        public string Name = "Aprendiz da Cuca";
        public int Level = 1;
        public int Xp;
        public int MaxHp = GameConfig.StartMaxHp;
        public int Hp = GameConfig.StartMaxHp;
        public int Lives = GameConfig.StartLives;
        public int Score;
        public int EnergyPerTurn = GameConfig.StartEnergy;
        public int HandSize = GameConfig.HandSize;

        public int PendingLevelUps;
        public int MonstersDefeated;
        public int BattlesWon;
        public int QuizCorrect;
        public int QuizAnswered;
        public bool BossDefeated;

        public readonly List<CardData> Deck = new List<CardData>();
        public readonly List<string> Abilities = new List<string>();
        public readonly List<string> Relics = new List<string>();

        // Cuscuz: bônus permanentes e o "peso" (porções ainda não digeridas)
        public int Peso;
        public int CuscuzEaten;
        public int BonusStrength;     // Cuscuz com Charque
        public int BonusStartBlock;   // Cuscuz com Leite
        public int BonusFirstHand;    // Cuscuz com Manteiga de Garrafa

        public int XpToNext => GameConfig.XpToNext(Level);

        /// <summary>0 normal · 1 bucho cheio · 2 gordinho · 3 gordo.</summary>
        public int FatStage => Peso >= GameConfig.PesoGordo ? 3
                             : Peso >= GameConfig.PesoGordinho ? 2
                             : Peso >= GameConfig.PesoBuchoCheio ? 1 : 0;

        public int FatEnergyPenalty => FatStage >= 3 ? 2 : FatStage >= 1 ? 1 : 0;

        /// <summary>Máximo de cartas jogadas por turno (0 = sem limite).</summary>
        public int MaxCardsPerTurn => FatStage >= 3 ? 2 : FatStage >= 2 ? 3 : 0;

        public int EffectiveEnergy => Mathf.Max(1, EnergyPerTurn - FatEnergyPenalty);

        public bool CanJump => FatStage < GameConfig.JumpBlockFatStage;

        public string FatLabel
        {
            get
            {
                switch (FatStage)
                {
                    case 3: return "Gordo";
                    case 2: return "Gordinho";
                    case 1: return "Bucho cheio";
                    default: return Peso > 0 ? "Satisfeito" : "Leve";
                }
            }
        }

        public string FatPenaltyText
        {
            get
            {
                if (FatStage == 0) return "sem penalidade";
                string text = $"-{FatEnergyPenalty} de energia";
                if (MaxCardsPerTurn > 0) text += $", máx. {MaxCardsPerTurn} cartas por turno";
                return text;
            }
        }

        /// <summary>Lutar queima calorias: cada vitória digere uma porção.</summary>
        public void Digest(int amount) => Peso = Mathf.Max(0, Peso - amount);

        public PlayerData()
        {
            Deck.AddRange(CardDatabase.StartingDeck());
        }

        /// <summary>Soma XP e aplica os níveis ganhos. Retorna quantos níveis subiu.</summary>
        public int AddXp(int amount)
        {
            Xp += amount;
            int gained = 0;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                MaxHp += GameConfig.HpPerLevel;
                Hp = Mathf.Min(MaxHp, Hp + GameConfig.HpPerLevel + GameConfig.LevelUpHeal);
                gained++;
            }
            PendingLevelUps += gained;
            return gained;
        }

        public int Heal(int amount)
        {
            int before = Hp;
            Hp = Mathf.Clamp(Hp + amount, 0, MaxHp);
            return Hp - before;
        }
    }
}
