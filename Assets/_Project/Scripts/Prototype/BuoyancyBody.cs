using UnityEngine;

namespace ThisWillFloat.Prototype
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyancyBody : MonoBehaviour
    {
        [SerializeField] private WaveSurface waveSurface;
        [SerializeField] private Transform[] buoyancyPoints;
        [SerializeField] private Transform[] splashPoints;
        [SerializeField] private float floatDepth = 0.7f;
        [SerializeField] private float targetSubmersion = 0.35f;
        [SerializeField] private float maxBuoyancyForcePerPoint = 62f;
        [SerializeField] private float pointDamping = 3.8f;
        [SerializeField] private float waterDrag = 2.1f;
        [SerializeField] private float waterAngularDrag = 2.6f;
        [SerializeField] private float uprightAssist = 0.08f;
        [SerializeField] private float waveNormalInfluence = 0.08f;
        [SerializeField] private ParticleSystem splashParticles;
        [SerializeField] private float splashVelocityThreshold = 0.55f;
        [SerializeField] private float splashSurfaceBand = 0.22f;
        [SerializeField] private float sinkingNoReturnDepth = 1.6f;
        [SerializeField] private float floodSubmersionThreshold = 0.7f;
        [SerializeField] private float capsizeAngleThreshold = 58f;
        [SerializeField] private float floodDelay = 1.1f;
        [SerializeField] private float floodRate = 0.075f;
        [SerializeField] private bool automaticFlooding = true;

        private Rigidbody cachedRigidbody;
        private float startingDrag;
        private float startingAngularDrag;
        private float buoyancyScale = 1f;
        private float buoyancyBonusScale;
        private float floodTimer;
        private bool fullySinking;
        private bool flooding;

        public Rigidbody Rigidbody => cachedRigidbody;
        public float SubmergedPointRatio { get; private set; }
        public float AverageSubmersion { get; private set; }

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            startingDrag = cachedRigidbody.linearDamping;
            startingAngularDrag = cachedRigidbody.angularDamping;
        }

        private void Reset()
        {
            waveSurface = FindAnyObjectByType<WaveSurface>();
        }

        private void FixedUpdate()
        {
            if (waveSurface == null || buoyancyPoints == null || buoyancyPoints.Length == 0)
            {
                return;
            }

            int submergedPoints = 0;
            float totalSubmersion = 0f;

            EmitSurfaceSplashes(splashPoints);

            foreach (Transform point in buoyancyPoints)
            {
                if (point == null)
                {
                    continue;
                }

                float waterHeight = waveSurface.GetHeight(point.position);
                float signedDepth = waterHeight - point.position.y;
                Vector3 pointVelocity = cachedRigidbody.GetPointVelocity(point.position);
                EmitSurfaceSplash(point, waterHeight, signedDepth, pointVelocity);

                if (fullySinking || buoyancyScale <= 0f)
                {
                    continue;
                }

                if (buoyancyScale < 0.08f && point.position.y < waveSurface.BaseWaterHeight - sinkingNoReturnDepth)
                {
                    fullySinking = true;
                    continue;
                }

                float submersion = Mathf.Clamp01(signedDepth / floatDepth);

                if (submersion <= 0f)
                {
                    continue;
                }

                submergedPoints++;
                totalSubmersion += submersion;

                Vector3 waveNormal = waveSurface.GetNormal(point.position);
                Vector3 liftDirection = Vector3.Slerp(Vector3.up, waveNormal, waveNormalInfluence).normalized;
                float effectiveBuoyancyScale = Mathf.Clamp(buoyancyScale + buoyancyBonusScale, 0f, 1.45f);
                float liftAmount = maxBuoyancyForcePerPoint * effectiveBuoyancyScale * Mathf.Clamp01(submersion / targetSubmersion);
                cachedRigidbody.AddForceAtPosition(liftDirection * liftAmount, point.position, ForceMode.Force);

                Vector3 dampingForce = -pointVelocity * (pointDamping * cachedRigidbody.mass / buoyancyPoints.Length);
                cachedRigidbody.AddForceAtPosition(dampingForce, point.position, ForceMode.Force);
            }

            SubmergedPointRatio = submergedPoints / (float)buoyancyPoints.Length;
            AverageSubmersion = buoyancyPoints.Length > 0 ? totalSubmersion / buoyancyPoints.Length : 0f;
            UpdateFlooding();

            float dragBlend = Mathf.Clamp01(SubmergedPointRatio);
            cachedRigidbody.linearDamping = Mathf.Lerp(startingDrag, waterDrag, dragBlend);
            cachedRigidbody.angularDamping = Mathf.Lerp(startingAngularDrag, waterAngularDrag, dragBlend);

            if (SubmergedPointRatio > 0f && uprightAssist > 0f)
            {
                Vector3 torqueAxis = Vector3.Cross(transform.up, Vector3.up);
                cachedRigidbody.AddTorque(torqueAxis * uprightAssist * cachedRigidbody.mass * SubmergedPointRatio, ForceMode.Force);
            }
        }

        public void SetWaveSurface(WaveSurface surface)
        {
            waveSurface = surface;
        }

        public void SetBuoyancyPoints(Transform[] points)
        {
            buoyancyPoints = points;
        }

        public void SetSplashPoints(Transform[] points)
        {
            splashPoints = points;
        }

        public void SetSplashParticles(ParticleSystem particles)
        {
            splashParticles = particles;
        }

        public void SetBuoyancyScale(float scale)
        {
            buoyancyScale = Mathf.Clamp01(scale);
            if (buoyancyScale > 0.9f)
            {
                flooding = false;
                fullySinking = false;
                floodTimer = 0f;
                buoyancyBonusScale = 0f;
            }
        }

        public void AddBuoyancyBonus(float bonus)
        {
            buoyancyBonusScale = Mathf.Clamp(buoyancyBonusScale + Mathf.Max(0f, bonus), 0f, 0.45f);
        }

        public void TriggerFlooding()
        {
            flooding = true;
            floodTimer = floodDelay;
        }

        public void EmitImpactSplash(Vector3 worldPosition, float strength)
        {
            if (splashParticles == null || waveSurface == null)
            {
                return;
            }

            float waterHeight = waveSurface.GetHeight(worldPosition);
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = new Vector3(worldPosition.x, waterHeight, worldPosition.z),
                velocity = Vector3.up * Mathf.Lerp(1.2f, 2.8f, Mathf.Clamp01(strength)),
                startLifetime = Mathf.Lerp(0.28f, 0.58f, Mathf.Clamp01(strength)),
                startSize = Mathf.Lerp(0.055f, 0.18f, Mathf.Clamp01(strength)),
                startColor = new Color(0.82f, 0.94f, 1f, 0.74f)
            };

            splashParticles.Emit(emitParams, Mathf.RoundToInt(Mathf.Lerp(14f, 46f, Mathf.Clamp01(strength))));
        }

        public void SetAutomaticFlooding(bool value)
        {
            automaticFlooding = value;
            if (!automaticFlooding && !flooding)
            {
                floodTimer = 0f;
            }
        }

        private void UpdateFlooding()
        {
            if (!automaticFlooding && !flooding)
            {
                return;
            }

            float capsizeAngle = Vector3.Angle(transform.up, Vector3.up);
            bool deeplySubmerged = AverageSubmersion >= floodSubmersionThreshold && SubmergedPointRatio >= 0.75f;
            bool capsizedInWater = capsizeAngle >= capsizeAngleThreshold && SubmergedPointRatio >= 0.5f;

            if (deeplySubmerged || capsizedInWater)
            {
                floodTimer += Time.fixedDeltaTime;
                if (floodTimer >= floodDelay)
                {
                    flooding = true;
                }
            }
            else if (!flooding)
            {
                floodTimer = Mathf.Max(0f, floodTimer - Time.fixedDeltaTime * 0.5f);
            }

            if (!flooding)
            {
                return;
            }

            buoyancyScale = Mathf.MoveTowards(buoyancyScale, 0f, floodRate * Time.fixedDeltaTime);
            if (buoyancyScale <= 0.02f)
            {
                fullySinking = true;
            }
        }

        private void EmitSurfaceSplash(Transform point, float waterHeight, float signedDepth, Vector3 pointVelocity)
        {
            if (splashParticles == null || pointVelocity.magnitude < splashVelocityThreshold)
            {
                return;
            }

            if (Mathf.Abs(signedDepth) > splashSurfaceBand)
            {
                return;
            }

            splashParticles.transform.position = new Vector3(point.position.x, waterHeight, point.position.z);
            int splashCount = Mathf.Clamp(Mathf.CeilToInt(pointVelocity.magnitude * 0.75f), 1, 2);
            splashParticles.Emit(splashCount);
        }

        private void EmitSurfaceSplashes(Transform[] points)
        {
            if (points == null)
            {
                return;
            }

            foreach (Transform point in points)
            {
                if (point == null)
                {
                    continue;
                }

                float waterHeight = waveSurface.GetHeight(point.position);
                float signedDepth = waterHeight - point.position.y;
                Vector3 pointVelocity = cachedRigidbody.GetPointVelocity(point.position);
                EmitSurfaceSplash(point, waterHeight, signedDepth, pointVelocity);
            }
        }
    }
}
