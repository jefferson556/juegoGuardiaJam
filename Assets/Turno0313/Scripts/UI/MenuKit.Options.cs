using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turno0313
{
    public sealed partial class MenuKit
    {
        public bool OptionsOpen { get; private set; }
        int optionTab, optionPage;
        bool bindPad;
        InputActionRebindingExtensions.RebindingOperation capturing;
        InputAction captureAction;
        string captureTitle, feedback;
        float captureStarted;
        const int RowsPerPage = 6;
        struct BindingRow
        {
            public string title; public InputAction action; public int index;
            public BindingRow(string title, InputAction action, int index) { this.title = title; this.action = action; this.index = index; }
        }
        List<BindingRow> BindingRows()
        {
            GameInput.Enable();
            var rows = new List<BindingRow>();
            if (!bindPad)
            {
                rows.Add(new BindingRow("Avanzar", GameInput.Move, 1)); rows.Add(new BindingRow("Retroceder", GameInput.Move, 2));
                rows.Add(new BindingRow("Izquierda", GameInput.Move, 3)); rows.Add(new BindingRow("Derecha", GameInput.Move, 4));
            }
            var actions = new[] { GameInput.Run, GameInput.Interact, GameInput.Report, GameInput.Flashlight, GameInput.Notes, GameInput.Cameras, GameInput.Crouch };
            var names = new[] { "Correr", "Interactuar", "Reportar", "Linterna", "Bitácora", "CCTV", "Agacharse" };
            for (int n = 0; n < actions.Length; n++)
                for (int i = 0; i < actions[n].bindings.Count; i++)
                    if (actions[n].bindings[i].path.StartsWith(bindPad ? "<Gamepad>" : "<Keyboard>"))
                    { rows.Add(new BindingRow(names[n], actions[n], i)); break; }
            return rows;
        }
        public void OpenOptions() { OptionsOpen = true; optionTab = optionPage = 0; feedback = null; Reset(); }
        public void CloseOptions()
        {
            CancelBinding(); OptionsOpen = false; DemoSettings.Current.Save(); DemoSettingsRuntime.StopPreview(); Reset();
        }
        public void Dispose() { CancelBinding(); DemoSettingsRuntime.StopPreview(); }
        public bool HandleOptionsBack()
        {
            if (!OptionsOpen) return false;
            if (capturing != null) return true;
            if (BackPressed()) CloseOptions(); else Navigate();
            return true;
        }
        void EnsureOptionVisible() { /* Binding pages contain only visible, navigable rows. */ }
        void TickBinding()
        {
            if (capturing != null && Time.unscaledTime - captureStarted > 10) CancelBinding();
        }
        void CancelBinding()
        {
            var operation = capturing; capturing = null;
            if (operation != null) operation.Cancel();
            if (captureAction != null) { captureAction.Enable(); captureAction = null; }
        }
        void Capture(BindingRow row)
        {
            CancelBinding();
            captureTitle = row.title; captureStarted = Time.unscaledTime; captureAction = row.action;
            string oldPath = row.action.bindings[row.index].effectivePath;
            row.action.Disable();
            capturing = row.action.PerformInteractiveRebinding(row.index)
                .WithControlsHavingToMatchPath(bindPad ? "<Gamepad>/*" : "<Keyboard>/*")
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(op => { op.Dispose(); capturing = null; row.action.Enable(); captureAction = null; Reset(); })
                .OnComplete(op =>
                {
                    string newPath = row.action.bindings[row.index].effectivePath;
                    // Swap duplicate bindings rather than activating two actions with one press.
                    foreach (var other in BindingRows())
                        if ((other.action != row.action || other.index != row.index) && other.action.bindings[other.index].effectivePath == newPath)
                            other.action.ApplyBindingOverride(other.index, oldPath);
                    op.Dispose(); capturing = null; row.action.Enable(); captureAction = null;
                    DemoSettings.Current.StoreBindings(); feedback = "Control guardado."; Reset();
                });
            if (bindPad) capturing.WithControlsExcluding("<Gamepad>/buttonEast");
            capturing.Start();
        }
        public void OptionsScreen()
        {
            Shade(1);
            float w = Mathf.Min(860, Width - 40), x = (Width - w) / 2;
            var panel = new Rect(x, 35, w, 650);
            GUI.color = FillColor(Accent); Border(panel); GUI.color = Color.white;
            Heading(new Rect(x + 25, 50, w - 50, 40), "O P C I O N E S");
            string[] tabs = { "Controles", "Video", "Sonido" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(new Rect(x + 25 + i * (w - 50) / 3, 104, (w - 65) / 3, 42), tabs[i],
                    () => { CancelBinding(); optionTab = tab; optionPage = 0; Reset(); }, selected: optionTab == i);
            }
            if (capturing != null)
            {
                Text(new Rect(x + 25, 190, w - 50, 100), captureTitle + ": pulsa " + (bindPad ? "un botón del mando" : "una tecla") + ". Esc cancela. Tiempo máximo: 10 segundos.");
                if (bindPad && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) CancelBinding();
                Button(new Rect(x + 25, 330, w - 50, 42), "Cancelar", CancelBinding);
            }
            else if (optionTab == 0) ControlsOptions(x + 25, w - 50);
            else if (optionTab == 1) VideoOptions(x + 25, w - 50);
            else SoundOptions(x + 25, w - 50);
            if (capturing == null)
            {
                Button(new Rect(x + 25, 568, w - 50, 36), "Restablecer esta pestaña", () => DemoSettings.Current.RestoreTab(optionTab));
                Small(new Rect(x + 25, 609, w - 50, 23), feedback ?? "↑ ↓ Seleccionar · ← → Ajustar · Enter / A / Cruz Confirmar");
                Button(new Rect(x + 25, 637, w - 50, 34), "Volver", CloseOptions);
            }
        }
        void ControlsOptions(float x, float w)
        {
            var s = DemoSettings.Current;
            Small(new Rect(x, 154, w, 24), Gamepad.current == null ? "Sin mando conectado" : Gamepad.current.displayName);
            Button(new Rect(x, 182, w, 34), "Editar bindings: " + (bindPad ? "Mando" : "Teclado"), () => { bindPad = !bindPad; optionPage = 0; Reset(); });
            string[] symbols = { "Automático", "PS5", "Xbox" };
            Action<int> scheme = d => { s.symbols = (s.symbols + d + 3) % 3; s.Save(); };
            Button(new Rect(x, 222, w, 34), "Símbolos del mando: " + symbols[s.symbols], () => scheme(1), scheme);
            OptionSlider(new Rect(x, 262, w, 34), "Sensibilidad", s.sensitivity, .3f, 2, v => { s.sensitivity = v; s.Save(); });
            var rows = BindingRows();
            int pages = Mathf.CeilToInt(rows.Count / (float)RowsPerPage);
            optionPage = Mathf.Clamp(optionPage, 0, pages - 1);
            for (int i = 0; i < RowsPerPage && i + optionPage * RowsPerPage < rows.Count; i++)
            {
                var row = rows[i + optionPage * RowsPerPage];
                Button(new Rect(x, 302 + i * 35, w, 30), row.title + "   ·   " + row.action.GetBindingDisplayString(row.index), () => Capture(row));
            }
            Button(new Rect(x, 518, w * .48f, 32), "← Anterior", () => { optionPage = (optionPage + pages - 1) % pages; Reset(); });
            Button(new Rect(x + w * .52f, 518, w * .48f, 32), "Siguiente " + (optionPage + 1) + "/" + pages + " →", () => { optionPage = (optionPage + 1) % pages; Reset(); });
        }
        void VideoOptions(float x, float w)
        {
            var s = DemoSettings.Current;
            for (int i = 0; i < 8; i++)
            {
                GUI.color = FillColor(new Color(i / 7f, i / 7f, i / 7f));
                GUI.DrawTexture(new Rect(x + w * i / 8, 172, w / 8, 26), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            OptionSlider(new Rect(x, 220, w, 42), "Brillo", s.brightness, -.15f, .15f, v => { s.brightness = v; s.Save(); }, true);
            string[] priorities = { "Rendimiento", "Equilibrado", "Calidad" };
            Action<int> priority = d => { s.priority = (s.priority + d + 3) % 3; s.Save(); };
            Button(new Rect(x, 290, w, 42), "Prioridad: " + priorities[s.priority], () => priority(1), priority);
            Button(new Rect(x, 360, w, 42), "Ventana: " + (s.resolution == 0 ? "1152 × 720" : "1440 × 900"), () => { s.resolution = 1 - s.resolution; s.Save(); s.ApplyDisplay(); });
            Button(new Rect(x, 430, w, 42), "Alternar pantalla completa: " + (s.fullscreen ? "Sí" : "No"), () => { s.fullscreen = !s.fullscreen; s.Save(); s.ApplyDisplay(); });
            Small(new Rect(x, 490, w, 50), "Los cambios de ventana se aplican en el juego compilado. Brillo y prioridad se aplican al instante.");
        }
        void SoundOptions(float x, float w)
        {
            var s = DemoSettings.Current;
            Small(new Rect(x, 160, w, 38), "Los niveles se guardan automáticamente. Música controla el menú; ambiente controla lluvia y ventilación.");
            OptionSlider(new Rect(x, 210, w, 40), "Volumen general", s.master, 0, 1, v => { s.master = v; s.Save(); });
            OptionSlider(new Rect(x, 267, w, 40), "Música", s.music, 0, 1, v => { s.music = v; s.Save(); });
            OptionSlider(new Rect(x, 324, w, 40), "Efectos", s.effects, 0, 1, v => { s.effects = v; s.Save(); });
            OptionSlider(new Rect(x, 381, w, 40), "Ambiente", s.ambience, 0, 1, v => { s.ambience = v; s.Save(); });
            OptionSlider(new Rect(x, 438, w, 40), "Voces / diálogos", s.voices, 0, 1, v => { s.voices = v; s.Save(); });
            Button(new Rect(x, 508, w, 40), "Probar efectos y ambiente", DemoSettingsRuntime.Preview);
        }
        void OptionSlider(Rect r, string label, float value, float lo, float hi, Action<float> change, bool signed = false)
        {
            string amount = signed ? (value * 100).ToString("+0;-0;0") : Mathf.RoundToInt(value * 100) + "%";
            Button(new Rect(r.x, r.y, r.width * .47f, r.height), label + "  " + amount, () => {},
                d => change(Mathf.Clamp(value + d * (hi - lo) / 20, lo, hi)));
            var old = GUI.color; GUI.color = FillColor(HoverRed);
            float next = GUI.HorizontalSlider(new Rect(r.x + r.width * .5f, r.y + r.height / 2 - 6, r.width * .48f, 22), value, lo, hi);
            GUI.color = old;
            if (!Mathf.Approximately(next, value)) change(next);
        }
    }
}
