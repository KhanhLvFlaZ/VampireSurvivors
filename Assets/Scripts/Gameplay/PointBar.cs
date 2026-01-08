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

        public float CurrentPoints { get => currentPoints; set => currentPoints = value; }

        public void Setup(float currentPoints, float minPoints, float maxPoints, bool clamp = true)
        {
            this.currentPoints = currentPoints;
            this.minPoints = minPoints;
            this.maxPoints = maxPoints;
            this.clamp = clamp;
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
            float fillRatio = (currentPoints - minPoints) / (maxPoints - minPoints);
            float newWidth = barBackground.rect.width * fillRatio;
            barFill.sizeDelta = new Vector2(newWidth, barFill.sizeDelta.y);

            // Center the bar by adjusting position: when bar shrinks, move it to stay centered
            float widthDifference = barBackground.rect.width - newWidth;
            barFill.anchoredPosition = new Vector2(widthDifference / 2f, barFill.anchoredPosition.y);
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