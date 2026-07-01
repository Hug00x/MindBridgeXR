# MindBridgeXR

MindBridgeXR é uma experiência em realidade estendida desenvolvida em Unity para explorar tarefas de estimulação cognitiva em contexto doméstico virtual. O projeto organiza o utilizador em quatro fases progressivas: exploração livre da casa, navegação guiada entre divisões, jogo de memória na sala de jantar e uma tarefa funcional no exterior envolvendo seleção e entrega de alimentos.

O objetivo do protótipo é demonstrar uma base técnica e experimental para experiências imersivas orientadas a estimulação cognitiva, com foco em acessibilidade, instruções curtas, feedback positivo e recolha automática de métricas comportamentais. O projeto não deve ser interpretado como ferramenta clínica validada.

## Estado do Projeto

- Protótipo funcional em Unity.
- Fluxo completo com quatro fases sequenciais.
- Três cenas principais incluídas no build.
- Interação XR com comandos Meta Quest.
- Sistema de transições entre cenas com fade e reposicionamento do XR rig.
- Sistema de métricas com exportação para JSON, JSONL, CSV, TSV e relatórios textuais.
- Validação técnica interna feita durante o desenvolvimento.
- Sem avaliação formal com participantes externos.

## Tecnologias

| Componente | Versão / uso |
| --- | --- |
| Unity | 6000.3.7f1 |
| Render pipeline | Universal Render Pipeline 17.3.0 |
| XR Interaction Toolkit | 3.3.1 |
| Unity Input System | 1.18.0 |
| XR Management | 4.5.4 |
| OpenXR | 1.16.1 |
| Meta OpenXR | 2.3.1 |
| Meta XR SDK Core | 85.0.0 |
| Meta XR SDK Interaction OVR | 85.0.0 |
| Plataforma alvo | Android / Meta Quest 3 |
| Arquitetura Android | ARM64 |
| Android SDK | mínimo 32, alvo 34 |

## Estrutura Principal

```text
Assets/
  House/
    Scripts/
      TaskManager.cs
      SceneTransitionManager.cs
      SceneTransitionTrigger.cs
      SceneSpawnPoint.cs
      SceneRoomRegistry.cs
      RoomZone.cs
      MemoryGame/
      OutdoorFoodGame/
      Metrics/
    Models/
    Materials/
    Textures/
    Shaders/
  Scenes/
    Floor1_Scene.unity
    Floor2_Scene.unity
    Exterior_Scene.unity
  XR/
  Oculus/
Packages/
ProjectSettings/
```

## Cenas

As cenas ativas em `ProjectSettings/EditorBuildSettings.asset` são:

| Cena | Função |
| --- | --- |
| `Assets/Scenes/Floor1_Scene.unity` | Cena inicial, primeiro piso, gestor principal, jogo de memória e sala de jantar. |
| `Assets/Scenes/Floor2_Scene.unity` | Segundo piso da casa, usado nas fases de exploração e navegação guiada. |
| `Assets/Scenes/Exterior_Scene.unity` | Exterior/pátio, tarefa de recolha e entrega de alimentos. |

## Fluxo da Experiência

### Fase 1: Exploração Tutorial

O utilizador explora livremente a casa. Cada divisão possui zonas `RoomZone`, que notificam o `TaskManager` quando o jogador entra numa divisão.

O progresso é guardado por identificador único de divisão, evitando que zonas duplicadas contem como novas divisões. A interface apresenta temporariamente o progresso da exploração.

Métricas recolhidas:

- Tempo até à primeira divisão.
- Sequência de divisões visitadas.
- Divisões únicas.
- Entradas e revisitas.
- Mudanças de cena.
- Distância percorrida, se ativada.
- Tempo aproximado por divisão.

### Fase 2: Navegação Guiada

Depois de explorar todas as divisões, o utilizador passa para tarefas de navegação guiada. O sistema apresenta uma lista de destinos e destaca a divisão alvo quando esta está presente na cena atual.

Ordem configurada no código:

1. `floor2_bathroom2`
2. `floor2_bedroomB`
3. `floor1_bathroom1`
4. `exterior_patio`
5. `floor1_livingroom`

Métricas recolhidas:

- Tempo por tarefa.
- Destino de cada tarefa.
- Sequência de divisões percorridas.
- Mudanças de cena.
- Revisitas ou regressos antes de chegar ao destino.

### Fase 3: Jogo de Memória

O utilizador deve dirigir-se à sala de jantar, aproximar-se da mesa e jogar um minijogo de memória 3D. As cartas são selecionadas através de `XRSimpleInteractable`, com uma ponte feita por `XRMemoryCardSelectBridge`.

Componentes principais:

- `DiningMemoryPhaseController`
- `DiningTableZone`
- `MemoryMiniGame3DController`
- `MemoryCard3D`
- `XRMemoryCardSelectBridge`

Métricas recolhidas:

- Tempo até à sala de jantar.
- Tempo até à mesa.
- Duração do jogo.
- Cartas selecionadas.
- Número de tentativas.
- Tentativas corretas e incorretas.
- Pares encontrados.
- Tempo médio por tentativa.
- Taxa de acerto.
- Eficiência face ao mínimo teórico de tentativas.

### Fase 4: Recolha e Entrega de Alimentos

No exterior, o utilizador deve recolher uma lista e entregar os alimentos corretos na zona de entrega. Os alimentos usam `XRGrabInteractable`; a entrega só é processada depois de o objeto ser largado na zona correta.

Componentes principais:

- `OutdoorFoodPhaseController`
- `FoodCollectible`
- `FoodListPickup`
- `FoodDeliveryZone`
- `WorldArrowIndicator`
- `FoodType`
- `FoodDeliveryResult`

Requisitos de alimentos configurados:

| Alimento | Quantidade |
| --- | ---: |
| Cenoura | 3 |
| Batata | 2 |
| Maçã | 1 |
| Pretzel | 2 |
| Manga | 1 |
| Melancia | 1 |
| Tomate | 4 |

Métricas recolhidas:

- Tempo até recolher a lista.
- Alimentos agarrados.
- Tentativas de entrega.
- Entregas aceites e rejeitadas.
- Motivos de rejeição.
- Largadas sem entrega.
- Manipulações desnecessárias.
- Taxa de acerto.
- Tempo médio por alimento aceite.

## Arquitetura

O projeto usa uma arquitetura centrada num coordenador global e vários controladores especializados.

| Script / módulo | Responsabilidade |
| --- | --- |
| `TaskManager` | Controla a fase atual, o catálogo global de divisões, a progressão entre fases e o arranque das métricas. |
| `SceneTransitionManager` | Executa fade, carregamento de cenas, mensagens de transição e reposicionamento do XR rig. |
| `SceneTransitionTrigger` | Deteta passagens entre espaços e pede a transição para a cena e spawn de destino. |
| `SceneSpawnPoint` | Identifica pontos de entrada por `spawnID`. |
| `SceneRoomRegistry` | Regista as `RoomZone` da cena atual no `TaskManager`. |
| `RoomZone` | Representa uma divisão e notifica entrada do jogador. |
| `DiningMemoryPhaseController` | Controla a fase de memória, desde a chegada à sala até à conclusão do jogo. |
| `MemoryMiniGame3DController` | Gere o tabuleiro, cartas reveladas, tentativas e conclusão da ronda. |
| `MemoryCard3D` | Guarda estado individual de cada carta e anima a revelação/ocultação. |
| `OutdoorFoodPhaseController` | Controla a fase exterior, requisitos da lista, entregas e progresso persistente. |
| `FoodCollectible` | Representa um alimento manipulável e regista eventos de agarrar/largar. |
| `FoodDeliveryZone` | Valida alimentos largados na zona de entrega e evita duplicação por múltiplos colliders. |
| `MetricsManager` | Guarda a sessão, eventos, fases, tempos, interrupções e métricas derivadas. |
| `MetricsReportExporter` | Produz ficheiros comparativos e relatórios textuais a partir dos resumos de sessão. |
| `ParticipantIdEntryUI` | Cria a interface 3D para introduzir o código pseudonimizado do participante. |
| `BackgroundMusicManager` | Mantém música ambiente entre cenas. |
| `GrabSound`, `ImpactSound` | Reproduzem feedback sonoro de interação. |
| `LookAtPlayer` | Mantém textos ou painéis orientados para a câmara do jogador. |

## Métricas e Exportação

O sistema de métricas é iniciado no arranque da experiência depois da introdução do código pseudonimizado do participante.

Os dados são guardados em:

```text
Application.persistentDataPath/Metrics/
```

Em Android/Quest, esta pasta fica dentro da área persistente da aplicação.

Ficheiros gerados:

| Ficheiro | Conteúdo |
| --- | --- |
| `<sessionId>_summary.json` | Resumo completo da sessão e das quatro fases. |
| `<sessionId>_events.jsonl` | Eventos cronológicos detalhados. |
| `MindBridgeXR_AllMetrics.csv` | Formato longo com métricas e eventos agregáveis. |
| `MindBridgeXR_Comparacao.csv` | Tabela comparativa entre sessões/participantes. |
| `MindBridgeXR_Comparacao_Excel.tsv` | Versão TSV para importação mais simples em folhas de cálculo. |
| `Relatorios/<sessionId>_relatorio.txt` | Relatório textual individual por sessão. |

Os ficheiros não devem incluir nomes reais nem identificadores diretos. A associação entre código pseudonimizado e participante, caso exista em estudos futuros, deve ser guardada separadamente e com acesso restrito.

## Como Fazer Build and Run no Unity

Esta secção explica o processo desde a instalação do Unity até correr o projeto no Meta Quest.

### 1. Instalar o Unity

1. Instalar o Unity Hub.
2. No Unity Hub, instalar o editor Unity `6000.3.7f1`, que é a versão usada neste projeto.
3. Durante a instalação do editor, adicionar os módulos para Android:
   - `Android Build Support`
   - `Android SDK & NDK Tools`
   - `OpenJDK`
4. Se a versão `6000.3.7f1` não aparecer diretamente no Unity Hub, procurar a versão no arquivo de downloads da Unity e abrir a instalação através do Unity Hub.

Estes módulos são necessários porque o Meta Quest corre aplicações Android.

### 2. Abrir o projeto

1. Abrir o Unity Hub.
2. Escolher `Add project from disk`.
3. Selecionar a pasta raiz deste repositório, ou seja, a pasta que contém `Assets/`, `Packages/` e `ProjectSettings/`.
4. Abrir o projeto com Unity `6000.3.7f1`.
5. Aguardar até o Unity importar os assets e restaurar os pacotes definidos em `Packages/manifest.json`.
6. Abrir a cena inicial:

```text
Assets/Scenes/Floor1_Scene.unity
```

### 3. Confirmar as cenas do build

Antes de correr ou compilar, confirmar que as cenas principais estão incluídas no build:

1. Abrir `File > Build Profiles` ou `File > Build Settings`.
2. Verificar a lista `Scenes In Build`.
3. Confirmar que estas cenas estão ativas:

| Cena | Deve estar ativa? |
| --- | --- |
| `Assets/Scenes/Floor1_Scene.unity` | Sim |
| `Assets/Scenes/Floor2_Scene.unity` | Sim |
| `Assets/Scenes/Exterior_Scene.unity` | Sim |

### 4. Correr no editor

Para um teste rápido dentro do Unity:

1. Abrir `Assets/Scenes/Floor1_Scene.unity`.
2. Carregar no botão `Play`.
3. Usar XR Simulation, se estiver configurado no editor, para testar interações básicas sem headset.

O teste no editor é útil para validar lógica, cenas e UI, mas a experiência final deve ser testada no Meta Quest, porque os comandos, desempenho e comportamento XR real dependem do headset.

### 5. Preparar o Meta Quest

1. Ativar `Developer Mode` no Meta Quest.
2. Ligar o headset ao computador por USB.
3. Colocar o headset e aceitar a permissão de USB/debugging, se aparecer.
4. Confirmar que o Unity reconhece o dispositivo em `Run Device`, dentro de `Build Profiles` ou `Build Settings`.

### 6. Fazer Build and Run para Meta Quest

1. Abrir `File > Build Profiles` ou `File > Build Settings`.
2. Selecionar a plataforma `Android`.
3. Se ainda não estiver ativa, escolher `Switch Platform`.
4. Confirmar as configurações principais:

| Configuração | Valor esperado |
| --- | --- |
| Plataforma | Android |
| Dispositivo alvo | Meta Quest |
| Arquitetura | ARM64 |
| Minimum API Level | Android SDK 32 |
| Target API Level | Android SDK 34 |
| XR Plug-in | OpenXR |
| Meta Quest Support | Ativo |

5. Escolher o Meta Quest em `Run Device`.
6. Clicar em `Build And Run`.
7. Escolher uma pasta para guardar o ficheiro `.apk`, se o Unity pedir.
8. Aguardar a compilação, instalação no headset e arranque automático da aplicação.

Depois do `Build And Run`, a aplicação fica instalada no Meta Quest. Se não arrancar automaticamente, procurar a aplicação instalada no headset e abrir manualmente.

### 7. Problemas comuns

| Problema | Possível solução |
| --- | --- |
| O botão `Build And Run` não aparece ou está desativado | Confirmar que a plataforma Android está selecionada e que os módulos Android foram instalados no Unity Hub. |
| O Meta Quest não aparece em `Run Device` | Confirmar o cabo USB, aceitar a permissão dentro do headset e verificar se o Developer Mode está ativo. |
| Erro relacionado com SDK, NDK ou JDK | Reabrir o Unity Hub e garantir que `Android SDK & NDK Tools` e `OpenJDK` estão instalados para a versão correta do Unity. |
| A aplicação abre numa cena errada | Confirmar que `Floor1_Scene` está ativa e é a primeira cena principal em `Scenes In Build`. |
| A simulação no editor não corresponde ao headset | Testar no Meta Quest através de `Build And Run`, porque o comportamento final depende do dispositivo real. |

## Interação

O projeto foi desenhado para Meta Quest com comandos Touch Plus.

Interações principais:

- Locomoção contínua por joystick.
- Rotação discreta em incrementos de 45 graus.
- Agarrar lista e alimentos com `XRGrabInteractable`.
- Selecionar cartas com interação simples XR.
- Confirmar participante através de interface 3D no arranque.

A locomoção é temporariamente desativada durante a introdução do ID do participante e durante operações críticas de transição/reposicionamento.

## Assets e Recursos Externos

O projeto inclui modelos, texturas, sons e outros recursos visuais provenientes de várias fontes e pacotes. Antes de redistribuir o projeto, publicar binários ou disponibilizar assets separadamente, confirmar as licenças de todos os recursos em:

- `Assets/House/Models/`
- `Assets/House/Textures/`
- `Assets/House/Materials/`
- `Assets/House/Audio/`, caso exista na instalação local
- Pacotes importados da Unity Asset Store, Sketchfab, CGTrader, Pixabay ou outras fontes usadas durante o desenvolvimento

## Validação Atual

O protótipo foi testado internamente durante o desenvolvimento, incluindo:

- Execução completa das quatro fases.
- Transições entre primeiro piso, segundo piso e exterior.
- Reposicionamento do XR rig.
- Seleção das cartas.
- Manipulação e entrega de alimentos.
- Criação dos ficheiros de métricas.
- Correção de problemas de colisões duplicadas, persistência entre cenas e estados inconsistentes.

Não foram realizados:

- Testes formais com participantes externos.
- Avaliação com pessoas com diagnóstico de Alzheimer.
- Medições instrumentais de desempenho por profiler.
- Validação clínica.

## Limitações Conhecidas

- A experiência ainda não foi avaliada formalmente com utilizadores.
- As métricas recolhidas são comportamentais e não constituem instrumentos clínicos validados.
- A ordem das fases e das tarefas guiadas é fixa.
- O ambiente doméstico é genérico e pode não ser familiar para todos os utilizadores.
- Os comandos físicos podem aumentar a dificuldade para utilizadores sem experiência prévia em VR.
- O tempo por divisão é aproximado e depende da entrada noutras zonas.
- Algumas divisões podem ser cobertas por várias zonas com o mesmo identificador.
- Falhas de escrita/exportação de métricas são tratadas de forma silenciosa em alguns pontos.
- A estabilidade visual foi observada internamente, mas ainda não foi quantificada por profiling.

## Trabalho Futuro

Melhorias recomendadas:

- Fazer profiling no Meta Quest 3 para medir FPS, tempos de carregamento e uso de recursos.
- Sinalizar explicitamente erros de escrita/exportação das métricas.
- Validar automaticamente os ficheiros de dados no final de cada sessão.
- Atribuir identificadores semânticos explícitos a todas as cartas e alimentos.
- Adicionar opções de dificuldade adaptativa.
- Permitir variação ou contrabalanço da ordem das tarefas.
- Criar modos de locomoção alternativos.
- Substituir a representação dos comandos por mãos virtuais animadas.
- Avaliar hand tracking como alternativa opcional.
- Preparar avaliação formal de usabilidade, conforto e clareza das instruções após aprovação institucional.
- Apenas depois de validação de usabilidade e aprovação ética, considerar estudos com população clínica.

## Privacidade e Ética

Este projeto recolhe dados comportamentais associados a códigos pseudonimizados. Qualquer utilização com participantes reais deve cumprir requisitos institucionais e legais aplicáveis, incluindo:

- Consentimento informado.
- Aprovação ética quando necessária.
- Avaliação de impacto sobre proteção de dados, quando aplicável.
- Separação entre dados de identificação real e dados exportados pela aplicação.
- Definição clara do período de conservação dos dados.
- Procedimentos para acesso, correção e eliminação dos dados.


## Autor

Hugo Maia Serra  
Projeto Final de Licenciatura em Engenharia Informática  
Universidade da Beira Interior  
2025/2026
