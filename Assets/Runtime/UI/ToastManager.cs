using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        public GameObject toastPrefab;
        public Transform toastContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void ShowToast(string message)
        {
            if (toastPrefab == null)
            {
                Debug.LogError("[ToastManager] toastPrefab is null!");
                return;
            }

            var container = toastContainer ?? transform;
            var toastInstance = Instantiate(toastPrefab, container);
            var toastComponent = toastInstance.GetComponent<Toast>();

            if (toastComponent != null)
            {
                toastComponent.Show(message);
            }
            else
            {
                Debug.LogError("[ToastManager] Toast component not found on prefab!");
                Destroy(toastInstance);
            }
        }
    }
}
