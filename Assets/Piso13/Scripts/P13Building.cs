using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13
{
    public sealed class ElevatorMachine
    {
        public enum Phase
        {
            Open,
            Closing,
            Travelling,
            UnexpectedStop,
            Stalled,
            Rescue,
            Opening
        }

        public Phase phase;
        public int floor = 13, destination = 12, anomaly;
        public float elapsed, door = 1, cooldown = 180;
        public bool needsService;
        bool transferred;
        public bool Moving => phase != Phase.Open;
        public string Indicator => anomaly == 4 && phase == Phase.Travelling ? "−1" : phase == Phase.Stalled ? "—" : floor.ToString();
        public bool Request(int target, bool powered, bool inside, int night, int roll)
        {
            if (Moving || !powered || needsService || !inside || target == floor || target != 12 && target != 13)
                return false;
            destination = target;
            anomaly = night >= 3 && cooldown <= 0 && roll < 14 ? 1 + roll % 6 : 0;
            if (anomaly != 0)
                cooldown = 240;
            phase = Phase.Closing;
            elapsed = 0;
            transferred = false;
            return true;
        }

        public bool Rescue()
        {
            if (phase != Phase.Stalled)
                return false;
            phase = Phase.Rescue;
            elapsed = 0;
            needsService = true;
            return true;
        }

        public int Tick(float dt, bool power)
        {
            dt = Mathf.Clamp(dt, 0, 1);
            cooldown = Mathf.Max(0, cooldown - dt);
            if (!Moving)
                return 0;
            if (!power && (phase == Phase.Closing || phase == Phase.Travelling || phase == Phase.UnexpectedStop))
            {
                phase = Phase.Stalled;
                elapsed = 0;
            }

            elapsed += dt;
            switch (phase)
            {
                case Phase.Closing:
                    door = Mathf.Max(0, 1 - elapsed / 1.5f);
                    if (elapsed >= 1.5f)
                    {
                        phase = Phase.Travelling;
                        elapsed = 0;
                    }

                    break;
                case Phase.Travelling:
                    if (anomaly == 1 && elapsed >= 2 && !transferred)
                    {
                        phase = Phase.UnexpectedStop;
                        elapsed = 0;
                    }
                    else if (elapsed >= 6)
                    {
                        floor = destination;
                        transferred = true;
                        phase = Phase.Opening;
                        elapsed = 0;
                        return floor;
                    }

                    break;
                case Phase.UnexpectedStop:
                    if (elapsed >= 5)
                    {
                        transferred = true;
                        phase = Phase.Travelling;
                        elapsed = 2;
                    }

                    break;
                case Phase.Rescue:
                    door = 0;
                    if (elapsed >= 8)
                    {
                        phase = Phase.Opening;
                        elapsed = 0;
                        anomaly = 0;
                        return floor;
                    }

                    break;
                case Phase.Opening:
                    door = Mathf.Min(1, elapsed / 1.5f);
                    if (door >= 1)
                    {
                        phase = Phase.Open;
                        elapsed = 0;
                    }

                    break;
            }

            return 0;
        }
    }

    public sealed class P13Building
    {
        public sealed class Door
        {
            public DoorData data;
            public Transform pivot;
            public float fraction, target, debounce;
            public bool moving;
        }

        readonly P13Game game;
        public readonly List<Door> Doors = new List<Door>();
        public ElevatorMachine Lift = new ElevatorMachine();
        readonly List<Transform> elevatorLeaves = new List<Transform>();
        public P13Character Elena;
        public bool Ringing, Speaking, CallHeard, ArchiveRead, HelpHeard;
        public float AlteredLandingTime;
        bool falseDingPlayed;
        public int Attempts, Trips;
        float callClock, nextCall = 45, callRemaining, ringTime;
        int lineIndex;
        string[] lines, files;
        static readonly string[] introduction = {"¿Gabriel?... Qué bueno. Pensé que me habían dejado sola otra vez.", "Soy Elena... recepción.", "Te vi llegar hace un rato. Parecías un poquito nervioso.", "No tienes que hablar si no quieres... aunque es una pena. Me gusta conocer a los nuevos.", "Estoy abajo... en el doce.", "¿Por qué no bajas un momento? Así podemos conocernos como corresponde.", "No tardes demasiado, Gabriel."};
        static readonly string[] introFiles = {"ELENA_N2_001_LLAMADA_INICIAL", "ELENA_N2_002_PRESENTACION", "ELENA_N2_003_OBSERVACION", "ELENA_N2_004_COQUETA", "ELENA_N2_005_INVITACION", "ELENA_N2_006_INVITACION_2", "ELENA_N2_007_DESPEDIDA"};
        public P13Building(P13Game g)
        {
            game = g;
            foreach (var d in g.Data.doors)
            {
                var pivot = new GameObject("Hoja_" + d.id).transform;
                pivot.SetParent(g.World.Root);
                pivot.position = d.hinge;
                var visual = P13World.Model("Puerta_Madera", pivot);
                visual.transform.localScale = new Vector3(d.width, 1, 1);
                Doors.Add(new Door{data = d, pivot = pivot, fraction = d.floor == 13 && !d.locked ? 1 : 0});
            }

            foreach (float x in new[]{0f, 30f})
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = P13World.Box("AscensorHoja", P13Rules.RK(x + side * .9f, 1.2f, 7.26f), new Vector3(.65f, 2.4f, .06f), new Color(.19f, .2f, .21f), g.World.Root);
                    elevatorLeaves.Add(leaf.transform);
                }
            }

            Elena = new P13Character("Elena", g.World.Root, 1.68f);
            Elena.Root.position = new Vector3(26.5f, .8f, -2.7f);
            Elena.Root.rotation = Quaternion.Euler(0, 90, 0);
            Elena.Play("seated");
            Elena.Root.gameObject.SetActive(false);

            Publish();
        }

        void Publish()
        {
            game.World.Leaves.Clear();
            foreach (var d in Doors)
            {
                game.World.Leaves.Add(d.data.Bounds(d.fraction));
                d.pivot.rotation = Quaternion.Euler(0, d.data.yaw + d.data.swing * d.fraction, 0);
            }

            game.World.Revision++;
        }

        public bool Stable => !Doors.Any(d => d.moving) && !Lift.Moving;
        public List<DoorSave> Snapshot()
        {
            return Doors.Select(d => new DoorSave{id = d.data.id, open = d.fraction > .5f}).ToList();
        }

        public void Restore(P13Checkpoint s)
        {
            Speaking = Ringing = false;
            callClock = callRemaining = ringTime = 0;
            nextCall = 45;
            lineIndex = 0;
            game.Audio.StopDialogue();
            foreach (var d in Doors)
            {
                var saved = s.doors.FirstOrDefault(x => x.id == d.data.id);
                d.fraction = saved != null && saved.open && !d.data.locked ? 1 : 0;
                d.moving = false;
            }

            Lift = new ElevatorMachine{floor = s.floor, destination = s.floor == 13 ? 12 : 13, needsService = s.elevatorNeedsService};
            Attempts = s.callAttempts;
            CallHeard = s.callHeard;
            Trips = s.trips;
            ArchiveRead = s.archiveRead;
            HelpHeard = s.elenaHelpHeard;
            AlteredLandingTime = 0;
            falseDingPlayed = false;
            Publish();
        }

        public bool DoorInteract()
        {
            var eye = game.Player + Vector3.up * (game.EyeHeight - .8f);
            var forward = game.View.transform.forward;
            var candidates = Doors.Where(d => d.data.floor == game.Floor && Vector3.Distance(eye, d.data.Hotspot) < 2.8f && Vector3.Dot((d.data.Hotspot - eye).normalized, forward) > .86f).OrderBy(d => Vector3.Distance(eye, d.data.Hotspot));
            var door = candidates.FirstOrDefault();
            if (door == null)
                return false;
            var walls = game.Floor == 12 ? game.Data.floor12 : game.Data.walls.Concat(game.Data.furniture);
            if (walls.Any(o => o.Intersects(eye, door.data.Hotspot)))
                return false;
            Toggle(door, game.Player, game.Guard.Position);
            return true;
        }

        void Toggle(Door d, Vector3 actor, Vector3 other)
        {
            if (d.debounce > 0 || d.moving)
                return;
            d.debounce = .35f;
            if (d.data.locked)
            {
                game.Audio.Spatial("knock", d.data.Hotspot, .16f);
                return;
            }

            float target = d.fraction > .5f ? 0 : 1;
            for (int i = 0; i <= 24; i++)
            {
                var b = d.data.Bounds(Mathf.Lerp(d.fraction, target, i / 24f));
                if (b.Intersects(actor, actor, .38f) || b.Intersects(other, other, .58f))
                    return;
            }

            d.target = target;
            d.moving = true;
            game.Guard.Hear(d.data.Hotspot, 6);
        }

        public void Tick(float dt)
        {
            bool changed = false;
            foreach (var d in Doors)
            {
                d.debounce = Mathf.Max(0, d.debounce - dt);
                if (!d.moving)
                    continue;
                float f = Mathf.MoveTowards(d.fraction, d.target, Mathf.Min(.05f, dt) / d.data.duration);
                var b = d.data.Bounds(f);
                bool blocked = b.Intersects(game.Player, game.Player, .38f) || b.Intersects(game.Guard.Position, game.Guard.Position, .58f);
                game.Audio.Door(d.data.id, d.data.Hotspot, blocked ? d.fraction : f, d.target == 0, !blocked);
                if (blocked)
                    continue;
                d.fraction = f;
                changed = true;
                if (f == d.target)
                    d.moving = false;
            }

            if (changed)
                Publish();
            foreach (var d in Doors)
                if (!d.data.locked && d.fraction == 0 && !d.moving && d.data.floor == 13 && Vector3.Distance(d.data.Hotspot, game.Guard.Position) < 2.2f)
                    Toggle(d, game.Guard.Position, game.Player);
            var phase = Lift.phase;
            int arrival = Lift.Tick(dt, game.Energy > 0 && game.Horror.Multiplier(3) > .05f);
            if (phase != Lift.phase)
                game.Audio.Spatial(Lift.phase == ElevatorMachine.Phase.Opening ? "lift_ding" : "lift_motor", game.Player, .35f);
            if (arrival != 0)
            {
                float old = game.Floor == 12 ? 30 : 0;
                game.Floor = arrival;
                game.Player += new Vector3((arrival == 12 ? 30 : 0) - old, 0, 0);
                Trips++;
                game.Player.z = Mathf.Clamp(game.Player.z, -6.95f, -6.55f);
                if (Lift.anomaly == 3)
                    AlteredLandingTime = 16;
                // Reflection/operator manifestations remain absent while their original assets are absent.
                game.Input.Neutralize();
            }

            for (int i = 0; i < elevatorLeaves.Count; i++)
            {
                float origin = i < 2 ? 0 : 30, side = i % 2 == 0 ? -1 : 1;
                var p = elevatorLeaves[i].position;
                p.x = origin + side * (.33f + Lift.door * .63f);
                elevatorLeaves[i].position = p;
            }

            AlteredLandingTime = Mathf.Max(0, AlteredLandingTime - dt);
            if (Lift.phase == ElevatorMachine.Phase.Closing)
                falseDingPlayed = false;
            if (Lift.anomaly == 2 && Lift.phase == ElevatorMachine.Phase.Travelling && Lift.elapsed >= 2 && !falseDingPlayed)
            {
                falseDingPlayed = true;
                game.Audio.Spatial("lift_ding", game.Player, .35f);
            }

            if (Elena != null)
            {
                Elena.Root.gameObject.SetActive(game.Floor == 12);
                if (game.Floor == 12)
                    Elena.Play(HelpHeard ? "whisper" : CallHeard ? "seated_look" : "seated");
            }

            TickCall(dt);
        }

        public bool Inside => Mathf.Abs(game.Player.x - (game.Floor == 12 ? 30 : 0)) < .58f && game.Player.z < -6.52f;
        public bool Near(Vector3 p, float radius = 1.45f)
        {
            var a = game.Player;
            a.y = 1.2f;
            p.y = 1.2f;
            return Vector3.Distance(a, p) < radius && game.World.Clear(a, p);
        }

        public bool Interact()
        {
            if (game.Floor == 13 && Near(P13Rules.RK(-.95f, 1, -.7f)) && (Ringing || Speaking))
            {
                if (Speaking)
                    Interrupt();
                else
                {
                    Ringing = false;
                    Speaking = true;
                    lineIndex = 0;
                    callRemaining = 0;
                    lines = Attempts > 1 ? new[]{"Gabriel… ¿sigues ahí? No tienes que bajar ahora. Solo quería saber si me escuchabas."}.Concat(introduction).ToArray() : introduction;
                    files = Attempts > 1 ? new[]{"ELENA_N2_008_RELLAMADA"}.Concat(introFiles).ToArray() : introFiles;
                }

                return true;
            }

            if (Inside)
            {
                if (Lift.phase == ElevatorMachine.Phase.Stalled)
                    Lift.Rescue();
                else if (!Lift.Request(game.Floor == 13 ? 12 : 13, game.Energy > 0, true, game.Night, UnityEngine.Random.Range(0, 100)))
                    game.Message = Lift.needsService ? "Restablece el ascensor en Técnica." : "Ascensor sin energía.";
                return true;
            }

            var panel = game.Floor == 12 ? P13Rules.RK(34, .8f, 3.2f) : P13Rules.RK(5.2f, .8f, -1);
            if (Near(panel) && (Lift.needsService || game.Energy <= 0))
            {
                Lift.needsService = false;
                game.Energy = Mathf.Max(25, game.Energy);
                game.Message = "Circuito de emergencia restablecido.";
                return true;
            }

            if (game.Floor == 12 && game.Night >= 4 && Near(P13Rules.RK(33, .8f, -2.5f)))
            {
                ArchiveRead = true;
                game.Say("REGISTRO DE PLANTA", game.Night == 5 ? "Los planos repiten la misma planta. La sombra no pertenece a ningún empleado: el edificio conserva aquello que observa. Esta vez, entrega el registro y sal sin responder." : "La copia tiene otra fecha. Donde ayer decía enfermería, ahora figura un nombre tachado.", 10);
                return true;
            }

            return game.Floor == 12;
        }

        void TickCall(float dt)
        {
            if (game.Night != 2)
                return;
            callClock += dt;
            if (game.Threatened || Lift.Moving || game.CCTV || game.Task >= 0)
            {
                if (Speaking || Ringing)
                    Interrupt();
                return;
            }

            if (Speaking && !Near(P13Rules.RK(-.95f, 1, -.7f), 2.2f))
            {
                Interrupt();
                return;
            }

            if (CallHeard && !HelpHeard && game.Floor == 12 && Near(new Vector3(26.8f, .8f, 4.7f), 2.2f))
            {
                const string text = "El cuarto técnico está al otro lado del pasillo. Si el ascensor deja de responder, puedes restablecerlo desde allí. No hace falta que te quedes conmigo.";
                float duration = game.Audio.Dialogue("ELENA_N2_009_AYUDA");
                if (duration <= 0)
                    duration = Mathf.Max(3, text.Length / 13f);
                game.Say("ELENA", text, duration);
                HelpHeard = true;
                game.Save();
            }

            if (!CallHeard && !Speaking && !Ringing && callClock >= nextCall && game.Floor == 13 && Attempts < 3)
            {
                Attempts++;
                Ringing = true;
                callRemaining = 22;
                ringTime = 0;
            }

            if (Ringing)
            {
                callRemaining -= dt;
                ringTime -= dt;
                if (ringTime <= 0)
                {
                    game.Audio.Spatial("phone", P13Rules.RK(-.95f, 1, -.7f), .3f);
                    ringTime = 5;
                }

                if (callRemaining <= 0)
                    Interrupt();
            }

            if (Speaking)
            {
                callRemaining -= dt;
                if (callRemaining <= 0)
                {
                    if (lineIndex >= lines.Length)
                    {
                        CallHeard = true;
                        Speaking = false;
                        game.Audio.StopDialogue();
                        return;
                    }

                    float duration = game.Audio.Dialogue(files[lineIndex]);
                    if (duration <= 0)
                        duration = Mathf.Max(3, lines[lineIndex].Length / 13f);
                    game.Say("ELENA · TELÉFONO", lines[lineIndex], duration);
                    lineIndex++;
                    callRemaining = duration + .65f;
                }
            }
        }

        public void Interrupt()
        {
            game.Audio.StopDialogue();
            if (game.Speaker.StartsWith("ELENA"))
                game.Subtitle = game.Speaker = "";
            Speaking = Ringing = false;
            nextCall = callClock + 100;
        }

        public string Hint => Lift.phase == ElevatorMachine.Phase.Stalled ? game.Input.Label("interact") + ": retorno de emergencia" : Lift.Moving ? "Ascensor · Piso " + Lift.Indicator : Inside ? game.Input.Label("interact") + ": viajar al Piso " + (game.Floor == 13 ? 12 : 13) : Ringing && Near(P13Rules.RK(-.95f, 1, -.7f)) ? game.Input.Label("interact") + ": contestar teléfono" : "";
    }
}
