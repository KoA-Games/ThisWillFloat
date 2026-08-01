using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public enum DemoMaterialType
    {
        Plank,
        Log,
        FloatBarrel,
        HeavyCrate,
        Rock,
        Rope
    }

    public class DemoCarryableItem : MonoBehaviour
    {
        [SerializeField] private DemoMaterialType materialType = DemoMaterialType.HeavyCrate;
        [SerializeField] private float mass = 8f;
        [SerializeField] private float buoyancyBonus;
        [SerializeField] private float connectorStress;
        [SerializeField] private float connectorStrengthBonus;
        [SerializeField] private string displayName = "Cargo";

        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Rigidbody cachedRigidbody;
        private Collider[] colliders;

        public DemoMaterialType MaterialType => materialType;
        public float Mass => mass;
        public float BuoyancyBonus => buoyancyBonus;
        public float ConnectorStress => connectorStress;
        public float ConnectorStrengthBonus => connectorStrengthBonus;
        public string DisplayName => displayName;
        public bool IsPlaced { get; private set; }
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            originalParent = transform.parent;
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            cachedRigidbody = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            SetRestingPhysics();
        }

        public void Configure(DemoMaterialType type, string itemName, float itemMass, float itemBuoyancyBonus, float itemConnectorStress, float itemConnectorStrengthBonus)
        {
            materialType = type;
            displayName = itemName;
            mass = itemMass;
            buoyancyBonus = itemBuoyancyBonus;
            connectorStress = itemConnectorStress;
            connectorStrengthBonus = itemConnectorStrengthBonus;
        }

        public void PickUp(Transform holdPoint)
        {
            IsHeld = true;
            IsPlaced = false;
            transform.SetParent(holdPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetPhysicsEnabled(false);
        }

        public void Drop(Vector3 worldPosition, Quaternion worldRotation)
        {
            IsHeld = false;
            IsPlaced = false;
            transform.SetParent(null);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            SetPhysicsEnabled(true);
        }

        public void PlaceOnSlot(Transform slotTransform, Vector3 localPosition, Quaternion localRotation)
        {
            IsHeld = false;
            IsPlaced = true;
            transform.SetParent(slotTransform, false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            SetPhysicsEnabled(false);
        }

        public void ResetItem()
        {
            IsHeld = false;
            IsPlaced = false;
            transform.SetParent(originalParent);
            transform.SetPositionAndRotation(originalPosition, originalRotation);
            SetRestingPhysics();
        }

        private void SetRestingPhysics()
        {
            if (cachedRigidbody != null)
            {
                cachedRigidbody.isKinematic = false;
                cachedRigidbody.linearVelocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.isKinematic = true;
                cachedRigidbody.useGravity = false;
            }

            foreach (Collider itemCollider in colliders)
            {
                if (itemCollider != null)
                {
                    itemCollider.enabled = true;
                }
            }
        }

        private void SetPhysicsEnabled(bool value)
        {
            if (cachedRigidbody != null)
            {
                cachedRigidbody.isKinematic = false;
                cachedRigidbody.linearVelocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.isKinematic = !value;
                cachedRigidbody.useGravity = value;
            }

            foreach (Collider itemCollider in colliders)
            {
                if (itemCollider != null)
                {
                    itemCollider.enabled = value;
                }
            }
        }
    }
}
