using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    public class DragGhost3D : MonoBehaviour
    {
        [Header("Positioning")]
        public float cameraDistance = 1.5f;

        [Header("Velocity Tilt")]
        public bool enableVelocityTilt = false;
        public float velocitySensitivity = 5f;
        public float maxTiltAmount = 30f;
        public float rotationSmoothing = 0.15f;

        private Vector3 _targetPosition;
        private Vector3 _lastPosition;
        private Vector3 _currentTiltRotation;
        private Card3DView _cardView;
        private Vector3 _pivotOffset;   // transform.position - visual bounds centre, so the card centres on the cursor
        private bool _pivotCaptured;

        private void Awake()
        {
            _cardView = GetComponent<Card3DView>() ?? GetComponentInChildren<Card3DView>(true);
        }

        // The card's visual is offset from its transform origin (it would hang from the top of the
        // cursor). Capture that offset once so we can centre the card on the cursor instead.
        private void EnsurePivotOffset()
        {
            if (_pivotCaptured)
            {
                return;
            }
            _pivotCaptured = true;
            Bounds? bounds = null;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }
                if (bounds == null) bounds = renderer.bounds;
                else { var b = bounds.Value; b.Encapsulate(renderer.bounds); bounds = b; }
            }
            _pivotOffset = bounds.HasValue ? transform.position - bounds.Value.center : Vector3.zero;
            _pivotOffset.z = 0f; // keep the configured ghost Z
        }

        private void Update()
        {
            transform.position = _targetPosition + _pivotOffset;

            if (!enableVelocityTilt)
            {
                transform.rotation = Quaternion.identity;
                _lastPosition = _targetPosition;
                if (_cardView != null)
                {
                    _cardView.SetStatsOverlayRotation(Quaternion.identity);
                }

                return;
            }

            // Calculate velocity
            Vector3 velocity = (_targetPosition - _lastPosition) / Time.deltaTime;
            _lastPosition = _targetPosition;

            // Calculate tilt based on velocity direction
            // Moving up (Y+) -> rotate back (X-)
            // Moving right (X+) -> rotate right (Z+)
            float tiltX = Mathf.Clamp(-velocity.y * velocitySensitivity, -maxTiltAmount, maxTiltAmount);
            float tiltZ = Mathf.Clamp(velocity.x * velocitySensitivity, -maxTiltAmount, maxTiltAmount);
            Vector3 targetTilt = new Vector3(tiltX, 0, tiltZ);

            // Smooth rotation transition
            _currentTiltRotation = Vector3.Lerp(_currentTiltRotation, targetTilt, rotationSmoothing);
            transform.rotation = Quaternion.Euler(_currentTiltRotation);

            if (_cardView != null)
            {
                // Keep the stat numbers glued to their sockets (inherit the ghost's velocity tilt)
                // instead of counter-rotating, which slid them off the corners ("se cambia la carta").
                _cardView.SetStatsOverlayRotation(Quaternion.identity);
            }
        }

        public void SetTargetPosition(Vector3 screenPos, Camera cam)
        {
            EnsurePivotOffset();
            if (cam == null)
            {
                cam = Camera.main;
            }

            var ray = cam.ScreenPointToRay(screenPos);

            var ghostZ = -5f;
            var distanceToPlane = (ghostZ - cam.transform.position.z) / (ray.direction.z != 0 ? ray.direction.z : 0.001f);
            var point = ray.GetPoint(Mathf.Max(0.1f, distanceToPlane));

            _targetPosition = new Vector3(point.x, point.y, ghostZ);

            if (_lastPosition == Vector3.zero)
                _lastPosition = _targetPosition;

            // Snap immediately so the ghost never shows a frame at its spawn position before Update runs.
            transform.position = _targetPosition + _pivotOffset;
        }
    }
}
