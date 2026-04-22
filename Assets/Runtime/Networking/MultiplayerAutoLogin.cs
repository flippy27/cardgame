using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.UI;

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

            var playerInstanceName = ResolvePlayerInstanceName();
            GameLogger.Info("MultiplayerAutoLogin", $"Instance '{playerInstanceName}' starting auto-login");

            var autoLoginGo = new GameObject("MultiplayerAutoLogin");
            var autoLogin = autoLoginGo.AddComponent<MultiplayerAutoLogin>();
            autoLogin.StartAutoLogin(playerInstanceName);
        }

        private async void StartAutoLogin(string instanceName)
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

            ResetLocalSessionState(instanceName);

            // Auto-login con el usuario correspondiente a esta instancia
            var email = $"player{GetPlayerName(instanceName)}@flippy.com";
            var password = "123456";

            GameLogger.Info("MultiplayerAutoLogin", $"Instance '{instanceName}' logging in as {email}");

            var success = await GameService.Instance.Login(email, password);

            if (success)
            {
                GameLogger.Info("MultiplayerAutoLogin", $"Instance '{instanceName}' logged in successfully");
            }
            else
            {
                GameLogger.Error("MultiplayerAutoLogin", $"Instance '{instanceName}' login failed");
            }

            Destroy(gameObject);
        }

        private static void ResetLocalSessionState(string instanceName)
        {
            try
            {
                var previousPlayerId = GameService.Instance.AuthService?.CurrentPlayerId;
                if (!string.IsNullOrWhiteSpace(previousPlayerId))
                {
                    GameLogger.Warning("MultiplayerAutoLogin", $"Instance '{instanceName}' clearing stale session for player '{previousPlayerId}' before auto-login.");
                }

                GameService.Instance.Logout();
                GamePlayStateManager.Instance?.Reset();
                MatchStateMachine.EndMatch();
            }
            catch (Exception ex)
            {
                GameLogger.Warning("MultiplayerAutoLogin", $"Instance '{instanceName}' could not fully reset state before auto-login: {ex.Message}");
            }
        }

        private static string ResolvePlayerInstanceName()
        {
            var arguments = Environment.GetCommandLineArgs();
            var nameIndex = Array.IndexOf(arguments, "-name");
            if (nameIndex >= 0 && nameIndex + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[nameIndex + 1]))
            {
                return arguments[nameIndex + 1];
            }

            GameLogger.Warning("MultiplayerAutoLogin", $"No '-name' launch argument found. Arguments: {string.Join(" ", arguments)}");
            return "Player1";
        }

        private static string GetPlayerName(string instanceName)
        {
            var playerNumber = ExtractPlayerNumber(instanceName);
            return playerNumber switch
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
                _ => throw new InvalidOperationException($"Instance '{instanceName}' not mapped. Add more cases in GetPlayerName().")
            };
        }

        private static int ExtractPlayerNumber(string instanceName)
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                return 1;
            }

            var match = Regex.Match(instanceName, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var playerNumber))
            {
                return playerNumber;
            }

            return instanceName.Trim().Equals("Player", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        }
    }
}
