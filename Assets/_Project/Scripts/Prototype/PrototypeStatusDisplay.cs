using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public class PrototypeStatusDisplay : MonoBehaviour
    {
        [SerializeField] private BuoyancyBody watchedBody;
        [SerializeField] private SectionConnector[] watchedConnectors;
        [SerializeField] private FailurePrototypeController controller;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 220f), GUI.skin.box);
            GUILayout.Label("Prototype Goal: 10-second failure scene");
            GUILayout.Label("1 left cargo / 2 right cargo");
            GUILayout.Label("B balanced / H too heavy / U unbalanced");
            GUILayout.Label("Drag mouse: orbit camera");

            if (controller != null)
            {
                GUILayout.Label($"Preset: {controller.CurrentPreset}");
                GUILayout.Label($"Cargo L/R: {controller.LeftCargoCount} / {controller.RightCargoCount}");
            }

            if (watchedBody != null)
            {
                GUILayout.Label($"Wet buoyancy points: {watchedBody.SubmergedPointRatio:P0}");
                GUILayout.Label($"Average point submersion: {watchedBody.AverageSubmersion:P0}");
            }

            if (watchedConnectors != null)
            {
                for (int i = 0; i < watchedConnectors.Length; i++)
                {
                    SectionConnector connector = watchedConnectors[i];
                    if (connector == null)
                    {
                        continue;
                    }

                    string state = connector.IsBroken
                        ? "BROKEN"
                        : $"{connector.Stress01:P0} stress / {connector.WarningProgress:P0} warning / {(connector.BreakArmed ? "armed" : "safe")}";
                    GUILayout.Label($"Connector {i + 1}: {state}");
                }
            }

            GUILayout.EndArea();
        }

        public void Watch(BuoyancyBody body, SectionConnector[] connectors)
        {
            watchedBody = body;
            watchedConnectors = connectors;
        }

        public void WatchController(FailurePrototypeController prototypeController)
        {
            controller = prototypeController;
        }
    }
}
