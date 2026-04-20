# Multiplayer Play Mode Auto-Login Guide

## 🎮 Cómo Usar

### 1. Habilita Multiplayer Play Mode en Unity Editor
```
Windows → Multiplayer Play Mode → Create or Edit a Profile
```

### 2. Crea configuración con 2+ instancias
```
Profile Settings:
  - Instance Count: 2 (o más, ej: 3, 4, 5)
  - Enable Multiplayer Play Mode: ✅ ON
```

### 3. Haz Play en la escena
Cuando presiones Play:
- **Instancia 1** (AppDomain 1): Es el editor principal (control manual)
- **Instancia 2** (AppDomain 2): Auto-login como `playerone@flippy.com` / `123456`
- **Instancia 3** (AppDomain 3): Auto-login como `playertwo@flippy.com` / `123456`
- **Instancia 4** (AppDomain 4): Auto-login como `playerthree@flippy.com` / `123456`
- Etc.

---

## 🔐 Usuarios Disponibles

| Instancia | AppDomain | Email | Password |
|-----------|-----------|-------|----------|
| Editor (manual) | 1 | (tú logueas) | — |
| Auto 1 | 2 | playerone@flippy.com | 123456 |
| Auto 2 | 3 | playertwo@flippy.com | 123456 |
| Auto 3 | 4 | playerthree@flippy.com | 123456 |
| Auto 4 | 5 | playerfour@flippy.com | 123456 |
| Auto 5 | 6 | playerfive@flippy.com | 123456 |

---

## 🛠️ Cómo Funciona

### Sistema de Detección de Instancia
```csharp
int instanceId = AppDomain.CurrentDomain.Id;

// AppDomain 1 = Main editor
// AppDomain 2 = Primera instancia secundaria
// AppDomain 3 = Segunda instancia secundaria
// Etc.
```

Cada instancia en Multiplayer Play Mode **tiene su propio AppDomain** con ID único.

### Auto-Login Flow
```
Scene Loads
  ↓
MultiplayerAutoLogin.AutoLogin() executes
  ↓
Detecta AppDomain.CurrentDomain.Id
  ↓
Espera a que GameService esté ready
  ↓
Login automático con usuario correspondiente
  ↓
Jugador listo para jugar
```

---

## ✅ Ventajas

- ✅ No necesitas loguear manualmente cada instancia
- ✅ Cada instancia es jugador diferente automáticamente
- ✅ Compatible con API (cada jugador tiene tokens válidos)
- ✅ Perfecto para testing multiplayer local
- ✅ Solo funciona en Editor (no afecta builds)

---

## ⚠️ Consideraciones

### Usuarios Deben Existir en tu API
Asegúrate de que estos usuarios estén creados en tu servidor:
- playerone@flippy.com / 123456
- playertwo@flippy.com / 123456
- playerthree@flippy.com / 123456
- Etc.

Si no existen, agrega un endpoint `/register` o crea las cuentas manualmente.

### AppDomain 1 (Main Editor)
La instancia principal (AppDomain 1) **NO auto-loguea**. Esto te permite:
- Controlar la escena manualmente
- Loguear como usuario diferente
- Debuggear sin interferencias

---

## 🔧 Para Cambiar Usuarios

Edita `MultiplayerAutoLogin.GetPlayerName()`:

```csharp
private static string GetPlayerName(int playerNumber)
{
    return playerNumber switch
    {
        1 => "myplayer1",      // Cambiar aquí
        2 => "myplayer2",      // O aquí
        _ => $"player{playerNumber}"
    };
}
```

---

## 📝 Troubleshooting

### "Unauthorized" cuando intenta loguear
- ✅ Verifica que los usuarios existan en tu API
- ✅ Verifica que contraseña sea "123456"
- ✅ Verifica que config.json tenga URL correcta

### No auto-loguea
- ✅ Verifica que Multiplayer Play Mode esté habilitado
- ✅ Verifica que haya múltiples instancias configuradas
- ✅ Revisa console para mensajes `[MultiplayerAutoLogin]`

### AppDomain ID incorrecto
Algunos setups pueden tener AppDomain IDs diferentes. Revisa console:
```
[MultiplayerAutoLogin] Instance X starting
```

Y ajusta el mapping en `GetPlayerName()`.

---

**Auto-login solo funciona en Editor. Los builds ignoran este sistema completamente.**
