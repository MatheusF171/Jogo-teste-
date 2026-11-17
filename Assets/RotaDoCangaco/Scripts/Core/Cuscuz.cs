using System;
using System.Collections.Generic;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>Uma porção de cuscuz que pode ser escolhida na panela das áreas de descanso.</summary>
    public class CuscuzOption
    {
        public string Id;
        public string Name;
        public string Description;
        public string Flavor;
        public Action<PlayerData> Apply;
    }

    /// <summary>
    /// Cardápio da panela de cuscuz. Cada porção dá um bônus permanente, mas aumenta o Peso:
    /// a partir de 3 porções não digeridas o herói fica pesado, perde energia e joga menos cartas por turno.
    /// </summary>
    public static class CuscuzMenu
    {
        public static readonly List<CuscuzOption> Options = new List<CuscuzOption>
        {
            new CuscuzOption
            {
                Id = "ovo", Name = "Cuscuz com Ovo",
                Description = "+5 de vida máxima e recupera 8 de vida.",
                Flavor = "O clássico do café da manhã nordestino.",
                Apply = p => { p.MaxHp += 5; p.Heal(8); }
            },
            new CuscuzOption
            {
                Id = "charque", Name = "Cuscuz com Charque",
                Description = "+1 de Força em todas as batalhas.",
                Flavor = "Carne de sol desfiada dá sustança.",
                Apply = p => p.BonusStrength++
            },
            new CuscuzOption
            {
                Id = "leite", Name = "Cuscuz com Leite",
                Description = "Começa cada batalha com +4 de escudo.",
                Flavor = "Molhadinho, do jeito que a vó fazia.",
                Apply = p => p.BonusStartBlock += 4
            },
            new CuscuzOption
            {
                Id = "manteiga", Name = "Cuscuz com Manteiga de Garrafa",
                Description = "+1 carta na primeira mão de cada batalha.",
                Flavor = "Manteiga da terra, dourada e cheirosa.",
                Apply = p => p.BonusFirstHand++
            },
        };

        public static void Eat(PlayerData player, CuscuzOption option)
        {
            option.Apply(player);
            player.Peso++;
            player.CuscuzEaten++;
        }

        /// <summary>Texto do que acontece com o peso se comer mais uma porção.</summary>
        public static string NextServingWarning(PlayerData player)
        {
            int next = player.Peso + 1;
            if (next >= GameConfig.PesoGordo) return "Mais uma porção: <color=#FF6B6B>GORDO</color> (-2 de energia, máx. 2 cartas por turno, não consegue pular)";
            if (next >= GameConfig.PesoGordinho) return "Mais uma porção: <color=#FFA24D>Gordinho</color> (-1 de energia, máx. 3 cartas por turno, não consegue pular)";
            if (next >= GameConfig.PesoBuchoCheio) return "Mais uma porção: <color=#FFD24D>Bucho cheio</color> (-1 de energia por turno)";
            return "Mais uma porção: <color=#8CFF8C>tudo certo</color>, ainda cabe!";
        }

        public static Color PesoColor(int peso)
        {
            if (peso >= GameConfig.PesoGordo) return new Color(1f, 0.35f, 0.35f);
            if (peso >= GameConfig.PesoGordinho) return new Color(1f, 0.6f, 0.25f);
            if (peso >= GameConfig.PesoBuchoCheio) return new Color(1f, 0.85f, 0.3f);
            return new Color(0.5f, 0.95f, 0.5f);
        }
    }
}
