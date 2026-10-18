using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    // The look every menu in the game shares (title, pause, end screens), taken from the Piso 13 menu: the cover
    // art behind a dark left panel, left-aligned buttons with "›" on the focused one, hover/confirm sounds and the
    // menu music. Mouse, keyboard and gamepad. Used by GameMenus (Turno_Piso13) and P13RealidadMenus (Piso13
    // Realidad). The owner calls Tick() every frame and Navigate() while a menu is open, and draws in OnGUI
    // between Begin() and End().
    public sealed class MenuKit
    {
        // Where "Menú principal" goes from any scene.
        public const string TitleScene = "Turno_Piso13";
        // Virtual canvas: 720 units tall, as wide as the screen's aspect needs.
        public const float Height = 720;
        public float Width { get; private set; } = 1152;

        public static readonly Color Ink = new Color(.79f, .84f, .83f);
        public static readonly Color Accent = new Color(.62f, .76f, .72f);
        public static readonly Color TitleRed = new Color(1f, .25f, .16f);

        readonly AudioSource ui, music;
        readonly AudioClip hover, confirm;
        readonly List<Action> commands = new List<Action>();
        int focus, hovered = -1;
        bool pointerMoved;
        Vector2 previousMouse;
        float lastHover, lastClick, musicVolume;
        bool musicWanted;
        Texture2D cover;
        GUIStyle title, heading, body, small, button;

        public MenuKit(GameObject host)
        {
            hover = Resources.Load<AudioClip>("Audio/P13_UI_Hover");
            confirm = Resources.Load<AudioClip>("Audio/P13_UI_Confirm");
            ui = Source(host);
            music = Source(host);
            music.clip = Resources.Load<AudioClip>("Audio/P13_Menu_Ulrich");
            music.loop = true;
        }

        // Menus keep sounding while the game is paused (AudioListener.pause).
        static AudioSource Source(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.ignoreListenerPause = true;
            return source;
        }

        // Focus back on the first button, for each menu that opens.
        public void Reset()
        {
            focus = 0;
            hovered = -1;
            commands.Clear();
        }

        public void Music(bool on)
        {
            musicWanted = on;
            if (on && music.clip && !music.isPlaying)
                music.Play();
        }

        // Every frame: fades the menu music in and out.
        public void Tick()
        {
            musicVolume = Mathf.MoveTowards(musicVolume, musicWanted ? .45f : 0, Time.unscaledDeltaTime * .6f);
            music.volume = musicVolume;
            if (!musicWanted && musicVolume <= 0 && music.isPlaying)
                music.Stop();
        }

        // While a menu is open: arrows / W S / d-pad move the focus, Enter / Space / A choose.
        public void Navigate()
        {
            if (commands.Count == 0)
                return;
            int direction = Vertical();
            if (direction != 0)
            {
                focus = (focus + direction + commands.Count) % commands.Count;
                Hover();
            }

            focus = Mathf.Clamp(focus, 0, commands.Count - 1);
            if (ConfirmPressed())
            {
                Confirm();
                commands[focus]?.Invoke();
            }
        }

        // Esc / Start: opens the pause.
        public static bool PausePressed()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        // Esc / Start / B: closes the pause.
        public static bool BackPressed()
        {
            var pad = Gamepad.current;
            return PausePressed() || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }

        static int Vertical()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            int direction = 0;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    direction--;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    direction++;
            }

            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame)
                    direction--;
                if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame)
                    direction++;
            }

            return Math.Sign(direction);
        }

        static bool ConfirmPressed()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        public void Hover()
        {
            if (Time.unscaledTime - lastHover < .09f || Time.unscaledTime - lastClick < .12f)
                return;
            lastHover = Time.unscaledTime;
            if (hover)
                ui.PlayOneShot(hover, .45f);
        }

        public void Confirm()
        {
            if (Time.unscaledTime - lastClick < .09f)
                return;
            lastClick = Time.unscaledTime;
            ui.Stop();
            if (confirm)
                ui.PlayOneShot(confirm, .55f);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Drawing (inside OnGUI) ----

        public void Begin()
        {
            Styles();
            float scale = Screen.height / Height;
            Width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            pointerMoved = (Event.current.mousePosition - previousMouse).sqrMagnitude > .01f;
            commands.Clear();
        }

        public void End()
        {
            if (Event.current.type == EventType.Repaint)
                previousMouse = Event.current.mousePosition;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        void Styles()
        {
            if (title != null)
                return;
            cover = Resources.Load<Texture2D>("UI/Cover");
            title = new GUIStyle(GUI.skin.label) { fontSize = 55, fontStyle = FontStyle.Bold, wordWrap = true };
            title.normal.textColor = Ink;
            heading = new GUIStyle(title) { fontSize = 24 };
            body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            body.normal.textColor = Ink;
            small = new GUIStyle(body) { fontSize = 14 };
            small.normal.textColor = new Color(.6f, .66f, .66f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 12, 8, 8) };
        }

        public static void Panel(Rect r, float alpha)
        {
            GUI.color = new Color(.018f, .025f, .03f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public void Shade(float alpha) => Panel(new Rect(0, 0, Width, Height), alpha);

        // The Piso 13 cover, framed on the lit elevator door and the red "13". It is anchored to the panel edge,
        // so on any screen width the "PISO" lettering stays under the panel and doesn't fight with the game's
        // title; the art's own black edges blend into the black background on wide screens.
        public void CoverWithPanel(float panelWidth)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Width, Height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (cover)
            {
                const float size = 1440;
                GUI.DrawTexture(new Rect(panelWidth + 20 - .47f * size, Height / 2 - .478f * size, size, size), cover);
            }

            Panel(new Rect(0, 0, panelWidth, Height), .96f);
            for (int i = 0; i < 6; i++)
                Panel(new Rect(panelWidth + i * 10, 0, 10, Height), .8f * (1 - (i + 1) / 7f));
        }

        public void Title(Rect r, string text, Color? color = null)
        {
            title.normal.textColor = color ?? Ink;
            GUI.Label(r, text, title);
        }

        public void Heading(Rect r, string text) => GUI.Label(r, text, heading);

        public void Text(Rect r, string text, Color? color = null)
        {
            body.normal.textColor = color ?? Ink;
            GUI.Label(r, text, body);
        }

        public void Small(Rect r, string text) => GUI.Label(r, text, small);

        public void Button(Rect r, string text, Action action)
        {
            int id = commands.Count;
            commands.Add(action);
            bool inside = r.Contains(Event.current.mousePosition);
            if (inside && pointerMoved && hovered != id)
            {
                hovered = id;
                focus = id;
                Hover();
            }

            var old = GUI.backgroundColor;
            GUI.backgroundColor = focus == id ? new Color(.26f, .42f, .39f) : new Color(.10f, .15f, .16f);
            bool clicked = GUI.Button(r, (focus == id ? "›  " : "    ") + text, button);
            GUI.backgroundColor = old;
            if (focus == id)
            {
                GUI.color = Accent;
                GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (clicked)
            {
                focus = id;
                Confirm();
                action?.Invoke();
            }

            if (!inside && hovered == id)
                hovered = -1;
        }

        // The same pause screen in every scene.
        public void PauseScreen(Action resume, Action mainMenu)
        {
            Shade(.85f);
            float x = (Width - 420) / 2;
            Title(new Rect(x, 120, 500, 80), "PAUSA");
            Button(new Rect(x, 255, 420, 50), "Continuar", resume);
            Button(new Rect(x, 320, 420, 50), "Menú principal", mainMenu);
            Button(new Rect(x, 385, 420, 50), "Salir", Quit);
        }
    }
}
