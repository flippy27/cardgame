using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    [Serializable]
    public sealed class AttackMotionPreset
    {
        public string label = "Motion";

        [Header("Attacker")]
        public float windupDuration = 0.1f;
        public float lungeDuration = 0.18f;
        public float returnDuration = 0.18f;
        public float lungeLift = 0.16f;
        public float attackerLungeDistance = 0.42f;
        public float meleeReachFactor = 0.62f;
        public float rangedLungeMultiplier = 0.58f;
        public AnimationCurve lungeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Projectile")]
        public float projectileDuration = 0.36f;
        public float projectileLaunchHeight = 0.42f;
        public float projectileImpactHeight = 0.28f;
        public float arcHeight = 0.42f;
        public float projectileScale = 0.22f;
        public AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Color projectileTint = new(1f, 0.85f, 0.2f, 1f);

        [Header("Impact Feel")]
        public float impactHitStopDuration = 0.035f;
        public float defenderRecoilDistance = 0.16f;
        public float defenderRecoilDuration = 0.16f;
        public float defenderSquashAmount = 0.12f;
        public float postHitDelay = 0.09f;
        public float slotPulseIntensity = 1.2f;
        public float slotPulseDuration = 0.18f;
        public Color slotPulseColor = new(1f, 0.45f, 0.15f, 1f);
    }

    public class AttackEffectSystem : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject attackProjectilePrefab;
        [SerializeField] private GameObject damagePopupPrefab;

        [Header("Scene References")]
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private Transform damagePopupRoot;
        [SerializeField] private BattleCameraShake cameraShake;

        [Header("Timings")]
        [SerializeField] private float betweenEventsDelay = 0.22f;
        [SerializeField] private float deathPause = 0.38f;
        [SerializeField] private float hitFlashDuration = 0.14f;

        [Header("Motion Presets (1-5)")]
        [SerializeField] private AttackMotionPreset[] motionPresets = new AttackMotionPreset[5];

        [Header("Damage Popup")]
        [SerializeField] private float damagePopupHeightOffset = 0.4f;
        [SerializeField] private Color damageColor = new(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color poisonDamageColor = new(0.65f, 1f, 0.45f, 1f);

        public static AttackEffectSystem Instance { get; private set; }
        public float BetweenEventsDelay => betweenEventsDelay;
        public float DeathPause => deathPause;

        private void Awake()
        {
            Instance = this;
            EnsureDefaults();
            EnsureSceneReferences();
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }

        public bool IsRangedAttack(ICardDisplay attacker)
        {
            if (attacker?.CardData == null)
            {
                return false;
            }

            return AttackPresentationResolver.UsesProjectile(attacker.CardData);
        }

        public IEnumerator PlayCardAttack(
            ICardDisplay attacker,
            ICardDisplay defender,
            Board3DSlot impactedSlot,
            int damage,
            int motionLevel,
            int shakeLevel)
        {
            if (!attacker.TryGetTransform(out var attackerTransform))
            {
                yield break;
            }

            var targetPosition = ResolveImpactPosition(defender, impactedSlot, attackerTransform.position);
            yield return PlayAttackSequence(
                attackerTransform,
                defender,
                impactedSlot,
                targetPosition,
                damage,
                motionLevel,
                shakeLevel,
                IsRangedAttack(attacker));
        }

        public IEnumerator PlayHeroAttack(
            ICardDisplay attacker,
            Board3DSlot impactedSlot,
            Vector3 targetPosition,
            int damage,
            int motionLevel,
            int shakeLevel)
        {
            if (!attacker.TryGetTransform(out var attackerTransform))
            {
                yield break;
            }

            yield return PlayAttackSequence(
                attackerTransform,
                null,
                impactedSlot,
                targetPosition,
                damage,
                motionLevel,
                shakeLevel,
                IsRangedAttack(attacker));
        }

        public IEnumerator PlayDamagePopup(Vector3 worldPosition, int amount, bool isPoison = false)
        {
            var popup = InstantiateDamagePopup(worldPosition + Vector3.up * damagePopupHeightOffset);
            if (popup != null)
            {
                popup.Play(amount.ToString(), isPoison ? poisonDamageColor : damageColor);
            }

            yield return null;
        }

        public IEnumerator FlashCard(ICardDisplay card)
        {
            if (card == null)
            {
                yield break;
            }

            card.SetColor(Color.red);
            yield return new WaitForSeconds(hitFlashDuration);
            if (card != null)
            {
                card.ResetColor();
            }
        }

        public GameObject CreateFallbackProjectile()
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectile.name = "AttackProjectile_Fallback";
            projectile.transform.localScale = Vector3.one * 0.22f;

            var renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = new Color(1f, 0.85f, 0.2f, 1f);
                renderer.material = material;
            }

            var collider = projectile.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return projectile;
        }

        private IEnumerator PlayAttackSequence(
            Transform attackerTransform,
            ICardDisplay defender,
            Board3DSlot impactedSlot,
            Vector3 targetPosition,
            int damage,
            int motionLevel,
            int shakeLevel,
            bool useProjectile)
        {
            if (attackerTransform == null)
            {
                yield break;
            }

            EnsureSceneReferences();
            var motion = GetMotionPreset(motionLevel);
            var attackerOrigin = attackerTransform.position;
            var impactPosition = targetPosition + Vector3.up * motion.projectileImpactHeight;
            var attackDirection = (impactPosition - attackerOrigin);
            if (attackDirection.sqrMagnitude < 0.0001f)
            {
                attackDirection = Vector3.forward;
            }

            attackDirection.Normalize();

            var strikeDistance = useProjectile
                ? motion.attackerLungeDistance * motion.rangedLungeMultiplier
                : Mathf.Min(Vector3.Distance(attackerOrigin, targetPosition) * motion.meleeReachFactor, motion.attackerLungeDistance);

            var lungeTarget = attackerOrigin + attackDirection * strikeDistance;

            yield return AnimateAttackerForward(attackerTransform, attackerOrigin, lungeTarget, motion);

            if (useProjectile)
            {
                var projectileStart = attackerTransform.position + Vector3.up * motion.projectileLaunchHeight;
                yield return AnimateProjectile(projectileStart, impactPosition, motion);
            }

            yield return PlayImpactFeedback(defender, impactedSlot, attackDirection, motion, shakeLevel, damage);
            yield return AnimateAttackerReturn(attackerTransform, attackerOrigin, motion);
        }

        private Vector3 ResolveImpactPosition(ICardDisplay defender, Board3DSlot impactedSlot, Vector3 fallbackPosition)
        {
            if (defender != null && defender.TryGetTransform(out var defenderTransform))
            {
                return defenderTransform.position;
            }

            if (impactedSlot != null)
            {
                return impactedSlot.transform.position;
            }

            return fallbackPosition;
        }

        private IEnumerator AnimateAttackerForward(
            Transform attackerTransform,
            Vector3 startPosition,
            Vector3 lungeTarget,
            AttackMotionPreset motion)
        {
            if (attackerTransform == null)
            {
                yield break;
            }

            if (motion.windupDuration > 0f)
            {
                yield return AnimateTransformPosition(
                    attackerTransform,
                    startPosition,
                    startPosition - (lungeTarget - startPosition).normalized * 0.05f,
                    motion.windupDuration,
                    motion.lungeCurve,
                    motion.lungeLift * 0.15f);
            }

            yield return AnimateTransformPosition(
                attackerTransform,
                attackerTransform.position,
                lungeTarget,
                motion.lungeDuration,
                motion.lungeCurve,
                motion.lungeLift);
        }

        private IEnumerator AnimateAttackerReturn(
            Transform attackerTransform,
            Vector3 returnPosition,
            AttackMotionPreset motion)
        {
            if (attackerTransform == null)
            {
                yield break;
            }

            yield return AnimateTransformPosition(
                attackerTransform,
                attackerTransform.position,
                returnPosition,
                motion.returnDuration,
                motion.returnCurve,
                0f);
        }

        private IEnumerator AnimateTransformPosition(
            Transform targetTransform,
            Vector3 startPosition,
            Vector3 endPosition,
            float duration,
            AnimationCurve curve,
            float liftAmount)
        {
            if (targetTransform == null || duration <= 0f)
            {
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (targetTransform == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var progress = curve != null ? curve.Evaluate(normalized) : normalized;

                var position = Vector3.Lerp(startPosition, endPosition, progress);
                position.y += Mathf.Sin(normalized * Mathf.PI) * liftAmount;
                targetTransform.position = position;
                yield return null;
            }

            if (targetTransform != null)
            {
                targetTransform.position = endPosition;
            }
        }

        private IEnumerator AnimateProjectile(Vector3 startPosition, Vector3 endPosition, AttackMotionPreset motion)
        {
            var projectile = InstantiateProjectile(motion);
            if (projectile == null)
            {
                yield break;
            }

            var transformToMove = projectile.transform;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, motion.projectileDuration);

            while (elapsed < duration)
            {
                if (transformToMove == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var progress = motion.travelCurve != null ? motion.travelCurve.Evaluate(normalized) : normalized;
                var position = Vector3.Lerp(startPosition, endPosition, progress);
                position.y += Mathf.Sin(progress * Mathf.PI) * motion.arcHeight;

                transformToMove.position = position;
                transformToMove.localScale = Vector3.one * motion.projectileScale;
                yield return null;
            }

            Destroy(projectile);
        }

        private IEnumerator PlayImpactFeedback(
            ICardDisplay defender,
            Board3DSlot impactedSlot,
            Vector3 attackDirection,
            AttackMotionPreset motion,
            int shakeLevel,
            int damage)
        {
            impactedSlot?.PulseImpact(motion.slotPulseColor, motion.slotPulseIntensity, motion.slotPulseDuration);

            if (shakeLevel > 0)
            {
                cameraShake?.PlayLevel(shakeLevel);
            }

            if (motion.impactHitStopDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(motion.impactHitStopDuration);
            }

            if (defender != null && defender.TryGetTransform(out var defenderTransform))
            {
                yield return AnimateDefenderRecoil(defenderTransform, attackDirection, motion);
            }

            if (motion.postHitDelay > 0f)
            {
                yield return new WaitForSeconds(motion.postHitDelay);
            }
        }

        private IEnumerator AnimateDefenderRecoil(Transform defenderTransform, Vector3 attackDirection, AttackMotionPreset motion)
        {
            if (defenderTransform == null || motion.defenderRecoilDuration <= 0f)
            {
                yield break;
            }

            var startPosition = defenderTransform.position;
            var startScale = defenderTransform.localScale;
            var recoilDirection = attackDirection.sqrMagnitude > 0.0001f ? attackDirection.normalized : Vector3.forward;
            var recoilTarget = startPosition + recoilDirection * motion.defenderRecoilDistance;
            var squashScale = new Vector3(
                startScale.x + motion.defenderSquashAmount,
                Mathf.Max(0.25f, startScale.y - motion.defenderSquashAmount),
                startScale.z + motion.defenderSquashAmount);

            var halfDuration = motion.defenderRecoilDuration * 0.5f;
            var elapsed = 0f;

            while (elapsed < halfDuration)
            {
                if (defenderTransform == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                defenderTransform.position = Vector3.Lerp(startPosition, recoilTarget, t);
                defenderTransform.localScale = Vector3.Lerp(startScale, squashScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (defenderTransform == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                defenderTransform.position = Vector3.Lerp(recoilTarget, startPosition, t);
                defenderTransform.localScale = Vector3.Lerp(squashScale, startScale, t);
                yield return null;
            }

            if (defenderTransform != null)
            {
                defenderTransform.position = startPosition;
                defenderTransform.localScale = startScale;
            }
        }

        private GameObject InstantiateProjectile(AttackMotionPreset motion)
        {
            EnsureSceneReferences();

            var projectile = attackProjectilePrefab != null
                ? Instantiate(attackProjectilePrefab, projectileRoot)
                : CreateFallbackProjectile();

            if (projectile == null)
            {
                return null;
            }

            if (projectileRoot != null && projectile.transform.parent != projectileRoot)
            {
                projectile.transform.SetParent(projectileRoot, worldPositionStays: true);
            }

            ApplyProjectileTint(projectile, motion.projectileTint);
            return projectile;
        }

        private DamagePopup3D InstantiateDamagePopup(Vector3 position)
        {
            EnsureSceneReferences();

            GameObject popupGo;
            if (damagePopupPrefab != null)
            {
                popupGo = Instantiate(damagePopupPrefab, position, Quaternion.identity, damagePopupRoot);
            }
            else
            {
                popupGo = CreateFallbackDamagePopup(position);
            }

            return popupGo != null ? popupGo.GetComponent<DamagePopup3D>() : null;
        }

        private GameObject CreateFallbackDamagePopup(Vector3 position)
        {
            var popupGo = new GameObject("DamagePopup_Fallback");
            popupGo.transform.position = position;
            if (damagePopupRoot != null)
            {
                popupGo.transform.SetParent(damagePopupRoot, worldPositionStays: true);
            }

            var text = popupGo.AddComponent<TextMeshPro>();
            text.fontSize = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = damageColor;

            popupGo.AddComponent<DamagePopup3D>();
            return popupGo;
        }

        private AttackMotionPreset GetMotionPreset(int level)
        {
            EnsureDefaults();
            var index = Mathf.Clamp(level - 1, 0, motionPresets.Length - 1);
            return motionPresets[index];
        }

        private void ApplyProjectileTint(GameObject projectile, Color tint)
        {
            var renderers = projectile.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var material = renderer.material;
                material.color = tint;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", tint * 0.6f);
                }
            }
        }

        private void EnsureSceneReferences()
        {
            if (projectileRoot == null)
            {
                projectileRoot = transform;
            }

            if (damagePopupRoot == null)
            {
                damagePopupRoot = transform;
            }

            if (cameraShake == null && Camera.main != null)
            {
                cameraShake = Camera.main.GetComponent<BattleCameraShake>();
                if (cameraShake == null)
                {
                    cameraShake = Camera.main.gameObject.AddComponent<BattleCameraShake>();
                }
            }
        }

        private void EnsureDefaults()
        {
            if (motionPresets == null || motionPresets.Length != 5)
            {
                Array.Resize(ref motionPresets, 5);
            }

            for (var index = 0; index < motionPresets.Length; index++)
            {
                motionPresets[index] ??= CreateDefaultMotionPreset(index);
            }
        }

        private static AttackMotionPreset CreateDefaultMotionPreset(int index)
        {
            return index switch
            {
                0 => new AttackMotionPreset
                {
                    label = "Level 1",
                    windupDuration = 0.08f,
                    lungeDuration = 0.16f,
                    returnDuration = 0.14f,
                    lungeLift = 0.1f,
                    attackerLungeDistance = 0.28f,
                    meleeReachFactor = 0.52f,
                    rangedLungeMultiplier = 0.45f,
                    projectileDuration = 0.28f,
                    arcHeight = 0.14f,
                    projectileScale = 0.14f,
                    impactHitStopDuration = 0.018f,
                    defenderRecoilDistance = 0.08f,
                    defenderRecoilDuration = 0.1f,
                    defenderSquashAmount = 0.05f,
                    postHitDelay = 0.06f,
                    slotPulseIntensity = 0.6f,
                    slotPulseDuration = 0.12f,
                    travelCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0.15f, 0.12f),
                        new Keyframe(1f, 1f, 1.2f, 0f))
                },
                1 => new AttackMotionPreset
                {
                    label = "Level 2",
                    windupDuration = 0.1f,
                    lungeDuration = 0.2f,
                    returnDuration = 0.16f,
                    lungeLift = 0.14f,
                    attackerLungeDistance = 0.34f,
                    meleeReachFactor = 0.56f,
                    rangedLungeMultiplier = 0.5f,
                    projectileDuration = 0.34f,
                    arcHeight = 0.22f,
                    projectileScale = 0.17f,
                    impactHitStopDuration = 0.026f,
                    defenderRecoilDistance = 0.11f,
                    defenderRecoilDuration = 0.12f,
                    defenderSquashAmount = 0.07f,
                    postHitDelay = 0.075f,
                    slotPulseIntensity = 0.8f,
                    slotPulseDuration = 0.14f,
                    travelCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0.12f, 0.2f),
                        new Keyframe(1f, 1f, 1.8f, 0f))
                },
                2 => new AttackMotionPreset
                {
                    label = "Level 3",
                    windupDuration = 0.12f,
                    lungeDuration = 0.24f,
                    returnDuration = 0.19f,
                    lungeLift = 0.18f,
                    attackerLungeDistance = 0.42f,
                    meleeReachFactor = 0.6f,
                    rangedLungeMultiplier = 0.56f,
                    projectileDuration = 0.4f,
                    arcHeight = 0.34f,
                    projectileScale = 0.2f,
                    impactHitStopDuration = 0.034f,
                    defenderRecoilDistance = 0.15f,
                    defenderRecoilDuration = 0.14f,
                    defenderSquashAmount = 0.1f,
                    postHitDelay = 0.09f,
                    slotPulseIntensity = 1f,
                    slotPulseDuration = 0.16f,
                    travelCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0.08f, 0.28f),
                        new Keyframe(1f, 1f, 2.4f, 0f))
                },
                3 => new AttackMotionPreset
                {
                    label = "Level 4",
                    windupDuration = 0.14f,
                    lungeDuration = 0.28f,
                    returnDuration = 0.22f,
                    lungeLift = 0.24f,
                    attackerLungeDistance = 0.5f,
                    meleeReachFactor = 0.64f,
                    rangedLungeMultiplier = 0.62f,
                    projectileDuration = 0.46f,
                    arcHeight = 0.48f,
                    projectileScale = 0.24f,
                    impactHitStopDuration = 0.045f,
                    defenderRecoilDistance = 0.18f,
                    defenderRecoilDuration = 0.16f,
                    defenderSquashAmount = 0.12f,
                    postHitDelay = 0.11f,
                    slotPulseIntensity = 1.25f,
                    slotPulseDuration = 0.18f,
                    travelCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0.05f, 0.34f),
                        new Keyframe(1f, 1f, 2.8f, 0f))
                },
                _ => new AttackMotionPreset
                {
                    label = "Level 5",
                    windupDuration = 0.16f,
                    lungeDuration = 0.32f,
                    returnDuration = 0.24f,
                    lungeLift = 0.28f,
                    attackerLungeDistance = 0.58f,
                    meleeReachFactor = 0.68f,
                    rangedLungeMultiplier = 0.66f,
                    projectileDuration = 0.52f,
                    arcHeight = 0.64f,
                    projectileScale = 0.28f,
                    impactHitStopDuration = 0.06f,
                    defenderRecoilDistance = 0.22f,
                    defenderRecoilDuration = 0.2f,
                    defenderSquashAmount = 0.15f,
                    postHitDelay = 0.13f,
                    slotPulseIntensity = 1.55f,
                    slotPulseDuration = 0.22f,
                    travelCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0.02f, 0.42f),
                        new Keyframe(1f, 1f, 3.3f, 0f))
                }
            };
        }
    }
}
