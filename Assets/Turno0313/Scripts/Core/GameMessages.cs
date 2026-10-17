using System;

namespace Turno0313
{
    // Short on-screen messages ("Está cerrada.", report feedback...). Any HUD can listen to Shown.
    public static class GameMessages
    {
        public static event Action<string, float> Shown;

        public static void Show(string text, float seconds = 2.5f)
        {
            Shown?.Invoke(text, seconds);
        }
    }
}
