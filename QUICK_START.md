# 🚀 Quick Start - CardGame Testing

## 1. Create Scenes (30 seconds)

### In Unity Editor:
```
Top menu → Tools → CardGame Setup → Complete Setup
```

Wait for console to show:
```
✅ Scenes created
✅ Build Settings configured
🎉 Setup complete!
```

---

## 2. Test Login Flow (1 minute)

### Press Play (in editor)

You should see: **LoginScene** with login form

### Enter credentials:
```
Player ID: test_player
Password: test_password
```

### Click Login

Expected result:
- Status shows "✅ Login exitoso" (Login successful)
- UI transitions to **MenuScene**
- Player name displays at top

---

## 3. Test Menu (1 minute)

From MenuScene, verify buttons work:

| Button | Expected Result |
|--------|-----------------|
| **▶️ Play** | Loads BattleScene |
| **📊 Leaderboard** | Shows mock leaderboard (top 5 players) |
| **👤 Profile** | Shows your profile stats & mock matches |
| **⚙️ Settings** | Toggles settings panel |
| **🔧 API Test** | Opens API test menu (F10 also works) |
| **🚪 Logout** | Clears token, returns to LoginScene |

---

## 4. Test Battle (2 minutes)

### Click Play from Menu

You should see: **BattleScene** with battle UI

### Play the match:
1. Use board to place cards
2. Attack opponent
3. Play until match ends
4. You'll see result screen

### Expected API Call:
- Console should show: `[BattleScreen] Match completion result:`
- Match data sent to: `POST /api/matches/{matchId}/complete`

---

## 5. Test Persistent Login (2 minutes)

### From any scene:
1. **Close the game** (Play button → stop)
2. **Press Play again**
3. **You should load directly to MenuScene** (skip login)

Why? Token stored on device. AuthService loads it on startup.

### To test logout:
1. From MenuScene, click **🚪 Logout**
2. You're sent back to LoginScene
3. Close & reopen
4. **You're back at LoginScene** (token was cleared)

---

## 6. API Test Menu (Optional)

### Press F10 in game

Opens API testing panel with:
- Login/Register buttons
- List Cards button
- Search Cards
- Copy/Clear output

Try:
1. Click "Login" (uses email input field)
2. Click "List Cards" (fetches all 18 cards)
3. Click "Search Cards" (uses email field as search query)

---

## 🆘 If Something Breaks

| Problem | Solution |
|---------|----------|
| **Play button does nothing** | Run Tools → CardGame Setup → Complete Setup |
| **Can't login** | Check API is running: `dotnet run` in cardgameapi folder |
| **Wrong credentials error** | Try: email=`test_player`, password=`test_password` |
| **Scenes don't exist** | Assets/Scenes should have: LoginScene.unity, MenuScene.unity, CardDuelPrototype.unity |
| **Build fails** | Run Tools → Configure Build Settings |

---

## ✅ Success Criteria

All of these should work:

- [ ] Create scenes from Tools menu (instant)
- [ ] Login succeeds with test credentials
- [ ] Menu appears after login
- [ ] Play button loads battle
- [ ] Battle UI shows cards
- [ ] Match can be completed
- [ ] Logout works
- [ ] Persistent login works (reopen app)
- [ ] F10 shows API test menu
- [ ] Leaderboard/Profile display mock data

**If all checkboxes are ✅, you're ready to develop further!**

---

## 📊 Architecture at a Glance

```
Login (enter credentials)
  ↓
AuthService.Login() → POST /api/auth/login
  ↓
Token stored securely
  ↓
MenuScene loads (auth check passed)
  ↓
Play game → BattleScene
  ↓
Match ends
  ↓
MatchCompletionService → POST /api/matches/{id}/complete
  ↓
Server calculates Elo, updates database
  ↓
Result shown to player
  ↓
Back to MenuScene
```

---

## 🎯 What Happens Under the Hood

### Login
1. User enters credentials in LoginScene
2. MainMenuScreen.HandleLogin() calls AuthService.Login()
3. AuthService makes HTTP request to `/api/auth/login`
4. Server validates against UserAccount table
5. Server generates JWT token with 24-hour expiry
6. Client receives token, stores in SecurePlayerPrefs
7. AuthService.IsAuthenticated becomes true
8. SceneBootstrap detects auth, loads MenuScene

### Match Completion
1. BattleScreenPresenter detects duel ended
2. Calls HandleMatchCompletion()
3. Creates MatchCompletionService
4. Calls CompleteMatch() with match ID, players, winner, duration
5. Makes POST request to `/api/matches/{matchId}/complete`
6. Server's InMemoryMatchService.CompleteMatch():
   - Validates match exists
   - Verifies both players
   - Calculates Elo using EloRatingService
   - Updates MatchRecord in database
   - Returns success response
7. Client receives response, shows result
8. Can return to menu or play again

### Token Refresh
1. Before every API call, AuthService.RefreshTokenIfNeeded() checks expiry
2. If token expires within 5 minutes, refresh is triggered
3. Fresh token stored, subsequent calls use new token
4. If no valid token, services throw InvalidGameStateException

---

## 💡 Pro Tips

- **API runs on**: `http://localhost:5000` (default)
- **Database**: PostgreSQL (migrations auto-apply on startup)
- **Token lifetime**: 24 hours
- **Test credentials**: `test_player` / `test_password`
- **Elo default rating**: 1000
- **Elo K-factor**: 32 (standard for most games)

---

## 🎮 Ready?

1. **Tools → CardGame Setup → Complete Setup**
2. **Press Play**
3. **Test the flow above**
4. **Report any issues**

Enjoy! 🎉
