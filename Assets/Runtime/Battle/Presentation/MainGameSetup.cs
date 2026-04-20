using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Configura automáticamente la escena MainGame con todos los componentes 3D.
    /// Se ejecuta una sola vez al inicio.
    /// </summary>
    public class MainGameSetup : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[MainGameSetup] Configurando escena MainGame...");

            var mainCam = Camera.main;

            // Buscar o crear GameplayPresenter3D
            var presenter = FindFirstObjectByType<GameplayPresenter3D>();
            if (presenter == null)
            {
                var presenterGo = new GameObject("GameplayPresenter3D");
                presenter = presenterGo.AddComponent<GameplayPresenter3D>();
                Debug.Log("[MainGameSetup] ✓ GameplayPresenter3D creado");
            }

            // Agregar DragHandler3D a la cámara
            if (mainCam != null && mainCam.GetComponent<DragHandler3D>() == null)
            {
                var dragHandler = mainCam.gameObject.AddComponent<DragHandler3D>();
                dragHandler.enabled = true;
                Debug.Log("[MainGameSetup] ✓ DragHandler3D agregado a cámara");
            }

            // Buscar o crear EndTurnButton3D en el Canvas
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var endTurnBtn = FindFirstObjectByType<EndTurnButton3D>();
                if (endTurnBtn == null)
                {
                    var btnGo = new GameObject("EndTurnButton3D");
                    btnGo.transform.SetParent(canvas.transform);
                    var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
                    var txt = btnGo.AddComponent<TMPro.TextMeshProUGUI>();
                    txt.text = "END TURN";
                    var endTurn = btnGo.AddComponent<EndTurnButton3D>();
                    Debug.Log("[MainGameSetup] ✓ EndTurnButton3D creado");
                }
            }

            // Buscar o crear HUD3D
            var hud = FindFirstObjectByType<HUD3D>();
            if (hud == null && canvas != null)
            {
                var hudGo = new GameObject("HUD3D");
                hudGo.transform.SetParent(canvas.transform);
                hud = hudGo.AddComponent<HUD3D>();
                Debug.Log("[MainGameSetup] ✓ HUD3D creado");
            }

            Debug.Log("[MainGameSetup] ✓ Configuración completada");
        }
    }
}
