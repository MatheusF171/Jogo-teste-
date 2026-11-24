using System.Collections.Generic;

namespace RotaDoCangaco
{
    /// <summary>
    /// Mapa único do jogo: a Pedra do Redemoinho, no sertão nordestino (Caatinga).
    /// O mapa inteiro (inimigos, itens e o chefe) é visível desde o início para o jogador planejar o caminho.
    ///
    /// Rotas até o lajedo do Saci (canto superior direito), saindo da fazenda (canto inferior esquerdo):
    ///   • Ponte do açude: mais curta, mas o cardume de piranhas e os vigias da margem bloqueiam a passagem.
    ///   • Serra e alto: mais longa e com urubus/morcegos que rondam, porém com relíquia, baú de cartas e garrafada.
    /// Monstros em corredores de 1 tile obrigam a escolher entre lutar e pular (o pulo precisa de chão livre do outro lado).
    /// Os baús da relíquia e do ninho ficam atrás do guarda: não dá para pular em cima de um baú fechado.
    ///
    /// Legenda de terreno: # rocha · . trilha · , chão de caatinga · ~ açude · = ponte · W muro do lajedo
    ///                     _ piso de pedra · T árvore · K mandacaru (T, K, #, W e ~ bloqueiam)
    /// Legenda de objetos: P início · a–l grupos de monstros · B chefe · X saída (abre após o chefe)
    ///                     C baú de carta rara · R relíquia · H garrafada (cura) · Q cordel (quiz) · F fogueira
    ///                     U panela de cuscuz (fica ao lado da fogueira, formando a área de descanso)
    /// </summary>
    public static class SertaoMap
    {
        public const string Title = "Pedra do Redemoinho";
        public const string Region = "Sertão nordestino · Caatinga";

        static readonly string[] layout =
        {
            "########################################",
            "########################################",
            "###R#######################WWWWWWWWWWW##",
            "###g################,H,,K,#W_______X_W##",
            "###.################,,T,,,#W__W___W__W##",
            "##,.,,T,,###,,Q#####K,,,,,#W_________W##",
            "##,,,K,,.....,,..h..,,,k,,#W_____B___W##",
            "##C,,,,,,##,T,,#####,T,,,K#W__W___W__W##",
            "##,,T,b,,##,,,K#####,,,,,,#W_________W##",
            "###,,,,K,,,,,,,#####K,,T,,#WWWWW_WWWWW##",
            "#############.########,.,#######j#######",
            "#############.#######,,.,T,FU,,...,Q,K##",
            "#############.#######K,....,,.,,T,,,,###",
            "#############.########,,K,,,,i,,,K,,####",
            "#############f########~~~~~~~=~~~~~~####",
            "#############.#######~~~~~~~~=~~~~~~~###",
            "#############.#######~~~~~~~~e~~~~~~~###",
            "#############.########~~~~~~~=~~~~~~~###",
            "#############.,,K,,####~~~~~~=~~~~~~####",
            "###K,,,T####,.,,,,T,,######,...,,K,#####",
            "##,,,,Q,####,....,,,Q,#####,,.,,,,d,####",
            "##T,,,,,####,,T,.,FU,,######,.,T,,,,,###",
            "##___,,,####.....,,,,....c....,,,K,,Q###",
            "##_P_.....a..,,K.,,T,,######,K,,...,,###",
            "##___,,,####,,.,.,K,,#########,,#l#,,###",
            "##,,,K,,#####,,T,,,##############C######",
            "###,,,,#################################",
            "########################################",
        };

        public static readonly Dictionary<char, EncounterData> Encounters = new Dictionary<char, EncounterData>
        {
            // Fazenda e centro (início)
            ['a'] = new EncounterData("Sapos da Cacimba", false, ("sapo", 1), ("sapo", 2)),
            ['c'] = new EncounterData("Luz Perdida na Caatinga", false, ("fogo", 2), ("sapo", 1)),
            ['f'] = new EncounterData("Ferrões do Curral", false, ("escorpiao", 1), ("sapo", 2)),
            // Rota da ponte e margem sudeste
            ['d'] = new EncounterData("Carniceiros da Caatinga", true, ("urubu", 1), ("morcego", 2)),
            ['l'] = new EncounterData("Ninho de Escorpiões", false, ("escorpiao", 1), ("escorpiao", 1)),
            ['e'] = new EncounterData("Cardume do Açude", false, ("piranha", 1), ("piranha", 1), ("piranha", 2)),
            ['i'] = new EncounterData("Vigias da Margem", false, ("escorpiao", 1), ("urubu", 2)),
            // Rota da serra e do alto
            ['b'] = new EncounterData("Morcegos da Gruta", true, ("morcego", 1), ("morcego", 2)),
            ['g'] = new EncounterData("Queixada Furioso", false, ("queixada", 2)),
            ['h'] = new EncounterData("Assombração da Serra", false, ("fogo", 2), ("fogo", 2), ("morcego", 1)),
            ['k'] = new EncounterData("Revoada do Alto", true, ("urubu", 1), ("urubu", 2)),
            // Portão do lajedo e chefe
            ['j'] = new EncounterData("Bando de Queixadas", false, ("queixada", 1), ("queixada", 2)),
            ['B'] = new EncounterData("O Rei do Redemoinho", false, ("saci", 2)),
        };

        public static readonly MapPhase Phase = new MapPhase(Title, Region, layout, Encounters);

        public static int Width => Phase.Width;
        public static int Height => Phase.Height;
    }
}
