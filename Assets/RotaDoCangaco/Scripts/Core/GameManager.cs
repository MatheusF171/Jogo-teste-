using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RotaDoCangaco
{
    public enum GameState { Menu, Exploration, Battle, Modal, Ended }

    /// <summary>
    /// Ponto central do jogo: guarda o estado da partida e coordena mapa, batalhas e interface.
    /// Basta existir um GameObject com este componente na cena (veja Assets/Scenes/Main.unity).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Menu;
        public PlayerData Player { get; private set; }
        public System.Random Rng { get; private set; } = new System.Random();
        public MapController Map { get; private set; }
        public BattleController Battle { get; private set; }
        public UIManager UI { get; private set; }
        public MusicPlayer Music { get; private set; }

        readonly Queue<QuizQuestion> quizQueue = new Queue<QuizQuestion>();
        EncounterEntity activeEncounter;

        /// <summary>Se a cena aberta estiver vazia, cria o jogo sozinho ao apertar Play.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;
            var scene = SceneManager.GetActiveScene();
            bool emptyScene = scene.rootCount <= 3 && scene.GetRootGameObjects().All(g =>
                g.GetComponent<Camera>() != null || g.GetComponent<Light>() != null || g.transform.childCount == 0);
            if (emptyScene) new GameObject("Jogo").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            SetupCamera();
            Music = gameObject.AddComponent<MusicPlayer>(); // trilha sonora (depois da câmera, por causa do AudioListener)

            UI = gameObject.AddComponent<UIManager>();
            UI.Build(this);
            Map = new GameObject("Mapa").AddComponent<MapController>();
            Battle = gameObject.AddComponent<BattleController>();
            Battle.Init(this, UI.BattleView);
        }

        void Start()
        {
            ShowMenu();
            if (AutoTest.Requested) gameObject.AddComponent<AutoTest>();
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.06f, 0.035f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        void Update()
        {
            if (State == GameState.Exploration && !UI.OverlayOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) ShowPause();
                else if (Input.GetKeyDown(KeyCode.B)) ShowDeck();
            }
            else if (State == GameState.Modal && UI.OverlayOpen && Input.GetKeyDown(KeyCode.B) && deckOpen)
            {
                UI.CloseOverlay();
                deckOpen = false;
                ReturnToExploration();
            }
        }

        bool deckOpen;

        // ------------------------------------------------------------------ Fluxo geral

        public void ShowMenu()
        {
            State = GameState.Menu;
            Map.Clear();
            UI.ShowMenu();
            Music.PlayMenu();
        }

        public void StartNewRun()
        {
            Rng = new System.Random();
            Player = new PlayerData();
            quizQueue.Clear();
            foreach (var q in QuizDatabase.Questions.OrderBy(_ => Rng.Next())) quizQueue.Enqueue(q);

            Map.Build(this, SertaoMap.Phase);
            UI.Hud.Bind(Map);
            Music.PlayExploration(restart: true); // partida nova: exploração do início (na tela final, se já toca, segue tocando)
            ReturnToExploration();
            Toast("Atravesse a caatinga e enfrente o Saci na Pedra do Redemoinho! Pela ponte ou pela serra?", 4.5f);
        }

        public void ReturnToExploration()
        {
            State = GameState.Exploration;
            deckOpen = false;
            UI.ShowExploration();
        }

        void EnterModal() => State = GameState.Modal;

        public void Toast(string message, float duration = 2.5f) => UI.Hud.Toast(message, duration);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowPause()
        {
            EnterModal();
            string music = Music.Muted ? "Música: desligada" : "Música: ligada";
            UI.ShowModal("PAUSA", "A trilha espera por você.\n\n<size=20>Dica: jogue as cartas em ordem crescente de custo para fazer combos!</size>", null,
                (PauseLabel("Continuar"), ReturnToExploration),
                (PauseLabel(music), ToggleMusic),
                (PauseLabel("Menu principal"), ShowMenu),
                (PauseLabel("Sair do jogo"), Quit));
        }

        /// <summary>Com 4 botões na pausa a fonte fica um pouco menor para os textos caberem.</summary>
        static string PauseLabel(string text) => $"<size=24>{text}</size>";

        /// <summary>Liga/desliga a música e reabre a pausa para atualizar o texto do botão.</summary>
        void ToggleMusic()
        {
            Music.Muted = !Music.Muted;
            ShowPause();
        }

        public void ShowDeck()
        {
            if (State != GameState.Exploration) return;
            EnterModal();
            deckOpen = true;
            UI.ShowDeck(Player.Deck, ReturnToExploration);
        }

        // ------------------------------------------------------------------ Batalhas

        /// <summary>
        /// Antes de cada batalha: Lutar, Pular por cima (sem XP, o monstro fica) ou Recuar.
        /// O pulo é bloqueado contra chefes, contra grupos muito mais fracos e quando o herói está pesado.
        /// </summary>
        public void OfferEncounter(EncounterEntity encounter, bool caught)
        {
            if (encounter is BossEntity boss)
            {
                ShowBossIntro(boss);
                return;
            }
            EnterModal();

            string blocked = JumpBlockReason(encounter, out var landing);
            var buttons = new List<(string, System.Action)> { ("Lutar!", () => StartEncounter(encounter)) };
            if (blocked == null)
                buttons.Add(("Pular por cima", () =>
                {
                    Map.JumpOver(encounter, landing, () =>
                    {
                        ReturnToExploration();
                        Toast($"Você pulou por cima de \"{encounter.Data.Title}\"!");
                    });
                }));
            if (!caught) buttons.Add(("Recuar", ReturnToExploration));

            string jumpText = blocked == null
                ? "<color=#9CFF8A>Você pode pular por cima</color> (não ganha XP e o monstro continua no mapa)."
                : $"<color=#FF9A9A>Não dá para pular: {blocked}</color>";
            string group = encounter.Data.Enemies.Count > 1 ? "um grupo" : "um monstro";
            UI.ShowModal(caught ? "VOCÊ FOI ALCANÇADO!" : encounter.Data.Title.ToUpper(),
                $"No caminho há {group}: <b>{encounter.Data.Summary()}</b>.\n\n{jumpText}",
                SpriteLibrary.Get(encounter.Data.Leader.SpriteKey),
                buttons.ToArray());
        }

        string JumpBlockReason(EncounterEntity encounter, out Vector2Int landing)
        {
            landing = Vector2Int.zero;
            int gap = Player.Level - encounter.MaxLevel;
            if (gap >= GameConfig.JumpBlockLevelGap)
                return $"eles são fracos demais perto de você (Nv.{encounter.MaxLevel} contra o seu Nv.{Player.Level}). Enfrente-os!";
            if (!Player.CanJump)
                return $"você está pesado demais de tanto cuscuz ({Player.FatLabel})!";
            if (!Map.TryGetJumpLanding(encounter, out landing))
                return "não há espaço do outro lado para aterrissar.";
            return null;
        }

        public void StartEncounter(EncounterEntity encounter)
        {
            activeEncounter = encounter;
            State = GameState.Battle;
            Music.PlayBattle();
            UI.ShowBattle();
            Battle.Begin(encounter.Data, OnBattleFinished);
        }

        public void ShowBossIntro(BossEntity boss)
        {
            EnterModal();
            UI.ShowModal("O REI DO REDEMOINHO",
                "O vento levanta poeira no lajedo. Quando a poeira baixa, o Saci já está do outro lado.\n\n" +
                "<size=22><color=#FFB0C8>Ele joga poeira nos olhos, assobia chamando urubus, resiste ao Sono e ganha força com metade da vida. " +
                "Não dá para pular por cima de um chefe!</color></size>",
                SpriteLibrary.Get(boss.Data.Leader.SpriteKey),
                ("Enfrentar!", () => StartEncounter(boss)),
                ("Ainda não", ReturnToExploration));
        }

        void OnBattleFinished(BattleSystem result)
        {
            var encounter = activeEncounter;
            activeEncounter = null;
            Music.PlayExploration(); // vitória ou derrota: volta a trilha da exploração de onde parou

            if (!result.PlayerWon)
            {
                HandleDefeat();
                return;
            }

            Player.Hp = Mathf.Max(1, result.Player.Hp);
            Player.Score += result.ScoreEarned;
            Player.MonstersDefeated += result.MonstersKilled;
            Player.BattlesWon++;
            Player.AddXp(result.XpEarned);
            int pesoBefore = Player.Peso;
            Player.Digest(1);
            if (pesoBefore > 0) Toast($"A luta queimou calorias: peso {pesoBefore} » {Player.Peso} ({Player.FatLabel})", 3f);
            Map.RemoveEntity(encounter);
            UI.ShowExploration();
            EnterModal();

            if (encounter is BossEntity)
            {
                OnBossDefeated((BossEntity)encounter);
                return;
            }

            if (Player.PendingLevelUps == 0 && pesoBefore == 0) Toast($"Vitória! +{result.XpEarned} XP  ·  +{result.ScoreEarned} pontos");
            ProcessLevelUps(ReturnToExploration);
        }

        void HandleDefeat()
        {
            Player.Lives--;
            if (Player.Lives <= 0)
            {
                GameOver();
                return;
            }
            Player.Hp = Player.MaxHp;
            if (Map.EntityAt(Map.PlayerPos) is EncounterEntity) Map.RetreatPlayer();
            UI.ShowExploration();
            EnterModal();
            UI.ShowModal("VOCÊ DESMAIOU...",
                $"Uma benzedeira encontrou você caído na trilha e cuidou dos seus ferimentos.\n\n" +
                $"<b>-1 vida.</b> Restam <b>{Player.Lives}</b>. Sua vida foi restaurada, mas os monstros continuam lá.",
                HeroAnimator.Frame(6),
                ("Continuar", ReturnToExploration));
        }

        void OnBossDefeated(BossEntity boss)
        {
            Player.BossDefeated = true;
            var card = CardDatabase.Get(CardDatabase.BossCardId);
            Player.Deck.Add(card);
            Player.Abilities.Add(card.Name);
            Map.RevealExit();
            UI.Hud.Refresh();

            UI.ShowBossReward(card, boss.Data.Leader.Name, boss.Data.Leader.SpriteKey, () => ProcessLevelUps(() =>
            {
                EnterModal();
                UI.ShowModal("A SAÍDA SE ABRIU!",
                    $"O vento baixou. Siga até a saída dourada do lajedo com {GameConfig.MinScoreToWin} pontos para concluir a viagem.",
                    TextureFactory.MapObject("portal"),
                    ("Vamos lá!", ReturnToExploration));
            }));
        }

        /// <summary>Uma escolha de carta (1 entre 3) para cada nível ganho.</summary>
        void ProcessLevelUps(System.Action done)
        {
            if (Player.PendingLevelUps <= 0)
            {
                done?.Invoke();
                return;
            }
            EnterModal();
            int shownLevel = Player.Level - Player.PendingLevelUps + 1;
            var choices = CardDatabase.RollChoices(Rng, 3, false);
            UI.ShowCardChoice($"NÍVEL {shownLevel}!",
                $"+{GameConfig.HpPerLevel} de vida máxima. Escolha 1 carta para adicionar ao baralho:",
                choices,
                card =>
                {
                    if (card != null) Player.Deck.Add(card);
                    Player.PendingLevelUps--;
                    ProcessLevelUps(done);
                },
                true);
        }

        // ------------------------------------------------------------------ Objetos do mapa

        public void OpenChest(ChestEntity chest)
        {
            if (chest.Opened) return;
            chest.Opened = true;
            Player.Score += GameConfig.ChestScore;
            Map.RefreshEntityView(chest);
            EnterModal();

            switch (chest.Kind)
            {
                case ChestKind.Card:
                    UI.ShowCardChoice("BAÚ DE CARTAS", "Escolha 1 carta rara para o seu baralho:",
                        CardDatabase.RollChoices(Rng, 3, true),
                        card =>
                        {
                            if (card != null) Player.Deck.Add(card);
                            ReturnToExploration();
                        }, true);
                    break;

                case ChestKind.Relic:
                    Player.EnergyPerTurn++;
                    Player.Relics.Add("Rapadura Encantada");
                    UI.ShowModal("RELÍQUIA: RAPADURA ENCANTADA",
                        "Um tijolinho de rapadura que nunca acaba, esquecido por um tropeiro na gruta.\n\n<b>+1 de energia em todos os turnos!</b>",
                        TextureFactory.MapObject("chest_relic"),
                        ("Que delícia!", ReturnToExploration));
                    break;

                default:
                    Player.MaxHp += 5;
                    int healed = Player.Heal(20);
                    UI.ShowModal("GARRAFADA DA BENZEDEIRA",
                        $"Raízes e ervas curtidas numa garrafa. Amargo, mas funciona!\n\n<b>+5 de vida máxima</b> e <b>+{healed} de vida</b>.",
                        TextureFactory.MapObject("chest_heal"),
                        ("Continuar", ReturnToExploration));
                    break;
            }
        }

        public void UseCampfire(CampfireEntity fire)
        {
            if (fire.Used) return;
            EnterModal();
            int amount = Mathf.CeilToInt(Player.MaxHp * 0.5f);
            UI.ShowModal("FOGUEIRA",
                $"Descansar na fogueira recupera <b>{amount} de vida</b> (só pode ser usada uma vez).\n\n" +
                $"Vida atual: {Player.Hp}/{Player.MaxHp}\n<size=20><color=#FFE08A>Ao lado tem uma panela de cuscuz quentinho...</color></size>",
                TextureFactory.MapObject("campfire"),
                ("Descansar", () =>
                {
                    fire.Used = true;
                    int healed = Player.Heal(amount);
                    Map.RefreshEntityView(fire);
                    ReturnToExploration();
                    Toast($"Você descansou: +{healed} de vida.");
                }),
                ("Agora não", ReturnToExploration));
        }

        /// <summary>Panela de cuscuz: escolha uma porção (bônus permanente) e decida se continua comendo.</summary>
        public void OpenCuscuz(CuscuzPotEntity pot, string feedback = null)
        {
            if (pot.Servings <= 0 && feedback == null) return;
            EnterModal();
            UI.ShowCuscuz(Player, pot, feedback,
                option =>
                {
                    int stageBefore = Player.FatStage;
                    CuscuzMenu.Eat(Player, option);
                    pot.Servings--;
                    Map.RefreshEntityView(pot);
                    string msg = $"Hmm, que delícia! {option.Description}";
                    if (Player.FatStage > stageBefore)
                        msg += $"\n<color=#FFA24D>Ufa... você ficou {Player.FatLabel.ToLower()}: {Player.FatPenaltyText}.</color>";
                    OpenCuscuz(pot, msg);
                },
                ReturnToExploration);
        }

        public void OpenQuiz(QuizEntity quiz)
        {
            if (quiz.Read) return;
            if (quizQueue.Count == 0) foreach (var q in QuizDatabase.Questions) quizQueue.Enqueue(q);
            var question = quizQueue.Dequeue();
            EnterModal();
            UI.ShowQuiz(question, correct =>
            {
                quiz.Read = true;
                Player.QuizAnswered++;
                if (correct)
                {
                    Player.QuizCorrect++;
                    Player.Score += GameConfig.QuizScore;
                    Player.Heal(5);
                }
                Map.RefreshEntityView(quiz);
                ReturnToExploration();
            });
        }

        public void TryExit(ExitEntity exit)
        {
            if (State != GameState.Exploration || !exit.Active || !Player.BossDefeated) return;
            if (Player.Score < GameConfig.MinScoreToWin)
            {
                EnterModal();
                UI.ShowModal("PONTUAÇÃO INSUFICIENTE",
                    $"Você precisa de {GameConfig.MinScoreToWin} pontos para concluir a viagem. Você tem {Player.Score}.\n\n" +
                    "<size=22>Responda cordéis, abra baús e vença os monstros que restam no mapa.</size>",
                    null, ("OK", () =>
                    {
                        Map.RetreatPlayer();
                        ReturnToExploration();
                    }));
                return;
            }
            State = GameState.Ended;
            UI.ShowEnd(true, Stats(), StartNewRun, ShowMenu);
        }

        void GameOver()
        {
            State = GameState.Ended;
            UI.ShowEnd(false, Stats(), StartNewRun, ShowMenu);
        }

        string Stats()
        {
            return $"Pontuação final: <b><color=#FFC845>{Player.Score}</color></b>\n" +
                   $"Nível alcançado: <b>{Player.Level}</b>\n" +
                   $"Monstros derrotados: <b>{Player.MonstersDefeated}</b>\n" +
                   $"Cordéis acertados: <b>{Player.QuizCorrect}/{Player.QuizAnswered}</b>\n" +
                   $"Vidas restantes: <b>{Mathf.Max(0, Player.Lives)}</b>\n" +
                   $"Cartas no baralho: <b>{Player.Deck.Count}</b>\n" +
                   $"Porções de cuscuz: <b>{Player.CuscuzEaten}</b>\n" +
                   (Player.Abilities.Count > 0 ? $"Habilidades: <b><color=#FF7AA8>{string.Join(", ", Player.Abilities)}</color></b>" : "");
        }
    }
}
