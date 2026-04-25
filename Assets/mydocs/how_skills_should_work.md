there are defensive, offensive,equipable (this are not really skills but other cards, but their effects, should be treated as skills when equipped), utility and modifier   skills

defensive skills are for example :: Armor
offensive skills are for example :: (Some skill that makes the card do more damage for every card in your hand for example)
equipable skills are for example a weapon u equip on a unit (these really are other cards, but since it adds an ability to the unit, it should be considered when attacking or defending)
utiliy skills are for example:: a skill that when this unit ends its attack, it heals the local player left slot
modifier skills are for example :: skills that change the way the unit attacks, for example, a melee (top attacker), will attack the left or right slots


any unit card can have x amount of skills
skills are stored in the cards carddefinition
in the attack phase, when its the cards turn to attack, it should go through all skills before attacking
each skill will have an animation (to be determined how it works)
as the unit goes through the skills, it should excecute them one by one, (never at the same time)
when the unit finishes doing its skills, it should go to the next unit in the priority slot, if there are no more units for attacking, turn should end

defending (when i get attacked same thing), unit thats defending should check for its skills 
depending on the skills it has, the skill should excecute or be skipped



skills by description, excecution order etc:

armor -> if a card has armor, when it gets attacked armor should get hit first  (unless the other card has trample)
trample -> ignore armor on enemy card (if any)
shield -> negates any damage (lasts one attack received)
fly -> cant be defended by cards that dont fly, if enemy card that has to defend this card, ddoesnt have fly, attack goes directly to the enemy player
poison -> a card can have poison(X-Y) where X is the amount of damage it will do to the enemy thats poisoned, and Y is the amount of turns the poison will last, the poison is added to the enemy card once the enemy card has been attacked, poison will do its damage at the beggining of the battle phase and will, and Y turns will start counting the moment the enemy card gets hit
stun -> card hit with this, will not be able to attack on his next turn, stun will only apply once, meaning, when the card that has this ability, attacks another, it will do its effect (besides actually damaging the other card) and then stun will be "erased" from the card at play
leech -> when a card attacks an enemy card, it will recover the same hp it does as damage to the enemy card (doesnt work when attacking directly to player) (cards have no "max hp", therefore hp can go up infinetly)
enrage -> a card that has enrage will attack twice, but wont be able to attack in the next turn, meaning, this card is placed, waits its turn to attack, attacks, hits twice, then next turn wont attack, turn after that it cant attack again (dealing x2 damage every time it can actually attack)
regenerate -> regenerate((top,left,right,N)) will regenerate N health (notice regen not give, hp can only go as up as the card starting hp), to the card on "top,left or right" depending ot what it says
haste -> this card can attack on the turn its placed (other cards take 1 or more turn when placed to be able to attack)

 