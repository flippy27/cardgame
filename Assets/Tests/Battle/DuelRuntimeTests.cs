using System.Collections.Generic;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para DuelRuntime (flujo de batalla).
    /// Valida: jugar cartas, turnos, batalla, validaciones.
    /// </summary>
    public class DuelRuntimeTests
    {
        private DuelRuntime _duel;
        private DuelRulesProfile _rules;
        private CardDefinition _card1Mana;
        private CardDefinition _card2Mana;
        private CardDefinition _card3Mana;
        private DeckDefinition _deckA;
        private DeckDefinition _deckB;

        [SetUp]
        public void SetUp()
        {
            _rules = BattleTestHelpers.CreateTestRules();
            _card1Mana = BattleTestHelpers.CreateTestCard("card1", "Card 1", manaCost: 1, attack: 1, health: 2);
            _card2Mana = BattleTestHelpers.CreateTestCard("card2", "Card 2", manaCost: 2, attack: 2, health: 2);
            _card3Mana = BattleTestHelpers.CreateTestCard("card3", "Card 3", manaCost: 3, attack: 3, health: 3);

            _deckA = BattleTestHelpers.CreateTestDeck(_card1Mana, 10);
            _deckB = BattleTestHelpers.CreateTestDeck(_card1Mana, 10);

            _duel = new DuelRuntime(_rules);
            _duel.StartGame(_deckA, _deckB, seed: 42);
        }

        [Test]
        public void StartGame_InitializesPlayerStates()
        {
            Assert.AreEqual(20, _duel.State.GetPlayer(0).HeroHealth);
            Assert.AreEqual(20, _duel.State.GetPlayer(1).HeroHealth);
            Assert.AreEqual(0, _duel.State.ActivePlayerIndex);
            Assert.AreEqual(1, _duel.State.TurnNumber);
            Assert.IsFalse(_duel.State.DuelEnded);
        }

        [Test]
        public void StartGame_DrawsInitialHand()
        {
            // rules.startingHandSize = 4
            Assert.AreEqual(4, _duel.State.GetPlayer(0).Hand.Count);
            Assert.AreEqual(4, _duel.State.GetPlayer(1).Hand.Count);
        }

        [Test]
        public void CanPlayCard_SufficientMana_ReturnsTrue()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            var card = _card1Mana;
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = card });
            player.Mana = 2;

            var canPlay = _duel.CanPlayCard(0, "key1", BoardSlot.Front, out var reason);

            Assert.IsTrue(canPlay);
            Assert.AreEqual(string.Empty, reason);
        }

        [Test]
        public void CanPlayCard_InsufficientMana_ReturnsFalse()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            var card = _card3Mana;
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = card });
            player.Mana = 2;

            var canPlay = _duel.CanPlayCard(0, "key1", BoardSlot.Front, out var reason);

            Assert.IsFalse(canPlay);
            Assert.AreEqual("Not enough mana.", reason);
        }

        [Test]
        public void CanPlayCard_SlotOccupied_ReturnsFalse()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = _card1Mana });
            player.Mana = 5;
            player.Board[0].Occupant = new CardRuntime { RuntimeId = "existing" };

            var canPlay = _duel.CanPlayCard(0, "key1", BoardSlot.Front, out var reason);

            Assert.IsFalse(canPlay);
            Assert.AreEqual("Slot is occupied.", reason);
        }

        [Test]
        public void CanPlayCard_NotYourTurn_ReturnsFalse()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = _card1Mana });
            player.Mana = 5;
            _duel.State.ActivePlayerIndex = 1; // Player 1's turn

            var canPlay = _duel.CanPlayCard(0, "key1", BoardSlot.Front, out var reason);

            Assert.IsFalse(canPlay);
            Assert.AreEqual("Not this player's turn.", reason);
        }

        [Test]
        public void TryPlayCard_Success_DeductsManaCost()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = _card2Mana });
            player.Mana = 5;

            _duel.TryPlayCard(0, "key1", BoardSlot.Front);

            Assert.AreEqual(3, player.Mana, "2-card should cost 2 mana");
            Assert.AreEqual(0, player.Hand.Count, "Card should be removed from hand");
        }

        [Test]
        public void TryPlayCard_Success_PlaceCardOnBoard()
        {
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "key1", Definition = _card1Mana });
            player.Mana = 5;

            _duel.TryPlayCard(0, "key1", BoardSlot.Front);

            Assert.IsNotNull(player.Board[0].Occupant, "Card should be on board");
            Assert.AreEqual(_card1Mana.attack, player.Board[0].Occupant.Attack);
            Assert.AreEqual(_card1Mana.health, player.Board[0].Occupant.CurrentHealth);
        }

        [Test]
        public void TryEndTurn_Success_AdvancesTurnNumber()
        {
            _duel.TryEndTurn(0);

            Assert.AreEqual(2, _duel.State.TurnNumber);
        }

        [Test]
        public void TryEndTurn_Success_SwitchesActivePlayer()
        {
            _duel.TryEndTurn(0);

            Assert.AreEqual(1, _duel.State.ActivePlayerIndex);
        }

        [Test]
        public void TryEndTurn_Success_RestoresManaNextPlayer()
        {
            var player1 = _duel.State.GetPlayer(1);
            player1.MaxMana = 1;

            _duel.TryEndTurn(0);

            Assert.AreEqual(2, player1.MaxMana, "Max mana should increase by manaPerTurn (1)");
            Assert.AreEqual(2, player1.Mana, "Current mana should equal max mana");
        }

        [Test]
        public void TryEndTurn_OnWinCondition_EndsDuel()
        {
            var loser = _duel.State.GetPlayer(1);
            loser.HeroHealth = 5;

            // Manually place attacker that can deal 5 damage
            var attacker = new CardRuntime
            {
                RuntimeId = "attacker",
                OwnerIndex = 0,
                Attack = 5,
                CurrentHealth = 1,
                MaxHealth = 1,
                CurrentSlot = BoardSlot.Front,
                Definition = _card1Mana
            };
            _duel.State.GetPlayer(0).Board[0].Occupant = attacker;

            _duel.TryEndTurn(0);

            Assert.IsTrue(_duel.State.DuelEnded);
            Assert.AreEqual(DuelEndReason.EnemyHeroDefeated, _duel.State.EndReason);
        }

        [Test]
        public void GetLegalPlaySlots_ReturnsSlotsCardCanBePlayedIn()
        {
            var frontOnlyCard = BattleTestHelpers.CreateTestCard("front", "Front", canBePlayedInFront: true, canBePlayedInBack: false);
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "front_key", Definition = frontOnlyCard });
            player.Mana = 10;

            var slots = new List<BoardSlot>();
            _duel.GetLegalPlaySlots(0, "front_key", slots);

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(BoardSlot.Front, slots[0]);
        }

        [Test]
        public void GetLegalPlaySlots_OccupiedSlotExcluded()
        {
            var backOnlyCard = BattleTestHelpers.CreateTestCard("back", "Back", canBePlayedInFront: false, canBePlayedInBack: true);
            var player = _duel.State.GetPlayer(0);
            player.Hand.Clear();
            player.Hand.Add(new HandCardRuntime { RuntimeHandKey = "back_key", Definition = backOnlyCard });
            player.Mana = 10;
            player.Board[1].Occupant = new CardRuntime { RuntimeId = "occupied" };

            var slots = new List<BoardSlot>();
            _duel.GetLegalPlaySlots(0, "back_key", slots);

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(BoardSlot.BackRight, slots[0], "BackLeft is occupied, only BackRight available");
        }
    }
}
