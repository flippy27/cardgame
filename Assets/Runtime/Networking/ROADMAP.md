# Roadmap: Next Steps

## Phase 2: Real Server Integration (In Progress)

### API Endpoints to Consume
- [ ] POST /api/auth/login - replace mock auth
- [ ] POST /api/auth/refresh - token refresh
- [ ] POST /api/auth/logout - logout
- [ ] POST /api/matches/{id}/complete - send match result
- [ ] POST /api/matches/queue - join ranked queue
- [ ] GET /api/users/{id}/stats - player stats
- [ ] PATCH /api/decks/{id} - update deck

### Client Services to Build
- [ ] UserService: player profile, stats, achievements
- [ ] DeckService: build, save, validate decks
- [ ] MatchmakingService: queue, search, matchmaking logic
- [ ] SignalRService: real-time match updates (replaces NGO for multiplayer)
- [ ] ReplayService: download and view match replays

### Database Persistence
- [ ] Local SQLite database (for offline support)
- [ ] Sync strategy: upload local changes, download server state
- [ ] Conflict resolution (if played offline then reconnected)

### Testing
- [ ] Mock HTTP server for integration tests
- [ ] End-to-end test suite (login → match → result)
- [ ] Load testing: 1000 concurrent matches

## Phase 3: Gameplay Features

### Card System
- [ ] OnDamaged trigger (API needs support)
- [ ] OnDeath trigger (API needs support)
- [ ] Status effects: Burn, Poison, Stun, Frozen
- [ ] Auras (passive effects for nearby units)
- [ ] Hero abilities: unique per faction
- [ ] Item system: equip items for stat boosts

### Match Features
- [ ] Targeted abilities: player chooses target
- [ ] Mulligan phase: redraw opening hand
- [ ] Overtime/fatigue: health loss each turn if no progress
- [ ] Spectator mode: watch live matches
- [ ] Replays: automatic replay download + analysis

### Progression
- [ ] Daily quests: +50 exp
- [ ] Seasonal rewards: cosmetics, cards
- [ ] Battle pass: free vs premium tiers
- [ ] Achievements: unlock cosmetics
- [ ] Cosmetics: card skins, board themes, hero skins

## Phase 4: Social & Competitive

### Social
- [ ] Friends list: add/remove friends
- [ ] Direct challenges: 1v1 friend matches
- [ ] Clan system: join clans, clan wars
- [ ] Leaderboards: global, regional, seasonal
- [ ] Chat: in-game messaging

### Tournaments
- [ ] Tournament creation
- [ ] Round-robin/single elimination
- [ ] Prize pool distribution
- [ ] Replay analysis: AI-generated commentary

### Ranked Mode
- [ ] Seasonal ladder: 10 divisions (Bronze-Mythic)
- [ ] Decay: lose 10 rating/day if inactive
- [ ] Soft reset: season end → back to 1200
- [ ] Promotion matches: reach rank threshold → promotion series
- [ ] Demotion protection: first time reaching rank protected

## Phase 5: Optimization & Polish

### Performance
- [ ] Card visual pooling: reuse card objects
- [ ] Async asset loading: load cards on demand
- [ ] Network optimization: delta compression for snapshots
- [ ] Battery optimization: reduce update frequency when idle

### Quality
- [ ] Localization: 10+ languages
- [ ] Accessibility: colorblind mode, text scaling
- [ ] Audio: card sounds, music, voice acting
- [ ] Animation polish: card play, attack, death

### Deployment
- [ ] CI/CD pipeline: auto-build on commit
- [ ] A/B testing framework: experiment with features
- [ ] Telemetry: analytics, crash reporting (Firebase)
- [ ] Build variants: debug, staging, production

## Blocked Items (Waiting on API)

- [ ] OnDamaged trigger: API MatchEngine needs support
- [ ] OnDeath trigger: API MatchEngine needs support
- [ ] Manual targeting: API needs TargetCard endpoint
- [ ] Refresh token endpoint: API needs POST /api/auth/refresh
- [ ] Match completion endpoint: API needs POST /api/matches/{id}/complete
- [ ] Replay storage: API needs replay recording/download

## Timeline Estimate

| Phase | Effort | Timeline |
|-------|--------|----------|
| Phase 1 (Done) | ✓ | 1 session |
| Phase 2 | XXX | 2-3 weeks |
| Phase 3 | XXXXX | 4-6 weeks |
| Phase 4 | XXXXX | 4-6 weeks |
| Phase 5 | XXX | 2-3 weeks |
| **Total** | | **13-19 weeks** |

## Risk Areas

1. **Server Authority**: Client sends match result, server doesn't validate → cheating possible
   - **Mitigation**: Server must replay match from snapshot to verify result

2. **Deck Validation**: Only checked on client → invalid decks possible
   - **Mitigation**: Server re-validates deck on match start

3. **Rating Manipulation**: Client calculates rating change → exploit possible
   - **Mitigation**: Server calculates rating, client only displays

4. **Offline Mode**: Sync conflicts if player plays offline then reconnects
   - **Mitigation**: Server is source of truth, client syncs on reconnect

5. **Latency**: Real-time match synchronization challenging over network
   - **Mitigation**: Dedicated server ticks, client predicts then reconciles

## Monitoring & Metrics

- Active players, DAU, MAU
- Match completion rate
- Average match duration
- Rating distribution
- Deck variety (card pick rates)
- Error rates by endpoint
- Latency percentiles (p50, p95, p99)
- Crash rate by build

## Next Immediate Steps

1. Implement `/api/auth/login` endpoint call (replace mock)
2. Implement token refresh flow
3. Add MatchmakingService for queue logic
4. Test full login → match → logout flow
5. Add mock server for offline testing
