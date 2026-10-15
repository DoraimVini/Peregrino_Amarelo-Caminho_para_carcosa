---
type: Game System
title: Resiliência Mental
description: Sistema central que gerencia o estresse e a "sanidade" do protagonista durante o gameplay.
tags: [gameplay, mechanics, ui, core-loop]
timestamp: 2026-07-07T10:00:00Z
---

# Resiliência Mental

A **Resiliência Mental** é o principal medidor de saúde não-física do protagonista em "A Maldição da Cidade Pálida". Diferente de uma barra de "HP" tradicional, ela flutua constantemente com base em eventos da cidade, interações com anomalias e tempo de exposição ao ambiente hostil.

## Regras de Negócio e Cálculos

O valor de Resiliência Mental (RM) vai de `0.0` a `100.0`.

### Fatores de Dreno
- **Exposição (Passiva):** -0.5 RM por segundo se em zona de alta anomalia.
- **Dano Direto (Ativa):** Ataques de inimigos drenam entre 10 e 25 RM instantaneamente.

### A Resistência Anômala (2026-10-15)

Vem do equipamento (`StatType.DefesaAnomalia`): o conjunto inicial dá +1 por peça (R 3), o afixo
Couraçado +2 a 6 (item de nível ≥ 3, cresce 25% por nível), o Set 4/4/6 e a Coroa de Ossos 5.
Ela age de **dois jeitos**, porque golpe e desgaste são coisas diferentes:

| O que fere a mente | Regra | Exemplo com R 3 |
|---|---|---|
| **Golpe** (grito pontual do Byakhee, Cone de Gelo, ataques anômalos) | subtrai: `max(15% do golpe, golpe − R)` (`MitigacaoDeDano`) | golpe de 20 → 17 |
| **Dreno de anomalia** (grito contínuo do Byakhee, Pressão Psíquica, Eco de Carcosa, olhar do Rei) | porcentagem decrescente: passa `1 − R/(R+20)`, redução máxima 75% (`ResistenciaAnomala`) | 2 RM/s → 1,74 |
| **Preço** (custo de Artefato, `DrenoRM` do próprio item) | não reduz | custo 3 → 3 |

**Por que o dreno não usa a subtração:** dreno não tem golpe, tem taxa. Subtrair R de uma fatia de
quadro (ou da taxa por segundo) zera o dreno assim que R passa da taxa — foi o defeito que fazia o
conjunto inicial reduzir todo dreno a 15% (`DrenoContinuoTests`). A porcentagem é multiplicativa,
logo independe do quadro; 20 é a meia eficácia (R 20 corta pela metade) e cada ponto rende menos que
o anterior. Tabela: R 3 → 13%, R 9 → 31%, R 19 → 49%, R 40 → 67%, R ≥ 60 → 75% (teto — o horror
não zera). No Rito do Olhar ela se soma ao Anel (× 0,8 do olhar). O inventário mostra o corte no
resumo do Damião ("Resistência Anômala 3 — drenos da mente −13%").

### Fatores de Recuperação
- **Pontos de Luz:** Ficar sob postes de luz seguros restaura 2.0 RM por segundo.
- **Consumíveis:** Itens como "Chá Calmante" restauram 40 RM.

## Efeitos de Status (Thresholds)
- **>= 80 RM (Focado):** A visão fica levemente mais clara, [PlayerMovement](../scripts/runtime/player_movement_cs.md) ganha +10% de velocidade de movimento.
- **< 30 RM (Desesperado):** Visão de túnel, sons distorcidos, probabilidade de errar interações.
- **= 0 RM (Colapso):** Game Over ou transição para estado de "Sombra".

## Implementação Técnica
A lógica principal está implementada em `ResilienciaMental.cs` e `ResilienciaBar.cs`.
A interface gráfica usa eventos C# padrão (`event Action<ResilienciaChangedArgs> OnChanged`) para desacoplar a UI da lógica de gameplay.
