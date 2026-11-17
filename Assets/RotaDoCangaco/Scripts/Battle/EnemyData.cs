using System.Collections.Generic;

namespace RotaDoCangaco
{
    public enum EnemyTrait
    {
        Nenhum,
        Cardume,     // Piranha: +1 de dano para cada outra piranha viva
        Carniceiro   // Urubu: +50% de dano se o jogador estiver com menos da metade da vida
    }

    /// <summary>Uma ação do padrão de um inimigo. Os campos preenchidos definem o que ela faz.</summary>
    public class EnemyMove
    {
        public string Name;
        public int Damage;
        public int Hits = 1;
        public int Block;
        public StatusType? Status;
        public int StatusAmount;
        public bool StatusOnSelf;
        public string SummonId;
        public bool Cleanse;

        public bool IsAttack => Damage > 0;
    }

    /// <summary>Ficha de um tipo de inimigo (dados), separada do combatente vivo na batalha.</summary>
    public class EnemyData
    {
        public string Id;
        public string Name;
        public string SpriteKey;
        public string Habitat;
        public string Description;
        public int Level = 1;
        public int MaxHp = 10;
        public int Xp = 3;
        public int Score = 50;
        public int Range;             // distância máxima de onde consegue atacar (0 = corpo a corpo)
        public int Speed = 1;         // quantas casas avança por turno
        public bool Charges;          // avança e ataca no mesmo turno (investida)
        public bool ResistsSleep;     // Sono vira Fraqueza
        public bool IsBoss;
        public bool RandomStart = true;
        public EnemyTrait Trait;
        public readonly List<EnemyMove> Moves = new List<EnemyMove>();

        // Somente chefes: ação de fúria (abaixo de 50% da vida) e o novo padrão depois dela.
        public EnemyMove EnrageMove;
        public readonly List<EnemyMove> EnragedMoves = new List<EnemyMove>();
    }

    /// <summary>Grupo de inimigos de um encontro no mapa.</summary>
    public class EncounterData
    {
        public readonly string Title;
        public readonly bool Roams;
        public readonly List<KeyValuePair<string, int>> Enemies = new List<KeyValuePair<string, int>>();

        public EncounterData(string title, bool roams, params (string id, int distance)[] enemies)
        {
            Title = title;
            Roams = roams;
            foreach (var e in enemies) Enemies.Add(new KeyValuePair<string, int>(e.id, e.distance));
        }

        public EnemyData Leader => EnemyDatabase.Get(Enemies[0].Key);

        public string Summary()
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var e in Enemies)
            {
                if (!counts.ContainsKey(e.Key)) { counts[e.Key] = 0; order.Add(e.Key); }
                counts[e.Key]++;
            }
            var parts = new List<string>();
            foreach (var id in order)
            {
                var d = EnemyDatabase.Get(id);
                parts.Add((counts[id] > 1 ? counts[id] + "x " : "") + d.Name + " (Nv." + d.Level + ")");
            }
            return string.Join(", ", parts);
        }
    }
}
