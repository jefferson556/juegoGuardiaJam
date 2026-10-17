using UnityEngine;

namespace Turno0313
{
    // "El reloj marca 03:13 y no avanza": the caseta wall clock shows the night time with a blinking
    // colon and ticks every second. Once active it jumps back to 03:13, the colon stops and so does the tick.
    // It is the first anomaly of every night (AnomalyManager.firstOfNight).
    public sealed class ClockAnomaly : Anomaly
    {
        [SerializeField] TextMesh display;
        [SerializeField] string frozenTime = "03:13";
        [SerializeField] AudioSource source;
        [Tooltip("Tic-tac de cada segundo. Se calla cuando el reloj se detiene.")]
        [SerializeField] AudioClip tickClip;

        int lastSecond = -1;

        void LateUpdate()
        {
            if (!display)
                return;
            if (IsActive)
            {
                display.text = frozenTime;
                return;
            }

            var clock = NightClock.Instance;
            string time = clock ? clock.Display : frozenTime;
            // The colon blinks once per real second; it is hidden with a transparent colour so the digits don't shift.
            bool colon = Time.time % 1f < .5f;
            display.text = colon ? time : time.Replace(":", "<color=#00000000>:</color>");

            int second = Mathf.FloorToInt(Time.time);
            if (second != lastSecond)
            {
                lastSecond = second;
                if (source && tickClip)
                    source.PlayOneShot(tickClip);
            }
        }

        protected override void ShowNormal() => LateUpdate();

        protected override void ShowAnomalous()
        {
            if (display)
                display.text = frozenTime;
        }
    }
}
