using NUnit.Framework;
using UnityEngine;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Tests
{
    public class LocalCacheServiceTests
    {
        private LocalCacheService _cacheService;

        [SetUp]
        public void Setup()
        {
            _cacheService = new LocalCacheService();
            _cacheService.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            _cacheService.Clear();
        }

        [Test]
        public void Set_And_Get_String()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            _cacheService.Set(key, value);
            var retrieved = _cacheService.Get<string>(key);

            // Assert
            Assert.AreEqual(value, retrieved);
        }

        [Test]
        public void Get_NonExistent_ReturnsNull()
        {
            // Act
            var result = _cacheService.Get<string>("nonexistent");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void Has_ExistingKey_ReturnsTrue()
        {
            // Arrange
            _cacheService.Set("key1", "value1");

            // Act
            var result = _cacheService.Has("key1");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void Has_NonExistentKey_ReturnsFalse()
        {
            // Act
            var result = _cacheService.Has("nonexistent");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void Delete_RemovesKey()
        {
            // Arrange
            _cacheService.Set("key1", "value1");
            Assert.IsTrue(_cacheService.Has("key1"));

            // Act
            _cacheService.Delete("key1");

            // Assert
            Assert.IsFalse(_cacheService.Has("key1"));
        }

        [Test]
        public void Clear_RemovesAllKeys()
        {
            // Arrange
            _cacheService.Set("key1", "value1");
            _cacheService.Set("key2", "value2");

            // Act
            _cacheService.Clear();

            // Assert
            Assert.IsFalse(_cacheService.Has("key1"));
            Assert.IsFalse(_cacheService.Has("key2"));
        }

        [Test]
        public void Set_With_ExpiryHours()
        {
            // Arrange
            var key = "expiring_key";
            var value = "value";

            // Act
            _cacheService.Set(key, value, expiryHours: 1);

            // Assert
            Assert.IsTrue(_cacheService.Has(key));
        }

        [Test]
        public void Set_MultipleValues_RetrievesCorrectly()
        {
            // Arrange
            var obj1 = new TestObject { Id = 1, Name = "Test1" };
            var obj2 = new TestObject { Id = 2, Name = "Test2" };

            // Act
            _cacheService.Set("obj1", obj1);
            _cacheService.Set("obj2", obj2);

            // Assert
            var retrieved1 = _cacheService.Get<TestObject>("obj1");
            var retrieved2 = _cacheService.Get<TestObject>("obj2");

            Assert.AreEqual(1, retrieved1.Id);
            Assert.AreEqual("Test1", retrieved1.Name);
            Assert.AreEqual(2, retrieved2.Id);
            Assert.AreEqual("Test2", retrieved2.Name);
        }

        [Test]
        public void GetStats_ReturnsCorrectCount()
        {
            // Arrange
            _cacheService.Set("key1", "value1");
            _cacheService.Set("key2", "value2");

            // Act
            var (total, expired) = _cacheService.GetStats();

            // Assert
            Assert.GreaterOrEqual(total, 2);
            Assert.AreEqual(0, expired);
        }

        [System.Serializable]
        private class TestObject
        {
            public int Id;
            public string Name;
        }
    }

    public class OfflineSyncServiceTests
    {
        private OfflineSyncService _syncService;
        private LocalCacheService _cacheService;
        private CardGameApiClient _apiClient;

        [SetUp]
        public void Setup()
        {
            _cacheService = new LocalCacheService();
            _cacheService.Clear();
            _apiClient = new CardGameApiClient("http://localhost:5000");
            _syncService = new OfflineSyncService(_cacheService, _apiClient);
        }

        [TearDown]
        public void Teardown()
        {
            _cacheService.Clear();
        }

        [Test]
        public void IsOnline_InitiallyTrue()
        {
            // Assert
            Assert.IsTrue(_syncService.IsOnline);
        }

        [Test]
        public void PendingChanges_InitiallyZero()
        {
            // Assert
            Assert.AreEqual(0, _syncService.PendingChanges);
        }

        [Test]
        public void MarkPending_IncrementsPendingCount()
        {
            // Act
            _syncService.MarkPending("change1", "data1");

            // Assert
            Assert.AreEqual(1, _syncService.PendingChanges);
        }

        [Test]
        public void MarkPending_MultipleChanges()
        {
            // Act
            _syncService.MarkPending("change1", "data1");
            _syncService.MarkPending("change2", "data2");
            _syncService.MarkPending("change3", "data3");

            // Assert
            Assert.AreEqual(3, _syncService.PendingChanges);
        }

        [Test]
        public void GetPendingChanges_ReturnsMarkedChanges()
        {
            // Arrange
            _syncService.MarkPending("change1", "data1");
            _syncService.MarkPending("change2", "data2");

            // Act
            var pending = _syncService.GetPendingChanges();

            // Assert
            Assert.GreaterOrEqual(pending.Count, 2);
        }

        [Test]
        public void MarkSynced_DecrementsPendingCount()
        {
            // Arrange
            _syncService.MarkPending("change1", "data1");
            Assert.AreEqual(1, _syncService.PendingChanges);

            // Act
            _syncService.MarkSynced("change1");

            // Assert
            Assert.AreEqual(0, _syncService.PendingChanges);
        }

        [Test]
        public void SetOnlineStatus_UpdatesStatus()
        {
            // Arrange
            Assert.IsTrue(_syncService.IsOnline);

            // Act
            _syncService.SetOnlineStatus(false);

            // Assert
            Assert.IsFalse(_syncService.IsOnline);

            // Act
            _syncService.SetOnlineStatus(true);

            // Assert
            Assert.IsTrue(_syncService.IsOnline);
        }
    }
}
