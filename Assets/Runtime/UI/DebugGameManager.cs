#if UNITY_EDITOR
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Easy access to debug commands for UI buttons.
    /// Attach to any GameObject and drag DebugPanel reference in inspector.
    /// Button OnClick -> DebugGameManager.DrawCardForLocalPlayer() etc.
    /// </summary>
    public class DebugGameManager : MonoBehaviour
    {
        [SerializeField] private DebugPanel debugPanel;

        public void DrawCardForLocalPlayer()
        {
            if (debugPanel != null)
                debugPanel.DrawCardsToHand(0, 1);
        }

        public void DrawCardsForLocalPlayer(int count)
        {
            if (debugPanel != null)
                debugPanel.DrawCardsToHand(0, count);
        }

        public void DrawCardForEnemy()
        {
            if (debugPanel != null)
                debugPanel.DrawCardsToHand(1, 1);
        }

        public void RemoveCardFromLocalHandAt(int index)
        {
            if (debugPanel != null)
                debugPanel.RemoveCardFromHand(0, index);
        }

        public void RemoveCardFromEnemyHandAt(int index)
        {
            if (debugPanel != null)
                debugPanel.RemoveCardFromHand(1, index);
        }

        public void RemoveCardFromLocalBoard(BoardSlot slot)
        {
            if (debugPanel != null)
                debugPanel.RemoveCardFromBoardWithAnimation(0, slot);
        }

        public void RemoveCardFromEnemyBoard(BoardSlot slot)
        {
            if (debugPanel != null)
                debugPanel.RemoveCardFromBoardWithAnimation(1, slot);
        }

        public void KillLocalCardAt(BoardSlot slot)
        {
            if (debugPanel != null)
                debugPanel.KillCard(0, slot);
        }

        public void KillEnemyCardAt(BoardSlot slot)
        {
            if (debugPanel != null)
                debugPanel.KillCard(1, slot);
        }

        public void AddHealthToLocalCard(BoardSlot slot, int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyCardHealth(0, slot, amount);
        }

        public void AddHealthToEnemyCard(BoardSlot slot, int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyCardHealth(1, slot, amount);
        }

        public void AddAttackToLocalCard(BoardSlot slot, int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyCardAttack(0, slot, amount);
        }

        public void AddAttackToEnemyCard(BoardSlot slot, int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyCardAttack(1, slot, amount);
        }

        public void ModifyLocalPlayerHP(int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyPlayerHP(0, amount);
        }

        public void ModifyEnemyPlayerHP(int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyPlayerHP(1, amount);
        }

        public void ModifyLocalPlayerMana(int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyPlayerMana(0, amount);
        }

        public void ModifyEnemyPlayerMana(int amount)
        {
            if (debugPanel != null)
                debugPanel.ModifyPlayerMana(1, amount);
        }

        public void ClearLocalBoard()
        {
            if (debugPanel != null)
                debugPanel.ClearBoard(0);
        }

        public void ClearEnemyBoard()
        {
            if (debugPanel != null)
                debugPanel.ClearBoard(1);
        }

        public void PrintGameState()
        {
            if (debugPanel != null)
                debugPanel.PrintGameState();
        }

        public void EndLocalPlayerTurn()
        {
            if (debugPanel != null)
                debugPanel.EndTurnForPlayer(0);
        }

        public void EndEnemyPlayerTurn()
        {
            if (debugPanel != null)
                debugPanel.EndTurnForPlayer(1);
        }
    }
}
#endif
