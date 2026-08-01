using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Prototype
{
    public enum BoatFailurePreset
    {
        Balanced,
        TooHeavy,
        Unbalanced,
        Custom
    }

    public class FailurePrototypeController : MonoBehaviour
    {
        [SerializeField] private WaveSurface waveSurface;
        [SerializeField] private RaftSection frontSection;
        [SerializeField] private RaftSection rearSection;
        [SerializeField] private SectionConnector weakConnector;
        [SerializeField] private Transform leftCargoPoint;
        [SerializeField] private Transform rightCargoPoint;
        [SerializeField] private float cargoMass = 6f;
        [SerializeField] private Vector3 cargoSize = new Vector3(0.62f, 0.58f, 0.62f);
        [SerializeField] private GameObject[] cargoPrefabs;
        [SerializeField] private Material cargoMaterial;
        [SerializeField] private PhysicsMaterial cargoPhysicsMaterial;
        [SerializeField] private float connectorArmDelay = 10f;
        [SerializeField] private float presetCargoInterval = 0.75f;
        [SerializeField] private float heavySinkSpeed = 0.055f;
        [SerializeField] private float scriptedBreakDelay = 2f;
        [SerializeField] private float scriptedBreakDuration = 7f;
        [SerializeField] private int cargoPiecesPerPress = 3;
        [SerializeField] private int cargoAccidentThreshold = 4;
        [SerializeField] private float cargoWarningSeconds = 2.25f;
        [SerializeField] private float cargoAccidentSinkDuration = 7.5f;
        [SerializeField] private float cargoAccidentTorque = 20f;
        [SerializeField] private float cargoDeckSurfaceY = 0.17f;
        [SerializeField] private float cargoStackLayerHeight = 0.34f;

        private readonly List<GameObject> spawnedCargo = new List<GameObject>();
        private readonly List<CargoVisual> cargoVisuals = new List<CargoVisual>();
        private readonly Vector3 frontStartPosition = new Vector3(0f, 1.25f, 1.55f);
        private readonly Vector3 rearStartPosition = new Vector3(0f, 1.25f, -1.55f);
        private int pendingLeftCargo;
        private int pendingRightCargo;
        private float cargoTimer;
        private float heavyBuoyancyScale = 1f;
        private float cargoAccidentTimer;
        private float cargoAccidentBuoyancy = 1f;
        private int cargoAccidentSide;
        private bool cargoAccidentActive;
        private bool cargoAccidentCommitted;
        private bool cargoAccidentSplashEmitted;
        private bool demoLaunched = true;

        public BoatFailurePreset CurrentPreset { get; private set; } = BoatFailurePreset.Balanced;
        public int LeftCargoCount { get; private set; }
        public int RightCargoCount { get; private set; }

        private sealed class CargoVisual
        {
            public GameObject Object;
            public int Side;
            public Vector3 BaseLocalPosition;
            public int StackIndex;
        }

        private void Start()
        {
            ApplyPreset(BoatFailurePreset.Balanced);
        }

        private void Update()
        {
            ProcessPendingCargo();
            ProcessHeavySinking();
            ProcessBrokenRaftSinking();
            ProcessCargoAccident();

            if (PressedThisFrame(KeyCode.Alpha1))
            {
                CurrentPreset = BoatFailurePreset.Custom;
                SpawnCargo(leftCargoPoint, -1, LeftCargoCount);
                LeftCargoCount++;
                AfterCargoChanged(-1);
            }

            if (PressedThisFrame(KeyCode.Alpha2))
            {
                CurrentPreset = BoatFailurePreset.Custom;
                SpawnCargo(rightCargoPoint, 1, RightCargoCount);
                RightCargoCount++;
                AfterCargoChanged(1);
            }

            if (PressedThisFrame(KeyCode.B))
            {
                ApplyPreset(BoatFailurePreset.Balanced);
            }

            if (PressedThisFrame(KeyCode.H))
            {
                ApplyPreset(BoatFailurePreset.TooHeavy);
            }

            if (PressedThisFrame(KeyCode.U))
            {
                ApplyPreset(BoatFailurePreset.Unbalanced);
            }
        }

        public void ApplyPreset(BoatFailurePreset preset)
        {
            ClearCargo();
            ClearPendingCargo();
            CurrentPreset = preset;
            heavyBuoyancyScale = 1f;
            ResetCargoAccident();

            if (weakConnector != null)
            {
                weakConnector.SetBreakable(false);
                weakConnector.ForceReconnect();
            }

            ResetSection(frontSection, frontStartPosition, Quaternion.identity, 20f);
            ResetSection(rearSection, rearStartPosition, Quaternion.identity, 20f);
            SetRaftBuoyancyScale(1f);
            SetRaftAutomaticFlooding(preset != BoatFailurePreset.Unbalanced);
            Physics.SyncTransforms();

            if (weakConnector != null)
            {
                weakConnector.ForceReconnect();
            }

            Physics.SyncTransforms();

            switch (preset)
            {
                case BoatFailurePreset.Balanced:
                    break;
                case BoatFailurePreset.TooHeavy:
                    AddBalancedBallast(frontSection, 18f);
                    AddBalancedBallast(rearSection, 18f);
                    break;
                case BoatFailurePreset.Unbalanced:
                    StartScriptedBreak();
                    break;
            }
        }

        public void SetDemoLaunched(bool value)
        {
            demoLaunched = value;
            if (!demoLaunched)
            {
                ResetCargoAccident();
            }
        }

        public void ApplyDemoPlacedItem(DemoRaftSlot slot, DemoCarryableItem item)
        {
            if (slot == null || item == null || slot.TargetSection == null)
            {
                return;
            }

            Vector3 localLoadPosition = slot.LocalLoadPosition();
            slot.TargetSection.AddCargoLoad(localLoadPosition, item.Mass);
            if (item.BuoyancyBonus > 0f)
            {
                slot.TargetSection.BuoyancyBody.AddBuoyancyBonus(item.BuoyancyBonus);
            }

            int side = localLoadPosition.x < 0f ? -1 : 1;
            if (side < 0)
            {
                LeftCargoCount++;
            }
            else
            {
                RightCargoCount++;
            }

            if (item.ConnectorStrengthBonus > 0f && weakConnector != null)
            {
                weakConnector.Reinforce(item.ConnectorStrengthBonus);
            }

            if (item.ConnectorStress > 0f && weakConnector != null)
            {
                weakConnector.ArmBreakage(demoLaunched ? 0.5f : connectorArmDelay);
            }

            if (item.Mass >= 10f && Mathf.Abs(localLoadPosition.x) > 0.35f)
            {
                AfterCargoChanged(side);
            }
        }

        private void SpawnCargo(Transform spawnPoint, int side, int stackIndex)
        {
            if (spawnPoint == null)
            {
                return;
            }

            RaftSection carrier = spawnPoint.GetComponentInParent<RaftSection>();
            float sideSign = Mathf.Sign(side);
            float stackHeight = stackIndex / 2;
            float row = stackIndex % 2 == 0 ? -0.16f : 0.16f;
            float sideOffset = sideSign * (0.05f * (stackIndex % 3));

            for (int i = 0; i < cargoPiecesPerPress; i++)
            {
                GameObject cargo = CreateCargoVisual(i + stackIndex * cargoPiecesPerPress);
                float bottomY = cargoDeckSurfaceY + stackHeight * cargoStackLayerHeight + i * 0.015f;
                Vector3 pieceOffset = new Vector3(
                    sideOffset + (i - 1) * 0.18f,
                    bottomY,
                    row + (i % 2 == 0 ? -0.1f : 0.1f));
                Vector3 localCargoPosition = pieceOffset;

                if (carrier != null)
                {
                    Vector3 worldPosition = spawnPoint.TransformPoint(localCargoPosition);
                    cargo.transform.SetParent(carrier.transform, false);
                    cargo.transform.localPosition = carrier.transform.InverseTransformPoint(worldPosition);
                }
                else
                {
                    cargo.transform.SetParent(spawnPoint, false);
                    cargo.transform.localPosition = localCargoPosition;
                }

                cargo.transform.localRotation = Quaternion.Euler(0f, i * 7f, 0f);
                cargo.transform.localScale = CargoVisualScale(cargo, i);
                cargo.name = side < 0 ? "Heavy Test Cargo Left" : "Heavy Test Cargo Right";

                if (cargoPrefabs == null || cargoPrefabs.Length == 0)
                {
                    if (cargoMaterial != null)
                    {
                        cargo.GetComponent<Renderer>().sharedMaterial = cargoMaterial;
                    }

                    AddCargoStraps(cargo);
                }

                DisableVisualColliders(cargo);
                if (carrier != null)
                {
                    AlignVisualBottomToLocalY(cargo, carrier.transform, bottomY);
                }

                spawnedCargo.Add(cargo);
                cargoVisuals.Add(new CargoVisual
                {
                    Object = cargo,
                    Side = side,
                    BaseLocalPosition = cargo.transform.localPosition,
                    StackIndex = stackIndex
                });
            }

            if (carrier != null)
            {
                Vector3 loadPosition = carrier.transform.InverseTransformPoint(spawnPoint.TransformPoint(new Vector3(sideSign * 0.24f, 0.38f + stackHeight * 0.12f, row)));
                carrier.AddCargoLoad(loadPosition, cargoMass);
            }
        }

        private GameObject CreateCargoVisual(int index)
        {
            if (cargoPrefabs != null && cargoPrefabs.Length > 0)
            {
                GameObject prefab = cargoPrefabs[index % cargoPrefabs.Length];
                if (prefab != null)
                {
                    return Instantiate(prefab);
                }
            }

            return GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        private Vector3 CargoVisualScale(GameObject cargo, int pieceIndex)
        {
            if (cargoPrefabs == null || cargoPrefabs.Length == 0)
            {
                return cargoSize * (pieceIndex == 1 ? 1.08f : 1f);
            }

            string lowerName = cargo.name.ToLowerInvariant();
            float uniform = lowerName.Contains("big") ? 0.62f : 0.52f;
            if (lowerName.Contains("barrel"))
            {
                uniform *= 0.9f;
            }

            return Vector3.one * uniform;
        }

        private static void AlignVisualBottomToLocalY(GameObject visual, Transform parent, float targetLocalY)
        {
            if (visual == null || parent == null)
            {
                return;
            }

            if (!TryGetRendererLocalYBounds(visual, parent, out float minLocalY, out _))
            {
                return;
            }

            Vector3 localPosition = visual.transform.localPosition;
            localPosition.y += targetLocalY - minLocalY;
            visual.transform.localPosition = localPosition;
        }

        private static bool TryGetRendererLocalYBounds(GameObject visual, Transform parent, out float minLocalY, out float maxLocalY)
        {
            minLocalY = float.PositiveInfinity;
            maxLocalY = float.NegativeInfinity;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 worldCorner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            float localY = parent.InverseTransformPoint(worldCorner).y;
                            minLocalY = Mathf.Min(minLocalY, localY);
                            maxLocalY = Mathf.Max(maxLocalY, localY);
                        }
                    }
                }
            }

            return minLocalY < float.PositiveInfinity && maxLocalY > float.NegativeInfinity;
        }

        private static void AddCargoStraps(GameObject cargo)
        {
            CreateCargoStrap(cargo.transform, "Cargo Strap X", new Vector3(0f, 0.52f, 0f), new Vector3(0.09f, 0.04f, 1.08f));
            CreateCargoStrap(cargo.transform, "Cargo Strap Z", new Vector3(0f, 0.53f, 0f), new Vector3(1.08f, 0.035f, 0.08f));
        }

        private static void CreateCargoStrap(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject strap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strap.name = name;
            strap.transform.SetParent(parent, false);
            strap.transform.localPosition = localPosition;
            strap.transform.localRotation = Quaternion.identity;
            strap.transform.localScale = localScale;

            Renderer renderer = strap.GetComponent<Renderer>();
            renderer.material.color = new Color(0.06f, 0.045f, 0.035f);

            DisableVisualColliders(strap);
        }

        private void ProcessPendingCargo()
        {
            if (pendingLeftCargo <= 0 && pendingRightCargo <= 0)
            {
                return;
            }

            cargoTimer -= Time.deltaTime;
            if (cargoTimer > 0f)
            {
                return;
            }

            cargoTimer = presetCargoInterval;

            if (pendingLeftCargo >= pendingRightCargo && pendingLeftCargo > 0)
            {
                SpawnCargo(leftCargoPoint, -1, LeftCargoCount);
                LeftCargoCount++;
                pendingLeftCargo--;
                AfterCargoChanged(-1);
                return;
            }

            if (pendingRightCargo > 0)
            {
                SpawnCargo(rightCargoPoint, 1, RightCargoCount);
                RightCargoCount++;
                pendingRightCargo--;
                AfterCargoChanged(1);
            }
        }

        private void AfterCargoChanged(int side)
        {
            ArmConnectorAfterCargo();

            int sideCount = side < 0 ? LeftCargoCount : RightCargoCount;
            int otherCount = side < 0 ? RightCargoCount : LeftCargoCount;
            if (!cargoAccidentActive && sideCount >= cargoAccidentThreshold && sideCount >= otherCount + 2)
            {
                cargoAccidentActive = true;
                cargoAccidentCommitted = false;
                cargoAccidentSplashEmitted = false;
                cargoAccidentSide = side;
                cargoAccidentTimer = 0f;
                cargoAccidentBuoyancy = 1f;
            }
        }

        private void ProcessCargoAccident()
        {
            if (!cargoAccidentActive || frontSection == null || rearSection == null)
            {
                return;
            }

            if (!demoLaunched)
            {
                return;
            }

            cargoAccidentTimer += Time.deltaTime;
            float warningProgress = Mathf.Clamp01(cargoAccidentTimer / Mathf.Max(0.1f, cargoWarningSeconds));
            AnimateCargoPileShift(warningProgress);

            Vector3 rollTorque = frontSection.transform.forward * (-cargoAccidentSide * cargoAccidentTorque * Mathf.Lerp(0.25f, 1f, warningProgress));
            frontSection.Rigidbody.AddTorque(rollTorque, ForceMode.Force);
            rearSection.Rigidbody.AddTorque(rollTorque * 0.45f, ForceMode.Force);

            if (cargoAccidentTimer < cargoWarningSeconds)
            {
                return;
            }

            if (!cargoAccidentCommitted)
            {
                cargoAccidentCommitted = true;
                frontSection.BuoyancyBody.TriggerFlooding();
                rearSection.BuoyancyBody.TriggerFlooding();
                EmitBigSplash(frontSection.transform.position + frontSection.transform.right * cargoAccidentSide * 1.05f, 0.45f);
            }

            float sinkElapsed = cargoAccidentTimer - cargoWarningSeconds;
            float sinkProgress = Mathf.Clamp01(sinkElapsed / Mathf.Max(0.1f, cargoAccidentSinkDuration));
            float curvedSink = sinkProgress < 0.42f
                ? Mathf.Lerp(0f, 0.32f, Mathf.SmoothStep(0f, 1f, sinkProgress / 0.42f))
                : Mathf.Lerp(0.32f, 1f, Mathf.SmoothStep(0f, 1f, (sinkProgress - 0.42f) / 0.58f));
            cargoAccidentBuoyancy = Mathf.Clamp01(1f - curvedSink);
            SetRaftBuoyancyScale(cargoAccidentBuoyancy);

            Vector3 failureTorque = frontSection.transform.forward * (-cargoAccidentSide * cargoAccidentTorque * 0.65f * sinkProgress);
            frontSection.Rigidbody.AddTorque(failureTorque, ForceMode.Force);
            rearSection.Rigidbody.AddTorque(failureTorque * 0.55f, ForceMode.Force);

            if (!cargoAccidentSplashEmitted && cargoAccidentBuoyancy < 0.55f)
            {
                cargoAccidentSplashEmitted = true;
                EmitBigSplash(frontSection.transform.position + frontSection.transform.right * cargoAccidentSide * 1.25f, 1f);
            }
        }

        private void AnimateCargoPileShift(float progress)
        {
            float pulse = Mathf.Sin(Time.time * 22f) * 0.035f * progress;
            float slide = Mathf.SmoothStep(0f, 0.48f, progress);

            foreach (CargoVisual visual in cargoVisuals)
            {
                if (visual.Object == null || visual.Side != cargoAccidentSide)
                {
                    continue;
                }

                Vector3 target = visual.BaseLocalPosition;
                target.x += cargoAccidentSide * (slide + visual.StackIndex * 0.035f);
                target.y += Mathf.Abs(pulse) + visual.StackIndex * 0.015f * progress;
                target.z += Mathf.Sin(Time.time * 9f + visual.StackIndex) * 0.035f * progress;
                visual.Object.transform.localPosition = target;
                visual.Object.transform.localRotation = Quaternion.Euler(0f, visual.StackIndex * 8f, cargoAccidentSide * progress * 13f + pulse * 80f);
            }
        }

        private void EmitBigSplash(Vector3 worldPosition, float strength)
        {
            if (frontSection != null)
            {
                frontSection.BuoyancyBody.EmitImpactSplash(worldPosition, strength);
            }

            if (rearSection != null)
            {
                rearSection.BuoyancyBody.EmitImpactSplash(worldPosition, strength * 0.65f);
            }
        }

        private void ProcessHeavySinking()
        {
            if (CurrentPreset != BoatFailurePreset.TooHeavy)
            {
                return;
            }

            heavyBuoyancyScale = Mathf.MoveTowards(heavyBuoyancyScale, 0f, heavySinkSpeed * Time.deltaTime);
            SetRaftBuoyancyScale(heavyBuoyancyScale);
        }

        private void AddBalancedBallast(RaftSection section, float totalMass)
        {
            if (section == null)
            {
                return;
            }

            float halfMass = totalMass * 0.5f;
            section.AddCargoLoad(new Vector3(-0.55f, 0.02f, 0f), halfMass);
            section.AddCargoLoad(new Vector3(0.55f, 0.02f, 0f), halfMass);
            CreateBallastVisual(section.transform, new Vector3(-0.55f, 0.48f, 0f));
            CreateBallastVisual(section.transform, new Vector3(0.55f, 0.48f, 0f));
        }

        private void CreateBallastVisual(Transform parent, Vector3 localPosition)
        {
            GameObject cargo = CreateCargoVisual(spawnedCargo.Count);
            cargo.transform.SetParent(parent, false);
            cargo.transform.localPosition = localPosition;
            cargo.transform.localRotation = Quaternion.identity;
            cargo.transform.localScale = CargoVisualScale(cargo, spawnedCargo.Count);
            cargo.name = "Heavy Test Cargo";

            if (cargoPrefabs == null || cargoPrefabs.Length == 0)
            {
                if (cargoMaterial != null)
                {
                    cargo.GetComponent<Renderer>().sharedMaterial = cargoMaterial;
                }

                AddCargoStraps(cargo);
            }

            DisableVisualColliders(cargo);
            AlignVisualBottomToLocalY(cargo, parent, Mathf.Max(cargoDeckSurfaceY, localPosition.y));
            spawnedCargo.Add(cargo);
        }

        private static void DisableVisualColliders(GameObject cargo)
        {
            Collider[] colliders = cargo.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = false;
                Destroy(collider);
            }
        }

        private void ProcessBrokenRaftSinking()
        {
            if (weakConnector == null || !weakConnector.IsBroken)
            {
                return;
            }

            if (CurrentPreset != BoatFailurePreset.Unbalanced && CurrentPreset != BoatFailurePreset.Custom)
            {
                return;
            }

            if (frontSection != null)
            {
                frontSection.BuoyancyBody.TriggerFlooding();
            }

            if (rearSection != null)
            {
                rearSection.BuoyancyBody.TriggerFlooding();
            }
        }

        private void ArmConnectorAfterCargo()
        {
            if (weakConnector != null)
            {
                weakConnector.ArmBreakage(connectorArmDelay);
            }
        }

        private void StartScriptedBreak()
        {
            if (weakConnector != null)
            {
                weakConnector.StartScriptedFailure(scriptedBreakDelay, scriptedBreakDuration);
            }
        }

        private void SetRaftAutomaticFlooding(bool value)
        {
            if (frontSection != null)
            {
                frontSection.BuoyancyBody.SetAutomaticFlooding(value);
            }

            if (rearSection != null)
            {
                rearSection.BuoyancyBody.SetAutomaticFlooding(value);
            }
        }

        private void ClearCargo()
        {
            foreach (GameObject cargo in spawnedCargo)
            {
                if (cargo != null)
                {
                    cargo.SetActive(false);
                    Destroy(cargo);
                }
            }

            GameObject[] looseCargo = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (GameObject candidate in looseCargo)
            {
                if (candidate != null && candidate.name.StartsWith("Heavy Test Cargo"))
                {
                    candidate.SetActive(false);
                    Destroy(candidate);
                }
            }

            spawnedCargo.Clear();
            cargoVisuals.Clear();
            LeftCargoCount = 0;
            RightCargoCount = 0;
            ResetCargoAccident();
        }

        private void ResetCargoAccident()
        {
            cargoAccidentActive = false;
            cargoAccidentCommitted = false;
            cargoAccidentSplashEmitted = false;
            cargoAccidentTimer = 0f;
            cargoAccidentBuoyancy = 1f;
            cargoAccidentSide = 0;
        }

        private void ClearPendingCargo()
        {
            pendingLeftCargo = 0;
            pendingRightCargo = 0;
            cargoTimer = 0f;
        }

        private void SetRaftBuoyancyScale(float scale)
        {
            if (frontSection != null)
            {
                frontSection.BuoyancyBody.SetBuoyancyScale(scale);
            }

            if (rearSection != null)
            {
                rearSection.BuoyancyBody.SetBuoyancyScale(scale);
            }
        }

        private static void ResetSection(RaftSection section, Vector3 position, Quaternion rotation, float mass)
        {
            if (section == null)
            {
                return;
            }

            Rigidbody body = section.Rigidbody;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.position = position;
            body.rotation = rotation;
            section.ResetLoad(mass);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = false;
            body.Sleep();
            body.WakeUp();
        }

        private static bool PressedThisFrame(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyCode switch
            {
                KeyCode.Alpha1 => keyboard.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => keyboard.digit2Key.wasPressedThisFrame,
                KeyCode.B => keyboard.bKey.wasPressedThisFrame,
                KeyCode.H => keyboard.hKey.wasPressedThisFrame,
                KeyCode.U => keyboard.uKey.wasPressedThisFrame,
                _ => false
            };
        }
    }
}
