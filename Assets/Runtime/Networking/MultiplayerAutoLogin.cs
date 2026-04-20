using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Auto-login para Multiplayer Play Mode.
    /// Cada instancia se loguea como jugador diferente.
    /// Instancia 1 → playerone@flippy.com
    /// Instancia 2 → playertwo@flippy.com
    /// Etc.
    ///
    /// Detecta instancia por: tiempo de creación del AppDomain (cada instancia tiene uno único).
    /// </summary>
    public class MultiplayerAutoLogin : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoLogin()
        {
            if (!Application.isEditor)
                return;

            var domainId = System.AppDomain.CurrentDomain.Id;
            GameLogger.Info("MultiplayerAutoLogin", $"AppDomain {domainId} starting auto-login");

            var autoLoginGo = new GameObject("MultiplayerAutoLogin");
            var autoLogin = autoLoginGo.AddComponent<MultiplayerAutoLogin>();
            autoLogin.StartAutoLogin(domainId);
        }

        private async void StartAutoLogin(int instanceId)
        {
            // Espera a que GameService esté listo
            int retries = 0;
            while (GameService.Instance == null && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            if (GameService.Instance == null)
            {
                Debug.LogError("[AutoLogin] GameService not available");
                Destroy(gameObject);
                return;
            }

            // Auto-login con el usuario correspondiente a esta instancia
            var email = $"player{GetPlayerName(instanceId)}@flippy.com";
            var password = "123456";

            GameLogger.Info("MultiplayerAutoLogin", $"Instance {instanceId} logging in as {email}");

            var success = await GameService.Instance.Login(email, password);

            if (success)
            {
                GameLogger.Info("MultiplayerAutoLogin", $"Instance {instanceId} logged in successfully");
            }
            else
            {
                GameLogger.Error("MultiplayerAutoLogin", $"Instance {instanceId} login failed");
            }

            Destroy(gameObject);
        }

        private static string GetPlayerName(int domainId)
        {
            // AppDomain mapping (each AppDomain is a separate player):
            // AppDomain 1 → playerone@flippy.com
            // AppDomain 2 → playertwo@flippy.com
            // AppDomain 3 → playerthree@flippy.com
            // Etc.

            return domainId switch
            {
                1 => "one",
                2 => "two",
                3 => "three",
                4 => "four",
                5 => "five",
                6 => "six",
                7 => "seven",
                8 => "eight",
                9 => "nine",
                10 => "ten",
                _ => throw new System.InvalidOperationException($"AppDomain {domainId} not mapped. Add more cases in GetPlayerName().")
            };
        }
    }
}
