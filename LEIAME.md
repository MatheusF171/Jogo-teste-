# A Rota do Cangaço — Canavial Assombrado e Pedra do Redemoinho

Jogo em **Unity + C#** baseado no documento *"Arquivo da etapa 1 de engenharia"*, usando a
**opção de combate com cartas**. Visão de cima, exploração em grade inspirada em dungeon crawlers
de cartas. A viagem começa no Canavial Assombrado e segue para a Pedra do Redemoinho, no Sertão.

## Como abrir e jogar

1. No **Unity Hub**: *Add → Add project from disk* e escolha a pasta `RotaDoCangaco`.
   Versão usada: **Unity 6000.6.7f1** (Unity 6).
2. A cena `Assets/Scenes/Main.unity` abre sozinha. Aperte **Play**.
3. Executável pronto (Windows, offline): `Build/RotaDoCangaco.exe`.
   Para gerar de novo: menu **Rota do Cangaço → Gerar executável Windows**.

### Controles

| Onde | Ação |
|---|---|
| Mapa | **WASD / Setas** ou **clique** no destino para andar · **M** mapa inteiro · **B** baralho · **Esc** pausa |
| Batalha | **Clique** na carta (ou teclas **1–9**) e depois no alvo · **Espaço** encerra o turno · **botão direito / Esc** cancela o alvo |

## O que foi implementado

- **Herói**: o personagem azul (`1000207573.png`), chamado de *Aprendiz da Cuca*.
- **Ataques iniciais do folclore da Cuca**: Feitiço da Cuca, Caldeirão Borbulhante (veneno em área),
  Amuleto de Arruda, e a carta de personagem *Aprendiz da Cuca*. Recompensas como Poção do Sono,
  Cantiga da Cuca ("Nana, neném..."), Praga Rogada, Mau-Olhado, Vassoura Voadora e Gato Preto.
- **Sistema de cartas do documento**:
  - baralho inicial de **4 cartas**, uma delas o próprio personagem (fica em jogo e dá bônus);
  - **energia** por turno e custo por carta;
  - tipos **Ataque, Defesa, Habilidade, Bônus** (e Personagem);
  - **combo**: cartas em ordem crescente de custo aumentam o multiplicador; **Coringas** mantêm a sequência;
  - fim do turno: mão vai para o descarte, e o descarte é reembaralhado quando o monte acaba;
  - **inimigos em grupos que avançam** a cada turno (Longe → Médio → Perto), mostrando a intenção.
- **Nível**: cada monstro derrotado dá XP (cerca de 1 nível por grupo). A cada nível, escolha 1 entre 3 cartas.
- **Dois mapas** de 40x28, com monstros, baús, cordéis e fogueiras.
  Morcegos e urubus **rondam e perseguem** o herói. Minimapa e visão do mapa inteiro.
- **Monstros da região**: Sapo-Cururu, Morcego, Fogo-Fátuo, Urubu, Piranha (na ponte do rio), Escorpião e Queixada.
- **Chefe: Mula Sem Cabeça** no velho engenho. Invoca Fogos-Fátuos, resiste ao Sono e entra em fúria com 50% da vida.
- **Habilidade do chefe**: ao vencê-la, você ganha a carta **Fúria da Mula Sem Cabeça**
  (dano em área + Queimadura + escudo), que vai para o baralho.
- **Sertão**: derrote a Mula e passe pela porteira do engenho com 1200 pontos. Você leva o mesmo herói, baralho e recursos para a Caatinga. Contorne o açude, enfrente o Saci no lajedo e siga até a saída. A vitória contra o Saci rende a carta **Redemoinho do Saci**.

### Animações do herói

Os 8 quadros enviados viraram `heroi_f1..f8` (fundo removido) e são animados pelo `HeroAnimator`:

| Quadro | Uso |
|---|---|
| 3 e 4 | parado (respirando) — mapa, batalha, HUD e menu |
| 6, 7, 8 | andando pelo mapa |
| 2 | agachado: preparar e aterrissar o pulo |
| 1 | no ar durante o pulo; braços erguidos ao lançar uma carta; tela de vitória |
| 6 | levando dano na batalha; tela "Você desmaiou" |
| 5 | gordo (a partir de "Gordinho") |

### Pular por cima dos monstros

Ao encostar num monstro aparece a escolha **Lutar / Pular por cima / Recuar**. O pulo leva o herói
para o tile do outro lado, numa animação em arco (o monstro se encolhe de susto). Pular não dá XP e o
monstro continua no mapa; monstros que rondam ficam tontos por alguns passos.
O pulo é **bloqueado**:
- contra **chefes**;
- contra grupos **muito mais fracos** que o herói (nível do herói − maior nível do grupo ≥ 5);
- quando o herói está **pesado** (Gordinho ou Gordo);
- quando não há espaço livre do outro lado.

Se um monstro que ronda alcançar você, a escolha aparece sem a opção de recuar.

### Áreas de descanso: fogueira + panela de cuscuz

Cada fogueira tem ao lado uma **panela de cuscuz** com 4 porções. Cada porção dá um bônus **permanente** à escolha:

| Porção | Bônus |
|---|---|
| Cuscuz com Ovo | +5 de vida máxima e recupera 8 de vida |
| Cuscuz com Charque | +1 de Força em todas as batalhas |
| Cuscuz com Leite | começa cada batalha com +4 de escudo |
| Cuscuz com Manteiga de Garrafa | +1 carta na primeira mão de cada batalha |

Mas cada porção aumenta o **Peso**, e o herói vai engordando visivelmente:

| Peso | Estado | Penalidade |
|---|---|---|
| 0–2 | Leve / Satisfeito | nenhuma |
| 3 | Bucho cheio (herói mais largo) | −1 de energia por turno |
| 4 | Gordinho (quadro 5) | −1 de energia, máx. **3 cartas por turno**, não pula |
| 5+ | Gordo (quadro 5 mais largo) | −2 de energia, máx. **2 cartas por turno**, não pula |

Cada vitória em batalha digere 1 porção. Na simulação, comer com moderação (2 por panela) ajuda;
comer tudo antes das lutas faz perder mais vidas.

### Requisitos do documento

| Requisito | Onde está |
|---|---|
| RF01 Movimentação (teclado ou mouse) | `MapController` |
| RF02 Pontos e itens com perguntas de cultura | Cordéis no mapa (`QuizDatabase`, `GameManager.OpenQuiz`) |
| RF03 HUD com pontos, vidas e progresso | `HudView` (vida, XP, corações, pontos, minimapa, progresso) |
| RF04 Menu Iniciar / Instruções / Sair | `UIManager.BuildMenu` e `BuildInstructions` |
| RF05 Vitória e derrota | Saída do lajedo após vencer o Saci / Game Over ao perder as 3 vidas |
| RNF01 Offline | Nenhuma dependência de internet |
| RNF02 POO com 3+ classes | ~40 classes, com herança e polimorfismo (ex.: `CardEffect`, `MapEntity`, `Combatant`) |

## Estrutura do código (`Assets/RotaDoCangaco/Scripts`)

| Pasta | Classes principais |
|---|---|
| `Core` | `GameManager` (fluxo da partida), `PlayerData` (progresso e peso), `GameConfig` (balanceamento), `CuscuzMenu`, `AutoTest` |
| `Cards` | `CardData`, `CardEffect` e subclasses (`DamageEffect`, `BlockEffect`, `StatusEffect`...), `CardDatabase`, `Deck`, `ComboTracker` |
| `Battle` | `BattleSystem` (regras), `BattleController` (turnos animados), `Combatant` → `PlayerCombatant` / `EnemyCombatant` → `BossCombatant`, `EnemyDatabase`, `BattleAI` |
| `Map` | `MapData` (mapa em texto + encontros + perguntas), `MapController` (grade, movimento, perseguição), `MapEntity` → `EncounterEntity`, `BossEntity`, `ChestEntity`, `QuizEntity`, `CampfireEntity`, `ExitEntity` |
| `UI` | `UIManager`, `HudView`, `BattleView`, `CardView`, `EnemyView`, `UIFactory` |
| `Visual` | `HeroAnimator` (quadros do herói), `MapPainter` (pinta o canavial, rio e engenho), `TextureFactory` (ícones, panela e pratos de cuscuz), `PixelCanvas`, `SpriteLibrary` |

A interface e o mapa são montados **por código**: a cena só tem a câmera e um objeto com `GameManager`.

## Como ajustar

- **Dificuldade**: `GameConfig.cs` (vida, energia, tamanho da mão, combo, XP por nível, pontuação mínima,
  regras do pulo e limites de peso do cuscuz).
- **Cardápio do cuscuz**: `Cuscuz.cs`.
- **Cartas**: `CardDatabase.cs`. Cada carta é uma lista de efeitos, por exemplo
  `new DamageEffect(6), new StatusEffect(StatusType.Veneno, 2, EffectScope.Target)`.
- **Monstros e chefe**: `EnemyDatabase.cs` (vida, alcance, velocidade e padrão de ações).
- **Mapas**: `MapData.cs` (Canavial) e `SertaoMap.cs` (Sertão). As grades são desenhadas em texto.
  Depois de editar, use **Rota do Cangaço → Validar mapa e simular partidas**: o comando confere se tudo é
  alcançável nos dois mapas e simula 900 partidas do Canavial (resultado em `Logs/simulacao.txt`).
- **Sprites**: ficam em `Assets/RotaDoCangaco/Resources/Sprites` e são importados como Sprite automaticamente.
  As artes originais (inclusive dos outros chefes, para as próximas fases) estão em `Assets/RotaDoCangaco/Arte/Originais`.

## Próximas fases (sugestão)

Para cada nova região (Agreste, Litoral):
1. crie um novo mapa em texto;
2. adicione os monstros da região (o Mandacaru já tem arte);
3. adicione o chefe (Curupira, Boitatá, Iara, Boto e Lobisomem já têm arte);
4. crie a carta de habilidade ligada à lenda do chefe.

## Teste automático

`RotaDoCangaco.exe -autotest "C:\pasta"` percorre as duas fases, confere a transição e a saída final e salva capturas de tela e um log.
