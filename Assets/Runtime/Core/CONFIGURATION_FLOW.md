# Configuration Flow - Single Source of Truth

## 📍 Única Fuente de Verdad

```
Assets/Resources/config.json
         ↓
   ConfigManager.LoadConfig()
         ↓
   ConfigManager.GetApiBaseUrl()
         ↓
   GameBootstrap (log: "API Base URL: ...")
         ↓
   CardGameApiClient, AuthService, etc.
```

---

## 🔄 Flow Detallado

### 1. Game Startup
```
GameBootstrap.Initialize() runs (BeforeSceneLoad)
  ↓
ConfigManager.LoadConfig()
  ↓ Lee Assets/Resources/config.json
  ↓ Si no existe → log warning + usa ApiConfig default
```

### 2. URL Selection
```
ConfigManager.GetApiBaseUrl()
  ↓
if (config.json loaded && has baseUrl)
  ↓ return config.json baseUrl ✅
else
  ↓ return ApiConfig.BaseUrl ⚠️
```

### 3. Services Initialization
```
var apiBaseUrl = ConfigManager.GetApiBaseUrl()
  ↓ Log: "API Base URL: {apiBaseUrl}"
  ↓
CardGameApiClient(apiBaseUrl)
AuthService(apiBaseUrl)
HealthCheckPinger.Initialize(apiBaseUrl)
```

---

## 📋 Checklist - Verificar que TODO sea de config.json

| Component | Gets URL from |
|-----------|---------------|
| GameBootstrap | ✅ ConfigManager.GetApiBaseUrl() |
| CardGameApiClient | ✅ Recibe de GameBootstrap |
| AuthService | ✅ Recibe de GameBootstrap |
| HealthCheckPinger | ✅ Recibe de GameBootstrap |
| GameService | ✅ Usa ApiClient que recibió URL |

---

## ⚠️ Si Ves "localhost" en Logs

### Causas Posibles:

1. **config.json no se encontró**
   - ✅ Verifica: `Assets/Resources/config.json` existe
   - ✅ Verifica: Nombre correcto (sin .json.json)
   - ✅ Verifica: Ubicación correcta: `Resources/` folder

2. **config.json tiene sintaxis inválida**
   - ✅ Valida JSON: copiar a https://jsonlint.com/
   - ✅ Verifica: "baseUrl" existe en "api" section

3. **ApiConfig default está siendo usado**
   - ✅ Verifica logs: "Config loaded from Resources/config.json"
   - ✅ Si dice "config.json not found" → config.json está roto o no existe

---

## 🔍 Debug Logs Esperados

Cuando todo está correcto, deberías ver:

```
[Bootstrap] Initializing game services
[Config] Loaded config from Resources/config.json
[Bootstrap] API Base URL: http://localhost:5000
[Auth] Initialized with http://localhost:5000
[Bootstrap] Game initialized successfully
```

### ❌ Logs de Alerta:

```
[Config] config.json not found, using defaults  ← Config.json no encontrado
[Config] Failed to parse config                  ← config.json JSON inválido
[Config] No baseUrl in config                    ← baseUrl vacío en config.json
```

---

## 🚀 Para Cambiar URL

**Opción 1: Cambiar config.json (recomendado)**
```json
{
  "api": {
    "baseUrl": "https://api.cardduel.com"  ← Cambiar aquí
  }
}
```

**Opción 2: Variable de entorno (override)**
```bash
export API_BASE_URL="https://staging.cardduel.com"
# Luego run Unity
```

**Opción 3: Código (LAST RESORT)**
```csharp
ApiConfig.SetUrl("https://mi-api.com");  // Fallback, solo si lo demás falla
```

---

## 📱 Para Build en Teléfono

1. Modifica `Assets/Resources/config.json`
2. Setea `baseUrl` a URL de tu servidor
3. Build → APK/IPA
4. URL se bake en la build automáticamente

---

## ✅ Garantizado:

- ✅ SOLO config.json es la fuente de verdad
- ✅ Todos los logs incluyen qué URL se está usando
- ✅ Fallbacks son explícitos (logs muestran cuando fallan)
- ✅ No hay URLs hardcodeadas en el código ejecutable
