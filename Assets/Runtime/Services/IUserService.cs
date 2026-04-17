using System.Threading.Tasks;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Services
{
    public interface IUserService
    {
        Task<bool> Login(string email, string password);
        Task<bool> Register(string email, string username, string password);
        void Logout();
        string GetCurrentUserId();
        string GetAuthToken();
    }

    public class UserService : IUserService
    {
        private readonly Networking.AuthService _authService;

        public UserService(Networking.AuthService authService = null)
        {
            _authService = authService ?? new Networking.AuthService();
        }

        public async Task<bool> Login(string email, string password)
        {
            return await _authService.Login(email, password);
        }

        public async Task<bool> Register(string email, string username, string password)
        {
            return await _authService.Register(email, username, password);
        }

        public void Logout()
        {
            _authService.Logout();
            GameLogger.Info("User", "Logged out");
            GameEvents.RaiseDisconnected();
        }

        public string GetCurrentUserId() => _authService.CurrentPlayerId;
        public string GetAuthToken() => _authService.CurrentToken;
    }
}
