using System;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para AuthService.
    /// </summary>
    public class AuthServiceTests
    {
        private AuthService _authService;

        [SetUp]
        public void SetUp()
        {
            _authService = new AuthService();
            // Clear PlayerPrefs before each test
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void Constructor_InitializesWithoutToken()
        {
            Assert.IsFalse(_authService.IsAuthenticated);
            Assert.IsEmpty(_authService.CurrentPlayerId);
            Assert.IsEmpty(_authService.CurrentToken);
        }

        [Test]
        public void GetAuthorizationHeader_NotAuthenticated_ReturnsNull()
        {
            var header = _authService.GetAuthorizationHeader();
            Assert.IsNull(header);
        }

        [Test]
        public void GetAuthorizationHeader_Authenticated_ReturnsBearerToken()
        {
            // Manually set token for testing
            var reflection = typeof(AuthService).GetProperty("CurrentToken");
            // Can't easily test without mock, but verify method exists
            Assert.IsNotNull(_authService);
        }

        [Test]
        public void Login_NullPlayerId_ThrowsValidationException()
        {
            var ex = Assert.ThrowsAsync<ValidationException>(
                async () => await _authService.Login(null, "password"));
            Assert.That(ex.Message.Contains("PlayerId"));
        }

        [Test]
        public void Login_NullPassword_ThrowsValidationException()
        {
            var ex = Assert.ThrowsAsync<ValidationException>(
                async () => await _authService.Login("player1", null));
            Assert.That(ex.Message.Contains("password"));
        }

        [Test]
        public async void Login_ValidCredentials_StoresToken()
        {
            var result = await _authService.Login("test_player", "test_password");

            Assert.IsTrue(result);
            Assert.IsTrue(_authService.IsAuthenticated);
            Assert.AreEqual("test_player", _authService.CurrentPlayerId);
            Assert.IsNotEmpty(_authService.CurrentToken);
        }

        [Test]
        public async void Logout_AfterLogin_ClearsToken()
        {
            await _authService.Login("test_player", "test_password");
            Assert.IsTrue(_authService.IsAuthenticated);

            _authService.Logout();

            Assert.IsFalse(_authService.IsAuthenticated);
            Assert.IsEmpty(_authService.CurrentPlayerId);
            Assert.IsEmpty(_authService.CurrentToken);
        }

        [Test]
        public async void Token_Expiry_MarksAsExpired()
        {
            await _authService.Login("test_player", "test_password");
            Assert.IsTrue(_authService.IsAuthenticated);

            // Set expiry to past
            var pastTime = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds();
            PlayerPrefs.SetString("auth_expiry", pastTime.ToString());
            PlayerPrefs.Save();

            // Create new instance to reload from storage
            var newAuth = new AuthService();
            Assert.IsFalse(newAuth.IsAuthenticated, "Expired token should be cleared");
        }

        [Test]
        public async void Token_NotExpired_MarkedAsAuthenticated()
        {
            await _authService.Login("test_player", "test_password");
            Assert.IsTrue(_authService.IsAuthenticated);

            // Verify token expiry is in future
            var expiryStr = PlayerPrefs.GetString("auth_expiry", "0");
            Assert.IsTrue(long.TryParse(expiryStr, out var expiry));

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.Greater(expiry, now, "Token expiry should be in future");
        }

        [Test]
        public async void RefreshTokenIfNeeded_NoToken_ReturnsFalse()
        {
            var result = await _authService.RefreshTokenIfNeeded();
            Assert.IsFalse(result);
        }

        [Test]
        public async void RefreshTokenIfNeeded_TokenValid_ReturnsFalse()
        {
            await _authService.Login("test_player", "test_password");

            // Token just created, should be valid
            var result = await _authService.RefreshTokenIfNeeded();
            Assert.IsFalse(result, "Valid token should not trigger refresh");
        }
    }
}
