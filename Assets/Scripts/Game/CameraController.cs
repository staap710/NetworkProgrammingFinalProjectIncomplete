using UnityEngine;

namespace CoinRush.Game
{
    /// <summary>
    /// Smooth-follows two targets, keeping both players in frame.
    /// Attach to the Main Camera in GameScene.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Targets")]
        public Transform targetA; // local player (set by PlayerSpawner)
        public Transform targetB; // remote player (set by PlayerSpawner)

        [Header("Zoom")]
        public float minOrthoSize =  4f;
        public float maxOrthoSize =  9f;
        public float zoomPadding  =  3f;  // extra padding added to the player distance

        [Header("Smoothing")]
        public float positionSmoothing = 4f;
        public float zoomSmoothing     = 3f;

        [Header("Bounds (optional)")]
        public bool  clampPosition = true;
        public float minX = -10f;
        public float maxX =  10f;
        public float minY = -3f;
        public float maxY =  6f;

        private Camera _cam;
        private Vector3 _vel = Vector3.zero;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (targetA == null || targetB == null) return;

            // Midpoint between the two players
            Vector3 mid = (targetA.position + targetB.position) * 0.5f;
            mid.z = transform.position.z;

            // Optional world-space clamp
            if (clampPosition)
                mid = new Vector3(Mathf.Clamp(mid.x, minX, maxX),
                                  Mathf.Clamp(mid.y, minY, maxY),
                                  mid.z);

            transform.position = Vector3.SmoothDamp(transform.position, mid,
                                                    ref _vel, positionSmoothing * Time.deltaTime);

            // Zoom based on player separation
            float dist        = Vector2.Distance(targetA.position, targetB.position);
            float targetSize  = Mathf.Clamp(dist * 0.5f + zoomPadding, minOrthoSize, maxOrthoSize);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize,
                                               zoomSmoothing * Time.deltaTime);
        }
    }
}
