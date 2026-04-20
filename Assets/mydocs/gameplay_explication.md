theres 3 cards per player, 1 melee (front) and 2 ranged (back left and back right)
the order in which the cards are placed, attacked, and attack is top, left, right
if player's top card dies, left goes top, right goes left
if player left card dies
right goes left

always follow priority top -> left -> right

if one card moves/dies everything else moves up in priority

top is top priority
then left
then right

any card can be placed anywhere, card will only attack when they are either in the correct place (melee in top, ranged left or right, magic left or right)

melee only attacks on top unless skills say otherwise
ranged only attacks from left or right straight up to the left or right of the oponent unless skills say otherwise
magic only attacks from left or right diagonally to the left or right of the opponent unless skills say otherwise

if theres no card at top, left and right must be blocked and cards can not be played there, unless cards skill says otherwise

if theres no card at left (and theres card at top obviously), right must be blocked and cards can not be played there, unless cards skill says otherwise

if there are no cards in play, any card thats played can only be placed at top (unless card skill says otherwise)

- a card is placed on top, top will go left, left will go right
- (top has a card already) a card is placed on left, left will go right

--- 
#Animations (all animations in this section are a fast slerp)
following the above rules about priority and what not
animations should go the same way
scenarios
no cards in either of the 3 slots (slots left and right are blocked)
place card on top 

1 card is on top
- can just place card on left
- can place card on top, when hover on top of the previous card, previous card moves to the left slot 

1 card is on top and 1 card is on left
- can just place on right
- can place on left and previous left card moves right
- can place on top and previous top card goes left, previous left goes right

when card is moved away (and not placed) every card should go back where it was previously animated-ly

if the movement is too fast, animations should cancel and just animate back (no teleporting)