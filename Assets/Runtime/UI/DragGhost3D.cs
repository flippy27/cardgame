using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    public class DragGhost3D : MonoBehaviour
    {
        [Header("Positioning")]
        public float cameraDistance = 1.5f;

        [Header("Velocity Tilt")]
        public float velocitySensitivity = 5f;
        public float maxTiltAmount = 30f;
        public float rotationSmoothing = 0.15f;

        private Vector3 _targetPosition;
        private Vector3 _lastPosition;
        private Vector3 _currentTiltRotation;

        private void Update()
        {
            transform.position = _targetPosition;

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
        }

        public void SetTargetPosition(Vector3 screenPos, Camera cam)
        {
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

            Debug.Log($"[DragGhost3D] Screen: {screenPos}, World: {_targetPosition}");
        }
    }
}
