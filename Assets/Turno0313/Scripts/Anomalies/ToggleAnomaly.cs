using UnityEngine;

namespace Turno0313
{
    // Swaps a normal set of objects for an anomalous one: the door that appears where there was a wall,
    // the EXIT sign that points at a wall, a cup that is always full...
    public sealed class ToggleAnomaly : Anomaly
    {
        [SerializeField] GameObject[] normalObjects;
        [SerializeField] GameObject[] anomalousObjects;

        protected override void ShowNormal() => Show(false);
        protected override void ShowAnomalous() => Show(true);

        void Show(bool anomalous)
        {
            foreach (var o in normalObjects)
                if (o)
                    o.SetActive(!anomalous);
            foreach (var o in anomalousObjects)
                if (o)
                    o.SetActive(anomalous);
        }
    }
}
