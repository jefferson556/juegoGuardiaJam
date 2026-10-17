using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13
{
    public sealed class P13Character
    {
        public readonly Transform Root, Visual;
        readonly Animation animation;
        string playing;
        public P13Character(string model, Transform parent, float height)
        {
            Root = new GameObject(model + "_LogicalRoot").transform;
            Root.SetParent(parent);
            Visual = P13World.Model(model, Root).transform;
            var renderers = Visual.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            float scale = height / b.size.y;
            Visual.localScale = Vector3.one * scale;
            Visual.localPosition = new Vector3(0, -.8f - b.min.y * scale, 0);
            // The character FBX files face -Z after the RealityKit -> Unity axis conversion, but
            // the game logic treats yaw 0 as +Z (Atan2(x, z)). Turn the model around to match.
            Visual.localRotation = Quaternion.Euler(0, 180, 0);
            animation = Visual.GetComponentInChildren<Animation>();
            if (!animation)
                throw new InvalidOperationException(model + ": animaciones FBX no importadas; ejecuta Piso 13 > Preparar proyecto.");
            foreach (AnimationState s in animation)
                s.wrapMode = WrapMode.Loop;
        }

        public void Play(string name, float speed = 1, bool loop = true)
        {
            if (animation[name] == null)
            {
                if (!animation["idle"])
                    return;
                name = "idle";
            }

            if (playing != name)
            {
                animation.CrossFade(name, .22f);
                playing = name;
            }

            animation[name].wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            animation[name].speed = speed;
        }

        public void Pause(bool p)
        {
            if (animation == null) return;
            foreach (AnimationState s in animation)
                s.speed = p ? 0 : 1;
        }
    }

    public enum GuardState
    {
        Patrolling,
        Investigating,
        Hunting,
        Searching,
        AtDoor,
        Retreating
    }

    public sealed class P13Guard
    {
        readonly P13Game game;
        readonly P13Navigation navigation;
        public readonly P13Character Character;
        public GuardState State;
        public Vector3 Position;
        public float Yaw, Alert;
        public bool SeesPlayer;
        Vector3? memory;
        float clock, lastSeen = -999, lastHeard = -999, confidence, searchUntil = -1, replan, decision = 10, doorTime, stepDistance, glanceCooldown = 8, glanceTime;
        Vector3? gaze;
        List<Vector3> route = new List<Vector3>();
        int routeIndex, location = 8, destination = 8;
        float speed;
        readonly List<Noise> noises = new List<Noise>();
        struct Noise
        {
            public Vector3 p;
            public float radius, time;
        }

        static readonly Vector3[] destinations = {P13Rules.RK(0, .8f, .8f), P13Rules.RK(-6.8f, .8f, .6f), P13Rules.RK(0, .8f, 7.55f), P13Rules.RK(0, .8f, 4.5f), P13Rules.RK(-6.2f, .8f, 5.9f), P13Rules.RK(6.2f, .8f, 5.9f), P13Rules.RK(6.8f, .8f, .6f), P13Rules.RK(-6.8f, .8f, -6.2f), P13Rules.RK(6.8f, .8f, -6.2f), P13Rules.RK(0, .8f, 3.85f)};
        static readonly int[][] neighbors = {new[]{1, 6, 3}, new[]{0, 7}, new[]{3}, new[]{0, 2, 4, 5, 9}, new[]{3, 7}, new[]{3, 8}, new[]{0, 8}, new[]{1, 4}, new[]{6, 5}, new[]{3}};
        public P13Guard(P13Game g)
        {
            game = g;
            navigation = new P13Navigation(g.World);
            Character = new P13Character("MonsterGuard", g.World.Root, 2.22f);
            Reset();
        }

        public void Reset()
        {
            noises.Clear();
            replan = doorTime = stepDistance = glanceTime = speed = 0;
            glanceCooldown = 8;
            gaze = null;
            SeesPlayer = false;
            destination = 8;
            Character.Pause(false);
            Position = navigation.Safe(destinations[8]);
            Yaw = 180;
            State = GuardState.Patrolling;
            route.Clear();
            routeIndex = 0;
            location = 8;
            clock = 0;
            decision = 10;
            memory = null;
            Alert = confidence = 0;
            lastSeen = lastHeard = -999;
            searchUntil = -1;
            Publish();
        }

        public void Restore(P13Checkpoint s)
        {
            Reset();
            Position = s.guard;
            Yaw = s.guardYaw;
            Publish();
        }

        public void Hear(Vector3 p, float radius)
        {
            if (noises.Count > 12)
                noises.RemoveAt(0);
            noises.Add(new Noise{p = p, radius = radius, time = clock});
        }

        public void Tick(float dt)
        {
            clock += dt;
            replan -= dt;
            decision -= dt;
            glanceCooldown -= dt;
            glanceTime -= dt;
            if (game.Floor != 13)
            {
                Character.Play("idle");
                return;
            }

            var eye = Position + Vector3.up * 1.2f;
            var target = game.Player + Vector3.up * (game.EyeHeight - .8f);
            var delta = target - eye;
            var flat = new Vector3(delta.x, 0, delta.z);
            float distance = flat.magnitude;
            var forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            SeesPlayer = distance < (game.Crouched ? 5.5f : 11) && (distance < .6f || Vector3.Dot(forward, flat.normalized) >= Mathf.Cos(Mathf.PI * (game.Crouched ? .25f : .3f))) && game.World.Clear(eye, target);
            if (SeesPlayer)
            {
                confidence = Mathf.Min(1, confidence + dt * (game.Crouched && distance > 1.2f ? .8f : 3.5f));
                lastSeen = clock;
                memory = game.Player;
                searchUntil = -1;
                Alert = Mathf.Min(100, Alert + dt * (game.Crouched ? 30 : 95));
                if (State != GuardState.AtDoor && State != GuardState.Retreating)
                    State = confidence >= .55f ? GuardState.Hunting : GuardState.Investigating;
            }
            else
            {
                confidence = Mathf.Max(0, confidence - dt * 1.5f);
                Alert = Mathf.Max(0, Alert - dt * 4);
                if (State == GuardState.Hunting && clock - lastSeen > 1.4f)
                    State = GuardState.Investigating;
            }

            foreach (var n in noises)
                if (clock - n.time <= .6f && !SeesPlayer && Vector3.Distance(Position, n.p) <= n.radius * (game.World.Clear(eye, n.p + Vector3.up * .7f) ? 1 : .32f))
                {
                    memory = n.p;
                    lastHeard = clock;
                    searchUntil = -1;
                    if (State != GuardState.Hunting && State != GuardState.AtDoor && State != GuardState.Retreating)
                        State = GuardState.Investigating;
                    Alert = Mathf.Max(35, Alert);
                }

            noises.Clear();
            if (State == GuardState.AtDoor)
            {
                doorTime -= dt;
                Character.Play("wall_brace_left");
                if (!game.World.DoorClosed)
                {
                    State = GuardState.Hunting;
                    memory = game.Player;
                    Route(game.Player);
                    return;
                }

                if (Mathf.FloorToInt(doorTime + dt) != Mathf.FloorToInt(doorTime) && doorTime > 1)
                {
                    game.Audio.Spatial("knock", Position, .8f);
                    game.DoorShake = .12f;
                }

                if (doorTime <= 0)
                {
                    State = GuardState.Retreating;
                    Route(destinations[3]);
                    destination = 3;
                    decision = 8;
                }

                return;
            }

            if (SeesPlayer && distance < .85f && game.World.Clear(eye, target))
            {
                game.Capture();
                return;
            }

            if (State == GuardState.Investigating && memory.HasValue && (Vector3.Distance(Position, memory.Value) < .65f || clock - Mathf.Max(lastSeen, lastHeard) > 18))
            {
                State = GuardState.Searching;
                searchUntil = clock + 6;
                route.Clear();
            }

            if (State == GuardState.Searching)
            {
                Character.Play("investigate");
                Yaw += dt * 20;
                if (clock >= searchUntil)
                {
                    State = GuardState.Patrolling;
                    memory = null;
                    Alert = Mathf.Min(Alert, 15);
                    decision = 2;
                }

                Publish();
                return;
            }

            if ((State == GuardState.Hunting || State == GuardState.Investigating) && memory.HasValue && replan <= 0)
            {
                replan = .65f;
                var remembered = memory.Value;
                bool office = Mathf.Abs(remembered.x) < 2.6f && Mathf.Abs(remembered.z) < 2.6f;
                bool approach = game.World.DoorClosed && office && Position.z < -3.4f;
                Route(approach ? destinations[9] : remembered);
                destination = approach ? 9 : -1;
            }

            if (State == GuardState.Patrolling && routeIndex >= route.Count && decision <= 0)
            {
                decision = 10;
                float chance = .18f + Mathf.Min(1, game.Minute / 360f) * .45f;
                if (UnityEngine.Random.value < chance)
                {
                    var choices = neighbors[location];
                    destination = choices[UnityEngine.Random.Range(0, choices.Length)];
                    RouteAuthored(location, destination);
                }
            }

            bool moving = routeIndex < route.Count;
            if (moving)
            {
                var goal = route[routeIndex];
                var d = goal - Position;
                d.y = 0;
                if (d.magnitude < .12f)
                {
                    routeIndex++;
                    if (routeIndex >= route.Count)
                    {
                        location = destination >= 0 ? destination : location;
                        if (destination == 9 && game.World.DoorClosed)
                        {
                            State = GuardState.AtDoor;
                            doorTime = 5;
                            game.Audio.Spatial("knock", Position);
                            return;
                        }

                        if (State == GuardState.Retreating)
                            State = GuardState.Patrolling;
                    }
                }
                else
                {
                    float pace = game.Minute < 60 ? .85f : game.Minute < 180 ? .95f : game.Minute < 295 ? 1.05f : 1.15f;
                    float desired = (State == GuardState.Hunting ? 2.65f : State == GuardState.Investigating ? 1.35f : .85f) * pace;
                    speed = Mathf.MoveTowards(speed, desired, dt * 3);
                    var before = Position;
                    var next = Position + d.normalized * Mathf.Min(d.magnitude, speed * dt);
                    if (game.World.Clear(Position, next, P13Navigation.Radius, true) && game.World.CanStand(next, P13Navigation.Radius))
                        Position = next;
                    else
                    {
                        route.Clear();
                        replan = 0;
                    }

                    Yaw = Mathf.MoveTowardsAngle(Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 170);
                    float travelled = Vector3.Distance(before, Position);
                    stepDistance += travelled;
                    if (stepDistance > .72f)
                    {
                        stepDistance = 0;
                        game.Audio.Spatial("guard_step", Position, .55f);
                    }

                    Character.Play(State == GuardState.Hunting ? "run" : State == GuardState.Investigating ? "walk" : "patrol", Mathf.Clamp(speed / (State == GuardState.Hunting ? 2.65f : .85f), .65f, 1.6f));
                }
            }
            else
            {
                speed = 0;
                Character.Play("idle");
            }

            if (glanceCooldown <= 0 && !moving)
            {
                glanceCooldown = UnityEngine.Random.Range(8, 16);
                if (UnityEngine.Random.value < .4f)
                {
                    var camera = game.Data.cameras.Where(c => Vector3.Distance(c.position, eye) < 4 && game.World.Clear(eye, c.position)).OrderBy(c => Vector3.Distance(c.position, eye)).FirstOrDefault();
                    if (camera != null)
                    {
                        gaze = camera.position;
                        glanceTime = 1.8f;
                    }
                }
            }

            if (gaze.HasValue && glanceTime > 0 && !moving)
            {
                var d = gaze.Value - Position;
                Yaw = Mathf.MoveTowardsAngle(Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 90);
            }

            Publish();
        }

        void Route(Vector3 goal)
        {
            route = navigation.Path(Position, goal);
            routeIndex = 0;
        }

        void RouteAuthored(int from, int to)
        {
            var authored = game.Data.routes.FirstOrDefault(r => r.from == from && r.to == to);
            route.Clear();
            var p = Position;
            if (authored != null)
                foreach (var waypoint in authored.points)
                {
                    var part = navigation.Path(p, waypoint);
                    if (part.Count == 0)
                        continue;
                    route.AddRange(part);
                    p = part[part.Count - 1];
                }

            var end = navigation.Path(p, destinations[to]);
            route.AddRange(end);
            routeIndex = 0;
        }

        public float Interference(Vector3 camera)
        {
            float d = Vector3.Distance(Position + Vector3.up * 1.2f, camera);
            return d < 4 && game.World.Clear(camera, Position + Vector3.up * 1.2f) ? (1 - d / 4) * .65f : 0;
        }

        public void Publish()
        {
            Character.Root.position = Position;
            Character.Root.rotation = Quaternion.Euler(0, Yaw, 0);
        }
    }
}
