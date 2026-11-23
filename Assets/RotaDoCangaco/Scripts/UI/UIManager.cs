using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    /// <summary>
    /// Cria o Canvas e todas as telas: menu (RF04), instruções, HUD, batalha e janelas sobrepostas
    /// (mensagens, escolha de cartas, cordel/quiz, baralho, vitória e derrota).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public HudView Hud { get; private set; }
        public BattleView BattleView { get; private set; }
        public bool OverlayOpen => overlay != null && overlay.gameObject.activeSelf;

        GameManager game;
        RectTransform canvasRoot, menu, instructions, overlay;

        public void Build(GameManager gameManager)
        {
            game = gameManager;

            var canvasGo = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = (RectTransform)canvasGo.transform;
            canvasGo.transform.SetParent(transform, false);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            Hud = HudView.Create(canvasRoot, game);
            BattleView = BattleView.Create(canvasRoot);
            menu = BuildMenu();
            instructions = BuildInstructions();
            overlay = UIFactory.Rect(canvasRoot, "Sobreposicao").Stretch();
            HideAll();
        }

        void HideAll()
        {
            Hud.gameObject.SetActive(false);
            BattleView.gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            instructions.gameObject.SetActive(false);
            CloseOverlay();
        }

        public void ShowMenu()
        {
            HideAll();
            menu.gameObject.SetActive(true);
        }

        public void ShowInstructions()
        {
            HideAll();
            instructions.gameObject.SetActive(true);
        }

        public void ShowExploration()
        {
            HideAll();
            Hud.gameObject.SetActive(true);
            Hud.Refresh();
        }

        public void ShowBattle()
        {
            HideAll();
            BattleView.gameObject.SetActive(true);
        }

        public void CloseOverlay()
        {
            foreach (Transform t in overlay) Destroy(t.gameObject);
            overlay.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Menu e instruções

        RectTransform BuildMenu()
        {
            var root = UIFactory.Rect(canvasRoot, "Menu").Stretch();
            var bg = UIFactory.Image(root, "Fundo", SpriteLibrary.Get("chefes_do_folclore"), new Color(0.45f, 0.4f, 0.5f), false);
            bg.rectTransform.Stretch();
            bg.preserveAspect = false;
            var dim = UIFactory.Panel(root, "Escurecer", new Color(0.02f, 0.01f, 0.05f, 0.72f));
            dim.rectTransform.Stretch(-20, -20, -20, -20);

            var title = UIFactory.Text(root, "Titulo", "A ROTA DO CANGAÇO", 96, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1600, 130));
            var sub = UIFactory.Text(root, "Sub", "Protótipo · Fase 1: Canavial Assombrado\n<size=24>Batalhas por turnos com cartas contra os chefes do folclore brasileiro</size>",
                                     32, Color.white, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(1500, 90));

            var heroGlow = UIFactory.Image(root, "BrilhoHeroi", TextureFactory.Glow, new Color(0.4f, 0.55f, 1f, 0.55f), false);
            heroGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-460, -80), new Vector2(520, 520));
            var hero = UIFactory.Image(root, "Heroi", HeroAnimator.Frame(3));
            hero.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-460, -80), new Vector2(300, 300));
            HeroAnimator.Attach(hero.gameObject);
            var heroName = UIFactory.Text(root, "NomeHeroi", "Aprendiz da Cuca", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            heroName.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-460, -260), new Vector2(400, 40));

            var boss = UIFactory.Image(root, "Chefe", SpriteLibrary.Get("mula_carta"));
            boss.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(470, -90), new Vector2(300, 404));

            string[] labels = { "Iniciar", "Instruções", "Sair" };
            Action[] actions = { () => game.StartNewRun(), ShowInstructions, () => game.Quit() };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIFactory.Button(root, "Botao" + labels[i], labels[i], actions[i], i == 0 ? UIFactory.ButtonColor : UIFactory.ButtonAlt, 34);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0 - i * 100), new Vector2(380, 80));
            }

            var footer = UIFactory.Text(root, "Rodape", "Tema: História e Cultura Regional · Unity + C#", 20, new Color(0.8f, 0.8f, 0.85f));
            footer.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1200, 30));
            return root;
        }

        RectTransform BuildInstructions()
        {
            var root = UIFactory.Rect(canvasRoot, "Instrucoes").Stretch();
            var bg = UIFactory.Panel(root, "Fundo", new Color(0.04f, 0.03f, 0.08f, 1f));
            bg.rectTransform.Stretch(-20, -20, -20, -20);

            var title = UIFactory.Text(root, "Titulo", "COMO JOGAR", 64, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1200, 90));

            string left =
                "<b><color=#FFC845>EXPLORAÇÃO</color></b>\n" +
                "• Ande com <b>WASD/Setas</b> ou <b>clicando</b> no destino.\n" +
                "• O mapa mostra tudo desde o início: monstros, baús, cordéis, fogueiras e o chefe. Planeje a rota! (<b>M</b> mostra o mapa inteiro)\n" +
                "• Morcegos e urubus <b>rondam</b> e perseguem você quando chega perto.\n" +
                "• <b>Cordel</b>: responda perguntas sobre a cultura da região e ganhe pontos.\n" +
                "• <b>Fogueira</b>: recupera vida. <b>Baús</b>: cartas raras, relíquias e remédios.\n" +
                "• Ao encostar num monstro: <b>Lutar</b>, <b>Pular por cima</b> (sem XP) ou <b>Recuar</b>. Não dá para pular chefes, monstros 5+ níveis abaixo de você, nem estando gordo.\n" +
                "• <b>Panela de cuscuz</b> (ao lado das fogueiras): cada porção dá um bônus permanente, mas comer demais deixa você pesado: menos energia e menos cartas por turno. Vencer batalhas faz digerir.\n\n" +
                "<b><color=#FFC845>VIDAS E VITÓRIA</color></b>\n" +
                $"• Você começa com {GameConfig.StartLives} vidas. Perder uma batalha custa 1 vida.\n" +
                $"• Derrote a <b>Mula Sem Cabeça</b> e chegue à porteira com {GameConfig.MinScoreToWin} pontos. No Sertão, vença o <b>Saci</b> e saia do lajedo.";
            string right =
                "<b><color=#FFC845>BATALHA COM CARTAS</color></b>\n" +
                $"• Todo turno você recebe <b>{GameConfig.StartEnergy} de energia</b> e compra uma mão. Cada carta custa energia.\n" +
                "• <b>COMBO:</b> jogue cartas em ordem <b>crescente de custo</b> (0 » 1 » 2...) e as próximas ficam mais fortes. <b>Coringas</b> mantêm a sequência.\n" +
                "• Os inimigos <b>avançam</b> a cada turno (Longe » Médio » Perto). Veja a <b>intenção</b> acima de cada um e equilibre ataque e escudo.\n" +
                "• A carta <b>Aprendiz da Cuca</b> é você: fica em jogo dando bônus.\n" +
                "• Clique numa carta (ou tecle 1–9) e depois no alvo. <b>Espaço</b> encerra o turno.\n\n" +
                "<b><color=#FFC845>EVOLUÇÃO</color></b>\n" +
                "• Monstros dão XP. A cada nível, escolha 1 entre 3 cartas novas.\n" +
                "• Vencer o chefe dá uma carta de habilidade ligada à lenda dele.";

            var l = UIFactory.Text(root, "Esquerda", left, 24, Color.white, TextAnchor.UpperLeft);
            l.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-440, 20), new Vector2(820, 720));
            var r = UIFactory.Text(root, "Direita", right, 24, Color.white, TextAnchor.UpperLeft);
            r.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(440, 20), new Vector2(820, 720));

            var back = UIFactory.Button(root, "Voltar", "Voltar", ShowMenu, UIFactory.ButtonColor, 32);
            ((RectTransform)back.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(320, 76));
            return root;
        }

        // ------------------------------------------------------------------ Janelas sobrepostas

        RectTransform OpenOverlay(string name, float dim = 0.75f)
        {
            CloseOverlay();
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            var root = UIFactory.Rect(overlay, name).Stretch();
            var shade = UIFactory.Panel(root, "Escurecer", new Color(0f, 0f, 0.02f, dim));
            shade.rectTransform.Stretch(-20, -20, -20, -20);
            return root;
        }

        /// <summary>Janela de mensagem com imagem opcional e botões.</summary>
        public void ShowModal(string title, string body, Sprite image, params (string label, Action action)[] buttons)
        {
            var root = OpenOverlay("Mensagem");
            bool hasImage = image != null;
            var size = new Vector2(hasImage ? 1200 : 900, 520);
            var border = UIFactory.Panel(root, "Borda", new Color(1f, 0.78f, 0.25f, 0.55f));
            border.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(8, 8));
            var panel = UIFactory.Panel(root, "Painel", new Color(0.09f, 0.07f, 0.13f, 1f));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size);

            float textX = 0f, textW = 820f;
            if (hasImage)
            {
                var img = UIFactory.Image(panel.transform, "Imagem", image);
                img.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(200, 10), new Vector2(330, 440));
                textX = 165f;
                textW = 780f;
            }
            var t = UIFactory.Text(panel.transform, "Titulo", title, 40, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(textX, -55), new Vector2(textW, 60));
            var b = UIFactory.Text(panel.transform, "Texto", body, 25, Color.white, TextAnchor.UpperCenter);
            b.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(textX, 20), new Vector2(textW, 260));

            if (buttons == null || buttons.Length == 0) buttons = new[] { ("OK", (Action)null) };
            float bw = Mathf.Min(300f, (textW - 20f * (buttons.Length - 1)) / buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                var action = buttons[i].action;
                var button = UIFactory.Button(panel.transform, "Botao" + i, buttons[i].label, () =>
                {
                    CloseOverlay();
                    action?.Invoke();
                }, i == 0 ? UIFactory.ButtonColor : UIFactory.ButtonAlt, 28);
                float x = textX + (i - (buttons.Length - 1) * 0.5f) * (bw + 20f);
                ((RectTransform)button.transform).Place(new Vector2(0.5f, 0f), new Vector2(x, 70), new Vector2(bw, 72));
            }
        }

        /// <summary>Escolha de 1 entre N cartas (subida de nível, baú). onPick recebe null se pular.</summary>
        public void ShowCardChoice(string title, string subtitle, List<CardData> options, Action<CardData> onPick, bool allowSkip)
        {
            var root = OpenOverlay("EscolhaCarta", 0.82f);
            var t = UIFactory.Text(root, "Titulo", title, 64, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1400, 90));
            var s = UIFactory.Text(root, "Sub", subtitle, 28, Color.white);
            s.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1400, 50));

            for (int i = 0; i < options.Count; i++)
            {
                var card = options[i];
                var view = CardView.Create(root, card);
                var rt = (RectTransform)view.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - (options.Count - 1) * 0.5f) * 330f, -10f);
                rt.localScale = Vector3.one * 1.25f;
                var flavor = UIFactory.Text(root, "Sabor" + i, $"<i>{card.Flavor}</i>", 19, new Color(0.85f, 0.82f, 0.75f));
                flavor.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(rt.anchoredPosition.x, -240), new Vector2(300, 60));
                view.OnClicked = v =>
                {
                    CloseOverlay();
                    onPick?.Invoke(v.Card);
                };
                view.OnHover = (v, on) => v.SetSelected(on);
            }

            if (allowSkip)
            {
                var skip = UIFactory.Button(root, "Pular", "Pular", () =>
                {
                    CloseOverlay();
                    onPick?.Invoke(null);
                }, UIFactory.ButtonAlt, 26);
                ((RectTransform)skip.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(240, 64));
            }
        }

        /// <summary>Pergunta do cordel. onAnswered(acertou) é chamado ao clicar em "Continuar".</summary>
        public void ShowQuiz(QuizQuestion q, Action<bool> onAnswered)
        {
            var root = OpenOverlay("Cordel", 0.8f);
            var panel = UIFactory.Panel(root, "Folheto", new Color(0.95f, 0.89f, 0.72f, 1f));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150, 680));
            var ink = new Color(0.16f, 0.11f, 0.08f);

            var header = UIFactory.Text(panel.transform, "Cabecalho", "FOLHETO DE CORDEL", 40, ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            header.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1000, 60));
            var question = UIFactory.Text(panel.transform, "Pergunta", q.Question, 32, ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            question.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1000, 110));

            var result = UIFactory.Text(panel.transform, "Resultado", "", 26, ink, TextAnchor.MiddleCenter, FontStyle.Normal, false);
            result.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1000, 90));

            var buttons = new List<Button>();
            bool answered = false;
            bool correct = false;
            Button cont = null;

            for (int i = 0; i < q.Options.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(panel.transform, "Opcao" + i, q.Options[i], null, new Color(0.35f, 0.22f, 0.12f), 26);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1f), new Vector2(0, -260 - i * 84), new Vector2(820, 70));
                b.onClick.AddListener(() =>
                {
                    if (answered) return;
                    answered = true;
                    correct = index == q.Correct;
                    for (int k = 0; k < buttons.Count; k++)
                    {
                        var img = buttons[k].GetComponent<Image>();
                        img.color = k == q.Correct ? new Color(0.2f, 0.6f, 0.25f) : (k == index ? new Color(0.7f, 0.18f, 0.18f) : new Color(0.3f, 0.25f, 0.2f));
                    }
                    result.text = (correct ? $"<b>Acertou! +{GameConfig.QuizScore} pontos e +5 de vida.</b>\n" : "<b>Errou!</b>\n") + q.Explanation;
                    cont.gameObject.SetActive(true);
                });
                buttons.Add(b);
            }

            cont = UIFactory.Button(panel.transform, "Continuar", "Continuar", () =>
            {
                CloseOverlay();
                onAnswered?.Invoke(correct);
            }, UIFactory.ButtonColor, 28);
            ((RectTransform)cont.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(280, 66));
            cont.gameObject.SetActive(false);
        }

        public void ShowDeck(List<CardData> deck, Action onClose)
        {
            var root = OpenOverlay("Baralho", 0.88f);
            var t = UIFactory.Text(root, "Titulo", $"SEU BARALHO  <size=28>({deck.Count} cartas)</size>", 54, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1400, 80));

            var sorted = deck.OrderBy(c => c.Cost).ThenBy(c => c.Name).ToList();
            int perRow = 9;
            float scale = sorted.Count > 18 ? 0.62f : 0.72f;
            float cw = CardView.Width * scale + 14f, ch = CardView.Height * scale + 14f;
            for (int i = 0; i < sorted.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int rowCount = Mathf.Min(perRow, sorted.Count - row * perRow);
                var view = CardView.Create(root, sorted[i]);
                var rt = (RectTransform)view.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = new Vector2((col - (rowCount - 1) * 0.5f) * cw, -150f - ch * 0.5f - row * ch);
            }

            var close = UIFactory.Button(root, "Fechar", "Fechar (B)", () =>
            {
                CloseOverlay();
                onClose?.Invoke();
            }, UIFactory.ButtonColor, 28);
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(280, 66));
        }

        /// <summary>
        /// Panela de cuscuz: 4 sabores (bônus permanentes), medidor de peso e o herói engordando ao vivo.
        /// A tela é redesenhada a cada porção para o jogador decidir se continua comendo.
        /// </summary>
        public void ShowCuscuz(PlayerData player, CuscuzPotEntity pot, string feedback, Action<CuscuzOption> onEat, Action onClose)
        {
            var root = OpenOverlay("Cuscuz", 0.86f);
            var t = UIFactory.Text(root, "Titulo", "PANELA DE CUSCUZ", 60, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 80));
            string servings = pot.Servings > 0
                ? $"Porções na panela: <b>{pot.Servings}/{GameConfig.CuscuzServingsPerPot}</b>  ·  cada porção dá um bônus <b>permanente</b> e aumenta seu peso"
                : "<color=#FF9A9A>A panela está vazia!</color>";
            var sub = UIFactory.Text(root, "Sub", servings, 26, Color.white);
            sub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(1500, 40));

            // Panela e herói (à esquerda)
            var potGlow = UIFactory.Image(root, "BrilhoPanela", TextureFactory.Glow, new Color(1f, 0.8f, 0.3f, 0.35f), false);
            potGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-640, 150), new Vector2(420, 420));
            var potImg = UIFactory.Image(root, "Panela", TextureFactory.MapObject(pot.Servings > 0 ? "cuscuz" : "cuscuz_empty"));
            potImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-640, 150), new Vector2(300, 300));

            var heroImg = UIFactory.Image(root, "Heroi", HeroAnimator.Frame(3));
            heroImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-640, -150), new Vector2(230, 230));
            HeroAnimator.Attach(heroImg.gameObject).SetFatStage(player.FatStage);
            var heroLabel = UIFactory.Text(root, "Estado", $"Você: <b>{player.FatLabel}</b>", 24, CuscuzMenu.PesoColor(player.Peso), TextAnchor.MiddleCenter);
            heroLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-640, -290), new Vector2(420, 34));

            // Opções (grade 2x2)
            for (int i = 0; i < CuscuzMenu.Options.Count; i++)
            {
                var option = CuscuzMenu.Options[i];
                var button = UIFactory.Button(root, "Cuscuz" + i, "", () => onEat?.Invoke(option), new Color(0.35f, 0.22f, 0.1f), 20);
                var brt = (RectTransform)button.transform;
                brt.Place(new Vector2(0.5f, 0.5f), new Vector2(-90 + (i % 2) * 590, 175 - (i / 2) * 195), new Vector2(570, 180));
                button.interactable = pot.Servings > 0;
                var icon = UIFactory.Image(brt, "Prato", TextureFactory.CuscuzPlate(option.Id));
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(90, 0), new Vector2(150, 150));
                var optionName = UIFactory.Text(brt, "Nome", option.Name, 25, UIFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                optionName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(375, -32), new Vector2(390, 32));
                optionName.resizeTextForBestFit = true;
                optionName.resizeTextMinSize = 16;
                optionName.resizeTextMaxSize = 25;
                optionName.horizontalOverflow = HorizontalWrapMode.Wrap;
                optionName.verticalOverflow = VerticalWrapMode.Truncate;
                var desc = UIFactory.Text(brt, "Descricao", option.Description, 22, Color.white, TextAnchor.UpperLeft);
                desc.rectTransform.Place(new Vector2(0f, 1f), new Vector2(375, -88), new Vector2(380, 60));
                var flavor = UIFactory.Text(brt, "Sabor", $"<i>{option.Flavor}</i>", 17, new Color(0.9f, 0.82f, 0.68f), TextAnchor.UpperLeft);
                flavor.rectTransform.Place(new Vector2(0f, 1f), new Vector2(375, -145), new Vector2(380, 30));
            }

            // Medidor de peso
            var pesoLabel = UIFactory.Text(root, "PesoRotulo", "PESO", 24, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            pesoLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-330, -235), new Vector2(120, 40));
            string[] segmentNames = { "Leve", "1", "2", "Bucho cheio", "Gordinho", "Gordo" };
            for (int i = 0; i <= GameConfig.PesoGordo; i++)
            {
                bool filled = i <= player.Peso && (i > 0 || player.Peso == 0);
                var col = CuscuzMenu.PesoColor(i);
                if (!filled) col = new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.9f);
                var seg = UIFactory.Panel(root, "Peso" + i, col, TextureFactory.RoundedRectSmall);
                seg.raycastTarget = false;
                seg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-190 + i * 150, -235), new Vector2(140, 40));
                var segText = UIFactory.Text(seg.transform, "Texto", segmentNames[i], 16, filled ? Color.black : new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
                segText.rectTransform.Stretch();
            }
            string status = $"Peso atual: <b>{player.Peso}</b> ({player.FatLabel}: {player.FatPenaltyText}).  Cada vitória em batalha digere 1 porção.";
            var statusText = UIFactory.Text(root, "Status", status, 21, Color.white);
            statusText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(180, -290), new Vector2(1180, 32));
            var warn = UIFactory.Text(root, "Aviso", pot.Servings > 0 ? CuscuzMenu.NextServingWarning(player) : "", 21, Color.white);
            warn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(180, -325), new Vector2(1180, 32));

            if (!string.IsNullOrEmpty(feedback))
            {
                var fb = UIFactory.Text(root, "Retorno", feedback, 24, new Color(0.6f, 1f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
                fb.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(1500, 70));
            }

            var stop = UIFactory.Button(root, "Parar", pot.Servings > 0 ? "Parar de comer" : "Sair", () =>
            {
                CloseOverlay();
                onClose?.Invoke();
            }, UIFactory.ButtonColor, 28);
            ((RectTransform)stop.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 75), new Vector2(320, 70));
        }

        /// <summary>Retrato do chefe e carta conquistada.</summary>
        public void ShowBossReward(CardData card, string bossName, string spriteKey, Action onContinue)
        {
            var root = OpenOverlay("RecompensaChefe", 0.85f);
            var glow = UIFactory.Image(root, "Brilho", TextureFactory.Glow, new Color(1f, 0.3f, 0.5f, 0.6f), false);
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1300, 900));

            var t = UIFactory.Text(root, "Titulo", $"VITÓRIA CONTRA {bossName.ToUpperInvariant()}!", 58, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1600, 80));
            var s = UIFactory.Text(root, "Sub", "Você ganhou uma nova carta de habilidade.", 28, Color.white);
            s.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1500, 50));

            var boss = UIFactory.Image(root, "Chefe", SpriteLibrary.Get(spriteKey));
            boss.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-260, -40), new Vector2(390, 525));
            var view = CardView.Create(root, card);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(260, -40);
            rt.localScale = Vector3.one * 1.55f;
            view.SetSelected(true);

            var b = UIFactory.Button(root, "Continuar", "Continuar", () =>
            {
                CloseOverlay();
                onContinue?.Invoke();
            }, UIFactory.ButtonColor, 30);
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(300, 70));
        }

        /// <summary>Tela final de vitória ou derrota (RF05).</summary>
        public void ShowEnd(bool victory, string stats, Action onRestart, Action onMenu)
        {
            HideAll();
            var root = OpenOverlay(victory ? "Vitoria" : "Derrota", 0.92f);
            var image = UIFactory.Image(root, "Imagem", victory ? HeroAnimator.Frame(1) : HeroAnimator.Frame(6));
            image.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-480, 0), victory ? new Vector2(360, 360) : new Vector2(330, 444));
            if (victory)
            {
                var glow = UIFactory.Image(root, "Brilho", TextureFactory.Glow, new Color(1f, 0.85f, 0.3f, 0.5f), false);
                glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-480, 0), new Vector2(700, 700));
                glow.transform.SetSiblingIndex(image.transform.GetSiblingIndex());
            }

            var t = UIFactory.Text(root, "Titulo", victory ? "FASE CONCLUÍDA!" : "FIM DE JOGO",
                                   80, victory ? UIFactory.Accent : new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(200, 300), new Vector2(1100, 110));
            string sub = victory
                ? "Você atravessou o Canavial Assombrado e venceu o Saci no Sertão."
                : "Suas vidas acabaram durante a viagem.";
            var s = UIFactory.Text(root, "Sub", sub, 28, Color.white);
            s.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(200, 210), new Vector2(1000, 80));
            var st = UIFactory.Text(root, "Estatisticas", stats, 28, Color.white, TextAnchor.UpperLeft);
            st.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(200, -20), new Vector2(760, 330));

            var again = UIFactory.Button(root, "Novamente", "Jogar novamente", () => { CloseOverlay(); onRestart?.Invoke(); }, UIFactory.ButtonColor, 30);
            ((RectTransform)again.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(40, -300), new Vector2(340, 76));
            var menuBtn = UIFactory.Button(root, "Menu", "Menu principal", () => { CloseOverlay(); onMenu?.Invoke(); }, UIFactory.ButtonAlt, 30);
            ((RectTransform)menuBtn.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(400, -300), new Vector2(340, 76));
        }
    }
}
