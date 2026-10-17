using UnityEngine;

namespace Turno0313
{
    // Plays a random step clip every time the PlayerController completes a step.
    public sealed class Footsteps : MonoBehaviour
    {
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip[] clips;
        [SerializeField, Range(0, 1)] float volume = .45f;

        PlayerController player;
        int last = -1;

        void Awake()
        {
            player = GetComponentInParent<PlayerController>();
            if (player)
                player.Step += OnStep;
        }

        void OnDestroy()
        {
            if (player)
                player.Step -= OnStep;
        }

        void OnStep(bool running)
        {
            if (!source || clips == null || clips.Length == 0)
                return;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last)
                i = (i + 1) % clips.Length;
            last = i;
            source.pitch = Random.Range(.94f, 1.06f);
            source.PlayOneShot(clips[i], running ? volume * 1.3f : volume);
        }
    }
}
