# Security Recommendations

## Current Implementation

- **Token Storage:** PlayerPrefs (INSECURE)
- **HTTP:** Assumes HTTPS in production (verify!)
- **Auth:** Mock JWT generation for testing

## Critical Issues

### 1. Token Storage (PlayerPrefs)

**Problem:** PlayerPrefs are stored in plaintext on disk.

```
# macOS/Linux
~/Library/Preferences/[com.YourCompany.YourProductName]
cat ~/Library/Preferences/com.flippy.CardDuelMobile.plist

# Windows
Registry: HKCU\Software\Unity\UnityEditor\Flippy\CardDuelMobile
```

Any process can read tokens.

**Fix (Priority: CRITICAL)**

Use native secure storage:
- **iOS:** Keychain (via plugin)
- **Android:** EncryptedSharedPreferences or Keystore
- **Desktop:** OS credential manager

Example (Android):
```csharp
using AndroidKeyStore = UnityEngine.Android.AndroidKeyStore;

public class SecureAuthService : AuthService
{
    private const string KeystoreAlias = "cardduel_token";

    public override void SetToken(string token)
    {
        // On Android: use EncryptedSharedPreferences
        // On iOS: use Keychain (via native code)
        // On Desktop: use credential manager
    }
}
```

Or use a plugin:
- `com.unity.netcode.adapter.secure-transport` (partial)
- `PlayFab` (proprietary, but hardened)
- Custom native plugin

### 2. HTTPS Enforcement

**Problem:** HTTP is used in development, might not be enforced in production.

**Fix:**
```csharp
// Always require HTTPS
var apiUrl = baseUrl.StartsWith("http://localhost") 
    ? baseUrl 
    : baseUrl.Replace("http://", "https://");

if (!apiUrl.StartsWith("https://") && !apiUrl.Contains("localhost"))
{
    throw new SecurityException("Only HTTPS allowed in production");
}
```

### 3. Token Refresh

**Problem:** Current implementation doesn't handle refresh properly.

**Current:**
```csharp
// Only checks if expiring in < 5 minutes
public async Task<bool> RefreshTokenIfNeeded()
{
    if (secondsUntilExpiry < 300)
    {
        // Try to refresh...
        return false; // Not implemented!
    }
}
```

**Fix:**
1. Implement actual refresh endpoint call: `POST /api/auth/refresh`
2. Get new access token + refresh token
3. Store both securely
4. Auto-call before making API requests

```csharp
public async Task<bool> RefreshTokenIfNeeded()
{
    if (secondsUntilExpiry < 300)
    {
        return await AttemptTokenRefresh();
    }
    return true;
}

private async Task<bool> AttemptTokenRefresh()
{
    var refreshToken = GetSecureToken("refresh_token");
    // POST /api/auth/refresh with refresh_token
    // Get new access_token
    // Store both securely
}
```

### 4. JWT Validation

**Problem:** Client doesn't validate JWT signature.

**Current:**
```csharp
// Mock token (obviously insecure)
var token = GenerateMockToken(playerId);
```

**Fix:** Validate JWT signature on client when received from server.

```csharp
private bool ValidateJwtSignature(string token, string publicKey)
{
    var tokenParts = token.Split('.');
    if (tokenParts.Length != 3) return false;

    var payload = DecodeBase64(tokenParts[1]);
    var signature = DecodeBase64(tokenParts[2]);

    using var sha = new HMACSHA256(Encoding.UTF8.GetBytes(publicKey));
    var validSignature = sha.ComputeHash(
        Encoding.UTF8.GetBytes($"{tokenParts[0]}.{tokenParts[1]}"));

    return SignaturesMatch(validSignature, signature);
}
```

### 5. Deck Hash Validation

**Current:** Client sends deck hash, server verifies.

**Problem:** If deck definitions change, hashes mismatch.

**Fix:** Always re-hash client-side before sending:
```csharp
public static string ComputeDeckHash(IEnumerable<string> cardIds)
{
    var sorted = cardIds.OrderBy(x => x).ToList();
    var raw = string.Join("|", sorted);
    using var sha = SHA256.Create();
    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
    return string.Concat(bytes.Select(b => b.ToString("x2")));
}
```

## Threat Model

### Attack Vectors

1. **Local Token Theft**
   - Attacker reads PlayerPrefs
   - Impersonates player on API
   - **Mitigation:** Secure storage ✓

2. **Network Interception**
   - Man-in-the-middle on HTTP
   - Attacker intercepts token
   - **Mitigation:** HTTPS only ✓

3. **Token Reuse**
   - Attacker uses stolen token
   - **Mitigation:** Token expiry + refresh + HTTPS ✓

4. **Deck Cheating**
   - Client sends invalid cards
   - Invalid deck hash
   - **Mitigation:** Server re-validates cards ✓

5. **Match Manipulation**
   - Client claims false win
   - False rating changes
   - **Mitigation:** Server authority (not client-side completion) ✓

## Implementation Checklist

- [ ] Replace PlayerPrefs with native secure storage
- [ ] Enforce HTTPS in production
- [ ] Implement token refresh endpoint
- [ ] Validate JWT signatures on client
- [ ] Server-side match authority (don't trust client completion)
- [ ] Rate limiting on auth endpoints
- [ ] Audit logging for auth attempts
- [ ] Certificate pinning (optional, advanced)
- [ ] Obfuscate code (optional, assembly)

## Testing

```csharp
[Test]
public void AuthService_TokenStoredSecurely()
{
    // Verify token NOT in PlayerPrefs
    var playerPrefsToken = PlayerPrefs.GetString("auth_token", "");
    Assert.IsEmpty(playerPrefsToken, "Token should not be in PlayerPrefs!");
}

[Test]
public void CardGameApiClient_EnforcesHttps()
{
    Assert.Throws<Exception>(() => 
        new CardGameApiClient("http://production-server.com"));
}

[Test]
public void AuthService_RefreshesBeforeExpiry()
{
    // Set token to expire in 2 minutes
    // Call RefreshTokenIfNeeded
    // Verify new token obtained
}
```

## Resources

- [Unity Security Best Practices](https://docs.unity3d.com/Manual/SecurityPreferences.html)
- [OWASP Mobile Security Top 10](https://owasp.org/www-project-mobile-top-10/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [OAuth 2.0 for Mobile](https://tools.ietf.org/html/draft-ietf-oauth-native-apps)
