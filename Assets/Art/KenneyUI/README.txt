================================================================================
 Kenney UI - TEMPORARY placeholder skin for the Deck Builder (Task #10)
================================================================================

STATUS: TEMPORARY. This is a stand-in skin so the Deck Building screen is
legible and themed while final art is produced. The intent is that the art
will be swapped out later. Do NOT treat these sprites as final game art.

--------------------------------------------------------------------------------
 SOURCE / LICENSE (both CC0 - Creative Commons Zero, public domain)
--------------------------------------------------------------------------------

1) UI Pack (2.0)              -> kenney.nl/assets/ui-pack
   Downloaded zip:  https://kenney.nl/media/pages/assets/ui-pack/f651646eab-1718203990/kenney_ui-pack.zip
   Extracted to:    Assets/Art/KenneyUI/   (Font/, PNG/, Vector/, Sounds/, License.txt)

2) UI Pack - RPG Expansion   -> kenney.nl/assets/ui-pack-rpg-expansion
   Downloaded zip:  https://kenney.nl/media/pages/assets/ui-pack-rpg-expansion/7ec4a46657-1677661824/kenney_ui-pack-rpg-expansion.zip
   Extracted to:    Assets/Art/KenneyUI/RPGExpansion/   (PNG/, Vector/, Spritesheet/)

License: CC0 1.0 Universal (http://creativecommons.org/publicdomain/zero/1.0/).
Free for personal, educational and commercial use. Crediting Kenney /
www.kenney.nl is appreciated but not required. See License.txt in each folder.

The original .zip archives were deleted after extraction to keep the repo lean.

--------------------------------------------------------------------------------
 WHAT IS IN HERE
--------------------------------------------------------------------------------

UI Pack (2.0)  -- flat/modern, 6 color themes (Blue, Green, Grey, Red, Yellow,
                  Extra). Each color has Default/ and Double/ (2x resolution)
                  PNG variants. ~164 sprites per color.
  PNG/<Color>/<Default|Double>/
    button_rectangle_*   wide buttons: _flat _gloss _gradient _line _border
                         and _depth_* (with drop shadow). 9-slice friendly.
    button_square_*      square buttons (icon buttons / small actions)
    button_round_*       round buttons
    input_* (Extra only) input_rectangle, input_square, input_outline_*  (text fields)
    slide_horizontal_*   slider tracks (color + grey, section variants)
    slide_vertical_*     vertical slider / scrollbar tracks
    slide_hangle.png     slider handle
    check_square_*       checkbox / toggle (grey + color, checkmark/cross states)
    check_round_*        round toggle
    icon_checkmark/cross/circle/square (+ _outline)   small status icons
    arrow_basic_<n|e|s|w>(_small)         pagination / nav arrows
    arrow_decorative_*                    fancier nav arrows
    star, star_outline(_depth)            rating / legendary marker
    divider, divider_edges (Extra)        horizontal separators
    icon_arrow_up/down, icon_play, icon_repeat (Extra)  dropdown/expander chevrons

UI Pack - RPG Expansion  -- parchment / fantasy theme, themes: beige, blue,
                  brown, grey + bar colors. Good for window backgrounds.
  RPGExpansion/PNG/
    panel_<beige|beigeLight|blue|brown>.png       WINDOW / PANEL background (9-slice)
    panelInset_<...>.png                          inset / sub-panel background (9-slice)
    buttonLong_<color>(_pressed).png              wide buttons (+ pressed state)
    buttonSquare_<color>(_pressed).png            square/icon buttons
    buttonRound_<color>.png                       round buttons
    arrow<Color>_left/right.png                   pagination arrows
    iconCheck/Circle/Cross_<color>.png            toggle/status icons
    bar<Color>_horizontal*/vertical* + barBack_*  progress / xp bars (3-slice)
    cursorHand/Sword/Gauntlet_*.png               custom cursors (optional)

Fonts (UI Pack 2.0):  Font/Kenney Future.ttf, Font/Kenney Future Narrow.ttf
  -> can be turned into a TMP Font Asset for a consistent themed look (optional).

Preview.png / Sample.png show the full UI Pack 2.0 sprite sheet at a glance.

--------------------------------------------------------------------------------
 RECOMMENDED MAPPING (Deck Builder)  -- see WIRING-PLAN section below
--------------------------------------------------------------------------------
 Window/panel backgrounds : RPGExpansion/PNG/panel_brown.png (9-slice)
 Inset list areas         : RPGExpansion/PNG/panelInset_beige.png (9-slice)
 Primary buttons          : PNG/Blue/Default/button_rectangle_depth_gloss.png
 Neutral/secondary buttons: PNG/Grey/Default/button_rectangle_depth_flat.png
 Destructive (delete)     : PNG/Red/Default/button_rectangle_depth_flat.png
 Icon/close/page buttons  : PNG/Grey/Default/button_square_depth_flat.png
 Text input fields        : PNG/Extra/Default/input_rectangle.png (9-slice)
 Dropdown background       : PNG/Extra/Default/input_rectangle.png + icon_arrow_down_dark.png
 Toggle (affordable-only) : PNG/Grey/Default/check_square_grey.png +
                            check_square_color_checkmark.png (checkmark state)
 Slider/scrollbar         : slide_horizontal_grey.png + slide_hangle.png
 Pagination arrows        : arrow_basic_w.png / arrow_basic_e.png (Grey/Default)
 Rarity star marker       : PNG/Yellow/Default/star.png

 Use the "Double" PNGs for crisp rendering on high-DPI mobile, or import the
 Vector/*.svg via the Unity SVG importer if pixel-perfect scaling is needed.
================================================================================
