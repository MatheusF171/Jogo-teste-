using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RotaDoCangaco.EditorTools
{
    /// <summary>
    /// Ferramentas do projeto (menu "Rota do Cangaço"): criar a cena, gerar o executável
    /// e simular partidas para conferir o balanceamento.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BuildPath = "Build/RotaDoCangaco.exe";

        // Abre a cena principal automaticamente na primeira vez que o projeto é aberto.
        static ProjectTools()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || SessionState.GetBool("RotaDoCangaco.SceneOpened", false)) return;
                SessionState.SetBool("RotaDoCangaco.SceneOpened", true);
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && File.Exists(ScenePath) && !active.isDirty)
                    EditorSceneManager.OpenScene(ScenePath);
            };
        }

        [MenuItem("Rota do Cangaço/Configurar projeto (cena e build)")]
        public static void Setup()
        {
            AssetDatabase.ImportAsset("Assets/RotaDoCangaco/Resources/Sprites",
                                      ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.1f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            new GameObject("Jogo").AddComponent<GameManager>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "A Rota do Cangaço";
            PlayerSettings.companyName = "Grupo Rota do Cangaço";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[RotaDoCangaco] Projeto configurado: cena " + ScenePath);
        }

        [MenuItem("Rota do Cangaço/Gerar executável Windows")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) Setup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[RotaDoCangaco] Build: {report.summary.result} ({report.summary.totalSize / (1024 * 1024)} MB) em {BuildPath}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------ Validação e simulação

        [MenuItem("Rota do Cangaço/Validar mapa e simular partidas")]
        public static void Simulate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== VALIDAÇÃO =====");
            sb.Append(ValidateContent());
            sb.AppendLine("===== SIMULAÇÃO (IA simples, escolhas de carta aleatórias) =====");
            sb.Append(RunScenario("Só o caminho obrigatório", new[] { "a", "c", "e", "f", "h", "i", "F", "j", "B" }, 300));
            sb.Append(RunScenario("Explorando tudo (2 cuscuz por panela)", new[] { "a", "b", "d", "C", "c", "F", "U2", "H", "l", "e", "f", "g", "R", "k", "C", "h", "i", "F", "U2", "j", "B" }, 300));
            sb.Append(RunScenario("Comilão (4 cuscuz por panela, logo antes do chefe)", new[] { "a", "b", "d", "C", "c", "F", "U4", "H", "l", "e", "f", "g", "R", "k", "C", "h", "i", "j", "F", "U4", "B" }, 300));
            Debug.Log("[RotaDoCangaco]\n" + sb);
            File.WriteAllText("Logs/simulacao.txt", sb.ToString());
        }

        static string ValidateContent()
        {
            var sb = new StringBuilder();
            int w = MapData.Width, h = MapData.Height;
            for (int y = 0; y < h; y++)
                if (MapData.Layout[y].Length != w) sb.AppendLine($"ERRO: linha {y} tem {MapData.Layout[y].Length} colunas (esperado {w})");

            Vector2Int start = Vector2Int.zero;
            var targets = new List<(char, Vector2Int)>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = MapData.Layout[y][x];
                if (c == 'P') start = new Vector2Int(x, y);
                else if ("#.,~=W_T".IndexOf(c) < 0) targets.Add((c, new Vector2Int(x, y)));
                if (char.IsLower(c) && !MapData.Encounters.ContainsKey(c)) sb.AppendLine($"ERRO: encontro '{c}' sem definição");
            }

            // BFS ignorando objetos (todos os objetos são pisáveis)
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || seen.Contains(n)) continue;
                    if (MapPainter.IsSolid(MapData.Layout[n.y][n.x])) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            foreach (var (c, pos) in targets)
                if (!seen.Contains(pos)) sb.AppendLine($"ERRO: objeto '{c}' em {pos} é inalcançável");

            var spriteKeys = new HashSet<string> { "heroi", "mula_retrato", "mula_carta", "mula_arte", "chefes_do_folclore" };
            for (int i = 1; i <= 8; i++) spriteKeys.Add("heroi_f" + i);
            foreach (var enc in MapData.Encounters.Values)
                foreach (var e in enc.Enemies) spriteKeys.Add(EnemyDatabase.Get(e.Key).SpriteKey);
            foreach (var key in spriteKeys)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/RotaDoCangaco/Resources/Sprites/{key}.png");
                if (sprite == null) sb.AppendLine($"ERRO: sprite '{key}' não encontrado ou não importado como Sprite");
            }
            sb.AppendLine($"Mapa {w}x{h}, {targets.Count} objetos, {seen.Count} tiles alcançáveis, {CardDatabase.All.Count()} cartas.");
            return sb.ToString();
        }

        static string RunScenario(string title, string[] route, int runs)
        {
            int wins = 0, totalLivesLost = 0, totalBossTurns = 0, bossFights = 0;
            float levelAtBoss = 0, hpAtBoss = 0, maxHpAtBoss = 0, deckAtBoss = 0, pesoAtBoss = 0;
            var defeatsAt = new Dictionary<string, int>();

            for (int run = 0; run < runs; run++)
            {
                var rng = new System.Random(1000 + run);
                var p = new PlayerData();
                bool dead = false;
                foreach (var step in route)
                {
                    if (dead) break;
                    if (step[0] == 'U')
                    {
                        int servings = step.Length > 1 ? step[1] - '0' : 2;
                        for (int i = 0; i < servings; i++)
                            CuscuzMenu.Eat(p, CuscuzMenu.Options[rng.Next(CuscuzMenu.Options.Count)]);
                        continue;
                    }
                    switch (step)
                    {
                        case "F": p.Heal(Mathf.CeilToInt(p.MaxHp * 0.5f)); continue;
                        case "H": p.MaxHp += 5; p.Heal(20); continue;
                        case "R": p.EnergyPerTurn++; continue;
                        case "C":
                            var rare = CardDatabase.RollChoices(rng, 3, true);
                            p.Deck.Add(rare[rng.Next(rare.Count)]);
                            continue;
                    }

                    var enc = MapData.Encounters[step[0]];
                    if (step == "B")
                    {
                        levelAtBoss += p.Level;
                        hpAtBoss += p.Hp;
                        maxHpAtBoss += p.MaxHp;
                        deckAtBoss += p.Deck.Count;
                        pesoAtBoss += p.Peso;
                    }
                    while (true)
                    {
                        bool won = RunBattle(p, enc, rng, out int turns);
                        if (step == "B") { totalBossTurns += turns; bossFights++; }
                        if (won) break;
                        p.Lives--;
                        totalLivesLost++;
                        defeatsAt[step] = defeatsAt.TryGetValue(step, out var v) ? v + 1 : 1;
                        if (p.Lives <= 0) { dead = true; break; }
                        p.Hp = p.MaxHp;
                    }
                    if (step == "B" && !dead) wins++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"--- {title} ({runs} partidas) ---");
            sb.AppendLine($"Vitórias contra o chefe: {wins * 100f / runs:0.0}%  ·  Vidas perdidas (média): {totalLivesLost / (float)runs:0.00}");
            sb.AppendLine($"Ao chegar no chefe: nível {levelAtBoss / runs:0.0}, vida {hpAtBoss / runs:0}/{maxHpAtBoss / runs:0}, baralho {deckAtBoss / runs:0.0} cartas, peso {pesoAtBoss / runs:0.0}");
            sb.AppendLine($"Turnos por luta com o chefe: {(bossFights > 0 ? totalBossTurns / (float)bossFights : 0):0.0}");
            sb.AppendLine("Derrotas por encontro: " + string.Join(", ", defeatsAt.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
            return sb.ToString();
        }

        static bool RunBattle(PlayerData p, EncounterData enc, System.Random rng, out int turns)
        {
            var battle = new BattleSystem(p, enc, new System.Random(rng.Next()));
            battle.Start();
            turns = 0;
            while (!battle.IsOver && turns < 60)
            {
                BattleAI.PlayTurn(battle);
                if (battle.IsOver) break;
                BattleAI.RunEnemyPhase(battle);
                turns++;
            }
            if (!battle.PlayerWon) return false;

            p.Hp = battle.Player.Hp;
            p.Score += battle.ScoreEarned;
            p.MonstersDefeated += battle.MonstersKilled;
            p.AddXp(battle.XpEarned);
            p.Digest(1);
            while (p.PendingLevelUps > 0)
            {
                var choices = CardDatabase.RollChoices(rng, 3, false);
                p.Deck.Add(choices[rng.Next(choices.Count)]);
                p.PendingLevelUps--;
            }
            return true;
        }
    }
}
