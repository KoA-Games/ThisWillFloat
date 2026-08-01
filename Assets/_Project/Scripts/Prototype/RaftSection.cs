using UnityEngine;

namespace ThisWillFloat.Prototype
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BuoyancyBody))]
    public class RaftSection : MonoBehaviour
    {
        [SerializeField] private string sectionName = "Raft Section";
        [SerializeField] private float structuralMass = 10f;
        [SerializeField] private Vector3 baseCenterOfMass = new Vector3(0f, -0.35f, 0f);

        private Rigidbody cachedRigidbody;
        private BuoyancyBody buoyancyBody;
        private float cargoMass;
        private Vector3 cargoMoment;

        public string SectionName => sectionName;
        public float StructuralMass => structuralMass;
        public Rigidbody Rigidbody
        {
            get
            {
                if (cachedRigidbody == null)
                {
                    cachedRigidbody = GetComponent<Rigidbody>();
                }

                return cachedRigidbody;
            }
        }

        public BuoyancyBody BuoyancyBody
        {
            get
            {
                if (buoyancyBody == null)
                {
                    buoyancyBody = GetComponent<BuoyancyBody>();
                }

                return buoyancyBody;
            }
        }

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            buoyancyBody = GetComponent<BuoyancyBody>();
            ResetLoad(structuralMass);
        }

        public void ResetLoad(float mass)
        {
            structuralMass = mass;
            cargoMass = 0f;
            cargoMoment = Vector3.zero;
            ApplyMassProperties();
        }

        public void AddCargoLoad(Vector3 localPosition, float mass)
        {
            cargoMass += mass;
            cargoMoment += localPosition * mass;
            ApplyMassProperties();
        }

        private void ApplyMassProperties()
        {
            Rigidbody.mass = structuralMass + cargoMass;

            Vector3 centerOfMass = (baseCenterOfMass * structuralMass + cargoMoment) / Rigidbody.mass;
            centerOfMass.y = Mathf.Clamp(centerOfMass.y, -0.45f, 0.18f);
            centerOfMass.x = Mathf.Clamp(centerOfMass.x, -0.55f, 0.55f);
            centerOfMass.z = Mathf.Clamp(centerOfMass.z, -0.55f, 0.55f);
            Rigidbody.centerOfMass = centerOfMass;
        }
    }
}
