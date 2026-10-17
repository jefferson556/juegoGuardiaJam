using System;
using UnityEngine;

namespace Turno0313
{
    public enum GameAct
    {
        Dream = 1,
        Reality = 2
    }

    [Flags]
    public enum ActMask
    {
        Dream = 1,
        Reality = 2,
        Both = Dream | Reality
    }

    public enum AnomalyState
    {
        Normal,
        Active,
        Resolved
    }

    // Base for every anomaly. Subclasses only decide how the normal and the anomalous versions look;
    // the AnomalyManager decides when to activate them and handles reports.
    public abstract class Anomaly : MonoBehaviour
    {
        [SerializeField] string displayName = "Anomalía";
        [SerializeField] ActMask acts = ActMask.Dream;
        [Tooltip("Falso cuando la versión normal no es un objeto que se pueda reportar (por ejemplo, un trozo de pared).")]
        [SerializeField] bool reportableWhenNormal = true;

        public string DisplayName => displayName;
        public ActMask Acts => acts;
        public AnomalyState State { get; private set; }
        public bool IsActive => State == AnomalyState.Active;
        // Active and already looking wrong. Some anomalies wait until the player looks away.
        public bool IsManifested => IsActive && Manifested;
        public bool Noticed { get; internal set; }
        public bool ReportableWhenNormal => reportableWhenNormal;

        Renderer[] renderers;

        protected virtual void Awake() => renderers = GetComponentsInChildren<Renderer>(true);

        // What the player has to see for the anomaly to count as visible.
        public virtual Bounds ViewBounds
        {
            get
            {
                var bounds = new Bounds();
                bool any = false;
                foreach (var r in renderers)
                {
                    if (!r || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    if (any)
                        bounds.Encapsulate(r.bounds);
                    else
                    {
                        bounds = r.bounds;
                        any = true;
                    }
                }

                return bounds;
            }
        }

        public bool AvailableIn(GameAct act) => (acts & (ActMask)(int)act) != 0;

        protected virtual bool Manifested => true;
        protected abstract void ShowNormal();
        protected abstract void ShowAnomalous();

        // Called every frame by the AnomalyManager. 'visible' means the player can see it right now.
        protected internal virtual void Tick(bool visible, float dt)
        {
        }

        internal void Activate()
        {
            State = AnomalyState.Active;
            Noticed = false;
            ShowAnomalous();
        }

        internal void Resolve()
        {
            State = AnomalyState.Resolved;
            ShowNormal();
        }

        internal void ResetToNormal()
        {
            State = AnomalyState.Normal;
            Noticed = false;
            ShowNormal();
        }
    }
}
