using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// Teste automático de ponta a ponta. Só é ativado pela linha de comando:
    ///   RotaDoCangaco.exe -autotest "C:\pasta\de\saida"
    /// Percorre menu, mapa, batalha, nível, cordel, baú, chefe e vitória, salvando capturas de tela e um log.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autotest");

        string outDir;
        readonly StringBuilder log = new StringBuilder();
        int errors;

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                log.AppendLine($"[{type}] {message}\n{stack}");
            }
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, "-autotest");
            outDir = idx + 1 < args.Length && !args[idx + 1].StartsWith("-")
                ? args[idx + 1]
                : Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(outDir);

            var game = GameManager.Instance;
            yield return new WaitForSeconds(1f);
            yield return Shot("01_menu");

            game.UI.ShowInstructions();
            yield return Shot("02_instrucoes");

            game.StartNewRun();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("03_mapa");

            game.Map.ToggleOverview();
            yield return new WaitForSeconds(1.6f);
            yield return Shot("04_mapa_inteiro");
            game.Map.ToggleOverview();
            yield return new WaitForSeconds(1f);

            // Escolha antes da batalha e pulo por cima dos sapos.
            var first = game.Map.All<EncounterEntity>().First(e => e.Key == 'a');
            game.Map.TeleportPlayer(first.Pos + Vector2Int.left);
            yield return new WaitForSeconds(0.6f);
            game.OfferEncounter(first, false);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("05a_encontro_opcoes");
            Click("Botao1");
            yield return new WaitForSeconds(0.16f + GameConfig.JumpDuration * 0.45f);
            yield return Shot("05b_pulando");
            yield return WaitFor(() => game.State == GameState.Exploration, 3f);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("05c_depois_do_pulo");
            log.AppendLine($"Pulo: herói em {game.Map.PlayerPos}, sapos em {first.Pos}");

            // Primeira batalha (sapos), com um combo manual para a captura.
            game.Map.TeleportPlayer(first.Pos + Vector2Int.left);
            yield return new WaitForSeconds(0.5f);
            game.StartEncounter(first);
            yield return new WaitForSeconds(1.3f);
            yield return Shot("05_batalha");

            var battle = game.Battle.Battle;
            var hero = battle.Deck.Hand.FirstOrDefault(c => c.Type == CardType.Personagem);
            if (hero != null) battle.PlayCard(hero, null);
            var spell = battle.Deck.Hand.FirstOrDefault(c => c.Id == "feitico");
            if (spell != null) battle.PlayCard(spell, battle.AliveEnemies.First());
            game.UI.BattleView.Refresh();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("06_combo");

            yield return game.Battle.AutoPlay(30, 0.4f);
            yield return WaitFor(() => game.State != GameState.Battle, 10f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("07_nivel");
            yield return PickCardsUntilExploration(game);

            // Cordel
            var quiz = game.Map.All<QuizEntity>().First();
            game.OpenQuiz(quiz);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("08_cordel");
            Click("Opcao0");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("09_cordel_resposta");
            Click("Continuar");
            yield return new WaitForSeconds(0.3f);

            // Baralho e baú
            game.ShowDeck();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("10_baralho");
            Click("Fechar");
            yield return new WaitForSeconds(0.3f);

            var chest = game.Map.All<ChestEntity>().First(c => c.Kind == ChestKind.Card);
            game.Map.TeleportPlayer(chest.Pos);
            game.OpenChest(chest);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("11_bau");
            yield return PickCardsUntilExploration(game);

            // Pulo bloqueado: monstros muito mais fracos que o herói.
            var weak = game.Map.All<EncounterEntity>().First(e => e.Key == 'c');
            game.Map.TeleportPlayer(weak.Pos + Vector2Int.left);
            int realLevel = game.Player.Level;
            game.Player.Level = 9;
            game.OfferEncounter(weak, false);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("11a_pulo_bloqueado_nivel");
            Click("Botao1");
            game.Player.Level = realLevel;
            yield return new WaitForSeconds(0.3f);

            // Panela de cuscuz: come todas as porções e fica gordinho.
            var pot = game.Map.All<CuscuzPotEntity>().First();
            game.Map.TeleportPlayer(pot.Pos + Vector2Int.down);
            game.OpenCuscuz(pot);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("11e_cuscuz");
            for (int i = 0; i < GameConfig.CuscuzServingsPerPot; i++)
            {
                Click("Cuscuz" + (i % 4));
                yield return new WaitForSeconds(0.3f);
            }
            yield return Shot("11f_cuscuz_cheio");
            Click("Parar");
            yield return new WaitForSeconds(1f);
            yield return Shot("11g_mapa_gordo");
            log.AppendLine($"Cuscuz: peso {game.Player.Peso} ({game.Player.FatLabel}), energia {game.Player.EffectiveEnergy}, máx. cartas {game.Player.MaxCardsPerTurn}");

            // Grupo de 3 inimigos com pouca vida: testa o layout e o caminho de derrota (perde 1 vida).
            var group = game.Map.All<EncounterEntity>().First(e => e.Key == 'h');
            game.Map.TeleportPlayer(group.Pos + Vector2Int.down);
            game.OfferEncounter(group, false);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("11h_pulo_bloqueado_gordo");
            Click("Botao1");
            yield return new WaitForSeconds(0.3f);
            game.Player.Hp = 6;
            game.StartEncounter(group);
            yield return new WaitForSeconds(1.3f);
            yield return Shot("11b_batalha_grupo");
            yield return game.Battle.AutoPlay(1, 0.3f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("11c_batalha_grupo_turno2");
            yield return game.Battle.AutoPlay(40, 0.2f);
            yield return WaitFor(() => game.State != GameState.Battle, 10f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("11d_resultado_grupo");
            log.AppendLine($"Vidas após o grupo: {game.Player.Lives}");
            yield return PickCardsUntilExploration(game);

            // Chefe (o herói recebe reforço para o teste ser curto)
            game.Player.MaxHp = 400;
            game.Player.Hp = 400;
            var boss = game.Map.All<BossEntity>().First();
            game.Map.TeleportPlayer(boss.Pos + Vector2Int.up);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("12_mapa_engenho");
            game.ShowBossIntro(boss);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("13_chefe_intro");
            Click("Botao0");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("14_chefe_batalha");

            yield return game.Battle.AutoPlay(3, 0.3f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("15_chefe_turnos");

            if (game.Battle.Battle != null)
            {
                var mula = game.Battle.Battle.Enemies.First(e => e.Data.IsBoss);
                mula.Hp = Mathf.Min(mula.Hp, 8);
                game.UI.BattleView.Refresh();
            }
            yield return game.Battle.AutoPlay(30, 0.3f);
            yield return WaitFor(() => game.State != GameState.Battle, 10f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("16_recompensa_chefe");
            Click("Continuar");
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 10 && FindCard() != null; i++)
            {
                FindCard().OnClicked(FindCard());
                yield return new WaitForSeconds(0.2f);
            }
            yield return Shot("17_porteira");
            Click("Botao0");
            yield return new WaitForSeconds(0.4f);

            // Saída / vitória
            game.Player.Score = Mathf.Max(game.Player.Score, GameConfig.MinScoreToWin);
            var exit = game.Map.All<ExitEntity>().First();
            game.Map.TeleportPlayer(exit.Pos + Vector2Int.left);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("18_saida");
            game.TryExit(exit);
            yield return new WaitForSeconds(0.6f);
            var player = game.Player;
            int score = player.Score, cards = player.Deck.Count;
            Click("Botao0");
            yield return WaitFor(() => game.Map.Phase == SertaoMap.Phase, 3f);
            if (!Check(game.Map.Phase == SertaoMap.Phase, "A porteira carregou o Sertão."))
            {
                Finish(game);
                yield break;
            }
            Check(game.Player == player && player.Score == score && player.Deck.Count == cards,
                "Jogador, pontos e baralho passaram para a fase 2.");
            var sertaoExit = game.Map.All<ExitEntity>().First();
            Check(!player.BossDefeated && !sertaoExit.Visible, "A saída do Sertão começa fechada.");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("19_sertao");

            var saci = game.Map.All<BossEntity>().First();
            var route = game.Map.FindPath(game.Map.PlayerPos, saci.Pos, _ => true);
            Check(route != null && route.Count > 0, "O início do Sertão tem caminho até o Saci.");
            game.Map.TeleportPlayer(saci.Pos + Vector2Int.left);
            game.ShowBossIntro(saci);
            yield return Shot("20_saci_intro");
            Click("Botao0");
            yield return WaitFor(() => game.State == GameState.Battle, 3f);
            if (!Check(game.State == GameState.Battle && game.Battle.Battle.Encounter.Leader.Id == "saci",
                    "A batalha contra o Saci abriu."))
            {
                Finish(game);
                yield break;
            }
            yield return new WaitForSeconds(1.4f);
            yield return Shot("21_saci_batalha");
            game.Battle.Battle.Enemies.First(e => e.Data.IsBoss).Hp = 8;
            yield return game.Battle.AutoPlay(25, 0.25f);
            yield return WaitFor(() => game.State != GameState.Battle, 10f);
            Check(sertaoExit.Visible && player.Deck.Any(c => c.Id == "redemoinho"),
                "Derrotar o Saci abre a saída e entrega a carta Redemoinho.");
            Click("Continuar");
            yield return new WaitForSeconds(0.3f);
            yield return PickCardsUntilExploration(game);
            game.Map.TeleportPlayer(sertaoExit.Pos);
            game.TryExit(sertaoExit);
            yield return new WaitForSeconds(0.6f);
            Check(game.State == GameState.Ended, "A saída do Sertão encerra a partida.");
            yield return Shot("22_vitoria");
            Finish(game);
        }

        bool Check(bool passed, string message)
        {
            if (!passed) errors++;
            log.AppendLine((passed ? "OK: " : "FALHOU: ") + message);
            return passed;
        }

        void Finish(GameManager game)
        {
            log.AppendLine($"Fim do teste. Erros: {errors}. Estado final: {game.State}. Pontos: {game.Player.Score}. Nível: {game.Player.Level}.");
            File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
            Application.Quit(errors == 0 ? 0 : 1);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            log.AppendLine("Captura: " + name);
            yield return null;
            yield return null;
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float t = 0f;
            while (!condition() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        static CardView FindCard() =>
            FindObjectsByType<CardView>().FirstOrDefault(c => c.OnClicked != null && c.isActiveAndEnabled);

        IEnumerator PickCardsUntilExploration(GameManager game)
        {
            for (int i = 0; i < 15 && game.State != GameState.Exploration; i++)
            {
                var card = FindCard();
                if (card != null) card.OnClicked(card);
                else Click("Botao0");
                yield return new WaitForSeconds(0.25f);
            }
        }

        void Click(string buttonName)
        {
            var button = FindObjectsByType<Button>()
                .FirstOrDefault(b => b.name == buttonName && b.isActiveAndEnabled && b.interactable);
            if (button != null) button.onClick.Invoke();
            else log.AppendLine("Botão não encontrado: " + buttonName);
        }
    }
}
