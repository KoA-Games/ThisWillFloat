using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public class DemoRaftSlot : MonoBehaviour
    {
        [SerializeField] private RaftSection targetSection;
        [SerializeField] private FailurePrototypeController physicsController;
        [SerializeField] private Renderer slotRenderer;
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.9f, 0.35f, 0.55f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.75f, 0.16f, 0.8f);
        [SerializeField] private float deckSurfaceY = 0.2f;
        [SerializeField] private Vector3 targetLocalLoadPosition;

        private DemoCarryableItem placedItem;

        public bool IsOccupied => placedItem != null;
        public DemoCarryableItem PlacedItem => placedItem;
        public RaftSection TargetSection => targetSection;

        private void Awake()
        {
            UpdateVisual();
        }

        public void Initialize(RaftSection section, FailurePrototypeController controller, float surfaceY, Vector3 localLoadPosition)
        {
            targetSection = section;
            physicsController = controller;
            deckSurfaceY = surfaceY;
            targetLocalLoadPosition = localLoadPosition;
            slotRenderer = GetComponentInChildren<Renderer>();
            UpdateVisual();
        }

        public bool CanPlace(DemoCarryableItem item)
        {
            return item != null && placedItem == null && targetSection != null;
        }

        public bool Place(DemoCarryableItem item)
        {
            if (!CanPlace(item))
            {
                return false;
            }

            placedItem = item;
            item.PlaceOnSlot(transform, Vector3.zero, Quaternion.identity);
            AlignItemToPreviewGrid(item);
            UpdateVisual();
            return true;
        }

        public void Clear()
        {
            placedItem = null;
            UpdateVisual();
        }

        public Vector3 LocalLoadPosition()
        {
            if (targetSection == null)
            {
                return Vector3.zero;
            }

            return targetLocalLoadPosition;
        }

        public void ApplyToRaft()
        {
            if (placedItem == null)
            {
                return;
            }

            MovePlacedItemToRaft();
            physicsController?.ApplyDemoPlacedItem(this, placedItem);
        }

        private void MovePlacedItemToRaft()
        {
            if (targetSection == null || placedItem == null)
            {
                return;
            }

            placedItem.PlaceOnSlot(targetSection.transform, targetLocalLoadPosition, Quaternion.identity);
            AlignItemToDeck(placedItem);
        }

        private void AlignItemToPreviewGrid(DemoCarryableItem item)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            float minWorldY = float.PositiveInfinity;
            foreach (Renderer itemRenderer in renderers)
            {
                minWorldY = Mathf.Min(minWorldY, itemRenderer.bounds.min.y);
            }

            if (minWorldY == float.PositiveInfinity)
            {
                return;
            }

            item.transform.position += Vector3.up * (transform.position.y + 0.03f - minWorldY);
        }

        private void AlignItemToDeck(DemoCarryableItem item)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || targetSection == null)
            {
                return;
            }

            float minLocalY = float.PositiveInfinity;
            foreach (Renderer itemRenderer in renderers)
            {
                Bounds bounds = itemRenderer.bounds;
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
                            minLocalY = Mathf.Min(minLocalY, targetSection.transform.InverseTransformPoint(worldCorner).y);
                        }
                    }
                }
            }

            if (minLocalY == float.PositiveInfinity)
            {
                return;
            }

            Vector3 worldOffset = targetSection.transform.TransformVector(Vector3.up * (deckSurfaceY - minLocalY));
            item.transform.position += worldOffset;
        }

        private void UpdateVisual()
        {
            if (slotRenderer == null)
            {
                return;
            }

            slotRenderer.material.color = IsOccupied ? occupiedColor : emptyColor;
        }
    }
}
