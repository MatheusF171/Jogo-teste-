using System.Collections.Generic;

namespace RotaDoCangaco
{
    public static class SertaoMap
    {
        // K é mandacaru. No sertão, # é rocha.
        static readonly string[] layout =
        {
            "########################################",
            "########################################",
            "############################WWWWWWWWWW##",
            "#####################,,,,###W______X_W##",
            "####################H,,,,K,#W_W____W_W##",
            "####################,,T,,,,#W________W##",
            "####################K,,,,,,#W____B___W##",
            "##################..,,Q,,,,.W_W____W_W##",
            "#################K..,,,,,,,f_________W##",
            "#################..........__________W##",
            "#################.e..#####,,WWWW__WWWW##",
            "#######T,,,######....,,,,,,,,,....######",
            "#####,C,,,,,.........,,,,,c,,,K,,#######",
            "#####,K,,,Q,,####,,,..K.,,T,,,,,,#######",
            "#####,,,,b,,,####,,,....,~~~~,.......###",
            "#####,,,,,,.............~~~~~~.......###",
            "#####T,,,,K,......,,....~~~~~~~...K,,###",
            "#####,,,K,,,,##,T,,,K..T,~~~~~~...,T,###",
            "#######,,,,,,##,,,,,,#,,,,,~~~,,,Q,,K###",
            "###############,,,,.....,,,,,,,,,,,,,###",
            "######,,,,K,###,,,,.K...,,,FU,,,Kd,#####",
            "###K,,,,T,,....,FU,.....,,,,,,,,,,,#####",
            "###___,,,,,....,,,,.....,,,,K,,,,T,#####",
            "###___,,,K,..a.,,,,.....####,,,,,K######",
            "###__P,,,,,....,,,Q,,########,,,,C######",
            "###,,K,,,,,,###,K,,,,###################",
            "####,,,,,,######,,,,####################",
            "########################################",
        };

        public static readonly MapPhase Phase = new MapPhase(
            "Fase 2 — Pedra do Redemoinho", "Sertão nordestino · Caatinga", layout,
            new Dictionary<char, EncounterData>
            {
                ['a'] = new EncounterData("Ferrões do Curral", false, ("escorpiao", 1), ("escorpiao", 2)),
                ['b'] = new EncounterData("Revoada da Serra", true, ("urubu", 1), ("urubu", 2)),
                ['c'] = new EncounterData("Queixadas do Açude", false, ("queixada", 1), ("queixada", 2)),
                ['d'] = new EncounterData("Vigias da Margem", false, ("escorpiao", 1), ("urubu", 2)),
                ['e'] = new EncounterData("Luzes no Lajedo", false, ("fogo", 2), ("fogo", 2)),
                ['f'] = new EncounterData("Bando do Alto", false, ("urubu", 1), ("queixada", 2)),
                ['B'] = new EncounterData("O Rei do Redemoinho", false, ("saci", 2)),
            });
    }
}
