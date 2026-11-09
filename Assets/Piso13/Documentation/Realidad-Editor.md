# Editar Piso13 Realidad

## Abrir y modificar
Abre `Assets/Piso13/Scenes/Piso13 Realidad.unity` sin entrar en Play. El mapa, luces, lluvia, cámara, enemigo y objetos interactivos ya están guardados. No es necesario ejecutar el generador para editar el nivel.

`Piso13Bootstrap / P13RealidadSceneBindings` reúne las referencias. Su Inspector permite validar y actualizar la lista espacial cuando agregas o eliminas marcadores.

## Mapa y colisión
- La geometría está en `Piso13_Realidad_World/Geometria`.
- El movimiento y la navegación mantienen el sistema AABB propio del juego. Los colliders de teléfono, taza y anomalías sirven para interacción; no sustituyen los obstáculos lógicos.
- Los marcadores `Wall`, `Furniture` y `Floor12Obstacle` muestran sus volúmenes al seleccionarlos. Edita Size y Transform sin rotarlos.
- Algunos proxies importados están bajo la malla correspondiente. Mueve esa malla para que se mueva también el proxy. En los demás casos, crea un contenedor común y mueve visual y proxy juntos; mover solo el visual no actualiza la colisión.
- Para agregar un obstáculo: crea un objeto con `P13RealidadSpatialAuthoring`, asigna Kind y tamaño, colócalo dentro del mundo y pulsa **Actualizar lista tras añadir marcadores** en el Inspector de bindings. Guarda la escena.
- `FloorBounds` limita la rejilla de navegación. `Room` clasifica habitaciones por ID y prioridad. Las superposiciones con prioridades distintas son intencionales: oficina y ascensor tienen prioridad sobre pasillo. Evita solapamientos ambiguos con la misma prioridad.
- Se conserva navegación en XZ sobre un plano, no un sistema de plantas apiladas o superficies inclinadas.

## Jugador y enemigo
Edita `PlayerSpawn` y `EnemySpawn`: posición y orientación se respetan al iniciar/reintentar. La altura de ojos sigue la lógica original del controlador.

Los nodos `Patrol` usan IDs 0–9 y conexiones por ID. Conserva el significado de 8 (almacén) y 9 (aproximación a puerta), ya que la progresión los utiliza. Mueve nodos a zonas transitables.

El enemigo está inactivo al inicio. No lo dejes activo para guardar una sesión normal: la advertencia controla su aparición.

## Cámaras CCTV
Los nueve marcadores CCTV conservan IDs 0–8. Mueve el marcador para cambiar posición y su Transform Target para cambiar objetivo. No son nueve cámaras de render adicionales: el jugador reutiliza una sola cámara para la vista CCTV.

Los IDs CAM05 y CAM06 mantienen relación con la secuencia final y la persona elegante; no reordenarlos como si fueran solo etiquetas.

## Objetos y anomalías
Mueve teléfono, cafetera, taza y cama directamente. Configura distancias y colliders en sus componentes. La estación CCTV usa su Transform, no sobrescribe la ubicación desde DeskCoordinates en la escena convertida.

El reloj y la persona están guardados pero inactivos; la máscara tiene sus visuales ocultos. Para editar el reloj/persona, habilítalos temporalmente en Edit Mode y vuelve a dejarlos inactivos antes de guardar o probar. Para la máscara, puedes editar sus mallas y puntos `MaskSpawn` sin hacerla aparecer en gameplay. El juego activa estas anomalías según sus eventos originales.

Los puntos `MaskSpawn` se importan al iniciar/reintentar. Los IDs corresponden a habitaciones. Las listas AllowedSpawnRooms y SpawnBehindRooms continúan controlando las reglas de aparición.

La cama se oculta en el final y se muestra el guardia durmiendo persistente. `ObjetivoFinal` acompaña a la cama; no borrarlo al cambiar decoración.

## Puerta e iluminación
Edita el contenedor `PuertaOficina` para desplazar juntos visual, extremos abierto/cerrado, punto de interacción y bloqueo. Cambiar solo el visual será reemplazado por el extremo activo durante gameplay.

Las intensidades guardadas son bases: el juego conserva presupuesto y multiplicadores originales. Se integraron los valores nocturnos del repo (.38/.32, luz de puerta .65, linterna 12) y el ambiente oscuro. Puedes ajustarlos desde la escena.

## Guardado, conversión y compatibilidad
- Guarda los cambios fuera de Play; las modificaciones durante una sesión son temporales.
- El convertidor detecta bindings existentes y no sobrescribe ni duplica el mundo.
- La copia previa a la conversión está en `Library/Piso13SceneBackups`; Library es local y puede borrarse por Unity. Usa control de versiones para conservar respaldos duraderos.
- No elimines `P13RealidadSceneBindings`: su ausencia permite la ruta procedural de compatibilidad.
- No se alteró el contrato compartido de World.json ni se sustituyó el controlador por NavMesh/CharacterController.
- `P13CoffeeCup.cs`, `P13ReportableProp.cs` y `P13PersonaEleganteAnomaly.cs` son archivos parciales con el nombre correcto para persistir esos componentes; su implementación sigue en P13RealidadInteractables.cs. Deben conservarse junto con sus .meta.

## Comprobar
1. **Piso 13 / Realidad / Validar escena editable**: referencias e IDs.
2. **Piso 13 / Realidad / Pruebas de autoría**: estructura, persistencia, datos espaciales e idempotencia.
3. Play: comprobar exploración, llamada, café, reloj, máscara, advertencia, persecución, puerta, pausa, captura y final.
4. Reintentar después de reportar anomalías o durante una llamada: se restauran estados sin reconstruir el mapa.

Las pruebas automatizadas no sustituyen una pasada manual completa ni garantizan que nuevas paredes estén visualmente alineadas con sus proxies. Las verificaciones ejecutadas y cualquier limitación deben constar en el reporte final de esta migración.
