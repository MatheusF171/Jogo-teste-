using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Conecta as regras (BattleSystem) à tela (BattleView): entrada do jogador (clique, teclas e arrastar carta),
    /// escolha de alvo, a animação rápida de cada carta e a fase dos inimigos animada, um inimigo por vez.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        public BattleSystem Battle { get; private set; }
        public bool IsBusy => busy || animating;

        GameManager game;
        BattleView view;
        Action<BattleSystem> onFinished;
        CardData selected;
        bool busy;
        bool animating; // animação da carta do herói (bloqueia a entrada sem mostrar "Turno dos inimigos")
        bool finishing;

        public void Init(GameManager gameManager, BattleView battleView)
        {
            game = gameManager;
            view = battleView;
            view.CardClicked += OnCardClicked;
            view.EnemyClicked += OnEnemyClicked;
            view.EndTurnClicked += EndTurn;
            view.CardDropped += OnCardDropped;
            view.CardDragStarted += OnCardDragStarted;
        }

        public void Begin(EncounterData encounter, Action<BattleSystem> finished)
        {
            onFinished = finished;
            selected = null;
            busy = false;
            animating = false;
            finishing = false;
            Battle = new BattleSystem(game.Player, encounter, new System.Random(game.Rng.Next()));
            view.Bind(Battle);
            Battle.Start();
            view.SetTargeting(null);
            view.ShowBanner(Battle.IsBossBattle ? encounter.Leader.Name.ToUpperInvariant() + "!" : "Turno 1");
            view.Refresh();
        }

        void Update()
        {
            if (Battle == null || game.State != GameState.Battle || finishing) return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelTarget();
                view.CancelCardDrag();
            }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) EndTurn();

            for (int i = 0; i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                if (i < Battle.Deck.Hand.Count) OnCardClicked(Battle.Deck.Hand[i]);
            }
        }

        void OnCardClicked(CardData card)
        {
            if (Battle == null || busy || animating || finishing || Battle.IsOver || view.IsDragging) return;
            if (selected == card)
            {
                CancelTarget();
                return;
            }
            if (!Battle.CanPlay(card, out var reason))
            {
                view.Flash(reason);
                return;
            }
            if (card.NeedsTarget && Battle.AliveEnemies.Count() > 1)
            {
                selected = card;
                view.SetTargeting(card);
                return;
            }
            Play(card, Battle.AliveEnemies.FirstOrDefault());
        }

        void OnEnemyClicked(EnemyCombatant enemy)
        {
            if (selected == null || busy || animating || finishing || enemy.IsDead) return;
            Play(selected, enemy);
        }

        /// <summary>Carta arrastada e solta: sobre um inimigo, na arena ou de volta na mão (já cancelado pela tela).</summary>
        void OnCardDropped(CardData card, EnemyCombatant enemy, bool inArena)
        {
            if (Battle == null || busy || animating || finishing || Battle.IsOver || card == null) return;
            if (!Battle.CanPlay(card, out var reason))
            {
                view.Flash(reason);
                return;
            }
            if (!card.NeedsTarget)
            {
                // Defesa, área, bônus e personagem: basta soltar na arena (ou em cima de qualquer inimigo).
                if (inArena || enemy != null) Play(card, Battle.AliveEnemies.FirstOrDefault());
                return;
            }
            if (enemy != null && !enemy.IsDead)
            {
                Play(card, enemy);
                return;
            }
            var alive = Battle.AliveEnemies.ToList();
            if (inArena && alive.Count == 1)
            {
                Play(card, alive[0]);
                return;
            }
            if (inArena) view.Flash("Solte sobre um inimigo");
        }

        void OnCardDragStarted(CardData card) => CancelTarget();

        void CancelTarget()
        {
            if (selected == null) return;
            selected = null;
            view.SetTargeting(null);
        }

        void Play(CardData card, EnemyCombatant target)
        {
            if (Battle == null || busy || animating || finishing || Battle.IsOver) return;
            selected = null;
            if (!Battle.CanPlay(card, out var reason))
            {
                view.SetTargeting(null);
                view.Flash(reason);
                return;
            }
            if (card.NeedsTarget && (target == null || target.IsDead)) target = Battle.AliveEnemies.FirstOrDefault();
            StartCoroutine(PlayRoutine(card, target));
        }

        /// <summary>
        /// Toca a animação da carta (bote, projétil, escudo...) e só aplica a carta no impacto,
        /// para o clarão, a tremida e os números de dano saírem junto com o golpe.
        /// </summary>
        IEnumerator PlayRoutine(CardData card, EnemyCombatant target)
        {
            animating = true;
            var battle = Battle;
            float impact = view.BeginCardPlay(card, target);
            if (impact > 0f) yield return new WaitForSeconds(impact);

            if (battle != null && Battle == battle && !finishing && !battle.IsOver)
            {
                if (card.NeedsTarget && (target == null || target.IsDead)) target = battle.AliveEnemies.FirstOrDefault();
                battle.PlayCard(card, target);
            }
            animating = false;
            view.EndCardPlay();
            if (Battle != null) CheckFinished();
        }

        public void EndTurn()
        {
            if (busy || animating || finishing || Battle == null || Battle.IsOver) return;
            CancelTarget();
            StartCoroutine(EnemyPhase());
        }

        IEnumerator EnemyPhase()
        {
            busy = true;
            view.SetBusy(true);
            Battle.BeginEnemyPhase();
            view.Refresh();
            yield return new WaitForSeconds(0.3f);

            foreach (var enemy in Battle.EnemyTurnOrder())
            {
                if (Battle == null || Battle.IsOver) break;
                if (enemy.IsDead) continue;

                // Preparação conforme a intenção (bote, projétil, escudo, aura); o turno é executado no impacto.
                float windup = view.PlayEnemyWindup(enemy);
                if (windup > 0f) yield return new WaitForSeconds(windup);
                if (Battle == null || Battle.IsOver) break;
                if (enemy.IsDead) continue;

                Battle.ExecuteEnemyTurn(enemy);
                view.Refresh();
                yield return new WaitForSeconds(windup > 0f ? 0.35f : 0.45f);
            }

            if (Battle != null && !Battle.IsOver)
            {
                Battle.StartPlayerTurn();
                view.ShowBanner($"Turno {Battle.Turn}");
            }
            busy = false;
            view.SetBusy(false);
            if (Battle != null) CheckFinished();
        }

        void CheckFinished()
        {
            view.Refresh();
            if (!Battle.IsOver || finishing) return;
            finishing = true;
            StartCoroutine(Finish());
        }

        IEnumerator Finish()
        {
            busy = true;
            view.ShowBanner(Battle.PlayerWon ? "VITÓRIA!" : "DERROTA...");
            yield return new WaitForSeconds(1.5f);
            var result = Battle;
            view.Unbind();
            Battle = null;
            busy = false;
            animating = false;
            onFinished?.Invoke(result);
        }

        /// <summary>Joga sozinho (teste automático): usa a IA simples até a batalha acabar.</summary>
        public IEnumerator AutoPlay(int maxTurns, float delay)
        {
            int turns = 0;
            while (Battle != null && !Battle.IsOver && turns++ < maxTurns)
            {
                while (busy || animating) yield return null;
                if (Battle == null || Battle.IsOver) break;
                BattleAI.PlayTurn(Battle);
                view.Refresh();
                CheckFinished();
                yield return new WaitForSeconds(delay);
                if (Battle == null || Battle.IsOver) break;
                EndTurn();
                yield return null;
                while (busy || animating) yield return null;
            }
            while (finishing && Battle != null) yield return null;
        }
    }
}
