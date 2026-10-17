using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13
{
    /// <summary>Resolution independent desktop UI, with mouse, keyboard and gamepad focus.</summary>
    public sealed class P13UI : MonoBehaviour
    {
        public P13Game Game;
        public bool Options;
        int tab, focus, hovered = -1;
        bool bindPad;
        string capturing;
        float captureAfter;
        Texture2D cover;
        GUIStyle title, body, small, button, heading;
        Vector2 scroll, previousMouse, journalScroll;
        bool pointerMoved;
        readonly List<Action> commands = new List<Action>();
        readonly List<Action<int>> adjustments = new List<Action<int>>();
        const float Width = 1152, Height = 720;
        Color ink = new Color(.79f, .84f, .83f), accent = new Color(.62f, .76f, .72f);
        public void Reset()
        {
            Options = false;
            capturing = null;
            focus = 0;
            hovered = -1;
            commands.Clear();
            adjustments.Clear();
            scroll = Vector2.zero;
        }

        public void Navigation()
        {
            if (capturing != null)
            {
                if (Time.unscaledTime < captureAfter)
                    return;
                if (P13Input.Back)
                {
                    capturing = null;
                    return;
                }

                string token = bindPad ? P13Input.CapturePad() : P13Input.CaptureKey();
                if (token != null)
                {
                    Game.Input.Remap(capturing, token, bindPad);
                    Game.SaveSettings();
                    capturing = null;
                    Game.Audio.Confirm();
                }

                return;
            }

            if (P13Input.Back)
            {
                if (Options)
                    CloseOptions();
                else if (Game.Phase == GamePhase.Paused)
                    Game.Pause(false);
                return;
            }

            int direction = P13Input.Vertical;
            if (direction != 0 && commands.Count > 0)
            {
                focus = (focus + direction + commands.Count) % commands.Count;
                Game.Audio.Hover();
            }

            if (focus < 0 || focus >= commands.Count)
                return;
            int horizontal = P13Input.Horizontal;
            if (horizontal != 0 && adjustments[focus] != null)
                adjustments[focus](horizontal);
            if (P13Input.Confirm)
            {
                Game.Audio.Confirm();
                var action = commands[focus];
                action?.Invoke();
            }
        }

        void Styles()
        {
            if (title != null)
                return;
            cover = Resources.Load<Texture2D>("UI/Cover");
            title = new GUIStyle(GUI.skin.label)
            {fontSize = 55, fontStyle = FontStyle.Bold, wordWrap = true};
            title.normal.textColor = ink;
            heading = new GUIStyle(title)
            {fontSize = 24};
            body = new GUIStyle(GUI.skin.label)
            {fontSize = 18, wordWrap = true};
            body.normal.textColor = ink;
            small = new GUIStyle(body)
            {fontSize = 14};
            button = new GUIStyle(GUI.skin.button)
            {fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 12, 8, 8)};
        }

        void OnGUI()
        {
            if (Game == null)
                return;
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / Width, Screen.height / Height, 1));
            pointerMoved = (Event.current.mousePosition - previousMouse).sqrMagnitude > .01f;
            commands.Clear();
            adjustments.Clear();
            if (Game.Phase == GamePhase.Error)
            {
                Panel(new Rect(0, 0, Width, Height), .98f);
                Label(new Rect(60, 55, 1000, 60), "No se pudo iniciar Piso 13", heading);
                Label(new Rect(60, 130, 1000, 500), Game.Failure, small);
                return;
            }

            if (Game.Phase == GamePhase.Menu)
                Menu();
            else if (Game.Phase == GamePhase.Paused)
            {
                Panel(new Rect(0, 0, Width, Height), .85f);
                if (!Options)
                    PauseMenu();
            }
            else if (Game.Phase == GamePhase.Won || Game.Phase == GamePhase.Lost)
                EndScreen();
            else if (Game.Phase == GamePhase.Playing)
                HUD();
            else if (Game.Phase == GamePhase.Capture)
            {
                Panel(new Rect(0, 0, Width, Height), .08f);
            }

            if (Options)
                OptionsPanel();
            if (capturing != null)
            {
                Panel(new Rect(220, 265, 712, 160), .98f);
                Label(new Rect(250, 290, 652, 55), bindPad ? "Pulsa el botón del mando que deseas asignar" : "Pulsa la tecla que deseas asignar", heading);
                Label(new Rect(250, 355, 650, 35), "ESC / B para cancelar", small);
            }

            if (Event.current.type == EventType.Repaint)
                previousMouse = Event.current.mousePosition;
        }

        void RepaintHover()
        { /* Focus is assigned only on pointer entry, never continuously. */
        }

        void Label(Rect r, string text, GUIStyle style = null)
        {
            GUI.Label(r, text, style ?? body);
        }

        static void Panel(Rect r, float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(.018f, .025f, .03f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Button(Rect r, string text, Action action, Action<int> adjust = null)
        {
            int id = commands.Count;
            commands.Add(action);
            adjustments.Add(adjust);
            bool inside = r.Contains(Event.current.mousePosition);
            if (inside && pointerMoved && hovered != id)
            {
                hovered = id;
                focus = id;
                Game.Audio.Hover();
            }

            var old = GUI.backgroundColor;
            GUI.backgroundColor = focus == id ? new Color(.26f, .42f, .39f) : new Color(.10f, .15f, .16f);
            if (GUI.Button(r, (focus == id ? "›  " : "    ") + text, button) && capturing == null)
            {
                focus = id;
                Game.Audio.Confirm();
                action?.Invoke();
            }

            if (!inside && hovered == id)
                hovered = -1;
            GUI.backgroundColor = old;
        }

        void Menu()
        {
            Panel(new Rect(0, 0, Width, Height), 1);
            if (cover)
                GUI.DrawTexture(new Rect(0, 0, Width, Height), cover, ScaleMode.ScaleAndCrop);
            Panel(new Rect(0, 0, 550, Height), .88f);
            Label(new Rect(70, 90, 430, 90), "PISO 13", title);
            Label(new Rect(74, 181, 410, 70), "Algo no está bien.\nTu turno comienza a medianoche.", body);
            float y = 300;
            if (Game.CanContinue)
            {
                Button(new Rect(70, y, 390, 48), "Continuar", Game.Continue);
                y += 62;
            }

            Button(new Rect(70, y, 390, 48), "Demo corta", Game.StartDemo);
            y += 62;
            Button(new Rect(70, y, 390, 48), "Opciones", OpenOptions);
            y += 62;
            Button(new Rect(70, y, 390, 48), "Salir", Quit);
            Label(new Rect(74, 625, 425, 70), Game.Message, small);
        }

        void PauseMenu()
        {
            Label(new Rect(365, 120, 500, 80), "PAUSA", title);
            Button(new Rect(365, 255, 420, 50), "Continuar", () => Game.Pause(false));
            Button(new Rect(365, 320, 420, 50), "Opciones", OpenOptions);
            Button(new Rect(365, 385, 420, 50), "Menú principal", Game.ToMenu);
            Button(new Rect(365, 450, 420, 50), "Salir", Quit);
        }

        void OpenOptions()
        {
            Options = true;
            focus = 0;
            hovered = -1;
            commands.Clear();
        }

        void CloseOptions()
        {
            Game.Settings.Apply();
            Game.SaveSettings();
            Options = false;
            focus = 0;
            commands.Clear();
            Game.Input.Neutralize();
        }

        void OptionsPanel()
        {
            Panel(new Rect(110, 45, 932, 640), .99f);
            Label(new Rect(145, 68, 600, 50), "OPCIONES", heading);
            string[] names = {"CONTROLES", "VIDEO", "SONIDO"};
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                Button(new Rect(145 + i * 290, 126, 275, 42), names[i], () =>
                {
                    tab = selected;
                    focus = selected;
                    scroll = Vector2.zero;
                }

                );
            }

            if (tab == 0)
                Controls();
            else if (tab == 1)
                Video();
            else
                Sound();
            Button(new Rect(750, 612, 250, 42), "Guardar y volver", CloseOptions);
        }

        void Controls()
        {
            Button(new Rect(145, 187, 400, 38), bindPad ? "Asignar: mando" : "Asignar: teclado", () =>
            {
                bindPad = !bindPad;
            }

            );
            Label(new Rect(580, 190, 415, 50), "Ratón o stick derecho: mirar\nESC / B: cerrar panel o pausar", small);
            scroll = GUI.BeginScrollView(new Rect(145, 240, 860, 355), scroll, new Rect(0, 0, 830, 570));
            for (int i = 0; i < P13Input.Actions.Length; i++)
            {
                var action = P13Input.Actions[i];
                var binding = Game.Settings.bindings.Find(b => b.action == action);
                Label(new Rect(0, i * 37, 360, 32), P13Input.Titles[i]);
                Button(new Rect(370, i * 37, 420, 32), bindPad ? (string.IsNullOrEmpty(binding.pad) ? "Stick izquierdo" : binding.pad) : binding.key, () =>
                {
                    capturing = action;
                    captureAfter = Time.unscaledTime + .2f;
                }

                );
            }

            Button(new Rect(0, 525, 790, 35), "Restablecer controles", () =>
            {
                Game.Settings.bindings = P13Input.Defaults();
                Game.SaveSettings();
            }

            );
            GUI.EndScrollView();
        }

        void Video()
        {
            var s = Game.Settings;
            string[] q = {"Baja", "Media", "Alta"};
            Button(new Rect(145, 205, 850, 42), "Calidad: " + q[s.quality], () =>
            {
                s.quality = (s.quality + 1) % 3;
                s.Apply();
            }

            , d =>
            {
                s.quality = (s.quality + d + 3) % 3;
                s.Apply();
            }

            );
            Button(new Rect(145, 267, 850, 42), "Pantalla: " + (s.fullscreen ? "Completa" : "Ventana"), () =>
            {
                s.fullscreen = !s.fullscreen;
                s.Apply();
            }

            );
            Button(new Rect(145, 329, 850, 42), "Resolución: " + s.width + " × " + s.height, () =>
            {
                s.width = s.width == 1152 ? 1440 : 1152;
                s.height = s.width == 1152 ? 720 : 900;
                s.Apply();
            }

            );
            Slider(new Rect(145, 402, 850, 52), "Brillo", s.brightness, -.15f, .15f, v => s.brightness = v);
            Slider(new Rect(145, 480, 850, 52), "Sensibilidad", s.sensitivity, .3f, 2, v => s.sensitivity = v);
        }

        void Sound()
        {
            var s = Game.Settings;
            Slider(new Rect(145, 195, 850, 58), "Volumen general", s.master, 0, 1, v => s.master = v);
            Slider(new Rect(145, 272, 850, 58), "Música", s.music, 0, 1, v => s.music = v);
            Slider(new Rect(145, 349, 850, 58), "Efectos", s.effects, 0, 1, v => s.effects = v);
            Slider(new Rect(145, 426, 850, 58), "Ambiente", s.ambience, 0, 1, v => s.ambience = v);
            Slider(new Rect(145, 503, 850, 58), "Voces", s.voices, 0, 1, v => s.voices = v);
        }

        void Slider(Rect r, string text, float value, float low, float high, Action<float> change)
        {
            Button(new Rect(r.x, r.y, 350, 36), text + "  " + Mathf.RoundToInt((value - low) / (high - low) * 100) + "%", () =>
            {
            }

            , d => change(Mathf.Clamp(value + d * (high - low) * .05f, low, high)));
            float next = GUI.HorizontalSlider(new Rect(r.x + 390, r.y + 14, 450, 25), value, low, high);
            if (Mathf.Abs(next - value) > .0001f)
                change(next);
        }

        void HUD()
        {
            var g = Game;
            Panel(new Rect(22, 18, 285, 75), .76f);
            Label(new Rect(37, 26, 270, 28), string.Format("{0:00}:{1:00} AM  ·  PISO {2}", g.Minute / 60, g.Minute % 60, g.Floor));
            Label(new Rect(37, 57, 260, 28), "Energía " + Mathf.CeilToInt(g.Energy) + "% · Noche " + g.Night, small);
            if (g.CCTV)
                Cameras();
            else if (g.Map)
                MapPanel();
            else
            {
                GUI.Label(new Rect(Width / 2 - 4, Height / 2 - 10, 20, 20), "·", body);
                Panel(new Rect(22, 604, 690, 91), .7f);
                Label(new Rect(37, 615, 660, 30), g.Objective, small);
                string hint = g.Building.Hint;
                if (string.IsNullOrEmpty(hint) && g.Clues.Count < g.Data.narrative.Length && g.Building.Near(g.Data.narrative[g.Clues.Count].position))
                    hint = g.Input.Label("examine") + ": examinar " + g.Data.narrative[g.Clues.Count].title;
                if (string.IsNullOrEmpty(hint))
                    hint = g.Input.Label("interact") + ": interactuar  ·  " + g.Input.Label("cctv") + ": CCTV  ·  " + g.Input.Label("map") + ": mapa";
                Label(new Rect(37, 652, 660, 30), hint, small);
                Label(new Rect(735, 638, 390, 55), g.Message, small);
            }

            if (g.Task >= 0)
            {
                Panel(new Rect(370, 460, 420, 80), .9f);
                Label(new Rect(390, 472, 380, 34), "Trabajando… " + Mathf.CeilToInt(g.TaskRemaining) + " s");
                Label(new Rect(390, 509, 380, 30), g.Input.Label("cancel") + ": cancelar", small);
            }

            if (!string.IsNullOrEmpty(g.Subtitle))
            {
                Panel(new Rect(225, 506, 702, 95), .9f);
                Label(new Rect(245, 515, 662, 24), g.Speaker, small);
                Label(new Rect(245, 541, 662, 58), g.Subtitle, body);
            }
        }

        void Cameras()
        {
            var g = Game;
            float interference = Mathf.Max(g.Horror.Interference, g.Guard.Interference(g.Observer));
            if (g.Horror.SignalLost)
                Panel(new Rect(0, 0, Width, Height), 1);
            if (interference > 0)
            {
                var old = GUI.color;
                GUI.color = new Color(.6f, .7f, .65f, interference * .16f);
                for (int y = 0; y < 720; y += 9)
                    GUI.DrawTexture(new Rect(0, y + (Time.frameCount % 3), Width, 2), Texture2D.whiteTexture);
                GUI.color = old;
            }

            Label(new Rect(350, 35, 720, 50), g.Data.cameras[g.CameraIndex].name, heading);
            if (g.Horror.SignalLost)
                Label(new Rect(430, 325, 500, 60), "SIN SEÑAL", heading);
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                Button(new Rect(30 + i * 123, 636, 115, 47), "CAM " + (i + 1).ToString("00"), () => g.CameraIndex = index);
            }

            Label(new Rect(30, 590, 1000, 35), "← / →: cambiar cámara  ·  Ratón / stick: orientar  ·  ESC / B: cerrar", small);
        }

        void MapPanel()
        {
            var g = Game;
            Panel(new Rect(105, 100, 942, 520), .96f);
            Label(new Rect(135, 118, 780, 40), "PLANTA " + g.Floor + "  ·  REGISTRO DE RONDA", heading);
            Rect area = new Rect(140, 175, 430, 415);
            Func<Vector3, Vector2> point = p => g.Floor == 13 ? new Vector2(area.x + (p.x + 8) / 16 * area.width, area.y + (8 - p.z) / 16 * area.height) : new Vector2(area.x + (p.x - 24) / 12 * area.width, area.y + (24 - p.z) / 32 * area.height);
            foreach (var o in g.World.Obstacles(g.Floor == 12))
            {
                var a = point(new Vector3(o.minX, 0, o.maxZ));
                var b = point(new Vector3(o.maxX, 0, o.minZ));
                var old = GUI.color;
                GUI.color = new Color(.4f, .48f, .47f);
                GUI.DrawTexture(new Rect(a.x, a.y, Mathf.Max(2, b.x - a.x), Mathf.Max(2, b.y - a.y)), Texture2D.whiteTexture);
                GUI.color = old;
            }

            Vector2 player = point(g.Player);
            Label(new Rect(player.x - 6, player.y - 12, 30, 25), "●");
            if (g.Floor == 13)
                for (int i = 0; i < 9; i++)
                {
                    if (!g.Discovered.Contains(i + 1) && i != 0)
                        continue;
                    var p = point(g.Data.cameras[i].target);
                    Label(new Rect(p.x - 12, p.y - 10, 80, 25), (i + 1).ToString("00"), small);
                }

            Label(new Rect(610, 180, 395, 70), g.Objective);
            var entries = g.Data.journal.Where(e => e.id == 1 || g.Discovered.Contains(e.id)).ToArray();
            journalScroll = GUI.BeginScrollView(new Rect(610, 270, 395, 295), journalScroll, new Rect(0, 0, 365, entries.Length * 175 + 80));
            for (int i = 0; i < entries.Length; i++)
            {
                Label(new Rect(0, i * 175, 360, 42), entries[i].title, body);
                Label(new Rect(0, i * 175 + 48, 360, 122), entries[i].text, small);
            }

            Label(new Rect(0, entries.Length * 175, 360, 50), "Pistas examinadas: " + g.Clues.Count + " / " + g.Data.narrative.Length, small);
            GUI.EndScrollView();
            Label(new Rect(610, 580, 395, 30), "ESC / B: cerrar mapa", small);
        }

        void EndScreen()
        {
            bool won = Game.Phase == GamePhase.Won;
            Panel(new Rect(0, 0, Width, Height), .94f);
            Label(new Rect(170, 160, 840, 130), won ? "06:00 AM\nTU TURNO HA FINALIZADO" : "FIN DEL TURNO", title);
            Label(new Rect(175, 335, 800, 75), won ? "Ronda registrada. Has sobrevivido hasta el amanecer." : "El guardia te ha encontrado.");
            Button(new Rect(175, 450, 420, 50), "Menú principal", Game.ToMenu);
        }

        void Quit()
        {
            Game.Save();
            Game.SaveSettings();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
