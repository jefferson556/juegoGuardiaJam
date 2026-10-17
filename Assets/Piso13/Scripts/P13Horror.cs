using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Piso13
{
    public enum HorrorKind
    {
        Flicker,
        Blackout,
        Knock,
        Pipe,
        Elevator,
        FalseSteps,
        Breath,
        Interference,
        SignalLoss,
        Apparition,
        Silence,
        SectorBlackout,
        FullBlackout,
        ShadowCrossing,
        Presence,
        MovingObject,
        Phone,
        Monitor,
        CameraOnly,
        CameraStare,
        ReturnSequence
    }

    public sealed class P13Horror
    {
        readonly P13Game game;
        readonly System.Random random = new System.Random();
        readonly Queue<HorrorKind> recent = new Queue<HorrorKind>();
        float clock, cooldown = 38, sinceThreat = 200, lastHeavy = -200, stormWait = 24, thunderWait = -1, flashTime = -1;
        int pulses, thunderIndex;
        float rainClock;
        public HorrorKind? Active;
        public float Elapsed;
        public int Room = 1, Count;
        public string Character;
        bool pending;
        int sequenceStage;
        GameObject silhouette;
        Vector3 manifestationStart;
        Transform movingProp;
        Vector3 propStart;
        public P13Horror(P13Game g)
        {
            game = g;
        }

        public int Intensity => game.Minute < 45 ? 0 : game.Minute < 120 ? 1 : game.Minute < 210 ? 2 : game.Minute < 295 ? 3 : 4;
        public static float Duration(HorrorKind k)
        {
            switch (k)
            {
                case HorrorKind.Flicker:
                    return 1.7f;
                case HorrorKind.Blackout:
                    return 4;
                case HorrorKind.SectorBlackout:
                    return 9;
                case HorrorKind.FullBlackout:
                    return 12;
                case HorrorKind.ReturnSequence:
                    return 18;
                case HorrorKind.ShadowCrossing:
                case HorrorKind.CameraOnly:
                    return .7f;
                case HorrorKind.Presence:
                case HorrorKind.MovingObject:
                case HorrorKind.Phone:
                case HorrorKind.Monitor:
                case HorrorKind.CameraStare:
                    return 3;
                case HorrorKind.Interference:
                    return 2.6f;
                case HorrorKind.SignalLoss:
                    return 1.3f;
                case HorrorKind.Apparition:
                    return .85f;
                case HorrorKind.Silence:
                    return 7;
                default:
                    return 1.5f;
            }
        }

        public void RequestSequence()
        {
            if (pending || Active == HorrorKind.ReturnSequence || game.SetPieceCompleted)
                return;
            pending = true;
            cooldown = 0;
        }

        public void Tick(float dt)
        {
            clock += dt;
            sinceThreat = game.Threatened ? 0 : sinceThreat + dt;
            Weather(dt);
            if (Active.HasValue)
            {
                if (game.Threatened && Active != HorrorKind.ReturnSequence)
                {
                    End();
                    cooldown = Mathf.Max(cooldown, 35);
                    return;
                }

                Elapsed += dt;
                if (Active == HorrorKind.ReturnSequence)
                {
                    if (Elapsed >= 3 && sequenceStage == 0)
                    {
                        game.Audio.Spatial("electric", P13Rules.RK(0, 2.5f, 4.7f));
                        sequenceStage = 1;
                    }

                    if (Elapsed >= 5 && sequenceStage == 1)
                    {
                        game.Audio.Spatial("lift_ding", P13Rules.RK(0, 1.8f, 6.4f));
                        sequenceStage = 2;
                    }

                    if (Elapsed >= 13 && sequenceStage == 2)
                    {
                        game.Audio.Spatial("electric", P13Rules.RK(0, 2.5f, 4.7f));
                        sequenceStage = 3;
                    }
                }

                if (silhouette)
                {
                    silhouette.SetActive(Active == HorrorKind.CameraOnly ? game.CCTV : !game.CCTV);
                    if (Active == HorrorKind.ShadowCrossing)
                        silhouette.transform.position = manifestationStart + game.View.transform.right * Elapsed * 2;
                }

                if (movingProp)
                    movingProp.localPosition = propStart + Vector3.right * Mathf.Sin(Elapsed * 2) * .08f;
                if (Elapsed >= Duration(Active.Value))
                {
                    bool heavy = Active == HorrorKind.ReturnSequence, manifest = silhouette != null;
                    if (heavy)
                    {
                        game.SetPieceCompleted = true;
                        game.Say("OPERADOR", "No le entregues tu linterna. Aún no has terminado el turno.", 6);
                    }

                    End();
                    int[] low = {48, 40, 32, 27, 22}, high = {72, 64, 52, 44, 36};
                    cooldown = Mathf.Max(random.Next(low[Intensity], high[Intensity] + 1), heavy ? 75 : manifest ? 45 : 0);
                }

                return;
            }

            if (game.Threatened || sinceThreat < 35)
            {
                cooldown = Mathf.Max(cooldown, 35);
                return;
            }

            cooldown -= dt;
            if (cooldown > 0)
                return;
            Room = game.CCTV ? game.CameraIndex + 1 : Mathf.Clamp(P13Rules.Room(game.Player), 1, 9);
            if (pending && !game.CCTV)
            {
                pending = false;
                Begin(HorrorKind.ReturnSequence);
                return;
            }

            var pool = new List<HorrorKind>{HorrorKind.Knock, HorrorKind.Pipe, HorrorKind.Elevator, HorrorKind.Silence};
            if (Intensity >= 1)
                pool.AddRange(new[]{HorrorKind.Flicker, HorrorKind.FalseSteps, HorrorKind.ShadowCrossing, HorrorKind.Presence});
            if (new[]{5, 7, 8, 9}.Contains(Room))
                pool.Add(HorrorKind.MovingObject);
            if (Room == 2)
                pool.Add(HorrorKind.Phone);
            if (Room == 6)
                pool.Add(HorrorKind.Monitor);
            if (Intensity >= 2 && clock - lastHeavy >= 100)
            {
                pool.Add(HorrorKind.SectorBlackout);
                if (Intensity >= 3)
                    pool.Add(HorrorKind.FullBlackout);
            }

            if (Intensity >= 2)
                pool.AddRange(new[]{HorrorKind.Breath, HorrorKind.Blackout});
            if (game.CCTV)
            {
                pool.AddRange(new[]{HorrorKind.Interference, HorrorKind.CameraOnly, HorrorKind.CameraStare});
                if (Intensity >= 2)
                    pool.Add(HorrorKind.SignalLoss);
            }

            if (Room >= 8 && Intensity >= 2)
                pool.Add(HorrorKind.Apparition);
            if (game.Night == 1)
                pool.RemoveAll(k => new[]{HorrorKind.Apparition, HorrorKind.FullBlackout, HorrorKind.SectorBlackout, HorrorKind.ShadowCrossing, HorrorKind.CameraOnly, HorrorKind.CameraStare, HorrorKind.Breath}.Contains(k));
            if (game.Energy < 10)
                pool.RemoveAll(k => k == HorrorKind.Flicker || k == HorrorKind.Phone || k == HorrorKind.Monitor);
            if (!game.Flashlight && game.Night >= 3)
                pool.Add(HorrorKind.Presence);
            pool.RemoveAll(k => recent.Contains(k));
            if (pool.Count == 0)
            {
                cooldown = 10;
                return;
            }

            Begin(pool[random.Next(pool.Count)]);
        }

        void Begin(HorrorKind kind)
        {
            Active = kind;
            Elapsed = 0;
            sequenceStage = 0;
            Count++;
            recent.Enqueue(kind);
            while (recent.Count > 3)
                recent.Dequeue();
            if (kind == HorrorKind.FullBlackout || kind == HorrorKind.SectorBlackout)
                lastHeavy = clock;
            var source = game.Data.cameras[Room - 1].target;
            switch (kind)
            {
                case HorrorKind.Knock:
                case HorrorKind.Pipe:
                    game.Audio.Spatial("knock", source, .35f);
                    break;
                case HorrorKind.FalseSteps:
                    game.Audio.Spatial("P13_PlayerStep_2", source, .35f);
                    break;
                case HorrorKind.Breath:
                    game.Audio.Spatial("P13_Player_Recovery", source, .10f);
                    break;
                case HorrorKind.Phone:
                    game.Audio.Spatial("phone", source, .25f);
                    break;
                case HorrorKind.Elevator:
                    game.Audio.Spatial("lift_ding", P13Rules.RK(0, 2, 7), .3f);
                    break;
                case HorrorKind.MovingObject:
                    movingProp = game.World.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Pista_" + game.Data.narrative[Mathf.Min(game.Clues.Count, 8)].id);
                    if (movingProp)
                        propStart = movingProp.localPosition;
                    break;
                case HorrorKind.Presence:
                case HorrorKind.Apparition:
                case HorrorKind.ShadowCrossing:
                case HorrorKind.CameraOnly:
                    Manifest(source);
                    break;
            }
        }

        void Manifest(Vector3 p)
        {
            // The source's brief anonymous shadow is distinct from the optional cast.
            // Ascensorista/Cuidadora/Archivista/SombraPrincipal assets are absent in the source;
            // never replace those named characters with generated humanoids.
            if (game.Night < 2 || game.Floor != 13)
                return;
            bool mustBeVisible = Active == HorrorKind.ShadowCrossing || Active == HorrorKind.Apparition || Active == HorrorKind.CameraOnly;
            if (Active == HorrorKind.ShadowCrossing)
                p = game.Player + game.View.transform.forward * 3.2f - game.View.transform.right * 1.2f;
            else if (Active != HorrorKind.Apparition)
                p = game.Player + game.View.transform.forward * 3.5f - game.View.transform.right * .55f;
            p.y = 0;
            var eye = p + Vector3.up * 1.5f;
            var delta = eye - game.Observer;
            bool inView = Vector3.Dot(delta.normalized, game.View.transform.forward) > .35f;
            if (!mustBeVisible && inView)
                return;
            if (Vector3.Distance(p, game.Guard.Position) < 2 || !game.World.CanStand(p, .18f))
                return;
            silhouette = new GameObject("SombraBreve_NoLetal");
            silhouette.transform.SetParent(game.World.Root);
            silhouette.transform.position = p;
            P13World.Box("Cuerpo", new Vector3(0, .95f, 0), new Vector3(.38f, 1.7f, .20f), new Color(.015f, .018f, .018f), silhouette.transform);
            P13World.Box("Cabeza", new Vector3(0, 1.93f, 0), new Vector3(.26f, .29f, .23f), new Color(.015f, .018f, .018f), silhouette.transform);
            manifestationStart = p;
        }

        public void End()
        {
            Active = null;
            if (silhouette)
                UnityEngine.Object.Destroy(silhouette);
            silhouette = null;
            if (movingProp)
                movingProp.localPosition = propStart;
            movingProp = null;
        }

        static int Sector(int room)
        {
            return new[]{5, 6, 3}.Contains(room) ? 1 : new[]{8, 9, 4}.Contains(room) ? 2 : 0;
        }

        public float Multiplier(int room)
        {
            if (!Active.HasValue)
                return 1;
            var k = Active.Value;
            if (k == HorrorKind.Flicker && room == Room)
                return Mathf.Sin(Elapsed * 47) > .1f ? 1 : .10f;
            if (k == HorrorKind.ReturnSequence)
                return Elapsed < 3 ? 1 : Elapsed > 13 ? Mathf.Clamp01((Elapsed - 13) / 3) : 0;
            bool affects = k == HorrorKind.FullBlackout || k == HorrorKind.SectorBlackout && Sector(room) == Sector(Room) || k == HorrorKind.Blackout && room == Room;
            if (!affects)
                return 1;
            float remaining = Duration(k) - Elapsed;
            return remaining < 1.5f ? Mathf.Max(0, 1 - remaining / 1.5f) : 0;
        }

        public bool SignalLost => Active == HorrorKind.SignalLoss || Multiplier(game.CameraIndex + 1) < .05f;
        public float Interference => Active == HorrorKind.Interference || Active == HorrorKind.CameraStare ? .65f : 0;
        void Weather(float dt)
        {
            rainClock += dt;
            stormWait -= dt;
            if (stormWait <= 0)
            {
                stormWait = 24 + random.Next(57);
                flashTime = 0;
                pulses = 1 + random.Next(3);
                thunderWait = .7f + (float)random.NextDouble() * 4.3f;
                thunderIndex = random.Next(3);
            }

            if (flashTime >= 0)
            {
                flashTime += dt;
                if (flashTime > pulses * .22f)
                    flashTime = -1;
            }

            if (thunderWait >= 0)
            {
                thunderWait -= dt;
                if (thunderWait < 0)
                    game.Audio.Spatial("P13_Thunder_" + thunderIndex, game.Observer + Vector3.up * 3, game.Threatened ? .10f : .3f);
            }

            float distance = game.World.Windows.Where(w => (w.position.x > 20) == (game.Observer.x > 20)).Select(w => Vector3.Distance(w.position, game.Observer)).DefaultIfEmpty(30).Min();
            float intensity = .75f + .13f * Mathf.Sin(rainClock * .071f) + .06f * Mathf.Sin(rainClock * .19f);
            game.Audio.Rain(intensity * (.09f + .13f / (1 + distance * .35f)), (Active == HorrorKind.Silence || Active == HorrorKind.FullBlackout || Active == HorrorKind.ReturnSequence) ? 0 : game.Threatened || game.Building.Speaking ? .35f : 1);
            foreach (var window in game.World.Windows)
            {
                bool near = Vector3.Distance(window.position, game.Observer) < 10;
                window.gameObject.SetActive(near);
                if (near)
                    for (int i = 0; i < window.childCount; i++)
                    {
                        var drop = window.GetChild(i);
                        var p = drop.localPosition;
                        p.y = .75f - ((rainClock * (1.7f + i % 5 * .13f) * intensity + i * .397f) % 1.5f);
                        drop.localPosition = p;
                    }
            }

            game.WeatherFlash = flashTime >= 0 && flashTime % .22f < .09f ? (1 - flashTime % .22f / .09f) * .7f : 0;
        }
    }
}
