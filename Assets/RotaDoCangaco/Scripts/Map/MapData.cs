using System.Collections.Generic;

namespace RotaDoCangaco
{
    /// <summary>
    /// Mapa único do protótipo: o Canavial Assombrado da Zona da Mata nordestina, à noite.
    /// O mapa inteiro (inimigos, itens e o chefe) é visível desde o início para o jogador planejar o caminho.
    ///
    /// Legenda de terreno: # cana (parede) · . trilha · , clareira · ~ rio · = ponte · W muro do engenho
    ///                     _ piso do engenho · T árvore (parede)
    /// Legenda de objetos: P início · a–l grupos de monstros · B chefe · X saída (abre após o chefe)
    ///                     C baú de carta rara · R relíquia · H garrafada (cura) · Q cordel (quiz) · F fogueira
    ///                     U panela de cuscuz (fica ao lado da fogueira, formando a área de descanso)
    /// </summary>
    public static class MapData
    {
        public const string PhaseTitle = "Fase 1 — Canavial Assombrado";
        public const string RegionName = "Zona da Mata canavieira · Nordeste";

        public static readonly string[] Layout =
        {
            "########################################",
            "##########################WWWWWWWWWWWWW#",
            "###R######################W_________X_W#",
            "###g######################W___________W#",
            "###,,,,T##################W___________W#",
            "###,,,,,#######T,,,,,,,###W_____B_____W#",
            "###,,,,,.......,,,Q,,,,..#W___________W#",
            "###,,,Q,##.####,,,,,,,,#.#W___________W#",
            "##########.####,,,,,,,T#h#W___________W#",
            "##########.########.####.#WWWWWW_WWWWWW#",
            "#######,,,,,,,#####.####.#######j#######",
            "#######,C,,k,,#####.####....i....#######",
            "#######,,,,,,T#####f#########,FU########",
            "###################.####################",
            "#~~~~~~~~~~~~~~~~~~=~~~~~~~~~~~~~~~~~~~#",
            "#~~~~~~~~~~~~~~~~~~e~~~~~~~~~~~~~~~~~~~#",
            "#C,,,,#############.####################",
            "#,,d,,##,,,,,T#####.####################",
            "#,,,,,##,,,b,,#####.####################",
            "##.#####,,.,,,.c....####################",
            "##.#####Q,.,,,#####.########,,,,,,T#####",
            "##.#####,,.,,,#####.########,,,,,H,#####",
            "##.#######.########........l,,,FU,,#####",
            "#T,,,,####.#################,,,,,Q,#####",
            "#,,,,,####.#################T,,,,,,#####",
            "#,,P,,..a..#############################",
            "#,,,,T##################################",
            "########################################",
        };

        public static readonly Dictionary<char, EncounterData> Encounters = new Dictionary<char, EncounterData>
        {
            ['a'] = new EncounterData("Sapos do Brejo", false, ("sapo", 1), ("sapo", 2)),
            ['b'] = new EncounterData("Revoada de Morcegos", true, ("morcego", 1), ("morcego", 2)),
            ['c'] = new EncounterData("Luz Perdida no Canavial", false, ("fogo", 2), ("sapo", 1)),
            ['d'] = new EncounterData("Carniceiros da Noite", true, ("urubu", 1), ("morcego", 2)),
            ['e'] = new EncounterData("Cardume da Ponte", false, ("piranha", 1), ("piranha", 1), ("piranha", 2)),
            ['f'] = new EncounterData("Ferrões na Trilha", false, ("escorpiao", 1), ("sapo", 2)),
            ['g'] = new EncounterData("Queixada Furioso", false, ("queixada", 2)),
            ['h'] = new EncounterData("Assombração do Engenho", false, ("fogo", 2), ("fogo", 2), ("morcego", 1)),
            ['i'] = new EncounterData("Guardas da Moenda", false, ("escorpiao", 1), ("urubu", 2)),
            ['j'] = new EncounterData("Bando de Queixadas", false, ("queixada", 1), ("queixada", 2)),
            ['k'] = new EncounterData("Urubus Rondando", true, ("urubu", 1), ("urubu", 2)),
            ['l'] = new EncounterData("Ninho de Escorpiões", false, ("escorpiao", 1), ("escorpiao", 1)),
            ['B'] = new EncounterData("A Fúria Sem Cabeça", false, ("mula", 2)),
        };

        public static int Width => Layout[0].Length;
        public static int Height => Layout.Length;
    }

    public class QuizQuestion
    {
        public string Question;
        public string[] Options;
        public int Correct;
        public string Explanation;
    }

    /// <summary>Perguntas dos cordéis (RF02): cultura e história da região e do folclore.</summary>
    public static class QuizDatabase
    {
        public static readonly List<QuizQuestion> Questions = new List<QuizQuestion>
        {
            new QuizQuestion
            {
                Question = "Segundo a lenda, em que noite a Mula Sem Cabeça costuma aparecer?",
                Options = new[] { "Na noite de quinta para sexta-feira", "Na noite de Natal", "Em toda noite de lua nova" },
                Correct = 0,
                Explanation = "A tradição diz que a maldição se manifesta na noite de quinta para sexta-feira."
            },
            new QuizQuestion
            {
                Question = "O que a Mula Sem Cabeça tem no lugar da cabeça?",
                Options = new[] { "Uma abóbora", "Fogo", "Uma caveira" },
                Correct = 1,
                Explanation = "Ela galopa soltando fogo pelo pescoço, onde deveria estar a cabeça."
            },
            new QuizQuestion
            {
                Question = "Qual produto foi a base da economia do Nordeste no período colonial?",
                Options = new[] { "Café", "Cana-de-açúcar", "Borracha" },
                Correct = 1,
                Explanation = "A cana-de-açúcar dominou a Zona da Mata nordestina nos séculos XVI e XVII."
            },
            new QuizQuestion
            {
                Question = "Como se chamava a propriedade onde a cana era moída e virava açúcar?",
                Options = new[] { "Engenho", "Seringal", "Garimpo" },
                Correct = 0,
                Explanation = "O engenho reunia a moenda, a casa das caldeiras e a casa de purgar."
            },
            new QuizQuestion
            {
                Question = "A Literatura de Cordel recebeu esse nome porque os folhetos eram...",
                Options = new[] { "Escritos com cordas de viola", "Pendurados em cordões para venda", "Amarrados em livros grossos" },
                Correct = 1,
                Explanation = "Nas feiras, os folhetos ficavam pendurados em cordões (cordéis)."
            },
            new QuizQuestion
            {
                Question = "Qual era o apelido de Virgulino Ferreira da Silva, o cangaceiro mais famoso?",
                Options = new[] { "Corisco", "Lampião", "Antônio Silvino" },
                Correct = 1,
                Explanation = "Virgulino era o Lampião. Corisco e Antônio Silvino também foram cangaceiros."
            },
            new QuizQuestion
            {
                Question = "Na cantiga de ninar tradicional, quem \"vem pegar\" a criança que não dorme?",
                Options = new[] { "A Cuca", "O Saci", "A Iara" },
                Correct = 0,
                Explanation = "\"Nana, neném, que a Cuca vem pegar\" é uma cantiga popular brasileira."
            },
            new QuizQuestion
            {
                Question = "Qual capitania se tornou a maior produtora de açúcar do Brasil colonial?",
                Options = new[] { "Ceará", "Pernambuco", "Piauí" },
                Correct = 1,
                Explanation = "Pernambuco liderou a produção açucareira nos séculos XVI e XVII."
            },
            new QuizQuestion
            {
                Question = "O caldo da cana fervido até endurecer em blocos vira...",
                Options = new[] { "Rapadura", "Tapioca", "Cuscuz" },
                Correct = 0,
                Explanation = "A rapadura é feita com o caldo de cana fervido e moldado em blocos."
            },
        };
    }
}
