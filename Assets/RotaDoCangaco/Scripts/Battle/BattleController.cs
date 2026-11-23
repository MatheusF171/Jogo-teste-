using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Conecta as regras (BattleSystem) à tela (BattleView): entrada do jogador, escolha de alvo
    /// e a fase dos inimigos animada, um inimigo por vez.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        public BattleSystem Battle { get; private set; }
        public bool IsBusy => busy;

        GameManager game;
        BattleView view;
        Action<BattleSystem> onFinished;
        CardData selected;
        bool busy;
        bool finishing;

        public void Init(GameManager gameManager, BattleView battleView)
        {
            game = gameManager;
            view = battleView;
            view.CardClicked += OnCardClicked;
            view.EnemyClicked += OnEnemyClicked;
            view.EndTurnClicked += EndTurn;
        }

        public void Begin(EncounterData encounter, Action<BattleSystem> finished)
        {
            onFinished = finished;
            selected = null;
            busy = false;
            finishing = false;
            Battle = new BattleSystem(game.Player, encounter, new System.Random(game.Rng.Next()));
            view.Bind(Battle, game.Map.Phase == SertaoMap.Phase);
            Battle.Start();
            view.SetTargeting(null);
            view.ShowBanner(Battle.IsBossBattle ? encounter.Leader.Name.ToUpperInvariant() + "!" : "Turno 1");
            view.Refresh();
        }

        void Update()
        {
            if (Battle == null || game.State != GameState.Battle || finishing) return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) CancelTarget();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) EndTurn();

            for (int i = 0; i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                if (i < Battle.Deck.Hand.Count) OnCardClicked(Battle.Deck.Hand[i]);
            }
        }

        void OnCardClicked(CardData card)
        {
            if (busy || finishing || Battle.IsOver) return;
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
            if (selected == null || busy || finishing || enemy.IsDead) return;
            Play(selected, enemy);
        }

        void CancelTarget()
        {
            if (selected == null) return;
            selected = null;
            view.SetTargeting(null);
        }

        void Play(CardData card, EnemyCombatant target)
        {
            selected = null;
            if (Battle.PlayCard(card, target)) view.HeroCast();
            view.SetTargeting(null);
            CheckFinished();
        }

        public void EndTurn()
        {
            if (busy || finishing || Battle == null || Battle.IsOver) return;
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
                if (Battle.IsOver) break;
                if (enemy.IsDead) continue;
                Battle.ExecuteEnemyTurn(enemy);
                view.Refresh();
                yield return new WaitForSeconds(0.55f);
            }

            if (!Battle.IsOver)
            {
                Battle.StartPlayerTurn();
                view.ShowBanner($"Turno {Battle.Turn}");
            }
            busy = false;
            view.SetBusy(false);
            CheckFinished();
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
            onFinished?.Invoke(result);
        }

        /// <summary>Joga sozinho (teste automático): usa a IA simples até a batalha acabar.</summary>
        public IEnumerator AutoPlay(int maxTurns, float delay)
        {
            int turns = 0;
            while (Battle != null && !Battle.IsOver && turns++ < maxTurns)
            {
                while (busy) yield return null;
                if (Battle == null || Battle.IsOver) break;
                BattleAI.PlayTurn(Battle);
                view.Refresh();
                CheckFinished();
                yield return new WaitForSeconds(delay);
                if (Battle == null || Battle.IsOver) break;
                EndTurn();
                yield return null;
                while (busy) yield return null;
            }
            while (finishing && Battle != null) yield return null;
        }
    }
}
