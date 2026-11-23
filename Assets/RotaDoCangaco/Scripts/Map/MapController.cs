using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RotaDoCangaco
{
    /// <summary>
    /// Exploração em grade vista de cima, no estilo dungeon crawler: o herói anda tile a tile
    /// (teclado ou clique), cada passo é um "turno" do mapa e alguns monstros rondam e perseguem.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector2Int PlayerPos { get; private set; }
        public Vector2Int PreviousPos { get; private set; }
        public Texture2D MapTexture { get; private set; }
        public MapPhase Phase { get; private set; }
        public bool Overview { get; private set; }
        public readonly List<MapEntity> Entities = new List<MapEntity>();

        /// <summary>Disparado quando algo muda no mapa (para atualizar o minimapa).</summary>
        public event Action Changed;

        const float ExploreZoom = 5.5f;

        GameManager game;
        char[,] terrain;
        Transform worldRoot;
        Sprite mapSprite;
        Transform player;
        SpriteRenderer playerSprite;
        HeroAnimator heroAnim;
        Transform playerLight;
        Transform playerShadow;
        float playerShadowBase = 1f;
        Camera cam;
        bool moving;
        bool jumping;
        int stepCount;
        readonly Queue<Vector2Int> path = new Queue<Vector2Int>();
        readonly List<Transform> fireflies = new List<Transform>();
        readonly List<Vector3> fireflyBase = new List<Vector3>();

        public bool IsBuilt => terrain != null;

        public Vector3 TileToWorld(Vector2Int t) => new Vector3(t.x + 0.5f, Height - 1 - t.y + 0.5f, 0f);

        public Vector2Int WorldToTile(Vector3 w) => new Vector2Int(Mathf.FloorToInt(w.x), Height - 1 - Mathf.FloorToInt(w.y));

        public bool InBounds(Vector2Int t) => t.x >= 0 && t.y >= 0 && t.x < Width && t.y < Height;

        public bool IsWalkable(Vector2Int t) => InBounds(t) && !MapPainter.IsSolid(terrain[t.x, t.y]);

        public MapEntity EntityAt(Vector2Int t) => Entities.FirstOrDefault(e => !e.Removed && e.Visible && e.Pos == t);

        public IEnumerable<T> All<T>() where T : MapEntity => Entities.OfType<T>().Where(e => !e.Removed);

        // ------------------------------------------------------------------ Construção

        public void Build(GameManager gameManager, MapPhase phase)
        {
            game = gameManager;
            Clear();
            cam = Camera.main;

            Phase = phase;
            Width = phase.Width;
            Height = phase.Height;
            terrain = new char[Width, Height];
            var entityChars = new List<(char ch, Vector2Int pos)>();

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                char ch = phase.Layout[y][x];
                if (MapData.TerrainTiles.IndexOf(ch) >= 0) terrain[x, y] = ch;
                else
                {
                    terrain[x, y] = '?';
                    entityChars.Add((ch, new Vector2Int(x, y)));
                }
            }
            foreach (var (_, pos) in entityChars) terrain[pos.x, pos.y] = GroundUnder(pos);

            worldRoot = new GameObject("Mundo").transform;
            worldRoot.SetParent(transform, false);

            MapTexture = phase == SertaoMap.Phase
                ? MapPainter.PaintSertao(terrain, 8721, phase.Layout)
                : MapPainter.Paint(terrain, 12345, layout: phase.Layout);
            var mapGo = new GameObject("Terreno");
            mapGo.transform.SetParent(worldRoot, false);
            var mapSr = mapGo.AddComponent<SpriteRenderer>();
            mapSprite = Sprite.Create(MapTexture, new Rect(0, 0, MapTexture.width, MapTexture.height), Vector2.zero, MapPainter.TilePx);
            mapSr.sprite = mapSprite;
            mapSr.sortingOrder = -1000;

            foreach (var (ch, pos) in entityChars) CreateEntity(ch, pos);
            foreach (var e in Entities) CreateView(e);
            CreatePlayer();
            CreateFireflies();

            Overview = false;
            SnapCamera();
            Changed?.Invoke();
        }

        public void Clear()
        {
            StopAllCoroutines();
            moving = false;
            jumping = false;
            stepCount = 0;
            path.Clear();
            Entities.Clear();
            fireflies.Clear();
            fireflyBase.Clear();
            if (worldRoot != null)
            {
                worldRoot.gameObject.SetActive(false);
                Destroy(worldRoot.gameObject);
            }
            if (mapSprite != null) Destroy(mapSprite);
            if (MapTexture != null) Destroy(MapTexture);
            worldRoot = null;
            terrain = null;
        }

        char GroundUnder(Vector2Int pos)
        {
            Vector2Int[] dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
            foreach (var d in dirs)
            {
                var n = pos + d;
                if (!InBounds(n)) continue;
                char t = terrain[n.x, n.y];
                if (t == '.' || t == ',' || t == '=' || t == '_') return t;
            }
            return '.';
        }

        void CreateEntity(char ch, Vector2Int pos)
        {
            switch (ch)
            {
                case 'P': PlayerPos = PreviousPos = pos; break;
                case 'B': Entities.Add(new BossEntity(ch, Phase.Encounters[ch], pos)); break;
                case 'X': Entities.Add(new ExitEntity(pos, Phase == SertaoMap.Phase ? "Saída do lajedo" : "Porteira para o Sertão")); break;
                case 'C': Entities.Add(new ChestEntity(ChestKind.Card, pos)); break;
                case 'R': Entities.Add(new ChestEntity(ChestKind.Relic, pos)); break;
                case 'H': Entities.Add(new ChestEntity(ChestKind.Heal, pos)); break;
                case 'Q': Entities.Add(new QuizEntity(pos)); break;
                case 'F': Entities.Add(new CampfireEntity(pos)); break;
                case 'U': Entities.Add(new CuscuzPotEntity(pos)); break;
                default:
                    if (Phase.Encounters.TryGetValue(ch, out var data)) Entities.Add(new EncounterEntity(ch, data, pos));
                    else Debug.LogWarning($"[RotaDoCangaco] Caractere desconhecido no mapa: '{ch}' em {pos}");
                    break;
            }
        }

        static void Fit(SpriteRenderer sr, float units)
        {
            var b = sr.sprite.bounds.size;
            float s = units / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        void CreateView(MapEntity e)
        {
            var go = new GameObject(e.GetType().Name);
            go.transform.SetParent(worldRoot, false);
            go.transform.position = TileToWorld(e.Pos);
            e.View = go;

            if (e.GlowColor.a > 0f)
            {
                var glow = new GameObject("Brilho").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(go.transform, false);
                glow.sprite = TextureFactory.Glow;
                glow.color = e.GlowColor;
                glow.sortingOrder = -500;
                Fit(glow, e.SpriteSize * 2.2f);
            }

            var sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(go.transform, false);
            sprite.sprite = e.Sprite;
            sprite.sortingOrder = SortOrder(e.Pos.y);
            Fit(sprite, e.SpriteSize);
            e.Renderer = sprite;

            // Grupos mostram os outros membros atrás do líder.
            if (e is EncounterEntity enc && !(e is BossEntity) && enc.Data.Enemies.Count > 1)
            {
                for (int i = 1; i < enc.Data.Enemies.Count; i++)
                {
                    var extra = new GameObject("Membro").AddComponent<SpriteRenderer>();
                    extra.transform.SetParent(go.transform, false);
                    extra.sprite = SpriteLibrary.Get(EnemyDatabase.Get(enc.Data.Enemies[i].Key).SpriteKey);
                    extra.sortingOrder = SortOrder(e.Pos.y) - 1;
                    Fit(extra, 0.62f);
                    extra.transform.localPosition = new Vector3(i % 2 == 1 ? 0.32f : -0.32f, 0.22f + 0.08f * i, 0f);
                    extra.color = new Color(0.85f, 0.85f, 0.85f);
                }
                sprite.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            }

            go.SetActive(e.Visible);
        }

        public void RefreshEntityView(MapEntity e)
        {
            if (e.View == null) return;
            e.View.SetActive(e.Visible && !e.Removed);
            if (e.Renderer != null) e.Renderer.sprite = e.Sprite;
            var glow = e.View.transform.Find("Brilho");
            if (glow != null) glow.gameObject.SetActive(e.GlowColor.a > 0f);
            Changed?.Invoke();
        }

        public void RemoveEntity(MapEntity e)
        {
            if (e == null) return;
            e.Removed = true;
            if (e.View != null) StartCoroutine(FadeAndDestroy(e.View));
            Changed?.Invoke();
        }

        IEnumerator FadeAndDestroy(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<SpriteRenderer>();
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                foreach (var r in renderers)
                {
                    var c = r.color;
                    c.a = 1f - t / 0.5f;
                    r.color = c;
                }
                go.transform.localScale = Vector3.one * (1f + t);
                yield return null;
            }
            Destroy(go);
        }

        public void RevealExit()
        {
            foreach (var exit in All<ExitEntity>())
            {
                exit.Active = true;
                RefreshEntityView(exit);
            }
        }

        static int SortOrder(int tileY) => tileY * 10;

        void CreatePlayer()
        {
            var go = new GameObject("Heroi");
            go.transform.SetParent(worldRoot, false);
            go.transform.position = TileToWorld(PlayerPos);
            player = go.transform;

            var light = new GameObject("Luz").AddComponent<SpriteRenderer>();
            light.transform.SetParent(player, false);
            light.sprite = TextureFactory.Glow;
            light.color = new Color(1f, 0.9f, 0.65f, 0.22f);
            light.sortingOrder = -600;
            Fit(light, 7f);
            playerLight = light.transform;

            var shadow = new GameObject("Sombra").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(player, false);
            shadow.sprite = TextureFactory.Shadow;
            shadow.sortingOrder = -400;
            Fit(shadow, 0.8f);
            shadow.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            playerShadow = shadow.transform;
            playerShadowBase = shadow.transform.localScale.x;

            playerSprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            playerSprite.transform.SetParent(player, false);
            playerSprite.sprite = HeroAnimator.Frame(3);
            playerSprite.sortingOrder = SortOrder(PlayerPos.y) + 5;
            Fit(playerSprite, 1f);
            heroAnim = HeroAnimator.Attach(playerSprite.gameObject);
        }

        void CreateFireflies()
        {
            var rng = new System.Random(99);
            var open = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (terrain[x, y] == ',' || terrain[x, y] == '~') open.Add(new Vector2Int(x, y));
            for (int i = 0; i < 40 && open.Count > 0; i++)
            {
                var tile = open[rng.Next(open.Count)];
                var sr = new GameObject("Vagalume").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(worldRoot, false);
                sr.sprite = TextureFactory.Glow;
                sr.color = new Color(0.85f, 1f, 0.45f, 0.8f);
                sr.sortingOrder = 2000;
                Fit(sr, 0.22f);
                var pos = TileToWorld(tile) + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, 0f);
                sr.transform.position = pos;
                fireflies.Add(sr.transform);
                fireflyBase.Add(pos);
            }
        }

        // ------------------------------------------------------------------ Loop

        void Update()
        {
            if (!IsBuilt) return;
            Animate();
            UpdateCamera(false);
            if (game == null || game.State != GameState.Exploration)
            {
                path.Clear();
                return;
            }

            UpdateHover();
            if (Input.GetKeyDown(KeyCode.M)) ToggleOverview();
            if (moving) return;

            var dir = ReadDirection();
            if (dir != Vector2Int.zero)
            {
                path.Clear();
                TryStep(dir);
                return;
            }

            if (Input.GetMouseButtonDown(0) && !PointerOverUI()) ClickToMove();
            if (path.Count > 0) TryStep(path.Dequeue() - PlayerPos);
        }

        static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        static Vector2Int ReadDirection()
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return new Vector2Int(0, -1);
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return new Vector2Int(0, 1);
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return new Vector2Int(-1, 0);
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return new Vector2Int(1, 0);
            return Vector2Int.zero;
        }

        void Animate()
        {
            float t = Time.time;
            foreach (var e in Entities)
            {
                if (e.Removed || e.Renderer == null) continue;
                if (e is EncounterEntity)
                {
                    float phase = e.Pos.x * 0.7f + e.Pos.y * 1.3f;
                    var lp = e.Renderer.transform.localPosition;
                    e.Renderer.transform.localPosition = new Vector3(lp.x, (e is BossEntity ? 0.1f : -0.08f) + Mathf.Sin(t * 3f + phase) * 0.05f, 0f);
                }
                if (e is BossEntity || e is ExitEntity || e is CampfireEntity)
                {
                    var glow = e.View.transform.Find("Brilho");
                    if (glow != null)
                    {
                        float s = 1f + Mathf.Sin(t * 2.5f) * 0.08f;
                        glow.localScale = Vector3.one * (e.SpriteSize * 2.2f / 1.28f) * s;
                    }
                }
            }

            if (playerSprite != null && !jumping)
                playerSprite.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * (moving ? 14f : 3f))) * (moving ? 0.08f : 0.03f), 0f);
            if (heroAnim != null)
            {
                heroAnim.SetWalking(moving && !jumping);
                heroAnim.SetFatStage(game != null && game.Player != null ? game.Player.FatStage : 0);
            }

            for (int i = 0; i < fireflies.Count; i++)
            {
                var b = fireflyBase[i];
                fireflies[i].position = b + new Vector3(Mathf.Sin(t * 0.7f + i) * 0.6f, Mathf.Cos(t * 0.9f + i * 1.7f) * 0.4f, 0f);
                var sr = fireflies[i].GetComponent<SpriteRenderer>();
                var c = sr.color;
                c.a = 0.35f + 0.45f * Mathf.Max(0f, Mathf.Sin(t * 2f + i * 2.3f));
                sr.color = c;
            }
        }

        // ------------------------------------------------------------------ Movimento

        void TryStep(Vector2Int dir)
        {
            var target = PlayerPos + dir;
            if (dir.x != 0) playerSprite.flipX = dir.x < 0;
            if (!IsWalkable(target))
            {
                path.Clear();
                return;
            }
            // Antes de entrar na batalha o herói pode escolher: lutar, pular por cima ou recuar.
            if (EntityAt(target) is EncounterEntity encounter)
            {
                path.Clear();
                game.OfferEncounter(encounter, false);
                return;
            }
            StartCoroutine(StepTo(target));
        }

        IEnumerator StepTo(Vector2Int target)
        {
            moving = true;
            var from = TileToWorld(PlayerPos);
            var to = TileToWorld(target);
            PreviousPos = PlayerPos;
            PlayerPos = target;
            playerSprite.sortingOrder = SortOrder(target.y) + 5;

            for (float t = 0f; t < GameConfig.StepDuration; t += Time.deltaTime)
            {
                player.position = Vector3.Lerp(from, to, t / GameConfig.StepDuration);
                yield return null;
            }
            player.position = to;
            stepCount++;
            Changed?.Invoke();

            var entity = EntityAt(PlayerPos);
            if (entity != null && !entity.IsUsed)
            {
                path.Clear();
                moving = false;
                entity.Interact(game);
                yield break;
            }

            if (stepCount % 2 == 0) yield return MoveRoamers();
            moving = false;
        }

        IEnumerator MoveRoamers()
        {
            foreach (var enc in All<EncounterEntity>().Where(e => e.Data.Roams).ToList())
            {
                if (enc.StunSteps > 0)
                {
                    enc.StunSteps = Mathf.Max(0, enc.StunSteps - 2);
                    continue;
                }
                int dist = Manhattan(enc.Pos, PlayerPos);
                if (dist > GameConfig.RoamAggroRadius) continue;
                if (Manhattan(enc.Origin, PlayerPos) > GameConfig.RoamLeashRadius + GameConfig.RoamAggroRadius) continue;

                // Já está colado no herói: o encontro acontece (com a opção de pular).
                if (dist == 1)
                {
                    path.Clear();
                    game.Toast($"{enc.Data.Title} alcançou você!");
                    game.OfferEncounter(enc, true);
                    yield break;
                }

                var route = FindPath(enc.Pos, PlayerPos, t => t == PlayerPos || EntityAt(t) == null);
                if (route == null || route.Count < 2) continue;
                var next = route[0];
                if (Manhattan(next, enc.Origin) > GameConfig.RoamLeashRadius) continue;

                var from = TileToWorld(enc.Pos);
                var to = TileToWorld(next);
                enc.Pos = next;
                for (float t = 0f; t < 0.12f; t += Time.deltaTime)
                {
                    enc.View.transform.position = Vector3.Lerp(from, to, t / 0.12f);
                    yield return null;
                }
                enc.View.transform.position = to;
                enc.Renderer.sortingOrder = SortOrder(next.y);
                Changed?.Invoke();

                if (Manhattan(enc.Pos, PlayerPos) == 1)
                {
                    path.Clear();
                    game.Toast($"{enc.Data.Title} alcançou você!");
                    game.OfferEncounter(enc, true);
                    yield break;
                }
            }
        }

        // ------------------------------------------------------------------ Pulo sobre monstros

        /// <summary>O tile do outro lado do monstro, se for possível aterrissar nele.</summary>
        public bool TryGetJumpLanding(EncounterEntity enc, out Vector2Int landing)
        {
            var dir = enc.Pos - PlayerPos;
            landing = enc.Pos + dir;
            if (Manhattan(enc.Pos, PlayerPos) != 1 || !IsWalkable(landing)) return false;
            var other = EntityAt(landing);
            return other == null || other.IsUsed;
        }

        /// <summary>
        /// Animação do pulo: agacha (quadro 2), voa em arco por cima do monstro (quadro 1) e aterrissa (quadro 2).
        /// </summary>
        public void JumpOver(EncounterEntity enc, Vector2Int landing, Action onDone)
            => StartCoroutine(JumpRoutine(enc, landing, onDone));

        IEnumerator JumpRoutine(EncounterEntity enc, Vector2Int landing, Action onDone)
        {
            moving = true;
            jumping = true;
            path.Clear();
            var from = TileToWorld(PlayerPos);
            var to = TileToWorld(landing);
            if (to.x != from.x) playerSprite.flipX = to.x < from.x;
            var spriteRt = playerSprite.transform;

            heroAnim.SetPose(HeroPose.Crouch);
            spriteRt.localPosition = new Vector3(0f, -0.06f, 0f);
            yield return new WaitForSeconds(0.16f);

            heroAnim.SetPose(HeroPose.Air);
            playerSprite.sortingOrder = 3000;
            var monster = enc.Renderer != null ? enc.Renderer.transform : null;
            var monsterScale = monster != null ? monster.localScale : Vector3.one;
            for (float t = 0f; t < GameConfig.JumpDuration; t += Time.deltaTime)
            {
                float p = t / GameConfig.JumpDuration;
                float height = 4f * p * (1f - p) * GameConfig.JumpHeight;
                player.position = Vector3.Lerp(from, to, p);
                spriteRt.localPosition = new Vector3(0f, height, 0f);
                spriteRt.localRotation = Quaternion.Euler(0f, 0f, (to.x >= from.x ? -1f : 1f) * Mathf.Sin(p * Mathf.PI) * 12f);
                playerShadow.localScale = Vector3.one * (1f - 0.45f * Mathf.Sin(p * Mathf.PI)) * playerShadowBase;
                // O monstro se encolhe de susto quando o herói passa por cima.
                if (monster != null)
                {
                    float duck = 1f - 0.25f * Mathf.Clamp01(1f - Mathf.Abs(p - 0.5f) * 3f);
                    monster.localScale = new Vector3(monsterScale.x, monsterScale.y * duck, monsterScale.z);
                }
                yield return null;
            }
            if (monster != null) monster.localScale = monsterScale;
            player.position = to;
            spriteRt.localRotation = Quaternion.identity;
            playerShadow.localScale = Vector3.one * playerShadowBase;

            PreviousPos = PlayerPos;
            PlayerPos = landing;
            playerSprite.sortingOrder = SortOrder(landing.y) + 5;

            heroAnim.SetPose(HeroPose.Crouch);
            spriteRt.localPosition = new Vector3(0f, -0.06f, 0f);
            yield return new WaitForSeconds(0.14f);
            heroAnim.SetPose(HeroPose.Idle);

            if (enc.Data.Roams) enc.StunSteps = GameConfig.JumpStunSteps;
            stepCount += 2;
            jumping = false;
            moving = false;
            Changed?.Invoke();
            onDone?.Invoke();
        }

        void ClickToMove()
        {
            if (cam == null) return;
            var world = cam.ScreenToWorldPoint(Input.mousePosition);
            var tile = WorldToTile(world);
            if (!IsWalkable(tile) || tile == PlayerPos) return;
            var route = FindPath(PlayerPos, tile, t => t == tile || EntityAt(t) == null || EntityAt(t).IsUsed);
            if (route == null) return;
            path.Clear();
            foreach (var step in route) path.Enqueue(step);
        }

        /// <summary>Busca em largura (BFS) na grade de 4 direções. Retorna os passos sem incluir a origem.</summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, Func<Vector2Int, bool> passable)
        {
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            prev[from] = from;
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) break;
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (prev.ContainsKey(n) || !IsWalkable(n) || !passable(n)) continue;
                    prev[n] = cur;
                    queue.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var result = new List<Vector2Int>();
            for (var c = to; c != from; c = prev[c]) result.Add(c);
            result.Reverse();
            return result;
        }

        static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>Volta o herói para o tile anterior (fugir do chefe ou após uma derrota).</summary>
        public void RetreatPlayer()
        {
            StopAllCoroutines();
            moving = false;
            path.Clear();
            var back = PreviousPos;
            if (back == PlayerPos || !IsWalkable(back)) back = FindFreeNeighbor(PlayerPos);
            PlayerPos = back;
            player.position = TileToWorld(PlayerPos);
            playerSprite.sortingOrder = SortOrder(PlayerPos.y) + 5;
            Changed?.Invoke();
        }

        Vector2Int FindFreeNeighbor(Vector2Int pos)
        {
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var d in dirs)
                if (IsWalkable(pos + d) && EntityAt(pos + d) == null) return pos + d;
            return pos;
        }

        /// <summary>Teleporta o herói (usado pelo teste automático).</summary>
        public void TeleportPlayer(Vector2Int tile)
        {
            PreviousPos = PlayerPos;
            PlayerPos = tile;
            player.position = TileToWorld(tile);
            SnapCamera();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Câmera e dicas

        public void ToggleOverview()
        {
            Overview = !Overview;
            game.UI.Hud.SetOverview(Overview);
        }

        public void SnapCamera() => UpdateCamera(true);

        void UpdateCamera(bool instant)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || player == null) return;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float size = Overview ? Mathf.Max(Height * 0.5f + 1.5f, Width * 0.5f / aspect + 1f) : ExploreZoom;
            Vector3 target = Overview ? new Vector3(Width * 0.5f, Height * 0.5f, -10f) : new Vector3(player.position.x, player.position.y, -10f);
            if (!Overview)
            {
                float halfW = size * aspect, halfH = size;
                target.x = Mathf.Clamp(target.x, halfW, Width - halfW);
                target.y = Mathf.Clamp(target.y, halfH - 1f, Height - halfH + 1.5f);
            }
            float k = instant ? 1f : 1f - Mathf.Exp(-6f * Time.deltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, k);
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, k);
            if (playerLight != null) playerLight.gameObject.SetActive(!Overview);
        }

        void UpdateHover()
        {
            if (cam == null || game.UI == null) return;
            string text = null;
            if (!PointerOverUI())
            {
                var tile = WorldToTile(cam.ScreenToWorldPoint(Input.mousePosition));
                var e = InBounds(tile) ? EntityAt(tile) : null;
                if (e != null) text = e.Tooltip;
            }
            game.UI.Hud.SetHover(text);
        }
    }
}
