using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        static double started;
        static int screenshotFrame=-1;
        static bool initialized;
        static PlayModeTestRunner()
        {
            var s=SessionState.GetString("PlayModeTest.State","Idle");
            if(s=="WaitingForCompile")EditorApplication.delayCall+=()=>{SessionState.SetString("PlayModeTest.State","EnteringPlayMode");EditorApplication.playModeStateChanged+=Changed;EditorApplication.isPlaying=true;};
            else if(s=="EnteringPlayMode" || s=="InPlayMode")
            {
                EditorApplication.playModeStateChanged+=Changed;
                if(EditorApplication.isPlaying)EditorApplication.update+=Tick;
            }
        }
        static void Changed(PlayModeStateChange s)
        {
            if(s!=PlayModeStateChange.EnteredPlayMode)return;
            EditorApplication.playModeStateChanged-=Changed;
            SessionState.SetString("PlayModeTest.State","InPlayMode");EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!initialized){initialized=true;started=EditorApplication.timeSinceStartup;}
            if(EditorApplication.timeSinceStartup-started<4)return;
            try
            {
                if(screenshotFrame<0)
                {
                    var game=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).First(c=>c!=null && c.GetType().Name=="P13RealidadGame");
                    var b=game.GetType().GetProperty("SceneBindings").GetValue(game);
                    var hand=(Transform)b.GetType().GetProperty("Hand").GetValue(b);
                    var camera=(Camera)b.GetType().GetProperty("View").GetValue(b);
                    if(!hand.gameObject.activeInHierarchy)throw new Exception("Mano inactiva en gameplay.");
                    foreach(var renderer in hand.GetComponentsInChildren<Renderer>(true))
                    {
                        var p=camera.WorldToViewportPoint(renderer.bounds.center);
                        if(!renderer.enabled || p.z<=0 || p.x<0 || p.x>1 || p.y<0 || p.y>1)throw new Exception("Mano fuera del encuadre: "+p);
                    }
                    const string path="Assets/Realidad_HandVerification.png";
                    SessionState.SetString("PlayModeTest.ScreenshotPath",path);ScreenCapture.CaptureScreenshot(path);screenshotFrame=Time.frameCount;
                }
                else if(Time.frameCount>screenshotFrame+1)
                {
                    Finish("PASS: mano activa, renderers habilitados y delante de la cámara; captura realizada.");
                }
                else if(EditorApplication.timeSinceStartup-started>12)Finish("FAIL: no se completó la captura.");
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string text)
        {
            EditorApplication.update-=Tick;SessionState.SetString("PlayModeTest.Result",text);SessionState.SetString("PlayModeTest.State","Done");EditorApplication.isPlaying=false;
        }
    }
}
