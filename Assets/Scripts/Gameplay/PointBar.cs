using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    public class PointBar : MonoBehaviour
    {
        [SerializeField] protected RectTransform barBackground, barFill;
        [SerializeField] protected UnityEvent onEmpty, onFull;

        protected float currentPoints, minPoints, maxPoints;
        protected bool clamp;
        protected float barWidth; // Store original width for scaling

        public float CurrentPoints { get => currentPoints; set => currentPoints = value; }

        public void Setup(float currentPoints, float minPoints, float maxPoints, bool clamp = true)
        {
            this.currentPoints = currentPoints;
            this.minPoints = minPoints;
            this.maxPoints = maxPoints;
            this.clamp = clamp;

            // Ensure barFill has proper anchors for rectangular bar (left-aligned)
            if (barFill != null)
            {
                barFill.anchorMin = new Vector2(0, 0.5f);  // Left, middle
                barFill.anchorMax = new Vector2(0, 0.5f);  // Left, middle
                barFill.pivot = new Vector2(0, 0.5f);      // Left, middle
            }

            // Store the original bar width for scaling calculations
            if (barBackground != null)
                barWidth = barBackground.rect.width;

            UpdateDisplay();
        }

        public void AddPoints(float points)
        {
            currentPoints += points;
            CheckPoints();
            UpdateDisplay();
        }

        public void SubtractPoints(float points)
        {
            currentPoints -= points;
            CheckPoints();
            UpdateDisplay();
        }

        public void SetPoints(float points)
        {
            currentPoints = points;
            CheckPoints();
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (barBackground == null || barFill == null) return;

            float fillRatio = (currentPoints - minPoints) / (maxPoints - minPoints);
            fillRatio = Mathf.Clamp01(fillRatio);

            // Simple rectangular bar: just adjust width, keep left-aligned
            float bgWidth = barBackground.rect.width;
            barFill.sizeDelta = new Vector2(bgWidth * fillRatio, barFill.sizeDelta.y);
            barFill.anchoredPosition = new Vector2(0, barFill.anchoredPosition.y);
        }

        private void CheckPoints()
        {
            if (currentPoints >= maxPoints)
            {
                onFull.Invoke();
                if (clamp)
                    currentPoints = maxPoints;
            }
            else if (currentPoints <= minPoints)
            {
                onEmpty.Invoke();
                if (clamp)
                    currentPoints = minPoints;
            }
        }
    }
}