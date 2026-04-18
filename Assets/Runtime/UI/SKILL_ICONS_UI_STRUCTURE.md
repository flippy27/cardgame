# Skill Icons UI Structure

Visual guide for card detail view layout with skill icons.

## Layout Structure

```
┌────────────────────────────────────┐
│     CARD DETAIL VIEW                │
├────────────────────────────────────┤
│                                    │
│          [CARD ART IMAGE]          │
│                                    │
│                                    │
│   [NAME]              [COST]       │
│   ──────────────────────────       │
│   [ATTACK]    [HEALTH]   [ARMOR]   │
│   [UNIT TYPE]                      │
│                                    │
├────────────────────────────────────┤
│   ◯ ◯ ◯              ← SKILL ICONS  │
│  (64x64 each, colored with symbols)│
└────────────────────────────────────┘
```

## Detailed Component Hierarchy

```
CardDetailViewPrefab (Canvas)
│
├─ ArtImage
│  └─ (Texture of card art)
│
├─ FrameImage
│  └─ (Background frame)
│
├─ TextContainer
│  ├─ TitleText ("Regenerating Warrior")
│  ├─ CostText ("3")
│  ├─ AttackText ("2")
│  ├─ HealthText ("2")
│  ├─ ArmorText ("0")
│  └─ UnitTypeText ("MELEE")
│
└─ SkillIconContainer ← KEY COMPONENT
   │  (Panel at bottom, ~70px height)
   │  (HorizontalLayoutGroup: spacing=8, padding=5)
   │
   ├─ SkillIcon_0 (Image component)
   │  └─ Texture: Icon_regenerate.png
   │
   ├─ SkillIcon_1 (Image component)
   │  └─ Texture: Icon_shield.png
   │
   └─ SkillIcon_2 (Image component)
      └─ Texture: Icon_poison.png
```

## CardSkillDisplay Component Assignment

```
CardDetailViewPrefab Inspector:
┌─────────────────────────────────────┐
│ Card Detail View (Script)            │
├─────────────────────────────────────┤
│ Card View Widget: [CardViewWidget]   │
│ Skill Display: [CardSkillDisplay] ← │
└─────────────────────────────────────┘

CardSkillDisplay Inspector:
┌─────────────────────────────────────┐
│ Card Skill Display (Script)          │
├─────────────────────────────────────┤
│ Skill Icon Container:               │
│   [SkillIconContainer Panel] ←──────→ (drag the Panel here)
│                                     │
│ Skill Icon Prefab:                  │
│   [SkillIconPrefab] ←──────────────→ (Image template)
│                                     │
│ Skill Icons:                        │
│   [SkillIconDefinition Asset] ←───→ (with 20 icons)
│                                     │
│ Max Skills Displayed: 3             │
└─────────────────────────────────────┘
```

## Icon Positioning

Each skill icon in container:

```
┌──────────────────────────────────────┐
│ SkillIconContainer (70px height)     │
│  ┌────┐  ┌────┐  ┌────┐            │
│  │ ◯  │  │ ◯  │  │ ◯  │  (spacing) │
│  │ HP │  │ sh │  │ ❌ │            │
│  │ +1 │  │ld  │  │    │            │
│  └────┘  └────┘  └────┘            │
│  64x64   64x64   64x64              │
│  (with symbol)                      │
└──────────────────────────────────────┘
```

## Sizing Reference

| Element | Size | Notes |
|---------|------|-------|
| Icon image | 64x64px | PNG textures |
| Icon circle radius | 26px | Inside 64x64 canvas |
| Container height | ~70px | Padding included |
| Container spacing | 8px | Between icons |
| Max icons displayed | 3 | Can show up to 3 skills |

## Color Legend

Colors distinguish skill types visually:

| Color | Skills | Example |
|-------|--------|---------|
| 🟢 Green | Defensive/Heal | Regenerate |
| 🔵 Blue | Protective | Shield, Fly |
| 🟡 Yellow | Evasion/Control | Stun, Dodge |
| 🔴 Red | Damage/Aggro | Trample, Enrage |
| 🟣 Purple | Mana/Magic | Mana Burn |
| 🟠 Orange | Crowd Control | Cleave, Taunt |
| ⚪ White | Utility | Evasion |

## Integration Points

### 1. CardDetailView → CardSkillDisplay

```csharp
public void SetCard(CardInHandDto dto)
{
    // ... existing code ...
    
    // Display skills
    if (skillDisplay != null && dto.definition != null)
    {
        skillDisplay.DisplaySkills(dto.definition);
    }
}
```

### 2. CardViewWidget → CardSkillDisplay

Alternative: Could also call from CardViewWidget.Bind():
```csharp
public void Bind(CardInHandDto dto)
{
    // ... existing code ...
    
    // Get CardSkillDisplay from parent and display
    skillDisplay?.DisplaySkills(dto.definition);
}
```

### 3. From Game Loop

When showing card preview:
```csharp
CardDetailView detailView = // ... get from UI
detailView.SetCard(cardInHandDto);
// CardDetailView automatically calls skillDisplay.DisplaySkills()
```

## Testing Checklist

- [ ] SkillIcons asset created with all 20 icons assigned
- [ ] SkillIconContainer added to prefab at bottom
- [ ] CardSkillDisplay component added to prefab root
- [ ] SkillDisplay references assigned in CardDetailView
- [ ] Icons appear when opening card detail
- [ ] Icons show correct symbols for card abilities
- [ ] Max 3 icons displayed (even if card has 4+ abilities)
- [ ] Icons center properly in container
- [ ] No overlap or alignment issues
- [ ] Transparent background (if desired)

## Performance Notes

- Icons are simple 64x64 textures (low memory)
- CardSkillDisplay reuses Image pool (no instantiation per skill)
- No animation overhead (static display)
- Safe to show/hide rapidly without GC pressure
