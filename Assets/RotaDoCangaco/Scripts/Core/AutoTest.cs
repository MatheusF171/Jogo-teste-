using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// Teste automático de ponta a ponta. Só é ativado pela linha de comando:
    ///   RotaDoCangaco.exe -autotest "C:\pasta\de\saida"
    /// Percorre menu, mapa, pulo, batalha, nível, cordel, baús, relíquia, cuscuz, ponte, chefe, meta de pontos e vitória,
    /// salvando capturas de tela e um log.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autotest");

        static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.down, Vector2Int.up };

        string outDir;
        readonly StringBuilder log = new StringBuilder();
        int errors;
        bool musicWasMuted;

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
            // A música pode ter sido desligada numa partida normal (fica salvo); o teste liga e restaura no fim.
            musicWasMuted = game.Music.Muted;
            if (musicWasMuted) game.Music.Muted = false;
            yield return new WaitForSeconds(1f);
            Check(game.Music.ClipsLoaded, "As três músicas (menu, exploração e combate) carregaram.");
            Check(game.Music.PlayingMenu && game.Music.CurrentTrackAudible, "A música do menu toca no menu principal.");
            yield return Shot("01_menu");

            game.UI.ShowInstructions();
            yield return Shot("02_instrucoes");
            Check(game.Music.PlayingMenu, "A tela \"Como jogar\" mantém a música do menu.");

            game.StartNewRun();
            yield return new WaitForSeconds(1.2f);
            Check(game.Music.PlayingExploration && game.Music.CurrentTrackAudible, "Ao começar a partida entra a música de exploração.");
            Check(game.Map.Phase == SertaoMap.Phase, "A partida começa na Pedra do Redemoinho.");
            var exit = game.Map.All<ExitEntity>().First();
            Check(!exit.Visible, "A saída do lajedo começa fechada.");
            var saci = game.Map.All<BossEntity>().First();
            var route = game.Map.FindPath(game.Map.PlayerPos, saci.Pos, _ => true);
            Check(route != null && route.Count > 0, "O início tem caminho até o Saci.");
            yield return Shot("03_mapa");

            game.Map.ToggleOverview();
            yield return new WaitForSeconds(1.6f);
            yield return Shot("04_mapa_inteiro");
            game.Map.ToggleOverview();
            yield return new WaitForSeconds(1f);

            // Escolha antes da batalha e pulo por cima dos sapos (corredor de saída da fazenda).
            var first = game.Map.All<EncounterEntity>().First(e => e.Key == 'a');
            var jumpFrom = JumpSpot(game.Map, first);
            game.Map.TeleportPlayer(jumpFrom);
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
            Check(game.Map.PlayerPos == first.Pos + (first.Pos - jumpFrom), "O pulo levou o herói para o outro lado dos sapos.");

            // Primeira batalha (sapos), com um combo manual para a captura.
            game.Map.TeleportPlayer(jumpFrom);
            yield return new WaitForSeconds(0.5f);
            Check(game.Music.PlayingExploration, "A exploração toca no mapa antes da batalha.");
            game.StartEncounter(first);
            yield return new WaitForSeconds(1.3f);
            Check(game.Music.PlayingBattle, "A música de combate toca durante a batalha.");
            yield return Shot("05_batalha");

            var battle = game.Battle.Battle;

            // Arrastar o Feitiço até o sapo mais distante, pelo mesmo caminho do mouse (eventos de ponteiro simulados).
            var feiticoView = FindObjectsByType<CardView>().FirstOrDefault(c => c.Draggable && c.Card.Id == "feitico");
            var farEnemy = battle.AliveEnemies.OrderByDescending(e => e.Distance).First();
            var farView = FindObjectsByType<EnemyView>().FirstOrDefault(v => v.Enemy == farEnemy);
            if (Check(feiticoView != null && farView != null, "A mão e os inimigos estão prontos para arrastar."))
            {
                int hpBefore = farEnemy.Hp;
                var drag = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = feiticoView.transform.position,
                };
                ExecuteEvents.Execute(feiticoView.gameObject, drag, ExecuteEvents.beginDragHandler);
                drag.dragging = true;
                drag.position = farView.transform.position;
                drag.pointerCurrentRaycast = new RaycastResult { gameObject = farView.gameObject };
                ExecuteEvents.Execute(feiticoView.gameObject, drag, ExecuteEvents.dragHandler);
                yield return new WaitForSeconds(0.3f);
                Check(game.UI.BattleView.IsDragging, "A carta está sendo arrastada sobre o inimigo.");
                yield return Shot("05d_arrastando_carta");
                ExecuteEvents.Execute(feiticoView.gameObject, drag, ExecuteEvents.endDragHandler);
                Check(game.Battle.IsBusy && farEnemy.Hp == hpBefore, "Soltar a carta inicia a animação e o dano só entra no impacto.");
                yield return new WaitForSeconds(0.1f);
                yield return Shot("05e_ataque_animado");
                yield return WaitFor(() => !game.Battle.IsBusy, 2f);
                Check(farEnemy.Hp < hpBefore || farEnemy.IsDead, "O Feitiço arrastado acertou o inimigo escolhido.");
                Check(!battle.Deck.Hand.Any(c => c.Id == "feitico"), "A carta arrastada saiu da mão.");

                // Soltar de volta na mão cancela.
                var amuletoView = FindObjectsByType<CardView>().FirstOrDefault(c => c.Draggable && c.Card.Id == "amuleto");
                if (amuletoView != null)
                {
                    int energyAtDrag = battle.Energy;
                    var back = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        position = amuletoView.transform.position,
                    };
                    ExecuteEvents.Execute(amuletoView.gameObject, back, ExecuteEvents.beginDragHandler);
                    back.dragging = true;
                    back.position = new Vector2(Screen.width * 0.5f, 20f);
                    ExecuteEvents.Execute(amuletoView.gameObject, back, ExecuteEvents.dragHandler);
                    yield return null;
                    ExecuteEvents.Execute(amuletoView.gameObject, back, ExecuteEvents.endDragHandler);
                    yield return new WaitForSeconds(0.4f);
                    Check(battle.Energy == energyAtDrag && battle.Deck.Hand.Any(c => c.Id == "amuleto") && !game.UI.BattleView.IsDragging,
                        "Soltar a carta de volta na mão cancela a jogada.");
                }
            }

            // Turno dos inimigos com a animação de ataque deles.
            game.Battle.EndTurn();
            yield return new WaitForSeconds(0.55f);
            yield return Shot("05f_ataque_inimigo");
            yield return WaitFor(() => !game.Battle.IsBusy, 6f);
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
            yield return new WaitForSeconds(1f);
            Check(game.Music.PlayingExploration, "Depois da batalha volta a música de exploração.");

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

            // Baralho e baú de cartas
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

            // Relíquia da gruta: +1 de energia por turno.
            var relic = game.Map.All<ChestEntity>().First(c => c.Kind == ChestKind.Relic);
            int energyBefore = game.Player.EnergyPerTurn;
            game.Map.TeleportPlayer(relic.Pos);
            game.OpenChest(relic);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("11i_reliquia");
            Check(game.Player.EnergyPerTurn == energyBefore + 1, "A relíquia dá +1 de energia por turno.");
            Click("Botao0");
            yield return new WaitForSeconds(0.3f);

            // Pulo bloqueado: monstros muito mais fracos que o herói.
            var weak = game.Map.All<EncounterEntity>().First(e => e.Key == 'c');
            game.Map.TeleportPlayer(JumpSpot(game.Map, weak));
            int realLevel = game.Player.Level;
            game.Player.Level = 9;
            game.OfferEncounter(weak, false);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("11a_pulo_bloqueado_nivel");
            Click("Botao1");
            game.Player.Level = realLevel;
            yield return new WaitForSeconds(0.3f);

            // Ponte do açude guardada pelo cardume de piranhas.
            var piranhas = game.Map.All<EncounterEntity>().First(e => e.Key == 'e');
            game.Map.TeleportPlayer(JumpSpot(game.Map, piranhas));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("11j_ponte_do_acude");

            // Panela de cuscuz: come todas as porções e fica gordinho.
            var pot = game.Map.All<CuscuzPotEntity>().First();
            game.Map.TeleportPlayer(FreeNeighbor(game.Map, pot.Pos));
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
            game.Map.TeleportPlayer(JumpSpot(game.Map, group));
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
            game.Map.TeleportPlayer(FreeNeighbor(game.Map, saci.Pos));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("12_mapa_lajedo");
            game.ShowBossIntro(saci);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("13_chefe_intro");
            Click("Botao0");
            yield return WaitFor(() => game.State == GameState.Battle, 3f);
            if (!Check(game.State == GameState.Battle && game.Battle.Battle.Encounter.Leader.Id == "saci",
                    "A batalha contra o Saci abriu."))
            {
                Finish(game);
                yield break;
            }
            yield return new WaitForSeconds(1.4f);
            yield return Shot("14_chefe_batalha");

            yield return game.Battle.AutoPlay(3, 0.3f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("15_chefe_turnos");

            if (game.Battle.Battle != null)
            {
                var boss = game.Battle.Battle.Enemies.First(e => e.Data.IsBoss);
                boss.Hp = Mathf.Min(boss.Hp, 8);
                game.UI.BattleView.Refresh();
            }
            yield return game.Battle.AutoPlay(30, 0.3f);
            yield return WaitFor(() => game.State != GameState.Battle, 10f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("16_recompensa_chefe");
            Check(game.Player.BossDefeated && exit.Visible && game.Player.Deck.Any(c => c.Id == CardDatabase.BossCardId),
                "Derrotar o Saci abre a saída e entrega a carta Redemoinho.");
            Click("Continuar");
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 10 && FindCard() != null; i++)
            {
                FindCard().OnClicked(FindCard());
                yield return new WaitForSeconds(0.2f);
            }
            yield return Shot("17_saida_aberta");
            Click("Botao0");
            yield return new WaitForSeconds(0.4f);

            // Meta de pontos: sem pontos suficientes a saída não conclui a viagem.
            int realScore = game.Player.Score;
            game.Player.Score = 0;
            game.Map.TeleportPlayer(FreeNeighbor(game.Map, exit.Pos));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("18_saida");
            game.TryExit(exit);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("18a_pontos_insuficientes");
            Check(game.State == GameState.Modal, "Sem a meta de pontos a saída mostra o aviso e não encerra a partida.");
            Click("Botao0");
            yield return new WaitForSeconds(0.4f);

            // Saída / vitória
            game.Player.Score = Mathf.Max(realScore, GameConfig.MinScoreToWin);
            game.Map.TeleportPlayer(FreeNeighbor(game.Map, exit.Pos));
            yield return new WaitForSeconds(0.3f);
            game.TryExit(exit);
            yield return new WaitForSeconds(0.6f);
            Check(game.State == GameState.Ended, "Com a meta de pontos, a saída do lajedo encerra a partida com vitória.");
            yield return Shot("19_vitoria");

            // Da tela final de volta ao menu principal: a música do menu recomeça.
            Click("Menu");
            yield return new WaitForSeconds(1.2f);
            Check(game.State == GameState.Menu && game.Music.PlayingMenu && game.Music.CurrentTrackAudible,
                  "Voltando ao menu pela tela final, a música do menu toca de novo.");
            Finish(game);
        }

        /// <summary>Tile de onde dá para pular o encontro: chão livre antes e depois do monstro, na mesma linha.</summary>
        static Vector2Int JumpSpot(MapController map, EncounterEntity e)
        {
            foreach (var d in Dirs)
            {
                Vector2Int from = e.Pos - d, land = e.Pos + d;
                if (Free(map, from) && Free(map, land)) return from;
            }
            return FreeNeighbor(map, e.Pos);
        }

        static Vector2Int FreeNeighbor(MapController map, Vector2Int pos)
        {
            foreach (var d in Dirs)
                if (Free(map, pos + d)) return pos + d;
            return pos;
        }

        static bool Free(MapController map, Vector2Int t)
        {
            if (!map.IsWalkable(t)) return false;
            var e = map.EntityAt(t);
            return e == null || e.IsUsed;
        }

        bool Check(bool passed, string message)
        {
            if (!passed) errors++;
            log.AppendLine((passed ? "OK: " : "FALHOU: ") + message);
            return passed;
        }

        void Finish(GameManager game)
        {
            if (musicWasMuted) game.Music.Muted = true;
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
