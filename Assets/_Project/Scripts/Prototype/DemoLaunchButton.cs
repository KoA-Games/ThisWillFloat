using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public enum DemoButtonAction
    {
        OpenBuildGrid,
        Launch
    }

    public class DemoLaunchButton : MonoBehaviour
    {
        [SerializeField] private DemoGameController gameController;
        [SerializeField] private DemoButtonAction action = DemoButtonAction.Launch;
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private Color readyColor = new Color(0.1f, 0.7f, 0.25f);
        [SerializeField] private Color launchedColor = new Color(0.7f, 0.18f, 0.12f);

        private bool launched;

        private void Awake()
        {
            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponentInChildren<Renderer>();
            }

            UpdateVisual();
        }

        public void Initialize(DemoGameController controller, DemoButtonAction buttonAction)
        {
            gameController = controller;
            action = buttonAction;
            launched = false;
            UpdateVisual();
        }

        public void Interact()
        {
            if (launched)
            {
                return;
            }

            launched = true;
            UpdateVisual();
            if (action == DemoButtonAction.OpenBuildGrid)
            {
                gameController?.OpenBuildGrid();
            }
            else
            {
                gameController?.Launch();
            }
        }

        public void ResetButton()
        {
            launched = false;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (buttonRenderer != null)
            {
                buttonRenderer.material.color = launched ? launchedColor : readyColor;
            }
        }
    }
}
