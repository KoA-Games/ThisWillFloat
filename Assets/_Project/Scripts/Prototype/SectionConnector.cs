using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public enum ConnectorType
    {
        LooseStack,
        Rope,
        Nails,
        Bracket
    }

    public class SectionConnector : MonoBehaviour
    {
        [SerializeField] private ConfigurableJoint joint;
        [SerializeField] private ConnectorType connectorType = ConnectorType.Rope;
        [SerializeField] private RaftSection sectionA;
        [SerializeField] private RaftSection sectionB;
        [SerializeField] private float hitPoints = 30f;
        [SerializeField] private float stressMultiplier = 1f;
        [SerializeField] private float warningThreshold = 0.45f;
        [SerializeField] private float breakThreshold = 1f;
        [SerializeField] private bool canBreak = false;
        [SerializeField] private float damagePerSecond = 1.5f;
        [SerializeField] private float warningSecondsBeforeBreak = 1f;
        [SerializeField] private float warningWobbleScale = 0.28f;
        [SerializeField] private float crackMotionForce = 1.1f;
        [SerializeField] private float separationImpulse = 2.4f;
        [SerializeField] private float scriptedMaxSlack = 1.1f;
        [SerializeField] private float scriptedMaxAngularSlack = 30f;
        [SerializeField] private float scriptedSinkDepth = 0.55f;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private Color healthyColor = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private Color warningColor = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.18f, 0.1f);

        private float maxHitPoints;
        private float baseMaxHitPoints;
        private float baseBreakThreshold;
        private float baseWarningSecondsBeforeBreak;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private float warningTimer;
        private float armDelayTimer;
        private float scriptedFailureDelay;
        private float scriptedFailureDuration;
        private Vector3 scriptedStartPositionA;
        private Vector3 scriptedStartPositionB;
        private Quaternion scriptedStartRotationA;
        private Quaternion scriptedStartRotationB;
        private float scriptedSinkTimer;
        private bool breakArmed;
        private bool broken;
        private bool scriptedFailure;
        private bool scriptedMotionStarted;
        private bool scriptedSinkActive;

        public float Stress01 { get; private set; }
        public float WarningProgress => Mathf.Clamp01(warningTimer / Mathf.Max(0.01f, warningSecondsBeforeBreak));
        public bool CanBreak => canBreak;
        public bool BreakArmed => breakArmed;
        public bool IsBroken => broken;

        private void Awake()
        {
            if (joint == null)
            {
                joint = GetComponent<ConfigurableJoint>();
            }

            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            maxHitPoints = Mathf.Max(1f, hitPoints);
            ApplyConnectorPreset();
            baseMaxHitPoints = maxHitPoints;
            baseBreakThreshold = breakThreshold;
            baseWarningSecondsBeforeBreak = warningSecondsBeforeBreak;
            ConfigureJoint();
            UpdateWarningVisual();
        }

        private void FixedUpdate()
        {
            if (sectionA == null || sectionB == null)
            {
                return;
            }

            if (scriptedSinkActive)
            {
                UpdateScriptedSinking();
                return;
            }

            if (broken)
            {
                return;
            }

            if (scriptedFailure)
            {
                UpdateScriptedFailure();
                return;
            }

            float massDifference = Mathf.Abs(sectionA.Rigidbody.mass - sectionB.Rigidbody.mass) * 0.02f;
            float angleDifference = Vector3.Angle(sectionA.transform.up, sectionB.transform.up) / 35f;
            float velocityDifference = (sectionA.Rigidbody.linearVelocity - sectionB.Rigidbody.linearVelocity).magnitude * 0.08f;
            float angularDifference = (sectionA.Rigidbody.angularVelocity - sectionB.Rigidbody.angularVelocity).magnitude * 0.12f;

            float rawStress = (massDifference + angleDifference + velocityDifference + angularDifference) * stressMultiplier;
            Stress01 = Mathf.Clamp01(rawStress / breakThreshold);

            if (canBreak && !breakArmed)
            {
                armDelayTimer -= Time.fixedDeltaTime;
                if (armDelayTimer <= 0f)
                {
                    breakArmed = true;
                    ConfigureJoint();
                }

                warningTimer = 0f;
                UpdateWarningMotion();
                UpdateWarningVisual();
                return;
            }

            if (canBreak && breakArmed && Stress01 > warningThreshold)
            {
                warningTimer += Time.fixedDeltaTime;
                float damage = (Stress01 - warningThreshold) * damagePerSecond * Time.fixedDeltaTime;
                hitPoints -= damage;
                UpdateWarningVisual();
                UpdateWarningMotion();
                ApplyCrackMotion();
            }
            else
            {
                warningTimer = Mathf.Max(0f, warningTimer - Time.fixedDeltaTime * 1.5f);
                UpdateWarningMotion();
                UpdateWarningVisual();
            }

            if (canBreak && breakArmed && hitPoints <= 0f && warningTimer >= warningSecondsBeforeBreak)
            {
                BreakConnector();
            }
        }

        public void Initialize(RaftSection firstSection, RaftSection secondSection, ConnectorType type)
        {
            sectionA = firstSection;
            sectionB = secondSection;
            connectorType = type;
        }

        public void SetBreakable(bool value)
        {
            canBreak = value;
            breakArmed = false;
            armDelayTimer = 0f;
            scriptedFailure = false;
            scriptedSinkActive = false;
            scriptedMotionStarted = false;
            scriptedSinkTimer = 0f;
            warningTimer = 0f;
            ConfigureJoint();
            UpdateWarningVisual();
        }

        public void ArmBreakage(float delay)
        {
            canBreak = true;
            breakArmed = false;
            armDelayTimer = Mathf.Max(0f, delay);
            scriptedFailure = false;
            scriptedSinkActive = false;
            warningTimer = 0f;
            hitPoints = maxHitPoints;
            ConfigureJoint();
            UpdateWarningVisual();
        }

        public void StartScriptedFailure(float delay, float duration)
        {
            canBreak = true;
            breakArmed = false;
            scriptedFailure = true;
            scriptedMotionStarted = false;
            scriptedSinkActive = false;
            scriptedSinkTimer = 0f;
            scriptedFailureDelay = Mathf.Max(0f, delay);
            scriptedFailureDuration = Mathf.Max(0.5f, duration);
            warningTimer = 0f;
            hitPoints = maxHitPoints;
            ConfigureJoint();
            UpdateWarningVisual();
        }

        public void Reinforce(float strengthBonus)
        {
            if (strengthBonus <= 0f)
            {
                return;
            }

            hitPoints += strengthBonus;
            maxHitPoints += strengthBonus;
            breakThreshold += strengthBonus * 0.025f;
            warningSecondsBeforeBreak += strengthBonus * 0.02f;
            UpdateWarningVisual();
        }

        public void ResetConnector()
        {
            broken = false;
            warningTimer = 0f;
            armDelayTimer = 0f;
            breakArmed = false;
            scriptedFailure = false;
            scriptedMotionStarted = false;
            scriptedSinkActive = false;
            scriptedSinkTimer = 0f;
            RestoreBaseStrength();
            hitPoints = maxHitPoints;

            if (sectionA != null)
            {
                sectionA.Rigidbody.isKinematic = false;
            }

            if (sectionB != null)
            {
                sectionB.Rigidbody.isKinematic = false;
            }

            if (transform != null)
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
            }

            ConfigureJoint();
            UpdateWarningVisual();
        }

        public void ForceReconnect()
        {
            canBreak = false;
            broken = false;
            scriptedFailure = false;
            scriptedMotionStarted = false;
            scriptedSinkActive = false;
            scriptedSinkTimer = 0f;
            breakArmed = false;
            warningTimer = 0f;
            RestoreBaseStrength();
            hitPoints = maxHitPoints;

            if (sectionA != null)
            {
                sectionA.Rigidbody.isKinematic = false;
            }

            if (sectionB != null)
            {
                sectionB.Rigidbody.isKinematic = false;
            }

            if (transform != null)
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
            }

            ConfigureJoint();
            UpdateWarningVisual();
        }

        private void RestoreBaseStrength()
        {
            maxHitPoints = Mathf.Max(1f, baseMaxHitPoints);
            breakThreshold = baseBreakThreshold;
            warningSecondsBeforeBreak = baseWarningSecondsBeforeBreak;
        }

        private void ApplyConnectorPreset()
        {
            if (canBreak)
            {
                maxHitPoints = Mathf.Max(1f, hitPoints);
                return;
            }

            switch (connectorType)
            {
                case ConnectorType.LooseStack:
                    hitPoints = Mathf.Max(hitPoints, 45f);
                    stressMultiplier = Mathf.Min(stressMultiplier, 0.8f);
                    breakThreshold = Mathf.Max(breakThreshold, 1.2f);
                    break;
                case ConnectorType.Rope:
                    hitPoints = Mathf.Max(hitPoints, 80f);
                    stressMultiplier = Mathf.Min(stressMultiplier, 0.6f);
                    breakThreshold = Mathf.Max(breakThreshold, 1.4f);
                    break;
                case ConnectorType.Nails:
                    hitPoints = Mathf.Max(hitPoints, 60f);
                    stressMultiplier = Mathf.Min(stressMultiplier, 0.75f);
                    breakThreshold = Mathf.Max(breakThreshold, 1.15f);
                    break;
                case ConnectorType.Bracket:
                    hitPoints = Mathf.Max(hitPoints, 100f);
                    stressMultiplier = Mathf.Min(stressMultiplier, 0.55f);
                    breakThreshold = Mathf.Max(breakThreshold, 1.35f);
                    break;
            }

            maxHitPoints = Mathf.Max(1f, hitPoints);
        }

        private void ConfigureJoint()
        {
            if (joint == null || sectionA == null || sectionB == null)
            {
                return;
            }

            joint.connectedBody = sectionB.Rigidbody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = sectionA.transform.InverseTransformPoint(transform.position);
            joint.connectedAnchor = sectionB.transform.InverseTransformPoint(transform.position);
            bool loose = canBreak && breakArmed;
            joint.xMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            joint.yMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            joint.zMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            joint.angularXMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            joint.angularYMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            joint.angularZMotion = loose ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;

            SoftJointLimit linearLimit = new SoftJointLimit { limit = ConnectorSlack() };
            joint.linearLimit = linearLimit;

            float angularSlack = ConnectorAngularSlack();
            joint.lowAngularXLimit = new SoftJointLimit { limit = -angularSlack };
            joint.highAngularXLimit = new SoftJointLimit { limit = angularSlack };
            joint.angularYLimit = new SoftJointLimit { limit = angularSlack };
            joint.angularZLimit = new SoftJointLimit { limit = angularSlack };
        }

        private void SetJointLimits(float linearSlack, float angularSlack)
        {
            if (joint == null)
            {
                return;
            }

            joint.linearLimit = new SoftJointLimit { limit = linearSlack };
            joint.lowAngularXLimit = new SoftJointLimit { limit = -angularSlack };
            joint.highAngularXLimit = new SoftJointLimit { limit = angularSlack };
            joint.angularYLimit = new SoftJointLimit { limit = angularSlack };
            joint.angularZLimit = new SoftJointLimit { limit = angularSlack };
        }

        private float ConnectorSlack()
        {
            return connectorType switch
            {
                ConnectorType.LooseStack => 0.35f,
                ConnectorType.Rope => 0.18f,
                ConnectorType.Nails => 0.08f,
                ConnectorType.Bracket => 0.03f,
                _ => 0.18f
            };
        }

        private float ConnectorAngularSlack()
        {
            return connectorType switch
            {
                ConnectorType.LooseStack => 28f,
                ConnectorType.Rope => 16f,
                ConnectorType.Nails => 8f,
                ConnectorType.Bracket => 4f,
                _ => 16f
            };
        }

        private void BreakConnector()
        {
            broken = true;
            warningTimer = warningSecondsBeforeBreak;
            if (joint != null)
            {
                joint.connectedBody = null;
            }

            PushSectionsApart();
            UpdateWarningVisual();
        }

        private void PushSectionsApart()
        {
            if (sectionA == null || sectionB == null)
            {
                return;
            }

            Vector3 splitDirection = sectionA.transform.position - sectionB.transform.position;
            if (splitDirection.sqrMagnitude < 0.01f)
            {
                splitDirection = sectionA.transform.forward;
            }

            splitDirection.y = 0f;
            splitDirection.Normalize();

            if (separationImpulse <= 0f)
            {
                return;
            }

            sectionA.Rigidbody.AddForce(splitDirection * separationImpulse, ForceMode.Impulse);
            sectionB.Rigidbody.AddForce(-splitDirection * separationImpulse, ForceMode.Impulse);
            sectionA.Rigidbody.AddTorque(Vector3.Cross(Vector3.up, splitDirection) * separationImpulse * 0.45f, ForceMode.Impulse);
            sectionB.Rigidbody.AddTorque(Vector3.Cross(splitDirection, Vector3.up) * separationImpulse * 0.45f, ForceMode.Impulse);
        }

        private void ApplyCrackMotion()
        {
            if (sectionA == null || sectionB == null)
            {
                return;
            }

            Vector3 splitDirection = sectionA.transform.position - sectionB.transform.position;
            if (splitDirection.sqrMagnitude < 0.01f)
            {
                splitDirection = sectionA.transform.forward;
            }

            splitDirection.y = 0f;
            splitDirection.Normalize();

            float pulse = 0.55f + Mathf.Sin(Time.time * 7f) * 0.45f;
            float force = crackMotionForce * WarningProgress * pulse;
            sectionA.Rigidbody.AddForce(splitDirection * force, ForceMode.Force);
            sectionB.Rigidbody.AddForce(-splitDirection * force, ForceMode.Force);
            sectionA.Rigidbody.AddTorque(Vector3.Cross(Vector3.up, splitDirection) * force * 0.35f, ForceMode.Force);
            sectionB.Rigidbody.AddTorque(Vector3.Cross(splitDirection, Vector3.up) * force * 0.35f, ForceMode.Force);
        }

        private void UpdateScriptedFailure()
        {
            if (!breakArmed)
            {
                scriptedFailureDelay -= Time.fixedDeltaTime;
                if (scriptedFailureDelay > 0f)
                {
                    UpdateWarningMotion();
                    UpdateWarningVisual();
                    return;
                }

                breakArmed = true;
                BeginScriptedMotion();
            }

            warningTimer += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(warningTimer / scriptedFailureDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            hitPoints = Mathf.Lerp(maxHitPoints, 0f, progress);
            Stress01 = Mathf.Lerp(warningThreshold, 1f, easedProgress);

            UpdateWarningVisual();
            UpdateWarningMotion();
            ApplyScriptedSplitMotion(easedProgress);

            if (progress >= 1f)
            {
                CompleteScriptedBreak();
            }
        }

        private void BeginScriptedMotion()
        {
            if (scriptedMotionStarted || sectionA == null || sectionB == null)
            {
                return;
            }

            scriptedMotionStarted = true;
            scriptedStartPositionA = sectionA.Rigidbody.position;
            scriptedStartPositionB = sectionB.Rigidbody.position;
            scriptedStartRotationA = sectionA.Rigidbody.rotation;
            scriptedStartRotationB = sectionB.Rigidbody.rotation;

            if (joint != null)
            {
                joint.connectedBody = null;
            }

            PrepareBodyForScriptedMotion(sectionA.Rigidbody);
            PrepareBodyForScriptedMotion(sectionB.Rigidbody);
        }

        private static void PrepareBodyForScriptedMotion(Rigidbody body)
        {
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        private void ApplyScriptedSplitMotion(float progress)
        {
            if (!scriptedMotionStarted || sectionA == null || sectionB == null)
            {
                return;
            }

            Vector3 splitDirection = scriptedStartPositionA - scriptedStartPositionB;
            splitDirection.y = 0f;
            if (splitDirection.sqrMagnitude < 0.01f)
            {
                splitDirection = Vector3.forward;
            }

            splitDirection.Normalize();
            float splitProgress = Mathf.Clamp01(progress);
            float extraSink = Mathf.Max(0f, progress - 1f) * scriptedSinkDepth * 0.8f;
            float gap = Mathf.Lerp(0f, scriptedMaxSlack, splitProgress);
            float sink = Mathf.Lerp(0f, scriptedSinkDepth, splitProgress) + extraSink;
            float bob = Mathf.Sin(Time.time * 3.5f) * 0.025f * (1f - splitProgress);

            sectionA.Rigidbody.MovePosition(scriptedStartPositionA + splitDirection * gap + Vector3.down * sink + Vector3.up * bob);
            sectionB.Rigidbody.MovePosition(scriptedStartPositionB - splitDirection * gap + Vector3.down * sink - Vector3.up * bob);

            float inwardTilt = Mathf.Lerp(0f, scriptedMaxAngularSlack * 0.65f, splitProgress);
            float twist = Mathf.Lerp(0f, scriptedMaxAngularSlack * 0.16f, splitProgress);
            Quaternion rotationA = Quaternion.AngleAxis(-inwardTilt, Vector3.right) * Quaternion.AngleAxis(twist, Vector3.up);
            Quaternion rotationB = Quaternion.AngleAxis(inwardTilt, Vector3.right) * Quaternion.AngleAxis(-twist, Vector3.up);
            sectionA.Rigidbody.MoveRotation(scriptedStartRotationA * rotationA);
            sectionB.Rigidbody.MoveRotation(scriptedStartRotationB * rotationB);
        }

        private void CompleteScriptedBreak()
        {
            scriptedFailure = false;
            broken = true;
            scriptedSinkActive = true;
            scriptedSinkTimer = 0f;
            warningTimer = warningSecondsBeforeBreak;
            hitPoints = 0f;

            UpdateWarningVisual();
        }

        private void UpdateScriptedSinking()
        {
            scriptedSinkTimer += Time.fixedDeltaTime;
            ApplyScriptedSplitMotion(1f + scriptedSinkTimer * 0.18f);
        }

        private void UpdateWarningMotion()
        {
            if (broken)
            {
                return;
            }

            float wobble = WarningProgress * warningWobbleScale;
            transform.localPosition = initialLocalPosition + new Vector3(
                Mathf.Sin(Time.time * 28f) * wobble,
                Mathf.Sin(Time.time * 37f) * wobble * 0.35f,
                Mathf.Cos(Time.time * 31f) * wobble);
            transform.localRotation = initialLocalRotation * Quaternion.Euler(
                Mathf.Sin(Time.time * 40f) * wobble * 25f,
                0f,
                Mathf.Cos(Time.time * 34f) * wobble * 25f);
        }

        private void UpdateWarningVisual()
        {
            if (warningRenderer == null)
            {
                return;
            }

            float damage01 = Mathf.Max(1f - Mathf.Clamp01(hitPoints / maxHitPoints), WarningProgress);
            Color color = damage01 < warningThreshold
                ? Color.Lerp(healthyColor, warningColor, damage01 / warningThreshold)
                : Color.Lerp(warningColor, criticalColor, Mathf.InverseLerp(warningThreshold, 1f, damage01));

            if (broken)
            {
                color = criticalColor;
            }

            warningRenderer.material.color = color;
        }
    }
}
