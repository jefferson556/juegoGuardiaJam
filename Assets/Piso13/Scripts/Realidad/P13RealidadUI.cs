using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    public sealed class P13RealidadUI : MonoBehaviour
    {
        public P13RealidadGame Game;

        const float Width = 1152f;
        const float Height = 720f;

        GUIStyle title, heading, body, small, button, warningTitle, modalBody;
        Color ink = new Color(0.9f, 0.96f, 0.94f);
        Color warningRed = new Color(1f, 0.28f, 0.28f);
        Color teal = new Color(0.24f, 0.75f, 0.68f);
        Color amber = new Color(0.95f, 0.76f, 0.22f);

        void Styles()
        {
            if (title != null) return;

            title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = ink;

            warningTitle = new GUIStyle(title)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            warningTitle.normal.textColor = warningRed;

            heading = new GUIStyle(title)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            heading.normal.textColor = ink;

            body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true
            };
            body.normal.textColor = ink;

            modalBody = new GUIStyle(body)
            {
                fontSize = 20,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            modalBody.normal.textColor = new Color(0.92f, 0.95f, 0.94f);

            small = new GUIStyle(body)
            {
                fontSize = 14
            };
            small.normal.textColor = new Color(0.72f, 0.82f, 0.8f);

            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        void OnGUI()
        {
            if (Game == null) return;
            Styles();

            // Matriz virtual responsiva 1152x720
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / Width, Screen.height / Height, 1));

            if (Game.Phase == P13RealidadGame.GamePhase.Capture)
            {
                // Velo oscuro sutil durante la animación de captura / jumpscare
                Panel(new Rect(0, 0, Width, Height), 0.08f);
                return;
            }

            if (Game.Phase == P13RealidadGame.GamePhase.Lost)
            {
                // La pantalla de fin de turno la dibuja P13RealidadMenus (mismo estilo que los demás menús)
                return;
            }

            if (Game.CCTV)
            {
                CamerasView();
                return;
            }

            // HUD de Juego
            HUD();

            // Ventana Modal a los 30 Segundos
            if (Game.ShowModal)
            {
                ModalWindow();
            }
        }

        static void Panel(Rect r, float alpha, Color? tint = null)
        {
            var old = GUI.color;
            GUI.color = tint ?? new Color(0.015f, 0.025f, 0.03f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void HUD()
        {
            // Retícula central
            GUI.Label(new Rect(Width / 2 - 4, Height / 2 - 10, 20, 20), "·", body);

            // Prompt de interacción / reporte en pantalla (centrado bajo la retícula)
            if (!string.IsNullOrEmpty(Game.CurrentInteractionPrompt))
            {
                var promptRect = new Rect(Width / 2 - 425, Height / 2 + 25, 850, 38);
                Panel(promptRect, 0.90f, new Color(0.04f, 0.08f, 0.10f, 0.92f));
                var promptStyle = new GUIStyle(body)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = ink;
                GUI.Label(promptRect, Game.CurrentInteractionPrompt, promptStyle);
            }

            // 1. Panel Superior Izquierdo: Reloj y Estado
            Panel(new Rect(22, 18, 300, 75), 0.78f);
            int hour = (Game.Minute / 60) % 24;
            int minute = Game.Minute % 60;
            GUI.Label(new Rect(37, 26, 280, 28), string.Format("{0:00}:{1:00} AM  ·  PISO 12", hour, minute), heading);
            // Anomalías reales reportadas (P13RealidadEnding): al llegar al total salen los créditos
            var ending = Game.Menus != null ? Game.Menus.Ending : null;
            string anomalies = ending != null ? "  ·  Anomalías " + ending.Found + "/" + ending.Total : "";
            GUI.Label(new Rect(37, 57, 280, 26), "Energía " + Mathf.CeilToInt(Game.Energy) + "%" + anomalies, small);

            // 2. Barra de Realidad (Aparece únicamente tras cerrar la ventana emergente de alerta)
            if (Game.ModalDismissed)
            {
                Panel(new Rect(340, 18, 580, 75), 0.85f);
                float r = Mathf.Clamp(Game.Reality, 0, 100);
                Color barColor = r > 50f ? teal : r > 25f ? amber : warningRed;

                string realityStatus;
                if (r > 50f)
                {
                    realityStatus = $"REALIDAD:  {Mathf.CeilToInt(r)}%";
                }
                else if (r > 25f)
                {
                    realityStatus = $"REALIDAD DISTORSIONÁNDOSE PELIGRO:  {Mathf.CeilToInt(r)}%";
                }
                else
                {
                    realityStatus = $"LA DISTORSIÓN DE LA REALIDAD ES INMINENTE:  {Mathf.CeilToInt(r)}%";
                }

                var labelStyle = new GUIStyle(small)
                {
                    fontStyle = FontStyle.Bold
                };
                labelStyle.normal.textColor = barColor;

                GUI.Label(new Rect(355, 26, 550, 26), realityStatus, labelStyle);

                // Barra de fondo
                Panel(new Rect(355, 54, 550, 22), 0.9f, new Color(0.08f, 0.12f, 0.14f, 1f));
                // Barra de progreso rellena
                Panel(new Rect(357, 56, (546 * (r / 100f)), 18), 1f, barColor);
            }

            // 3. Panel Inferior: Objetivos e Instrucciones
            Panel(new Rect(22, 604, 880, 91), 0.78f);

            // Texto de Objetivo Dinámico
            string objectiveText = !Game.ModalDismissed 
                ? "Algo no parece normal, busca lo que no encaja."
                : "Busca lo que no encaja, él se ha empezado a mover, si no te das prisa te encontrará.";

            GUI.Label(new Rect(37, 614, 850, 30), objectiveText, body);

            // Controles (E puerta, R reportar, V CCTV)
            GUI.Label(new Rect(37, 652, 850, 30), "E: puerta  ·  R: reportar anomalía  ·  V: CCTV  ·  F: linterna  ·  Esc: pausa", small);

            // Mensajes temporales si los hay (Toast centrado y destacado)
            if (!string.IsNullOrEmpty(Game.NotificationMessage))
            {
                bool isSuccess = Game.NotificationMessage.Contains("ANOMALÍA ENCONTRADA");
                bool isWarning = Game.NotificationMessage.Contains("FALSA ALARMA") || Game.NotificationMessage.Contains("demasiado lejos");
                Color bannerTint = isSuccess 
                    ? new Color(0.12f, 0.45f, 0.35f, 0.94f) 
                    : isWarning 
                        ? new Color(0.50f, 0.14f, 0.14f, 0.94f) 
                        : new Color(0.15f, 0.22f, 0.28f, 0.94f);

                Rect notifRect = new Rect(Width / 2 - 330, 108, 660, 42);
                Panel(notifRect, 0.94f, bannerTint);
                GUI.Label(new Rect(notifRect.x + 10, notifRect.y + 7, notifRect.width - 20, 28), Game.NotificationMessage, modalBody);
            }
        }

        void ModalWindow()
        {
            // Fondo oscuro atenuado
            Panel(new Rect(0, 0, Width, Height), 0.78f);

            // Cuadro de diálogo central
            Rect dialogRect = new Rect(200, 180, 752, 320);
            Panel(dialogRect, 0.97f, new Color(0.03f, 0.05f, 0.06f, 0.98f));

            // Borde superior decorativo rojo
            Panel(new Rect(dialogRect.x, dialogRect.y, dialogRect.width, 4), 1f, warningRed);

            // Título
            GUI.Label(new Rect(dialogRect.x + 20, dialogRect.y + 28, dialogRect.width - 40, 45), "ADVERTENCIA", warningTitle);

            // Mensaje requerido
            string msg = "Algo no encaja en este lugar... La anomalía te está cazando. Si no encuentras lo que está mal no saldrás nunca más de aquí.";
            GUI.Label(new Rect(dialogRect.x + 40, dialogRect.y + 90, dialogRect.width - 80, 110), msg, modalBody);

            // Botón interactivo de cierre
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.45f, 0.42f);
            if (GUI.Button(new Rect(Width / 2 - 130, dialogRect.y + 225, 260, 48), "CONTINUAR", button))
            {
                Game.DismissModal();
            }
            GUI.backgroundColor = oldBg;

            GUI.Label(new Rect(Width / 2 - 200, dialogRect.y + 280, 400, 25), "Pulsa CONTINUAR, E o ESPACIO para seguir", small);
        }

        void CamerasView()
        {
            var cameras = Game.Data?.cameras;
            if (cameras == null || cameras.Length == 0) return;

            int camIdx = Mathf.Clamp(Game.CameraIndex, 0, cameras.Length - 1);
            var currentCam = cameras[camIdx];

            // Líneas de estática
            float interference = Game.Enemy != null && Game.Enemy.gameObject.activeInHierarchy ? Game.Enemy.Interference(currentCam.position) : 0f;
            if (interference > 0.05f)
            {
                var old = GUI.color;
                GUI.color = new Color(0.6f, 0.8f, 0.7f, interference * 0.25f);
                for (int y = 0; y < 720; y += 8)
                {
                    GUI.DrawTexture(new Rect(0, y + (Time.frameCount % 4), Width, 2), Texture2D.whiteTexture);
                }
                GUI.color = old;
            }

            // Nombre de la cámara activa
            Panel(new Rect(30, 25, 520, 50), 0.8f);
            GUI.Label(new Rect(45, 33, 490, 36), "● REC  ·  " + currentCam.name, heading);

            // Botones de selección CAM 01 a CAM 09
            for (int i = 0; i < 9 && i < cameras.Length; i++)
            {
                int index = i;
                bool selected = i == Game.CameraIndex;
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = selected ? new Color(0.3f, 0.6f, 0.5f) : new Color(0.1f, 0.15f, 0.16f);

                if (GUI.Button(new Rect(30 + i * 123, 636, 115, 47), "CAM " + (i + 1).ToString("00"), button))
                {
                    Game.SelectCamera(index);
                }
                GUI.backgroundColor = oldBg;
            }

            // Indicador inferior
            Panel(new Rect(30, 590, 800, 36), 0.75f);
            GUI.Label(new Rect(45, 596, 770, 26), "← / →: cambiar cámara  ·  Ratón: orientar  ·  V / ESC: salir de CCTV", small);

            // Prompt de reporte de anomalía en CCTV (ej. CAM 06 con la Persona Elegante)
            if (!string.IsNullOrEmpty(Game.CurrentInteractionPrompt))
            {
                var promptRect = new Rect(Width / 2 - 275, 530, 550, 42);
                Panel(promptRect, 0.92f, new Color(0.40f, 0.08f, 0.08f, 0.92f));
                GUI.Label(promptRect, Game.CurrentInteractionPrompt, body);
            }
        }
    }
}
