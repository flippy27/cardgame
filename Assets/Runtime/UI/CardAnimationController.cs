using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Centralized animation system for card widgets.
    /// - Handles all card movements (preview, placement, displacement)
    /// - Interrupts previous animations gracefully
    /// - Tracks animation state per card
    /// - No reparenting, position-only animations
    /// </summary>
    public class CardAnimationController : MonoBehaviour
    {
        private class AnimationState
        {
            public CardViewWidget card;
            public Coroutine coroutine;
            public Vector3 targetPos;
            public float startTime;
            public float duration;
            public Action onComplete;
        }

        private Dictionary<CardViewWidget, AnimationState> _activeAnimations = new();
        private MonoBehaviour _coroutineHost;

        public void Initialize(MonoBehaviour host)
        {
            _coroutineHost = host;
        }

        /// <summary>
        /// Animate a card to target anchor. Reparents first, then animates smoothly.
        /// Notifies the target slot to claim ownership of the widget.
        /// </summary>
        public void AnimateToAnchor(CardViewWidget card, RectTransform targetAnchor, float duration, Action onComplete = null, BoardSlotButton targetSlot = null)
        {
            if (card == null || targetAnchor == null)
                return;

            // Cancel previous animation
            if (_activeAnimations.TryGetValue(card, out var existing))
            {
                if (existing.coroutine != null)
                {
                    _coroutineHost.StopCoroutine(existing.coroutine);
                }
                _activeAnimations.Remove(card);
            }

            var cardRect = card.transform as RectTransform;
            if (cardRect == null)
                return;

            var fromPos = cardRect.position;

            // Reparent to target anchor (preserving world position)
            cardRect.SetParent(targetAnchor, worldPositionStays: true);

            // Notify target slot to claim this widget
            if (targetSlot != null)
            {
                targetSlot.ClaimWidget(card);
            }

            // Target is centered in anchor
            var toPos = targetAnchor.position;

            var state = new AnimationState
            {
                card = card,
                targetPos = toPos,
                startTime = Time.time,
                duration = duration,
                onComplete = onComplete
            };

            state.coroutine = _coroutineHost.StartCoroutine(AnimateRoutine(state, fromPos, toPos));
            _activeAnimations[card] = state;
        }

        /// <summary>
        /// Animate a card to world position (without reparenting).
        /// </summary>
        public void AnimateToPosition(CardViewWidget card, Vector3 targetWorldPos, float duration, Action onComplete = null)
        {
            if (card == null)
                return;

            // Cancel previous animation
            if (_activeAnimations.TryGetValue(card, out var existing))
            {
                if (existing.coroutine != null)
                {
                    _coroutineHost.StopCoroutine(existing.coroutine);
                }
                _activeAnimations.Remove(card);
            }

            var cardRect = card.transform as RectTransform;
            if (cardRect == null)
                return;

            var fromPos = cardRect.position;
            var state = new AnimationState
            {
                card = card,
                targetPos = targetWorldPos,
                startTime = Time.time,
                duration = duration,
                onComplete = onComplete
            };

            state.coroutine = _coroutineHost.StartCoroutine(AnimateRoutine(state, fromPos, targetWorldPos));
            _activeAnimations[card] = state;
        }

        /// <summary>
        /// Stop animation and snap card to target position immediately.
        /// </summary>
        public void SnapToPosition(CardViewWidget card, Vector3 worldPos)
        {
            if (card == null)
                return;

            if (_activeAnimations.TryGetValue(card, out var state))
            {
                if (state.coroutine != null)
                {
                    _coroutineHost.StopCoroutine(state.coroutine);
                }
                _activeAnimations.Remove(card);
            }

            var cardRect = card.transform as RectTransform;
            if (cardRect != null)
            {
                cardRect.position = worldPos;
            }
        }

        /// <summary>
        /// Cancel all animations.
        /// </summary>
        public void CancelAll()
        {
            foreach (var state in _activeAnimations.Values)
            {
                if (state.coroutine != null)
                {
                    _coroutineHost.StopCoroutine(state.coroutine);
                }
            }
            _activeAnimations.Clear();
        }

        private IEnumerator AnimateRoutine(AnimationState state, Vector3 fromPos, Vector3 toPos)
        {
            while (state.startTime + state.duration > Time.time && state.card != null)
            {
                var elapsed = Time.time - state.startTime;
                var t = Mathf.Clamp01(elapsed / state.duration);
                t = Mathf.SmoothStep(0, 1, t);

                var cardRect = state.card.transform as RectTransform;
                if (cardRect != null)
                {
                    cardRect.position = Vector3.Lerp(fromPos, toPos, t);
                }

                yield return null;
            }

            // Final position
            if (state.card != null)
            {
                var cardRect = state.card.transform as RectTransform;
                if (cardRect != null)
                {
                    cardRect.position = toPos;
                }
            }

            state.onComplete?.Invoke();
            _activeAnimations.Remove(state.card);
        }
    }
}
