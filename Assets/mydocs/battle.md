once battle phase begins, the current player units, start excecuting attacks (skills/abilities the normal attack (if it applyes))
they follow the priority order defined in other files in this folder

skills 
'./how_skills_should_work.md'

when a unit is targeted by something that does damage to it, points equal to the damage done to it, must be substracted from either armor, hp etc and they should persist until it dies (reaching 0 or below) or its healed

attacks go in order of priority, top attacks first, then left then right

if no card is found in the opponents play area, every remaining attack must go to the other players health

ranged will always prioritize straight across first, if sstraight, if theres no card there, it will attack top then, ranged should never attack diagonally

magic will always prioritize diagonal first, if theres no card there, it will attack top then, magic should never attack straight

