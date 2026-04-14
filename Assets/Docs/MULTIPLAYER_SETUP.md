# Multiplayer setup

## Requisitos

- Proyecto linkeado a Unity Services
- Authentication activo
- Multiplayer Services activo
- NGO + Unity Transport
- Multiplayer Play Mode para test local

## Opciones online incluidas

### Quick Match
Usa `MatchmakeSessionAsync` con `QuickJoinOptions`.
Sirve para 2 jugadores y crea sesión si no encuentra una compatible.

### Join by Code
El host crea una sesión privada o pública y comparte el código.

### Matchmaker Queue
Hay soporte base para cola avanzada por nombre de queue.
Debes configurar la queue en UGS.

## Configuración mínima

1. Abre `Project Settings > Services` y vincula el proyecto.
2. Revisa que `Authentication` funcione.
3. Para quick match/join code, basta Relay/Lobby vía Multiplayer Services.
4. Para queue avanzada, crea la queue en Dashboard o Deployment Window.

## Multiplayer Play Mode

Para test local:
1. `Window > Multiplayer > Multiplayer Play Mode`
2. habilita `Player 2`
3. corre la escena
4. desde un editor crea o quick match
5. desde el otro entra por quick match o join code

## Importante

El proyecto está armado para **2 jugadores exactos**.

## Desconexiones
La lógica base:
- detecta cierre de sesión / shutdown de NGO
- permite volver al menú
- deja punto de extensión para reconexión y host migration

## Producción real
Para release te conviene sumar:
- recovery de sesión
- telemetry
- reconnection UX
- anti-cheat de comandos
- analytics
- economy / collection
- persistencia cloud
