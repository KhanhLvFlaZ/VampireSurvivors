using System.Collections.Generic;
using UnityEngine;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Manages camera following for a specific player
    /// Handles smooth following and world bounds
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

        [Header("Group Follow")]
        [SerializeField] private bool useGroupFollow = true;
        [SerializeField] private float padding = 2f;

        [Header("Bounds")]
        [SerializeField] private bool enforceBounds = false;
        [SerializeField] private Vector2 boundMin = Vector2.zero;
        [SerializeField] private Vector2 boundMax = new Vector2(100, 100);

        private Camera attachedCamera;
        private Vector3 targetPosition;
        private readonly List<Transform> targets = new List<Transform>();

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            bool hasGroup = useGroupFollow && targets.Count > 0;

            if (followTarget == null && !hasGroup)
                return;

            if (hasGroup)
            {
                targetPosition = UpdateGroupTarget();
            }
            else
            {
                targetPosition = followTarget.position + offset;
            }

            // Apply bounds if enabled
            if (enforceBounds && attachedCamera.orthographic)
            {
                float cameraHeight = attachedCamera.orthographicSize;
                float cameraWidth = cameraHeight * attachedCamera.aspect;

                targetPosition.x = Mathf.Clamp(targetPosition.x, boundMin.x + cameraWidth, boundMax.x - cameraWidth);
                targetPosition.y = Mathf.Clamp(targetPosition.y, boundMin.y + cameraHeight, boundMax.y - cameraHeight);
            }

            // Smooth follow
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        }

        public void UpdateTarget(Transform newTarget)
        {
            followTarget = newTarget;
        }

        public void RegisterTarget(Transform newTarget)
        {
            if (newTarget == null)
                return;

            if (!targets.Contains(newTarget))
            {
                targets.Add(newTarget);
            }

            if (useGroupFollow)
            {
                UpdateGroupTarget();
            }
        }

        private Vector3 UpdateGroupTarget()
        {
            if (targets.Count == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            foreach (var t in targets)
            {
                if (t != null)
                {
                    sum += t.position;
                }
            }

            var centroid = sum / Mathf.Max(1, targets.Count);
            targetPosition = centroid + offset;

            if (attachedCamera != null && attachedCamera.orthographic && targets.Count > 1)
            {
                float maxDistance = 0f;
                foreach (var t in targets)
                {
                    if (t != null)
                    {
                        maxDistance = Mathf.Max(maxDistance, Vector2.Distance(centroid, t.position));
                    }
                }

                float desiredSize = maxDistance + padding;
                attachedCamera.orthographicSize = Mathf.Lerp(attachedCamera.orthographicSize, desiredSize, Time.deltaTime * smoothSpeed);
            }

            return targetPosition;
        }

        public void SetBounds(Vector2 min, Vector2 max)
        {
            boundMin = min;
            boundMax = max;
            enforceBounds = true;
        }
    }
}
