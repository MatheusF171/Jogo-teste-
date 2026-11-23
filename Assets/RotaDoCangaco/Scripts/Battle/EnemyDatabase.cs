using System.Collections.Generic;

namespace RotaDoCangaco
{
    /// <summary>
    /// Monstros da Fase 1 (canavial da Zona da Mata, à noite) e o chefe Mula Sem Cabeça.
    /// </summary>
    public static class EnemyDatabase
    {
        static readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();

        static EnemyDatabase() { Build(); }

        public static EnemyData Get(string id) => enemies[id];

        public static EnemyCombatant Create(string id)
        {
            var data = Get(id);
            return data.IsBoss ? new BossCombatant(data) : new EnemyCombatant(data);
        }

        static EnemyMove Attack(string name, int damage, int hits = 1, StatusType? status = null, int amount = 0)
            => new EnemyMove { Name = name, Damage = damage, Hits = hits, Status = status, StatusAmount = amount };

        static EnemyMove Defend(string name, int block) => new EnemyMove { Name = name, Block = block };

        static EnemyMove Buff(string name, StatusType status, int amount, int block = 0, bool cleanse = false)
            => new EnemyMove { Name = name, Status = status, StatusAmount = amount, StatusOnSelf = true, Block = block, Cleanse = cleanse };

        static EnemyMove Summon(string name, string id) => new EnemyMove { Name = name, SummonId = id };

        static void Add(EnemyData data) => enemies[data.Id] = data;

        static void Build()
        {
            Add(new EnemyData
            {
                Id = "sapo", Name = "Sapo-Cururu", SpriteKey = "sapo", Habitat = "Brejo", Description = "Cospe gosma venenosa de longe.",
                Level = 1, MaxHp = 15, Xp = 3, Score = 50, Range = 2, Speed = 1,
                Moves = { Attack("Gosma Venenosa", 3, 1, StatusType.Veneno, 2), Defend("Coaxar", 5), Attack("Linguada", 5) }
            });
            Add(new EnemyData
            {
                Id = "morcego", Name = "Morcego", SpriteKey = "morcego", Habitat = "Noite", Description = "Voa rápido e drena sua energia.",
                Level = 1, MaxHp = 11, Xp = 3, Score = 50, Range = 0, Speed = 2,
                Moves = { Attack("Mordida", 5), Attack("Drenar Energia", 2, 1, StatusType.Drenado, 1) }
            });
            Add(new EnemyData
            {
                Id = "fogo", Name = "Fogo-Fátuo", SpriteKey = "fogo_fatuo", Habitat = "Noite", Description = "Chama perdida que queima à distância.",
                Level = 2, MaxHp = 17, Xp = 5, Score = 80, Range = 2, Speed = 1,
                Moves = { Attack("Chama Fria", 4, 1, StatusType.Queimadura, 3), Defend("Tremeluzir", 6) }
            });
            Add(new EnemyData
            {
                Id = "urubu", Name = "Urubu", SpriteKey = "urubu", Habitat = "Canavial", Description = "Ronda os mais fracos: bate mais forte se você estiver ferido.",
                Level = 2, MaxHp = 20, Xp = 5, Score = 80, Range = 0, Speed = 2, Trait = EnemyTrait.Carniceiro,
                Moves = { Attack("Bicada", 6), Buff("Rondar", StatusType.Forca, 2), Attack("Bicada", 6) }
            });
            Add(new EnemyData
            {
                Id = "piranha", Name = "Piranha", SpriteKey = "piranha", Habitat = "Rio", Description = "Ataca em cardume: mais forte em grupo.",
                Level = 2, MaxHp = 12, Xp = 5, Score = 80, Range = 0, Speed = 1, Charges = true, Trait = EnemyTrait.Cardume,
                Moves = { Attack("Mordida em Cardume", 3) }
            });
            Add(new EnemyData
            {
                Id = "escorpiao", Name = "Escorpião", SpriteKey = "escorpiao", Habitat = "Canavial", Description = "Ferroada envenenada e carapaça dura.",
                Level = 3, MaxHp = 26, Xp = 8, Score = 120, Range = 0, Speed = 1,
                Moves = { Attack("Ferroada Envenenada", 5, 1, StatusType.Veneno, 3), Defend("Carapaça", 8), Attack("Pinçada", 8) }
            });
            Add(new EnemyData
            {
                Id = "queixada", Name = "Queixada", SpriteKey = "queixada", Habitat = "Mata", Description = "Avança em investida: anda e ataca no mesmo turno.",
                Level = 3, MaxHp = 32, Xp = 8, Score = 120, Range = 0, Speed = 1, Charges = true,
                Moves = { Attack("Investida", 10), Buff("Bufar", StatusType.Forca, 2, 6), Attack("Investida", 10) }
            });

            // ---------- Chefe ----------
            var mula = new EnemyData
            {
                Id = "mula", Name = "Mula Sem Cabeça", SpriteKey = "mula_arte", Habitat = "Engenho",
                Description = "A Fúria Sem Cabeça. Galopa nas noites de quinta para sexta soltando fogo pelo pescoço.",
                Level = 5, MaxHp = 140, Xp = 40, Score = 1000, Range = 0, Speed = 2, Charges = true,
                ResistsSleep = true, IsBoss = true, RandomStart = false,
                Moves =
                {
                    Attack("Relincho em Chamas", 5, 1, StatusType.Queimadura, 4),
                    Attack("Coice Duplo", 7, 2),
                    Summon("Chamar Fogos-Fátuos", "fogo"),
                    Attack("Coice Duplo", 7, 2),
                },
                EnrageMove = Buff("Fúria Sem Cabeça", StatusType.Forca, 3, 15, true),
            };
            mula.EnragedMoves.Add(Attack("Pisoteio Flamejante", 12, 1, StatusType.Queimadura, 3));
            mula.EnragedMoves.Add(Attack("Coice Duplo", 7, 2));
            mula.EnragedMoves.Add(Attack("Galope Infernal", 16));
            mula.EnragedMoves.Add(Summon("Chamar Fogos-Fátuos", "fogo"));
            Add(mula);
            var saci = new EnemyData
            {
                Id = "saci", Name = "Saci-Pererê", SpriteKey = "saci", Habitat = "Pedra do Redemoinho",
                Description = "Levanta poeira no lajedo e some no meio do redemoinho.",
                Level = 8, MaxHp = 130, Xp = 45, Score = 1000, Range = 1, Speed = 2,
                Charges = true, IsBoss = true, RandomStart = false,
                Moves =
                {
                    Attack("Poeira nos Olhos", 6, 1, StatusType.Fraqueza, 2),
                    Defend("Sumir na Poeira", 10),
                    Attack("Rodopio", 6, 2),
                },
                EnrageMove = Buff("Vento no Lajedo", StatusType.Forca, 2, 12, true),
            };
            saci.EnragedMoves.Add(Attack("Redemoinho", 9, 2));
            saci.EnragedMoves.Add(Attack("Poeira nos Olhos", 8, 1, StatusType.Fraqueza, 2));
            saci.EnragedMoves.Add(Defend("Sumir na Poeira", 10));
            Add(saci);
        }
    }
}
