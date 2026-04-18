# Skill Icons System

Visual representation of card abilities through icon display.

## Setup

### 1. Generate Icons

In Unity Editor:
- **Tools → Icons → Create or Refresh All**

This generates 20 colored circular icons (64x64 PNG) for all skills in `Assets/Runtime/Icons/Skills/`.

### 2. Create SkillIconDefinition Asset

1. Right-click in Assets → Create → Cards → Skill Icon Definition
2. Name it "SkillIcons"
3. In Inspector, set "Skill Icons" list size to 20
4. For each skill, assign the corresponding icon PNG file

**Skill ID → Icon mapping:**
- regenerate → Icon_regenerate.png
- shield → Icon_shield.png
- taunt → Icon_taunt.png
- dodge → Icon_dodge.png
- evasion → Icon_evasion.png
- reflection → Icon_reflection.png
- fly → Icon_fly.png
- trample → Icon_trample.png
- cleave → Icon_cleave.png
- poison → Icon_poison.png
- stun → Icon_stun.png
- execute → Icon_execute.png
- ricochet → Icon_ricochet.png
- leech → Icon_leech.png
- enrage → Icon_enrage.png
- mana_burn → Icon_mana_burn.png
- last_stand → Icon_last_stand.png
- charge → Icon_charge.png
- haste → Icon_haste.png
- lifelink → Icon_lifelink.png

### 3. Add CardSkillDisplay to Card UI

On your card large view UI:
1. Create a new horizontal layout group (or grid) for skill icons at bottom of card
2. Add `CardSkillDisplay` component
3. Assign:
   - **Skill Icon Container** → The layout transform
   - **Skill Icon Prefab** → An Image component to use as template
   - **Skill Icons** → The SkillIconDefinition asset created above
   - **Max Skills Displayed** → 3 (or whatever fits your card design)

### 4. Display Skills

In your card presenter/display script:
```csharp
var skillDisplay = GetComponent<CardSkillDisplay>();
skillDisplay.DisplaySkills(cardDefinition);
```

## Icon Colors

| Skill Type | Color | Skills |
|------------|-------|--------|
| Defensive | Green | Regenerate |
| | Light Blue | Shield |
| | Orange | Taunt |
| | Yellow | Dodge |
| | White | Evasion |
| | Magenta | Reflection |
| | Sky Blue | Fly |
| Offensive | Red | Trample, Enrage |
| | Orange-Red | Cleave |
| | Lime Green | Poison |
| | Bright Yellow | Stun |
| | Dark Red | Execute |
| | Gold | Ricochet |
| | Pink | Leech |
| | Purple | Mana Burn |
| Synergy | Brown-Gold | Last Stand |
| | Bright Gold | Charge |
| Utility | Cyan | Haste |
| | Rose | Lifelink |

## Customization

To customize icons:
1. Edit skill colors in `SkillIconGenerator.cs` (SkillColors dict)
2. Run **Tools → Icons → Create or Refresh All** again
3. Alternatively, replace PNG files in `Assets/Runtime/Icons/Skills/` manually

To change icon size:
1. Edit `ICON_SIZE = 64` in `SkillIconGenerator.cs`
2. Run **Tools → Icons → Clear All**
3. Run **Tools → Icons → Create or Refresh All**

## Future Enhancements

- Custom sprite-based icons
- Animated icon effects
- Skill descriptions on hover
- Skill filtering/sorting by type
- Icon scaling for different card sizes
