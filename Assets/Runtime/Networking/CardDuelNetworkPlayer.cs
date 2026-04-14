using System.Linq;
using Unity.Netcode;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Representa al jugador conectado en NGO.
    /// </summary>
    public sealed class CardDuelNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<int> PlayerIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PlayerIndex.Value = NetworkManager.Singleton.ConnectedClientsIds.ToList().IndexOf(OwnerClientId);
            }
        }
    }
}
