using System;
using System.IO;
using System.Linq;
using Piso13.Realidad;
using Turno0313;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Piso13.Editor
{
    [InitializeOnLoad]
    public static class DemoMenuSmokeTests
    {
        const string Key = "Piso13.DemoMenuSmoke";
        static int stage, checks;
        static double since;
        static float frozen;
        static Vector3 position;
        static string failure;
        static string serviceWarnings;
        static DemoMenuSmokeTests()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(Key, false))
                {
                    EditorApplication.update -= StartIfRequested;
                    EditorApplication.update += StartIfRequested;
                    return;
                }
                stage = checks = 0; since = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick; Application.logMessageReceived += Log;
            };
        }
        static void StartIfRequested()
        {
            const string request = "Temp/RunDemoMenuSmoke";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= StartIfRequested;
            File.Delete(request); Run();
        }
        [MenuItem("Turno 03:13/Abrir demo")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Turno0313/Scenes/Turno_Piso13.unity");
        }
        [MenuItem("Turno 03:13/Pruebas/Verificar menu demo")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Detén Play antes de ejecutar las pruebas.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "HasPrefs", PlayerPrefs.HasKey(DemoSettings.SaveKey));
            SessionState.SetString(Key + "Prefs", PlayerPrefs.GetString(DemoSettings.SaveKey));
            SessionState.SetBool(Key + "EnterOptions", EditorSettings.enterPlayModeOptionsEnabled);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            PlayerPrefs.DeleteKey(DemoSettings.SaveKey);
            EditorSceneManager.OpenScene("Assets/Turno0313/Scenes/Turno_Piso13.unity");
            EditorApplication.EnterPlaymode();
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // Unity account token exchange is an editor service, independent of offline gameplay.
            // Keep it in the report, while all other errors still fail the smoke test.
            if (message.StartsWith("UnityConnectWebRequestException: Token Exchange") &&
                stack.Contains("UnityEditor.Connect.") && !stack.Contains("Assets/"))
            { serviceWarnings += message + "\n"; return; }
            failure = message + "\n" + stack;
        }
        static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        static void Next() { stage++; since = EditorApplication.timeSinceStartup; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            try
            {
                if (failure != null) throw new Exception(failure);
                double elapsed = EditorApplication.timeSinceStartup - since;
                if (elapsed > 90) throw new TimeoutException("Etapa " + stage);
                if (elapsed < .6) return;
                var menu = GameMenus.Instance;
                if (stage == 0)
                {
                    if (!menu) return;
                    Check(menu.OnTitle && !menu.HasSession, "Continuar oculto al iniciar.");
                    Check(Time.timeScale == 0 && AudioListener.pause, "Título congela tiempo y ambiente.");
                    Check(!PlayerController.Instance.ControlEnabled, "No debe moverse bajo el menú.");
                    if (menu.Kit.VisibleLabels.Count == 0) return;
                    Check(menu.Kit.VisibleLabels.SequenceEqual(new[] { "Iniciar demo", "Opciones", "Salir" }), "Opciones principales iniciales incorrectas.");
                    Check(MenuKit.HoverRed.r > MenuKit.ButtonRed.r, "Hover debe ser rojo más brillante.");
                    Check(Resources.Load<Texture2D>("UI/Cover"), "Falta portada.");
                    Check(DemoSettingsRuntime.Mixer, "Falta mixer.");
                    foreach (var channel in new[] { "Master", "Music", "Effects", "Ambience", "Voices" })
                    { Check(DemoSettingsRuntime.Mixer.FindMatchingGroups(channel).Length > 0, "Falta canal " + channel); Check(DemoSettingsRuntime.Mixer.GetFloat(channel, out var level), "Parámetro no expuesto: " + channel); }
                    Directory.CreateDirectory("Documentation/Validation");
                    ScreenCapture.CaptureScreenshot("Documentation/Validation/Menu-inicial.png");
                    frozen = Time.time; Next();
                }
                else if (stage == 1)
                {
                    Check(Mathf.Approximately(Time.time, frozen), "Reloj avanzó bajo título.");
                    menu.Kit.OpenOptions(); ScreenCapture.CaptureScreenshot("Documentation/Validation/Opciones-controles.png"); Next();
                }
                else if (stage == 2)
                {
                    Check(menu.Kit.OptionsOpen && menu.OnTitle, "Opciones pierde contexto de título.");
                    var s = DemoSettings.Current; s.music = .2f; s.effects = 0; s.sensitivity = 1.7f; s.brightness = .1f; s.Save();
                    Check(DemoSettingsRuntime.Mixer.GetFloat("Music", out var music) && Mathf.Abs(music - DemoSettingsRuntime.Decibels(.2f)) < .01f, "Música no aplica volumen.");
                    Check(DemoSettingsRuntime.Mixer.GetFloat("Effects", out var fx) && fx == -80, "Efectos no se silencian.");
                    var saved = JsonUtility.FromJson<DemoSettings>(PlayerPrefs.GetString(DemoSettings.SaveKey));
                    Check(saved.sensitivity == 1.7f && saved.music == .2f, "Ajustes no persistieron.");
                    GameInput.Interact.ApplyBindingOverride(0, "<Keyboard>/q"); s.StoreBindings();
                    Check(s.bindings.Any(b => b.action == "Interact" && b.path == "<Keyboard>/q"), "Reasignación no persistió.");
                    s.RestoreTab(0); s.RestoreTab(1); s.RestoreTab(2);
                    Check(GameInput.Interact.bindings[0].effectivePath == "<Keyboard>/e", "Restablecer controles falló.");
                    menu.Kit.CloseOptions(); menu.StartGame(); Next();
                }
                else if (stage == 3)
                {
                    Check(menu.HasSession && !menu.OnTitle && Time.timeScale == 1 && !AudioListener.pause, "Iniciar demo falló.");
                    Check(PlayerController.Instance.ControlEnabled, "No se habilitó control.");
                    position = PlayerController.Instance.transform.position;
                    menu.SetPaused(true); frozen = Time.time; Next();
                }
                else if (stage == 4)
                {
                    Check(menu.Paused && Mathf.Approximately(Time.time, frozen), "Pausa no congeló el reloj.");
                    menu.BackToTitle(); ScreenCapture.CaptureScreenshot("Documentation/Validation/Menu-continuar.png"); Next();
                }
                else if (stage == 5)
                {
                    Check(menu.OnTitle && menu.HasSession, "Volver al menú perdió sesión.");
                    Check(menu.Kit.VisibleLabels.SequenceEqual(new[] { "Continuar demo", "Iniciar demo", "Opciones", "Salir" }), "Continuar no visible tras iniciar.");
                    menu.ContinueDemo(); Next();
                }
                else if (stage == 6)
                {
                    Check(!menu.OnTitle && PlayerController.Instance.ControlEnabled && Time.timeScale == 1, "Continuar no reanudó.");
                    Check(Vector3.Distance(position, PlayerController.Instance.transform.position) < .1f, "Continuar movió al jugador.");
                    menu.BackToTitle(); menu.RestartDemo(); Next();
                }
                else if (stage == 7)
                {
                    if (!menu) return;
                    Check(menu.HasSession && !menu.OnTitle, "Iniciar de nuevo no inició escena nueva.");
                    Check(!GameMenus.StartOnNextLoad, "Solicitud de inicio no consumida.");
                    SceneManager.LoadScene("Piso13 Realidad"); Next();
                }
                else if (stage == 8)
                {
                    var game = P13RealidadGame.Instance;
                    if (!game || !game.Menus || elapsed < 4) return;
                    Check(game.Phase == P13RealidadGame.GamePhase.Playing, "Realidad no inicia jugando.");
                    game.Menus.SetPaused(true); frozen = game.GameTime; position = game.PlayerPosition; Next();
                }
                else if (stage == 9)
                {
                    var game = P13RealidadGame.Instance;
                    Check(!game.enabled && Mathf.Approximately(frozen, game.GameTime), "Realidad avanzó bajo pausa.");
                    game.LoadMainMenu(); Next();
                }
                else if (stage == 10)
                {
                    var game = P13RealidadGame.Instance;
                    Check(game && game.Menus.OnTitle && game.Menus.Paused, "Título de Realidad pierde escena.");
                    Check(game.Menus.Kit.VisibleLabels.Contains("Continuar demo"), "Falta Continuar en Realidad.");
                    game.Menus.Kit.OpenOptions(); Next();
                }
                else if (stage == 11)
                {
                    var game = P13RealidadGame.Instance;
                    Check(game.Menus.Kit.OptionsOpen && !game.enabled, "Opciones reactivó Realidad.");
                    game.Menus.Kit.CloseOptions(); game.Menus.ContinueDemo(); Next();
                }
                else if (stage == 12)
                {
                    var game = P13RealidadGame.Instance;
                    Check(game.enabled && !game.Menus.Paused && !game.Menus.OnTitle && Time.timeScale == 1, "Continuar Realidad falló.");
                    Check(Vector3.Distance(position, game.PlayerPosition) < .05f, "Continuar Realidad alteró posición.");
                    Finish(null);
                }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            if (SessionState.GetBool(Key + "HasPrefs", false)) PlayerPrefs.SetString(DemoSettings.SaveKey, SessionState.GetString(Key + "Prefs", ""));
            else PlayerPrefs.DeleteKey(DemoSettings.SaveKey);
            PlayerPrefs.Save();
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "EnterOptions", false);
            Time.timeScale = 1; AudioListener.pause = false;
            Directory.CreateDirectory("Documentation/Validation");
            string report = DateTime.UtcNow.ToString("O") + "\nUnity " + Application.unityVersion + "\n" + (error == null ? "PASS" : "FAIL") + " — " + checks + " checks; stage " + stage + "\n" + error + (string.IsNullOrEmpty(serviceWarnings) ? "" : "\nEditor service warnings (not gameplay):\n" + serviceWarnings);
            File.WriteAllText("Documentation/Validation/DemoMenuSmoke.txt", report);
            Debug.Log(report); EditorApplication.isPlaying = false;
        }
    }
}
