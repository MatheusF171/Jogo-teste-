namespace RotaDoCangaco
{
    /// <summary>
    /// Valores de balanceamento centralizados. Ajuste aqui para mudar a dificuldade do protótipo.
    /// </summary>
    public static class GameConfig
    {
        // Jogador
        public const int StartLives = 3;
        public const int StartMaxHp = 40;
        public const int HpPerLevel = 5;
        public const int LevelUpHeal = 1;      // cura extra ao subir de nível (além do HP máximo ganho)
        public const int StartEnergy = 3;
        public const int HandSize = 5;

        // Combo: cada carta jogada em ordem crescente de custo soma +ComboStep ao multiplicador
        public const float ComboStep = 0.25f;
        public const float ComboMax = 2.5f;

        // Pontuação
        public const int MinScoreToWin = 1200;
        public const int QuizScore = 100;
        public const int ChestScore = 25;

        // Batalha: 0 = perto, 1 = médio, 2 = longe
        public const int MaxDistance = 2;
        public const int MaxEnemies = 4;

        // Exploração
        public const float StepDuration = 0.13f;
        public const int RoamAggroRadius = 4;
        public const int RoamLeashRadius = 5;

        // Pulo sobre monstros: bloqueado contra chefes, contra grupos muito mais fracos e quando estiver gordo
        public const int JumpBlockLevelGap = 5;   // nível do herói - maior nível do grupo >= isto: não pode pular
        public const int JumpBlockFatStage = 2;   // a partir de "Gordinho" não consegue pular
        public const float JumpDuration = 0.55f;
        public const float JumpHeight = 1.4f;
        public const int JumpStunSteps = 6;       // passos em que o monstro pulado fica tonto (não persegue)

        // Panela de cuscuz (áreas de descanso)
        public const int CuscuzServingsPerPot = 4;
        public const int PesoBuchoCheio = 3;      // -1 de energia
        public const int PesoGordinho = 4;        // -1 de energia, máx. 3 cartas por turno
        public const int PesoGordo = 5;           // -2 de energia, máx. 2 cartas por turno

        /// <summary>XP necessário para sair do nível informado. Calibrado para ~1 nível por grupo de monstros.</summary>
        public static int XpToNext(int level) => 4 + 2 * (level - 1);
    }
}
