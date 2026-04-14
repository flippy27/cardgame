# Arquitectura

## Objetivo

Separar el juego en capas para que:

- el duelo no dependa de la UI
- la UI no dependa de la red
- la lógica de cartas no dependa de una implementación específica de matchmaking
- agregar nuevas habilidades sea barato y mantenible

## Módulos

### Runtime/Core
Tipos base, enums, utilidades y configuración general.

### Runtime/Data
ScriptableObjects y contratos de contenido:
- cartas
- habilidades
- efectos
- selectores de objetivo
- mazos
- reglas

### Runtime/Battle
Dominio del duelo:
- estado de partida
- instancias de cartas
- comandos
- resolución de batalla
- snapshots

### Runtime/Networking
Capa online 1v1:
- inicialización de UGS
- sesión multiplayer
- quick match
- join by code
- bridge entre NGO y el runtime de batalla

### Runtime/UI
Presentación:
- HUD
- slots
- mano
- matchmaking panel
- render de cartas

### Runtime/Input
Integración con Input System nuevo y UI mobile-friendly.

### Editor
Herramientas en `Tools/Cards`:
- estructura de proyecto
- generación de cartas prototipo
- creación de escena base
- materiales y texturas placeholder

---

## Principios

## 1. Server authoritative
La partida vive en el host/server.  
Los clientes envían intenciones:
- jugar carta
- terminar turno

El servidor valida, aplica y replica snapshot.

## 2. Snapshot pequeño y determinista
No se sincroniza todo con cientos de NetworkVariables.  
Se replica un snapshot chico del board y mano visible para mantener simpleza, legibilidad y costo de red bajo.

## 3. Cartas como datos
Las cartas son `ScriptableObject`, no clases hardcodeadas por carta.

## 4. Habilidades por composición
Una carta referencia una lista de `AbilityDefinition`.
Cada habilidad tiene:
- trigger
- selector de objetivo
- lista de efectos

Agregar una habilidad nueva normalmente significa:
1. crear una nueva clase `EffectDefinition` o `TargetSelectorDefinition`
2. crear el asset
3. asignarlo a una carta

## 5. Editor-first para prototipado
Las herramientas de `Tools/Cards` crean:
- carpetas
- assets
- texturas placeholder
- materiales
- mazos
- escena base

---

## Flujo online

1. El jugador entra al menú.
2. Se autentica anónimamente en UGS.
3. Puede:
   - Quick Match
   - Create Private Match
   - Join by Code
   - Matchmaker queue
4. Se crea/une a una `Session`.
5. NGO entra a la sesión usando la integración por defecto con `WithRelayNetwork()`.
6. El `CardDuelNetworkCoordinator` queda como autoridad del duelo.
7. Los clientes reciben snapshots y actualizan UI.

---

## Flujo del duelo

1. Se inicializa deck de ambos jugadores.
2. Se roba mano inicial.
3. El jugador activo puede jugar cartas.
4. Al apretar End Turn:
   - resuelven `OnTurnEnd`
   - se ejecuta battle phase del jugador activo
   - cambia turno
   - roba el nuevo jugador activo
5. Si alguien queda sin vida, termina la partida.

---

## Cómo extender sin romper

### Nueva carta
Crear `CardDefinition` asset y asignar:
- costo
- ataque
- vida
- selector
- habilidades
- visual profile

### Nuevo efecto
Hereda de `EffectDefinition` y sobreescribe `Resolve()`.

### Nuevo selector
Hereda de `TargetSelectorDefinition` y sobreescribe `SelectTargets()`.

### Nueva regla
Edita `DuelRulesProfile` o crea uno nuevo.

### Nuevo modo online
Agrega una implementación de `IMatchFlow` o amplía `MpsGameSessionService`.
