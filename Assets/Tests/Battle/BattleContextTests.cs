using System;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para BattleContext (mutaciones de batalla).
    /// Valida: damage, heal, armor, attack buff, hero damage.
    /// </summary>
    public class BattleContextTests
    {
        private DuelState _state;
        private BattleContext _context;

        [SetUp]
        public void SetUp()
        {
            _state = new DuelState();
            _state.Players[0] = new DuelPlayerState { PlayerIndex = 0, HeroHealth = 20 };
            _state.Players[1] = new DuelPlayerState { PlayerIndex = 1, HeroHealth = 20 };
            _context = new BattleContext(_state);
        }

        private CardRuntime CreateCardOnBoard(int playerIndex, string runtimeId, int attack, int health, int armor)
        {
            var card = new CardRuntime
            {
                RuntimeId = runtimeId,
                CardId = "test_card",
                DisplayName = "Test Card",
                OwnerIndex = playerIndex,
                Attack = attack,
                CurrentHealth = health,
                MaxHealth = health,
                Armor = armor,
                CurrentSlot = BoardSlot.Front
            };
            _state.GetPlayer(playerIndex).Board[0].Occupant = card;
            return card;
        }

        [Test]
        public void DealDamage_TargetExists_ReducesHealth()
        {
            var target = CreateCardOnBoard(0, "target", 1, 5, 0);

            _context.DealDamage("source", "target", 3, ignoreArmor: false);

            Assert.AreEqual(2, target.CurrentHealth);
        }

        [Test]
        public void DealDamage_KillsTarget_MarksAsDead()
        {
            var target = CreateCardOnBoard(0, "target", 1, 2, 0);

            _context.DealDamage("source", "target", 5, ignoreArmor: false);

            Assert.IsTrue(target.IsDead);
            Assert.AreEqual(0, _state.GetPlayer(0).Board[0].Occupant); // Should be cleaned up
        }

        [Test]
        public void DealDamage_WithArmor_ArmorAbsorbsDamage()
        {
            var target = CreateCardOnBoard(0, "target", 1, 5, 3);

            _context.DealDamage("source", "target", 5, ignoreArmor: false);

            Assert.AreEqual(3, target.Armor, "Armor should be 0 (3-3)");
            Assert.AreEqual(3, target.CurrentHealth, "Health should be 3 (5-2)");
        }

        [Test]
        public void DealDamage_IgnoreArmor_BypassesArmor()
        {
            var target = CreateCardOnBoard(0, "target", 1, 5, 3);

            _context.DealDamage("source", "target", 2, ignoreArmor: true);

            Assert.AreEqual(3, target.Armor, "Armor should not change");
            Assert.AreEqual(3, target.CurrentHealth, "Health should be reduced by full damage");
        }

        [Test]
        public void Heal_TargetExists_IncreasesHealth()
        {
            var target = CreateCardOnBoard(0, "target", 1, 3, 0);
            target.CurrentHealth = 2;

            _context.Heal("target", 2);

            Assert.AreEqual(3, target.CurrentHealth); // Capped at MaxHealth
        }

        [Test]
        public void Heal_DoesNotExceedMaxHealth()
        {
            var target = CreateCardOnBoard(0, "target", 1, 5, 0);

            _context.Heal("target", 10);

            Assert.AreEqual(5, target.CurrentHealth);
        }

        [Test]
        public void GainArmor_IncreasesArmorValue()
        {
            var target = CreateCardOnBoard(0, "target", 1, 5, 0);

            _context.GainArmor("target", 3);

            Assert.AreEqual(3, target.Armor);
        }

        [Test]
        public void ModifyAttack_IncreasesDamage()
        {
            var target = CreateCardOnBoard(0, "target", 2, 5, 0);

            _context.ModifyAttack("target", 3);

            Assert.AreEqual(5, target.Attack);
        }

        [Test]
        public void ModifyAttack_ClampsToZero()
        {
            var target = CreateCardOnBoard(0, "target", 2, 5, 0);

            _context.ModifyAttack("target", -5);

            Assert.AreEqual(0, target.Attack);
        }

        [Test]
        public void DamageHero_ReducesHeroHealth()
        {
            _context.DamageHero(0, 5);

            Assert.AreEqual(15, _state.GetPlayer(0).HeroHealth);
        }

        [Test]
        public void DamageHero_KillsHero_EndsDuel()
        {
            _context.DamageHero(0, 20);

            Assert.IsTrue(_state.DuelEnded);
            Assert.AreEqual(DuelEndReason.LocalHeroDefeated, _state.EndReason);
        }

        [Test]
        public void CleanupDeaths_RemovesDead()
        {
            var target = CreateCardOnBoard(0, "target", 1, 1, 0);
            target.CurrentHealth = 0;

            _context.CleanupDeaths();

            Assert.IsNull(_state.GetPlayer(0).Board[0].Occupant);
        }
    }
}
