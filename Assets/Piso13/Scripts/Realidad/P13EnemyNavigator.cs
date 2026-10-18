using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Gestor especializado de navegación y movimiento para el enemigo de Piso 13 Realidad.
    /// - Unifica el radio de colisión (0.45m).
    /// - Implementa deslizamiento de paredes (wall-sliding) para evitar atascos.
    /// - Detección y resolución escalonada de bloqueos.
    /// - Suavizado y avance dinámico de waypoints.
    /// </summary>
    public sealed class P13EnemyNavigator
    {
        public const float AgentRadius = 0.45f;

        readonly P13RealidadWorld world;
        readonly P13RealidadNavigation navigation;

        List<Vector3> route = new List<Vector3>();
        int routeIndex = 0;
        Vector3 currentGoal;
        bool hasGoal = false;

        float replanCooldown = 0f;
        float stuckTimer = 0f;
        Vector3 lastPositionSample;
        float positionSampleTimer = 0f;

        public bool HasPath => route != null && routeIndex < route.Count;
        public bool ReachedDestination => !hasGoal || !HasPath;
        public Vector3 CurrentGoal => currentGoal;
        public List<Vector3> CurrentRoute => route;

        public P13EnemyNavigator(P13RealidadWorld world, P13RealidadNavigation navigation)
        {
            this.world = world;
            this.navigation = navigation;
        }

        public void SetDestination(Vector3 goal, Vector3 currentPos, bool forceReplan = false)
        {
            goal.y = 0.8f;
            if (!hasGoal || forceReplan || (goal - currentGoal).sqrMagnitude > 0.36f || (replanCooldown <= 0f && (goal - currentGoal).sqrMagnitude > 0.04f))
            {
                currentGoal = goal;
                hasGoal = true;
                replanCooldown = 0.35f;
                CalculatePath(currentPos, goal);
            }
        }

        public void ClearPath()
        {
            route.Clear();
            routeIndex = 0;
            hasGoal = false;
        }

        void CalculatePath(Vector3 start, Vector3 goal)
        {
            if (navigation == null)
            {
                route.Clear();
                route.Add(goal);
                routeIndex = 0;
                return;
            }

            var safeStart = navigation.Safe(start);
            var safeGoal = navigation.Safe(goal);

            var path = navigation.Path(safeStart, safeGoal);
            if (path != null && path.Count > 0)
            {
                route = path;
                routeIndex = 0;
            }
            else
            {
                // Fallback: Si no hay camino directo, intentar ir hacia la posición segura más cercana
                route.Clear();
                if ((safeGoal - start).sqrMagnitude > 0.01f)
                {
                    route.Add(safeGoal);
                }
                routeIndex = 0;
            }
        }

        /// <summary>
        /// Realiza el paso de movimiento en dt con deslizamiento sobre paredes.
        /// Retorna la nueva posición y la rotación hacia donde mira el enemigo.
        /// </summary>
        public Vector3 TickMovement(Vector3 currentPos, float speed, float dt, float currentYaw, out float newYaw, out float travelled)
        {
            travelled = 0f;
            newYaw = currentYaw;

            if (replanCooldown > 0f) replanCooldown -= dt;

            // Monitor de atasco cada 0.5s
            positionSampleTimer += dt;
            if (positionSampleTimer >= 0.5f)
            {
                positionSampleTimer = 0f;
                float distMoved = Vector3.Distance(currentPos, lastPositionSample);
                lastPositionSample = currentPos;

                if (speed > 0.2f && hasGoal && distMoved < 0.12f)
                {
                    stuckTimer += 0.5f;
                    if (stuckTimer >= 1.0f)
                    {
                        // Resolver atasco: forzar recálculo desde celda segura
                        stuckTimer = 0f;
                        Vector3 safePos = navigation != null ? navigation.Safe(currentPos) : currentPos;
                        currentPos = safePos;
                        if (hasGoal)
                        {
                            CalculatePath(currentPos, currentGoal);
                        }
                    }
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - dt);
                }
            }

            if (!HasPath)
            {
                return currentPos;
            }

            Vector3 waypoint = route[routeIndex];
            waypoint.y = currentPos.y;

            Vector3 toWaypoint = waypoint - currentPos;
            toWaypoint.y = 0f;
            float distToWaypoint = toWaypoint.magnitude;

            // Llegada a waypoint intermedio
            if (distToWaypoint < 0.28f)
            {
                routeIndex++;
                if (routeIndex >= route.Count)
                {
                    return currentPos;
                }
                waypoint = route[routeIndex];
                waypoint.y = currentPos.y;
                toWaypoint = waypoint - currentPos;
                toWaypoint.y = 0f;
                distToWaypoint = toWaypoint.magnitude;
            }

            if (distToWaypoint < 0.001f)
            {
                return currentPos;
            }

            Vector3 moveDir = toWaypoint / distToWaypoint;
            float maxStep = speed * dt;
            float step = Mathf.Min(distToWaypoint, maxStep);

            Vector3 desiredNext = currentPos + moveDir * step;

            // Movimiento con deslizamiento (Sliding):
            Vector3 finalPos = currentPos;
            if (world != null)
            {
                // Intentar paso directo
                if (world.Clear(currentPos, desiredNext, AgentRadius, true) && world.CanStand(desiredNext, AgentRadius))
                {
                    finalPos = desiredNext;
                }
                else
                {
                    // Deslizamiento en ejes independientes (X primero, luego Z)
                    Vector3 testX = currentPos + new Vector3(moveDir.x * step, 0f, 0f);
                    if (world.Clear(currentPos, testX, AgentRadius, true) && world.CanStand(testX, AgentRadius))
                    {
                        finalPos = testX;
                    }

                    Vector3 testZ = finalPos + new Vector3(0f, 0f, moveDir.z * step);
                    if (world.Clear(finalPos, testZ, AgentRadius, true) && world.CanStand(testZ, AgentRadius))
                    {
                        finalPos = testZ;
                    }

                    // Si ambos ejes están bloqueados, recalcular ruta
                    if ((finalPos - currentPos).sqrMagnitude < 0.0001f)
                    {
                        if (replanCooldown <= 0f && hasGoal)
                        {
                            replanCooldown = 0.4f;
                            CalculatePath(currentPos, currentGoal);
                        }
                    }
                }
            }
            else
            {
                finalPos = desiredNext;
            }

            travelled = Vector3.Distance(currentPos, finalPos);

            // Orientación suave hacia la dirección de movimiento
            if (distToWaypoint > 0.05f)
            {
                float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                newYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, dt * 260f);
            }

            return finalPos;
        }
    }
}
