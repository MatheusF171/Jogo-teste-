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
            ApplyIcon();
            ApplySizeSettings();
            // Em modo batch dá para escolher a pasta: -buildPath "C:\pasta\RotaDoCangaco.exe"
            string path = CommandLineValue("-buildPath") ?? BuildPath;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded) RemoveOptionalFiles(path);
            Debug.Log($"[RotaDoCangaco] Build: {report.summary.result} ({report.summary.totalSize / (1024 * 1024)} MB) em {path}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        const string IconPath = "Assets/RotaDoCangaco/Arte/icone.png";

        /// <summary>Ícone do executável (o herói no pôr do sol). Importado sem compressão para não borrar.</summary>
        static void ApplyIcon()
        {
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer &&
                (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled ||
                 importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[RotaDoCangaco] Ícone não encontrado em {IconPath}; o executável fica com o ícone padrão.");
                return;
            }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        /// <summary>
        /// Deixa o executável menor: remove do build o código .NET que o jogo não usa (stripping alto)
        /// e usa só o Direct3D 11 (dispensa a pasta D3D12 de ~4,5 MB; o jogo é 2D e não precisa do 12).
        /// </summary>
        static void ApplySizeSettings()
        {
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        }

        /// <summary>
        /// Tira do build o que o jogo não precisa para rodar (testado com o teste automático completo):
        /// - pasta D3D12 (Agility SDK, ~4,5 MB): só é carregada com Direct3D 12, e o build usa só o 11;
        /// - UnityCrashHandler64.exe (~1,7 MB): só grava relatórios quando o jogo trava; sem ele o jogo roda igual.
        /// As DLLs dstorage ficam: o UnityPlayer.dll não abre sem elas.
        /// </summary>
        static void RemoveOptionalFiles(string exePath)
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(exePath));
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            string d3d12 = Path.Combine(root, "D3D12");
            if (System.Array.IndexOf(apis, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12) < 0 && Directory.Exists(d3d12))
                Directory.Delete(d3d12, true);
            string crashHandler = Path.Combine(root, "UnityCrashHandler64.exe");
            if (File.Exists(crashHandler)) File.Delete(crashHandler);
        }

        static string CommandLineValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // ------------------------------------------------------------------ Validação e simulação

        [MenuItem("Rota do Cangaço/Validar mapa e simular partidas")]
        public static void Simulate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== VALIDAÇÃO =====");
            sb.Append(ValidateContent(SertaoMap.Phase));
            sb.AppendLine("===== SIMULAÇÃO (IA simples, escolhas de carta aleatórias) =====");
            sb.Append(RunScenario("Só o caminho obrigatório (ponte do açude)", new[] { "a", "c", "F", "e", "i", "F", "j", "B" }, 300));
            sb.Append(RunScenario("Rota da serra (sem a ponte)", new[] { "a", "f", "F", "b", "C", "g", "R", "h", "k", "H", "F", "j", "B" }, 300));
            sb.Append(RunScenario("Explorando tudo (2 cuscuz por panela)", new[] { "a", "c", "d", "l", "C", "F", "U2", "f", "b", "C", "g", "R", "h", "k", "H", "i", "e", "F", "U2", "j", "B" }, 300));
            sb.Append(RunScenario("Comilão (4 cuscuz por panela, logo antes do chefe)", new[] { "a", "c", "d", "l", "C", "F", "U4", "f", "b", "C", "g", "R", "h", "k", "H", "i", "e", "j", "F", "U4", "B" }, 300));
            Debug.Log("[RotaDoCangaco]\n" + sb);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/simulacao.txt", sb.ToString());
        }

        static string ValidateContent(MapPhase phase)
        {
            var sb = new StringBuilder();
            int w = phase.Width, h = phase.Height;
            sb.AppendLine(phase.Title);
            for (int y = 0; y < h; y++)
                if (phase.Layout[y].Length != w)
                    return $"ERRO: linha {y} tem {phase.Layout[y].Length} colunas (esperado {w})\n";

            Vector2Int start = Vector2Int.zero;
            var targets = new List<(char, Vector2Int)>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = phase.Layout[y][x];
                if (c == 'P') start = new Vector2Int(x, y);
                else if (MapData.TerrainTiles.IndexOf(c) < 0) targets.Add((c, new Vector2Int(x, y)));
                if ((c == 'B' || char.IsLower(c)) && !phase.Encounters.ContainsKey(c)) sb.AppendLine($"ERRO: encontro '{c}' sem definição");
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
                    if (MapPainter.IsSolid(phase.Layout[n.y][n.x])) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            foreach (var (c, pos) in targets)
                if (!seen.Contains(pos)) sb.AppendLine($"ERRO: objeto '{c}' em {pos} é inalcançável");

            var spriteKeys = new HashSet<string> { "heroi", "menu_fundo", "logo_titulo" };
            for (int i = 1; i <= 8; i++) spriteKeys.Add("heroi_f" + i);
            foreach (var enc in phase.Encounters.Values)
                foreach (var e in enc.Enemies) spriteKeys.Add(EnemyDatabase.Get(e.Key).SpriteKey);
            foreach (var key in spriteKeys)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/RotaDoCangaco/Resources/Sprites/{key}.png");
                if (sprite == null) sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/RotaDoCangaco/Resources/Sprites/{key}.jpg");
                bool asBytes = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/RotaDoCangaco/Resources/Sprites/{key}.bytes") != null;
                if (sprite == null && !asBytes) sb.AppendLine($"ERRO: sprite '{key}' não encontrado ou não importado como Sprite");
            }
            sb.AppendLine($"Mapa {w}x{h}, {targets.Count} objetos, {seen.Count} tiles alcançáveis, {CardDatabase.All.Count()} cartas.");
            return sb.ToString();
        }

        static string RunScenario(string title, string[] route, int runs)
        {
            int wins = 0, totalLivesLost = 0, totalBossTurns = 0, bossFights = 0, scoreAtWin = 0, belowGoal = 0;
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
                        case "H": p.MaxHp += 5; p.Heal(20); p.Score += GameConfig.ChestScore; continue;
                        case "R": p.EnergyPerTurn++; p.Score += GameConfig.ChestScore; continue;
                        case "C":
                            var rare = CardDatabase.RollChoices(rng, 3, true);
                            p.Deck.Add(rare[rng.Next(rare.Count)]);
                            p.Score += GameConfig.ChestScore;
                            continue;
                    }

                    var enc = SertaoMap.Encounters[step[0]];
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
                    if (step == "B" && !dead)
                    {
                        wins++;
                        scoreAtWin += p.Score;
                        if (p.Score < GameConfig.MinScoreToWin) belowGoal++;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"--- {title} ({runs} partidas) ---");
            sb.AppendLine($"Vitórias contra o chefe: {wins * 100f / runs:0.0}%  ·  Vidas perdidas (média): {totalLivesLost / (float)runs:0.00}");
            sb.AppendLine($"Ao chegar no chefe: nível {levelAtBoss / runs:0.0}, vida {hpAtBoss / runs:0}/{maxHpAtBoss / runs:0}, baralho {deckAtBoss / runs:0.0} cartas, peso {pesoAtBoss / runs:0.0}");
            sb.AppendLine($"Turnos por luta com o chefe: {(bossFights > 0 ? totalBossTurns / (float)bossFights : 0):0.0}");
            sb.AppendLine($"Pontos ao vencer o chefe (sem cordéis): {(wins > 0 ? scoreAtWin / wins : 0)}  ·  abaixo da meta de {GameConfig.MinScoreToWin}: {belowGoal}");
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
