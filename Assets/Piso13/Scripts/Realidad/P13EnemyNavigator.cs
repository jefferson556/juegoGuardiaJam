using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piso13.Realidad
{
    /// <summary>
    /// Gestor especializado de navegación y movimiento para el enemigo de Piso 13 Realidad.
    /// - Radio de colisión optimizado a 0.36m para circular con holgura por puertas y pasillos estrechos.
    /// - Deslizamiento bidireccional sobre paredes (wall-sliding) con evasión perpendicular en esquinas.
    /// - Detección y salto automático de waypoints con línea de visión despejada.
    /// - Resolución proactiva de bloqueos para evitar que el enemigo se quede inmóvil al chocar.
    /// </summary>
    public sealed class P13EnemyNavigator
    {
        public const float AgentRadius = 0.36f;

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
        public bool ReachedDestination => hasGoal && Vector3.Distance(new Vector3(lastPositionSample.x, 0f, lastPositionSample.z), new Vector3(currentGoal.x, 0f, currentGoal.z)) < 0.65f;
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
            // Si no tiene meta, o no tiene camino válido, o se fuerza replan, o la meta cambió
            if (!hasGoal || !HasPath || forceReplan || (goal - currentGoal).sqrMagnitude > 0.36f || (replanCooldown <= 0f && (goal - currentGoal).sqrMagnitude > 0.04f))
            {
                currentGoal = goal;
                hasGoal = true;
                replanCooldown = 0.25f;
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
            route.Clear();
            routeIndex = 0;

            if (world == null || navigation == null)
            {
                route.Add(goal);
                return;
            }

            // 1. Línea de visión directa si el camino está libre
            if (world.Clear(start, goal, 0.05f, true))
            {
                route.Add(goal);
                return;
            }

            // 2. Cálculo mediante grafo de navegación A*
            var safeStart = navigation.Safe(start);
            var safeGoal = navigation.Safe(goal);

            var path = navigation.Path(safeStart, safeGoal);
            if (path != null && path.Count > 0)
            {
                route = path;
                if (Vector3.Distance(route[route.Count - 1], goal) > 0.25f && world.Clear(route[route.Count - 1], goal, 0.05f, true))
                {
                    route.Add(goal);
                }
            }
            else
            {
                // 3. Fallback hacia la celda segura de la meta o inicio
                if (Vector3.Distance(start, safeGoal) > 0.2f)
                {
                    route.Add(safeGoal);
                }
                else if (Vector3.Distance(start, goal) > 0.2f)
                {
                    route.Add(goal);
                }
            }
        }

        /// <summary>
        /// Realiza el paso de movimiento en dt con deslizamiento sobre paredes y resolución de atascos.
        /// Retorna la nueva posición y la rotación hacia donde mira el enemigo.
        /// </summary>
        public Vector3 TickMovement(Vector3 currentPos, float speed, float dt, float currentYaw, out float newYaw, out float travelled)
        {
            travelled = 0f;
            newYaw = currentYaw;

            if (replanCooldown > 0f) replanCooldown -= dt;

            if (!HasPath)
            {
                // Si tiene meta activa pero perdió el camino, recalcular inmediatamente
                if (hasGoal && replanCooldown <= 0f)
                {
                    replanCooldown = 0.25f;
                    CalculatePath(currentPos, currentGoal);
                }
                return currentPos;
            }

            // Atajo dinámico: si el enemigo puede ver directamente un waypoint más adelantado de la ruta,
            // salta los waypoints intermedios para no recortar mal esquinas ni tropezar
            if (world != null)
            {
                for (int k = route.Count - 1; k > routeIndex; k--)
                {
                    if (world.Clear(currentPos, route[k], 0.05f, true))
                    {
                        routeIndex = k;
                        break;
                    }
                }
            }

            Vector3 waypoint = route[routeIndex];
            waypoint.y = currentPos.y;

            Vector3 toWaypoint = waypoint - currentPos;
            toWaypoint.y = 0f;
            float distToWaypoint = toWaypoint.magnitude;

            // Llegada a waypoint intermedio
            if (distToWaypoint < 0.35f)
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

            Vector3 finalPos = currentPos;
            if (world != null)
            {
                Vector3 desiredNext = currentPos + moveDir * step;

                // 1. Intentar paso directo hacia el waypoint
                if (world.CanStand(desiredNext, AgentRadius) && world.Clear(currentPos, desiredNext, 0.05f, true))
                {
                    finalPos = desiredNext;
                }
                else
                {
                    // 2. Deslizamiento suave sobre paredes (Sliding bidireccional)
                    // Intento A: X primero, luego Z
                    Vector3 slideXZ = world.Move(currentPos, moveDir * step, AgentRadius);

                    // Intento B: Z primero, luego X
                    Vector3 slideZX = currentPos;
                    Vector3 testZ = currentPos + new Vector3(0f, 0f, moveDir.z * step);
                    if (world.CanStand(testZ, AgentRadius) && world.Clear(currentPos, testZ, 0.05f, true))
                        slideZX = testZ;
                    Vector3 testZX = slideZX + new Vector3(moveDir.x * step, 0f, 0f);
                    if (world.CanStand(testZX, AgentRadius) && world.Clear(slideZX, testZX, 0.05f, true))
                        slideZX = testZX;

                    // Elegir la opción que logre mayor avance hacia el waypoint
                    float distA = Vector3.Distance(slideXZ, waypoint);
                    float distB = Vector3.Distance(slideZX, waypoint);
                    finalPos = distA <= distB ? slideXZ : slideZX;

                    // 3. Si ambos ejes quedan bloqueados contra la pared o esquina
                    float movedDist = Vector3.Distance(currentPos, finalPos);
                    if (movedDist < 0.001f)
                    {
                        // Intentar deslizamiento lateral perpendicular a la dirección de avance
                        Vector3 perp1 = new Vector3(-moveDir.z, 0f, moveDir.x);
                        Vector3 perp2 = -perp1;
                        Vector3 testP1 = currentPos + perp1 * step;
                        Vector3 testP2 = currentPos + perp2 * step;

                        bool canP1 = world.CanStand(testP1, AgentRadius) && world.Clear(currentPos, testP1, 0.05f, true);
                        bool canP2 = world.CanStand(testP2, AgentRadius) && world.Clear(currentPos, testP2, 0.05f, true);

                        if (canP1 && !canP2) finalPos = testP1;
                        else if (canP2 && !canP1) finalPos = testP2;
                        else if (canP1 && canP2)
                        {
                            finalPos = Vector3.Distance(testP1, waypoint) <= Vector3.Distance(testP2, waypoint) ? testP1 : testP2;
                        }

                        // Si el waypoint actual es el que resulta inalcanzable, avanzar al siguiente o recalcular
                        if (Vector3.Distance(currentPos, finalPos) < 0.001f)
                        {
                            if (routeIndex < route.Count - 1)
                            {
                                routeIndex++; // Saltar waypoint inalcanzable
                            }
                            else if (replanCooldown <= 0f && hasGoal)
                            {
                                replanCooldown = 0.25f;
                                CalculatePath(currentPos, currentGoal);
                            }
                        }
                    }
                }
            }
            else
            {
                finalPos = currentPos + moveDir * step;
            }

            travelled = Vector3.Distance(currentPos, finalPos);

            // Monitor de atasco acumulado
            positionSampleTimer += dt;
            if (positionSampleTimer >= 0.4f)
            {
                positionSampleTimer = 0f;
                float distMoved = Vector3.Distance(finalPos, lastPositionSample);
                lastPositionSample = finalPos;

                if (speed > 0.2f && hasGoal && distMoved < 0.08f)
                {
                    stuckTimer += 0.4f;
                    if (stuckTimer >= 0.8f)
                    {
                        stuckTimer = 0f;
                        if (routeIndex < route.Count - 1)
                        {
                            routeIndex++;
                        }
                        else if (hasGoal)
                        {
                            Vector3 safePos = navigation != null ? navigation.Safe(finalPos) : finalPos;
                            finalPos = safePos;
                            CalculatePath(finalPos, currentGoal);
                        }
                    }
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - dt);
                }
            }

            // Orientación suave hacia donde se mueve o hacia el waypoint
            Vector3 faceDir = travelled > 0.001f ? (finalPos - currentPos).normalized : moveDir;
            if (faceDir.sqrMagnitude > 0.001f)
            {
                float targetYaw = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
                newYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, dt * 320f);
            }

            return finalPos;
        }
    }
}
