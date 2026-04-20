# API Configuration Guide

## 🎯 Configuración Centralizada

Toda configuración de API está centralizada en **un único lugar**:

### 1. **Primary Source: `Assets/Resources/config.json`**
Este es el archivo que se carga en la build final. Contiene:
```json
{
  "api": {
    "baseUrl": "http://localhost:5000",
    "timeoutSeconds": 30,
    "maxRetries": 3,
    "retryDelayMs": 500
  }
}
```

**Esto es lo que cambias para diferentes entornos:**
- **Dev**: `http://localhost:5000` (desarrollo local)
- **Staging**: `https://staging-api.cardduel.com`
- **Production**: `https://api.cardduel.com`
- **Mobile Build**: Cualquier URL que sea accesible desde el dispositivo

---

## 🔄 Cómo Funciona la Configuración

```
ConfigManager.LoadConfig()
  ↓ Lee Assets/Resources/config.json
  ↓
ConfigManager.GetApiBaseUrl()
  ↓ Retorna URL desde config
  ↓
GameService.InitializeServices()
  ↓ Pasa URL a CardGameApiClient, AuthService, etc.
```

### Fallback Chain:
1. **Environment variable**: `API_BASE_URL` (si está seteada)
2. **config.json**: Se carga en startup
3. **ApiConfig defaults**: Si config.json no existe
   - Editor: `http://localhost:5000`
   - Build: `https://api.cardduel.com`

---

## 📱 Para Build en Teléfono

### Opción 1: Cambiar config.json antes de Build
```bash
# Edita Assets/Resources/config.json
# Cambia "baseUrl" a la URL de tu servidor

# Luego haz Build → APK/IPA
```

### Opción 2: Usar Variable de Entorno (Avanzado)
```bash
# En CI/CD o build script
export API_BASE_URL="https://api.cardduel.com"

# Luego haz Build
```

---

## ✅ Archivos Involucrados

| Archivo | Rol | Uso |
|---------|-----|-----|
| `Assets/Resources/config.json` | **Source of Truth** | Cargado en startup, única fuente de verdad |
| `Assets/Runtime/Core/ApiConfig.cs` | Defaults | Fallback si config.json no existe |
| `Assets/Runtime/Core/ConfigManager.cs` | Cargador | Lee config.json y lo expone via `GetApiBaseUrl()` |
| `Assets/Runtime/Networking/GameService.cs` | Consumer | Usa `ConfigManager.GetApiBaseUrl()` |
| `Assets/Runtime/Networking/CardGameApiClient.cs` | Consumer | Usa URL de GameService |

---

## 🚀 Para Cambiar URL

**Nunca hardcodees URLs en el código.**

### ✅ CORRECTO:
Edita `Assets/Resources/config.json`:
```json
{
  "api": {
    "baseUrl": "https://api.cardduel.com"
  }
}
```

### ❌ INCORRECTO:
```csharp
// ❌ NO hagas esto
private string apiBaseUrl = "https://api.cardduel.com";
```

---

## 📝 Cambios Realizados (2026-04-18)

✅ Centralizado TODO en ConfigManager + config.json
✅ Removido hardcoded URL de GameService.cs
✅ Removido hardcoded URL de NetworkBootstrap.cs
✅ ApiConfig.cs usa `http://localhost:5000` para Editor
✅ config.json es el único archivo que necesitas cambiar

---

## 🔐 Nota de Seguridad

Para production:
- **Nunca commits la URL de prod en config.json**
- Usa CI/CD variables o CI/CD override file
- O setea variable de entorno `API_BASE_URL` en tiempo de build
