# Card Skill Display Setup Guide

Instructions to add skill icons to your card detail view UI.

## Step 1: Generate Skill Icons

1. In Unity Editor: **Tools → Icons → Create or Refresh All**
2. Icons are generated in `Assets/Runtime/Icons/Skills/`
3. Each skill gets a unique colored circle with symbol (64x64 PNG)

## Step 2: Create SkillIconDefinition Asset

1. Right-click in Assets folder
2. **Create → Cards → Skill Icon Definition**
3. Name it "SkillIcons"
4. Assign icon textures in Inspector:

```
Skill Icons (size = 20):
[0] regenerate → Icon_regenerate.png
[1] shield → Icon_shield.png
[2] taunt → Icon_taunt.png
[3] dodge → Icon_dodge.png
[4] evasion → Icon_evasion.png
[5] reflection → Icon_reflection.png
[6] fly → Icon_fly.png
[7] trample → Icon_trample.png
[8] cleave → Icon_cleave.png
[9] poison → Icon_poison.png
[10] stun → Icon_stun.png
[11] execute → Icon_execute.png
[12] ricochet → Icon_ricochet.png
[13] leech → Icon_leech.png
[14] enrage → Icon_enrage.png
[15] mana_burn → Icon_mana_burn.png
[16] last_stand → Icon_last_stand.png
[17] charge → Icon_charge.png
[18] haste → Icon_haste.png
[19] lifelink → Icon_lifelink.png
```

## Step 3: Modify Card Detail Prefab

Open `Assets/Prefabs/Cards/CardDetailViewPrefab.prefab`:

### 3a. Add Skill Icon Container

1. Inside the card layout, add a new Panel/Image at the **BOTTOM**
2. Name it: **"SkillIconContainer"**
3. Set Anchors: `bottom, stretch-horizontal`
4. Set Height: `70px`
5. Set Position Y: `10px` (from bottom)
6. Background: Optional (transparent or subtle color)

Layout:
```
CardDetailViewPrefab
├── Canvas
│   ├── ArtImage (top, large)
│   ├── FrameImage (background)
│   ├── Texts (name, cost, attack, health, armor)
│   └── SkillIconContainer (BOTTOM) ← ADD THIS
│       ├── SkillIcon_0
│       ├── SkillIcon_1
│       └── SkillIcon_2
```

### 3b. Add CardSkillDisplay Component

1. Select the **CardDetailViewPrefab** root object
2. **Add Component → CardSkillDisplay**
3. In Inspector, configure:
   - **Skill Icon Container** → drag "SkillIconContainer" Panel
   - **Skill Icon Prefab** → create prefab below (Step 4)
   - **Skill Icons** → drag the "SkillIcons" SkillIconDefinition asset
   - **Max Skills Displayed** → 3

## Step 4: Create Skill Icon Prefab

Create a template Image for skill icons:

1. Create new empty GameObject: **"SkillIconPrefab"**
2. Add **Image** component
3. Configure:
   - Image Type: **Simple**
   - Raycast Target: **FALSE** (don't block clicks)
   - Size: **64x64**
4. Add Layout Element:
   - Preferred Width: **64**
   - Preferred Height: **64**
5. **Save as Prefab** → `Assets/Prefabs/Cards/SkillIconPrefab.prefab`
6. Delete from scene

### Alternative: Use Horizontal Layout Group

In SkillIconContainer, add **Horizontal Layout Group**:
- Child Force Expand: Width=FALSE, Height=FALSE
- Spacing: 8
- Padding: Left=5, Right=5, Top=5, Bottom=5

## Step 5: Update CardDetailView Script

Modify `CardDetailView.cs`:

```csharp
public class CardDetailView : MonoBehaviour
{
    public CardViewWidget cardViewWidget;
    public CardSkillDisplay skillDisplay;  // ADD THIS

    public void SetCard(CardInHandDto dto)
    {
        _cardDto = dto;
        if (cardViewWidget != null)
        {
            cardViewWidget.Bind(dto);
        }
        
        // ADD THIS: Display skills from card definition
        if (skillDisplay != null && _cardDto.definition != null)
        {
            skillDisplay.DisplaySkills(_cardDto.definition);
        }
    }
}
```

Assign in Inspector:
- **Skill Display** → drag the CardSkillDisplay component

## Step 6: Test

1. Play the game
2. Click on any card in hand
3. Card detail view opens with skill icons at bottom
4. Should see 1-3 colored circles with symbols for that card's abilities

## Troubleshooting

**No icons showing:**
- Check SkillIconDefinition has all 20 icons assigned
- Verify card has abilities assigned in CardDefinition
- Check ability IDs match icon keys

**Icons look wrong:**
- Regenerate icons if colors changed
- Verify Icon_[skillId].png files exist in Icons/Skills folder
- Check CardSkillDisplay.skillIcons is assigned

**Icons too large/small:**
- Adjust SkillIconContainer Height (currently 70px)
- Adjust SkillIcon Prefab size (currently 64x64)
- Adjust Horizontal Layout Group spacing

## Future Enhancements

- Skill tooltips on hover
- Icon animations for active abilities
- Colored backgrounds for skill types
- Drag-to-reorder skill display
- Icon glow for powerful effects
