using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Prototype
{
    public enum DemoRunState
    {
        Building,
        Launched,
        Success,
        Failed
    }

    public class DemoGameController : MonoBehaviour
    {
        [SerializeField] private FailurePrototypeController physicsController;
        [SerializeField] private RaftSection frontSection;
        [SerializeField] private RaftSection rearSection;
        [SerializeField] private SectionConnector connector;
        [SerializeField] private DemoPlayerController player;
        [SerializeField] private DemoLaunchButton buildButton;
        [SerializeField] private DemoLaunchButton launchButton;
        [SerializeField] private DemoRaftSlot[] slots;
        [SerializeField] private DemoCarryableItem[] items;
        [SerializeField] private Transform raftRoot;
        [SerializeField] private GameObject buildGridRoot;
        [SerializeField] private Vector3 raftForward = Vector3.forward;
        [SerializeField] private float launchSpeed = 1.6f;
        [SerializeField] private float successSeconds = 18f;
        [SerializeField] private float tooHeavySubmersion = 0.78f;
        [SerializeField] private float badBalanceAngle = 54f;
        [SerializeField] private Vector3 playerStartPosition = new Vector3(0f, 0.9f, -7.5f);
        [SerializeField] private Quaternion playerStartRotation = Quaternion.identity;

        private float launchTimer;
        private bool showDevUi = true;
        private bool buildGridOpen;
        private string message = "Build a boat and escape";
        private string prompt = "E: pick up / place";
        private DemoRunState state = DemoRunState.Building;

        public DemoRunState State => state;
        public string Message => message;

        private void Awake()
        {
            playerStartPosition = new Vector3(82f, 1.2f, -58f);
            playerStartRotation = Quaternion.Euler(0f, -90f, 0f);
            EnsureDemoWorldFallbacks();
            RefreshSceneReferences();
        }

        private void Start()
        {
            ResetRun();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    ResetRun();
                }

                if (keyboard.f1Key.wasPressedThisFrame)
                {
                    showDevUi = !showDevUi;
                }
            }

            if (state == DemoRunState.Launched)
            {
                UpdateLaunchedRun();
            }
        }

        private void FixedUpdate()
        {
            if (state != DemoRunState.Launched)
            {
                return;
            }

            Vector3 force = raftForward.normalized * launchSpeed;
            frontSection?.Rigidbody.AddForce(force, ForceMode.Acceleration);
            rearSection?.Rigidbody.AddForce(force, ForceMode.Acceleration);
        }

        public void SetPrompt(string value)
        {
            prompt = value;
        }

        public void OpenBuildGrid()
        {
            if (state != DemoRunState.Building || buildGridOpen)
            {
                return;
            }

            buildGridOpen = true;
            if (buildGridRoot != null)
            {
                buildGridRoot.SetActive(true);
            }

            buildButton?.ResetButton();
            message = "Blueprint grid opened";
            prompt = "Place materials on the beach grid, then press launch";
        }

        public void Launch()
        {
            if (state != DemoRunState.Building)
            {
                return;
            }

            if (!buildGridOpen)
            {
                message = "Open the build grid first";
                prompt = "Use the green build button on the beach";
                launchButton?.ResetButton();
                return;
            }

            SetRaftVisibleAndPhysical(true);
            physicsController?.ApplyPreset(BoatFailurePreset.Balanced);
            ApplyPlacedSlotsToRaft();

            state = DemoRunState.Launched;
            launchTimer = 0f;
            message = "Launched";
            prompt = "Survive the crossing";
            physicsController?.SetDemoLaunched(true);
            connector?.ArmBreakage(0.5f);

            if (player != null && frontSection != null)
            {
                Vector3 ridePosition = frontSection.transform.TransformPoint(new Vector3(0f, 0.45f, -0.65f));
                player.SnapToMovingParent(frontSection.transform, ridePosition, frontSection.transform.rotation);
            }
        }

        public void ResetRun()
        {
            RefreshSceneReferences();
            state = DemoRunState.Building;
            launchTimer = 0f;
            buildGridOpen = false;
            message = "Build a boat and escape";
            prompt = "E: pick up materials / press build button";
            physicsController?.ApplyPreset(BoatFailurePreset.Balanced);
            physicsController?.SetDemoLaunched(false);
            buildButton?.ResetButton();
            launchButton?.ResetButton();
            SetRaftVisibleAndPhysical(false);

            if (buildGridRoot != null)
            {
                buildGridRoot.SetActive(false);
            }

            if (slots != null)
            {
                foreach (DemoRaftSlot slot in slots)
                {
                    slot?.Clear();
                }
            }

            if (items != null)
            {
                foreach (DemoCarryableItem item in items)
                {
                    item?.ResetItem();
                }
            }

            player?.ResetPlayer(playerStartPosition, playerStartRotation);
        }

        private void RefreshSceneReferences()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<DemoPlayerController>();
            }

            if (slots == null || slots.Length == 0)
            {
                slots = FindObjectsByType<DemoRaftSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (items == null || items.Length == 0)
            {
                items = FindObjectsByType<DemoCarryableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
        }

        private void EnsureDemoWorldFallbacks()
        {
            if (GameObject.Find("Runtime Beach Spawn Pad") == null)
            {
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "Runtime Beach Spawn Pad";
                pad.transform.position = new Vector3(82f, 0.8f, -58f);
                pad.transform.localScale = new Vector3(64f, 0.24f, 44f);
                pad.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Warm Sand", new Color(0.78f, 0.55f, 0.25f, 1f));
            }

            if (GameObject.Find("Runtime Ocean Catch Floor") == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Runtime Ocean Catch Floor";
                floor.transform.position = new Vector3(0f, -8.5f, 0f);
                floor.transform.localScale = new Vector3(560f, 0.35f, 560f);
                floor.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Deep Ocean Floor", new Color(0.04f, 0.1f, 0.14f, 1f));
            }

            if (GameObject.Find("Runtime Ocean Depth Color") == null)
            {
                GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
                volume.name = "Runtime Ocean Depth Color";
                volume.transform.position = new Vector3(0f, -10f, 0f);
                volume.transform.localScale = new Vector3(560f, 20f, 560f);
                volume.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Ocean Volume", new Color(0.03f, 0.25f, 0.5f, 0.24f));
                Destroy(volume.GetComponent<Collider>());
            }
        }

        private static Material CreateRuntimeMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (color.a < 0.99f)
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetInt("_ZWrite", 0);
                }

                if (material.HasProperty("_Cull"))
                {
                    material.SetFloat("_Cull", 0f);
                }

                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }

        private void ApplyPlacedSlotsToRaft()
        {
            if (slots == null)
            {
                return;
            }

            foreach (DemoRaftSlot slot in slots)
            {
                if (slot != null && slot.IsOccupied)
                {
                    slot.ApplyToRaft();
                }
            }
        }

        private void SetRaftVisibleAndPhysical(bool value)
        {
            SetSectionVisibleAndPhysical(frontSection, value);
            SetSectionVisibleAndPhysical(rearSection, value);
        }

        private static void SetSectionVisibleAndPhysical(RaftSection section, bool value)
        {
            if (section == null)
            {
                return;
            }

            foreach (Renderer renderer in section.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject != section.gameObject)
                {
                    renderer.enabled = value;
                }
            }

            foreach (Collider collider in section.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = value;
            }

            Rigidbody body = section.Rigidbody;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = !value;
            body.useGravity = value;
        }

        private void UpdateLaunchedRun()
        {
            launchTimer += Time.deltaTime;

            if (connector != null && connector.IsBroken)
            {
                Fail("FAILED: Weak connection");
                return;
            }

            float frontSubmersion = frontSection != null ? frontSection.BuoyancyBody.AverageSubmersion : 0f;
            float rearSubmersion = rearSection != null ? rearSection.BuoyancyBody.AverageSubmersion : 0f;
            float averageSubmersion = (frontSubmersion + rearSubmersion) * 0.5f;
            if (averageSubmersion >= tooHeavySubmersion)
            {
                Fail("FAILED: Too much cargo");
                return;
            }

            float frontAngle = frontSection != null ? Vector3.Angle(frontSection.transform.up, Vector3.up) : 0f;
            float rearAngle = rearSection != null ? Vector3.Angle(rearSection.transform.up, Vector3.up) : 0f;
            if (Mathf.Max(frontAngle, rearAngle) >= badBalanceAngle)
            {
                Fail("FAILED: Bad balance");
                return;
            }

            if (launchTimer >= successSeconds)
            {
                state = DemoRunState.Success;
                message = "ESCAPED";
                prompt = "R: restart";
                physicsController?.SetDemoLaunched(false);
            }
        }

        private void Fail(string reason)
        {
            state = DemoRunState.Failed;
            message = reason;
            prompt = "R: restart";
            physicsController?.SetDemoLaunched(false);
        }

        private void OnGUI()
        {
            DrawCrosshair();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 124f, 420f, 108f), GUI.skin.box);
            GUILayout.Label(message);
            GUILayout.Label(prompt);
            if (state == DemoRunState.Launched)
            {
                GUILayout.Label($"Escape timer: {launchTimer:0.0} / {successSeconds:0.0}");
            }
            GUILayout.EndArea();

            if (!showDevUi)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width - 316f, 16f, 300f, 128f), GUI.skin.box);
            GUILayout.Label($"State: {state}");
            GUILayout.Label($"Build grid: {(buildGridOpen ? "open" : "closed")}");
            GUILayout.Label($"Slots used: {CountUsedSlots()} / {(slots != null ? slots.Length : 0)}");
            GUILayout.Label("R reset / F1 dev UI");
            GUILayout.EndArea();
        }

        private static void DrawCrosshair()
        {
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            Color previousColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(x - 8f, y - 1f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 8f, 2f, 16f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.DrawTexture(new Rect(x - 7f, y, 14f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y - 7f, 1f, 14f), Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private int CountUsedSlots()
        {
            int count = 0;
            if (slots == null)
            {
                return count;
            }

            foreach (DemoRaftSlot slot in slots)
            {
                if (slot != null && slot.IsOccupied)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
