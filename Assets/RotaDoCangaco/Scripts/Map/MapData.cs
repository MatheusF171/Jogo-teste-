using System.Collections.Generic;

namespace RotaDoCangaco
{
    /// <summary>
    /// Dados compartilhados do mapa. O mapa do jogo (Pedra do Redemoinho) está em <see cref="SertaoMap"/>.
    /// </summary>
    public static class MapData
    {
        /// <summary>Caracteres do layout que são terreno; qualquer outro é um objeto (veja a legenda em SertaoMap).</summary>
        public const string TerrainTiles = "#.,~=W_TK";
    }

    public sealed class MapPhase
    {
        public readonly string Title;
        public readonly string Region;
        public readonly string[] Layout;
        public readonly Dictionary<char, EncounterData> Encounters;

        public MapPhase(string title, string region, string[] layout, Dictionary<char, EncounterData> encounters)
        {
            Title = title;
            Region = region;
            Layout = layout;
            Encounters = encounters;
        }

        public int Width => Layout[0].Length;
        public int Height => Layout.Length;
    }

    public class QuizQuestion
    {
        public string Question;
        public string[] Options;
        public int Correct;
        public string Explanation;
    }

    /// <summary>Perguntas dos cordéis (RF02): cultura e história do Sertão e do folclore.</summary>
    public static class QuizDatabase
    {
        public static readonly List<QuizQuestion> Questions = new List<QuizQuestion>
        {
            new QuizQuestion
            {
                Question = "Como o Saci-Pererê costuma ser descrito?",
                Options = new[] { "Com os pés virados para trás", "Com uma perna só e um gorro vermelho", "Com cabelos de fogo" },
                Correct = 1,
                Explanation = "O Saci pula numa perna só, usa gorro vermelho e fuma cachimbo. Os pés virados para trás são do Curupira."
            },
            new QuizQuestion
            {
                Question = "Segundo a lenda, onde o Saci gosta de se esconder?",
                Options = new[] { "No fundo dos rios", "Nas cavernas da serra", "Dentro dos redemoinhos de vento" },
                Correct = 2,
                Explanation = "Diz a tradição que o Saci viaja nos redemoinhos, e que dá para prendê-lo jogando uma peneira sobre eles."
            },
            new QuizQuestion
            {
                Question = "O nome \"Caatinga\" vem do tupi e significa...",
                Options = new[] { "Mata branca", "Terra vermelha", "Rio seco" },
                Correct = 0,
                Explanation = "Na seca a vegetação perde as folhas e a mata fica acinzentada, quase branca."
            },
            new QuizQuestion
            {
                Question = "Onde existe o bioma Caatinga?",
                Options = new[] { "Em toda a América do Sul", "Somente no Brasil", "Também na África" },
                Correct = 1,
                Explanation = "A Caatinga é o único bioma exclusivamente brasileiro e cobre boa parte do Nordeste."
            },
            new QuizQuestion
            {
                Question = "Para o sertanejo, quando o mandacaru floresce na seca, é sinal de que...",
                Options = new[] { "O inverno vai ser frio", "Vai faltar comida", "A chuva está chegando" },
                Correct = 2,
                Explanation = "A florada do mandacaru é tida como aviso de chuva, crença lembrada até nas músicas de Luiz Gonzaga."
            },
            new QuizQuestion
            {
                Question = "Para que serve um açude no Sertão?",
                Options = new[] { "Guardar água para os períodos de seca", "Criar peixes ornamentais", "Lavar o gado" },
                Correct = 0,
                Explanation = "O açude represa a água da chuva para abastecer pessoas, animais e plantações durante a estiagem."
            },
            new QuizQuestion
            {
                Question = "Luiz Gonzaga, sanfoneiro pernambucano, ficou conhecido como...",
                Options = new[] { "Rei do Frevo", "Rei do Baião", "Rei do Cangaço" },
                Correct = 1,
                Explanation = "Gonzagão levou o baião, o xote e o xaxado do Sertão para todo o Brasil."
            },
            new QuizQuestion
            {
                Question = "O chapéu dos cangaceiros, com a aba em meia-lua, era feito de...",
                Options = new[] { "Palha de carnaúba", "Lata", "Couro" },
                Correct = 2,
                Explanation = "O chapéu de couro enfeitado com estrelas e moedas virou um dos símbolos do cangaço."
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
        };
    }
}
