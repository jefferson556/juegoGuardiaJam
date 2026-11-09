# Project Overview
- **Game Title:** Piso 13; escena objeto de esta migración: `Piso13 Realidad`.
- **High-Level Concept:** Experiencia de terror en primera persona donde un guardia explora el edificio, observa cámaras CCTV, interactúa con objetos y reporta anomalías mientras una amenaza lo persigue. Esta tarea convierte la configuración espacial del nivel en contenido persistente del editor sin rediseñar el juego.
- **Players:** Un jugador, enemigo controlado por IA según el código revisado.
- **Inspiration / Reference Games:** No especificadas; no se necesitan nuevas referencias para conservar la experiencia existente.
- **Tone / Art Direction:** Terror nocturno, oficinas, lluvia y anomalías; conservar modelos y materiales actuales.
- **Target Platform:** Standalone Windows 64, según contexto proporcionado.
- **Screen Orientation / Resolution:** Experiencia de escritorio; conservar resolución y comportamiento de ventana actuales, por verificar en Project Settings.
- **Render Pipeline:** URP según shaders utilizados por el proyecto; activo reportado `PC_RPAsset`. Confirmar su tipo antes de implementar.
- **Unity:** 6000.5.5f1 según contexto proporcionado.
- **Alcance propuesto, pendiente de aprobación:** Mapa, utilería, configuración de jugador/enemigo, cámaras CCTV, habitaciones, obstáculos y puntos de aparición editables. El código continúa gestionando el comportamiento. No convertir la UI ni sustituir la navegación por NavMesh.

## Alternativas consideradas
1. **Escena editable completa — recomendada:** Referencias persistentes, configuración espacial en componentes y generación solo como herramienta inicial. Mayor esfuerzo de migración, pero una sola fuente de verdad espacial y menor riesgo al modificar el nivel. Desde MDA, cambia la autoría de las mecánicas sin cambiar las dinámicas de vigilancia y persecución ni la sensación de tensión.
2. **Solo entorno visual:** Más rápida, pero conservaría obstáculos y posiciones lógicas independientes, por lo que mover paredes, muebles o cámaras seguiría requiriendo cambios adicionales.
3. **Generador en el editor sin migración de runtime:** Permite previsualizar la base, pero el arranque actual seguiría borrando o reconstruyendo objetos; exige un modo explícito para respetar lo editado.

El selector de preferencias falló técnicamente. La opción 1 se presenta como propuesta, no como elección confirmada. La aprobación de este documento confirmará el alcance; si se prefiere otra alternativa, revisar este mismo archivo antes de implementar.

# Game Mechanics
## Core Gameplay Loop
Conservar exploración, observación CCTV, consumo y autorrellenado del café, reportes correctos/incorrectos, llamada del teléfono, aparición del reloj y máscara, advertencia, persecución y final de la demo. Mantener temporizadores, penalizaciones y orden de eventos.

Separar:
- **Autoría persistente:** Transform, material, collider de interacción, punto de cámara, región, spawn y configuración inicial.
- **Estado de sesión:** Energía, sueño, visibilidad de anomalías, puerta abierta/cerrada, alertas, animaciones, sonidos y progresión.

Los objetos futuros pueden quedar inactivos o con visuales ocultos en la escena guardada; el editor debe permitir seleccionarlos y previsualizarlos sin adelantarlos en gameplay.

## Controls and Input Methods
Conservar teclado y ratón y los controles existentes, incluyendo E para interacción y R para reporte. El contexto indica New Input System; verificar `activeInputHandler` antes de cualquier modificación de controles. No migrar ni añadir controles en esta tarea. Si fuese imprescindible añadir una acción, usar InputAction/PlayerInput y el backend activo, sin ampliar sondeos de entrada.

# UI
Sin rediseño ni conversión de HUD o menús. Mantener `P13RealidadUI` y `P13RealidadMenus`.

Interfaz de autoría propuesta:
```text
Piso 13 / Realidad
  Convertir escena actual a editable… [confirmación, solo primera conversión]
  Validar escena editable

Inspector: P13RealidadSceneBindings
  Mundo y puerta
  Jugador y spawn
  Enemigo y spawn
  Objetos interactivos y anomalías
  CCTV / Habitaciones / Obstáculos / Puntos de patrulla
  Validación de referencias
```
Gizmos con colores diferenciados para obstáculos, regiones, CCTV, patrullas, spawn y alcance de interacción. Mostrar detalles al seleccionar para evitar saturar Scene View. Si se incluye previsualización de objetos ocultos, debe ser reversible y no modificar su estado inicial guardado accidentalmente.

# Key Asset & Context
## Hallazgos verificados por lectura
- `Assets/Piso13/Scenes/Piso13 Realidad.unity`: contiene `Piso13Bootstrap`, componentes `P13RealidadGame`, `P13RealidadUI`, `P13RealidadAudio`, un `EnemySpawn` y objetos de audio; no contiene la geometría principal del mundo en la jerarquía YAML revisada.
- `P13RealidadGame.Awake()` añade audio/UI/menús y llama a `StartGameSession()`. La escena ya contiene algunos de esos componentes: evitar duplicados.
- `StartGameSession()` ejecuta `CleanupSceneObjects()`, `BuildWorld()`, `SetupPlayer()`, `SetupEnemy()` y `SetupAnomalies()`.
- `CleanupSceneObjects()` destruye cámara, mundo, enemigo y objetos por referencias y nombres. Sus bucles `while(GameObject.Find(...))` junto a destrucción diferida en Play pueden ser inseguros; reemplazar ese patrón en la escena migrada.
- `BuildWorld()` vuelve a leer `Resources/Data/World.json` y sobrescribe `Data`, por lo que editar solo el campo serializado de la escena no resuelve la autoría.
- `P13RealidadWorld.Build()` instancia diez modelos del entorno, puerta de oficina, luces y lluvia. `Model()` aplica conversión mediante `P13_AXIS_O/X/Y/Z`, y elimina colliders. Preservar esa conversión y la desactivación de habitaciones duplicadas dentro de `Piso13_CAM01`.
- `UpdateLights()` sobrescribe intensidad y posición de puerta con constantes: adaptar a valores base y extremos editables.
- Movimiento y visibilidad utilizan `WorldData`/`Obstacle`, no directamente los colliders del escenario. La navegación usa una rejilla propia y `Revision`.
- `P13RealidadRules.Room()`, `CanStand()`, selección de piso y destinos del enemigo incluyen coordenadas fijas. Hay que desacoplar las relevantes para que mover la geometría no deje la lógica atrás.
- `P13CCTVStation.SetupCollider()` sobrescribe Transform desde `DeskCoordinates`.
- `P13CoffeeCup.Awake()` crea líquido y material; persistir referencias para no duplicar el líquido convertido.
- `P13HumanMaskAnomaly.Awake()` inicializa puntos por defecto; una lista de menos de nueve puntos puede ser reemplazada. Respetar puntos configurados explícitamente.
- `P13RealidadEnemy.Awake()` construye visual y aplica spawn; reutilizar visual y referencias persistentes.
- `SpawnClockPentagramAnomaly()` tiene ruta de creación tardía y ruta de reactivación. Garantizar que el reloj persistente tenga todos sus componentes antes de usar la segunda.
- `P13RealidadAudio.Setup()` genera fuentes aunque existen fuentes guardadas: inicialización idempotente y reutilización explícita, sin rediseño de audio.

## Archivos a modificar
En `Assets/Piso13/Scripts/Realidad/`:
- `P13RealidadGame.cs`: arranque por referencias, reset de sesión, eventos, cámara CCTV, activación del reloj y eliminación de reconstrucción destructiva en modo editable.
- `P13RealidadWorld.cs`: constructor/enlace del mundo existente, geometría lógica desde componentes, bases de iluminación y movimiento de puerta; conservar navegación propia.
- `P13RealidadEnemy.cs`, `P13EnemyNavigator.cs`, `P13EnemyPerception.cs`: analizar consumidores y adaptar solo los que dependan de destinos o geometría fijos.
- `P13RealidadData.cs`: consulta de habitación/piso basada en configuración espacial para Realidad, preservando otras escenas.
- `P13CCTVStation.cs`: Transform como fuente espacial y configuración de collider sin reposicionar silenciosamente.
- `P13RealidadInteractables.cs`, `P13Anomaly.cs`, `P13HumanMaskAnomaly.cs`, `P13VintagePhone.cs`: reutilizar contenido, separar configuración de reset y revisar eventos/visibilidad.
- `P13RealidadAudio.cs`: reutilizar fuentes y evitar setup duplicado.

Crear, nombres definitivos a confirmar durante implementación:
- `P13RealidadSceneBindings.cs`: referencias serializadas y validación del contenido persistente.
- `P13RealidadSpatialAuthoring.cs`: componentes para obstáculos, límites/regiones, nodos de patrulla y vistas CCTV; dividir en archivos por componente si Unity lo requiere para adjuntarlos correctamente.
- `Assets/Piso13/Editor/P13RealidadSceneConverter.cs`: conversión explícita y no destructiva, Undo y validación.
- `Assets/Piso13/Editor/P13RealidadSceneBindingsEditor.cs`: inspector y gizmos de autoría.
- Pruebas bajo `Assets/Piso13/Tests/Editor/`, respetando assemblies y paquetes de pruebas existentes.
- Materiales persistentes adicionales en `Assets/Piso13/Materials/Realidad/` para lluvia/líquido si no existen equivalentes.
- Prefabs de utilería/configuración en `Assets/Piso13/Prefabs/Realidad/` cuando aporten reutilización; instancias de modelos FBX vinculadas, no duplicación de mallas.

## Recursos reutilizados, sin modificar originales
- `Assets/Piso13/Resources/Data/World.json`: semilla de importación y fuente de datos no espaciales; no sobrescribir ni cambiar su contrato compartido.
- Modelos bajo `Assets/Piso13/Resources/Models/`: `Piso13_CAM01`, `CAM02_Recepcion` a `CAM09_Almacen`, `Piso12_Administrativo`, `CAM01_Puerta`, `Piso13_HandFlashlight`, `MonsterGuard`, teléfono, cafetera, taza, cama, persona, máscara y reloj.
- Materiales y clips existentes, conversión de ejes y configuración de importación en `Assets/Piso13/Editor/P13Project.cs`.
- `Assets/Piso13/Scripts/P13Data.cs`: contrato compartido; preferir adaptación aislada en Realidad antes de modificarlo.

## Jerarquía objetivo
```text
Piso13Bootstrap [Game + UI + Menus + Audio + SceneBindings]
Piso13_Realidad_World
  Geometria [modelos vinculados; posiciones actuales]
  PuertaOficina [visual + puntos abierto/cerrado]
  Iluminacion [luces + parámetros base]
  Lluvia [ventanas + gotas]
  Interactuables [teléfono, taza, cafetera, cama, estación CCTV]
  Anomalias [persona, máscara, reloj]
  ConfiguracionEspacial
    Obstaculos [paredes/muebles/piso12]
    Regiones [IDs conservados y límites]
    CCTV [posición + objetivo]
    Patrulla [nodos + conexiones]
    Spawns [jugador, enemigo, máscara, secuencia final]
PlayerCamera [AudioListener + Linterna + mano]
RealidadEnemy [visual y componentes; inactivo al inicio]
```
No imponer nombres de hijos de los FBX ni mover arbitrariamente el nivel. Configuración espacial y visual deben tener parent/relación explícita: mover un contenedor de pared mueve también su proxy de colisión. Decoración no bloqueante no crea obstáculos automáticamente. El convertidor no puede inferir con fiabilidad la colisión funcional de todas las mallas.

# Implementation Steps
1. **Completar inventario y dependencias.** Confirmar escena activa, cambios sin guardar, URP, input, fuentes de audio, tests y jerarquía real. Consultar dependencias/dependientes de escena, scripts y recursos antes de editarlos, con herramientas de dependencia disponibles; contrastar con búsqueda de consumidores y `AssetDatabase.GetDependencies`. Inspeccionar también secuencia final, navegador, percepción y todos los usos de `Data`, ventanas, puerta y regiones. Guardar baseline de posiciones, estado inicial y gameplay. **Dependencias:** ninguna. La inspección en vivo anterior no respondió, por lo que queda obligatoria antes de implementación.
2. **Añadir contrato persistente y autoría espacial.** Crear bindings y componentes con referencias directas, IDs estables y gizmos. Importar obstáculos actuales como volúmenes editables; obtener `Obstacle` desde sus bounds en espacio mundial. Como la navegación usa AABB, avisar/restringir rotaciones de obstáculos que amplíen de forma significativa esos bounds; no prometer colisión exacta de formas arbitrarias. Importar regiones, límites, nodos y vistas CCTV con las coordenadas actuales. **Dependencias:** paso 1.
3. **Preparar inicialización idempotente.** Separar construir/configurar/resetear. En modo editable, usar bindings obligatorios y no reconstruir silenciosamente ante referencias ausentes: dar error de validación. Reutilizar componentes existentes; no ejecutar gameplay desde el convertidor ni añadir `ExecuteAlways` al controlador. Conservar generación antigua solo si se necesita para compatibilidad, tras modo explícito. **Archivos:** Game, World, Enemy, interactuables, máscara, teléfono, audio. **Dependencias:** pasos 1–2.
4. **Implementar conversión de editor.** Menú limitado a esta escena, guardar cambios pendientes con consentimiento y respaldar la versión inicial mediante control de versiones o copia acordada. Resolver modelos existentes y crear el contenido mediante APIs editoriales, instancias vinculadas y Undo. Reproducir transformaciones `P13_AXIS`, materiales, luces, colisiones de interacción y estados iniciales. Convertir líquido y gotas a recursos/referencias persistentes; no reutilizar directamente helpers que llaman `Destroy` como si fuesen seguros en Edit Mode. No generar mediante Play ni mediante llamada a `Awake`/`StartGameSession`. Crear reloj completamente configurado pero oculto hasta el evento. **Dependencias:** pasos 2–3.
5. **Convertir la escena y conectar referencias.** Ejecutar una vez, revisar correspondencia espacial y asignar cada binding. Auditar `EnemySpawn` existente antes de crear otro. Reutilizar o retirar únicamente fuentes obsoletas identificadas. Segunda ejecución del convertidor debe detectar la migración y no sobrescribir ediciones; cualquier reconstrucción futura será separada, explícita y confirmada. **Archivo:** escena objetivo y nuevos prefabs/materiales. **Dependencias:** paso 4.
6. **Consumir la configuración editable durante gameplay.** Generar la representación lógica a partir de obstáculos/regiones al iniciar, sin reemplazarla por JSON. Adaptar selección de habitación/piso y rutas; conservar IDs y grafo actual, pero posiciones y conexiones editables. CCTV usa Transform de punto y objetivo. Puerta usa extremos editables y bloqueo lógico coherente. Intensidades parten de bases editadas; conservar multiplicadores y presupuesto actuales. Invalidar navegación por `Revision` cuando cambie un obstáculo dinámico. No migrar a NavMesh ni a CharacterController en este alcance. **Dependencias:** pasos 2–5.
7. **Reset de sesión sin borrar autoría.** Al reintentar, resetear estados, corutinas, audio, puerta, café, eventos y visuales. Reactivar anomalías reportadas conservando Transform/configuración inicial. Suscripciones deben añadirse una sola vez y desuscribirse apropiadamente. Para efectos realmente temporales, usar una raíz runtime explícita y destrucción acotada, nunca búsquedas destructivas globales por nombre. Revisar Guardia durmiendo y anclajes de secuencia final para que respeten la autoría sin cambiar la cinematografía. **Dependencias:** pasos 3 y 6.
8. **Validación y guía de uso.** Añadir validación de referencias, IDs, destinos transitables, regiones ambiguas, recursos ausentes y objetos duplicados. Documentar cómo mover una pared con su proxy, cambiar una cámara, editar spawn, previsualizar anomalías y conservar modificaciones. Ejecutar pruebas y comprobar regresiones de otras escenas. **Dependencias:** pasos 5–7.

# Verification & Testing
## Editor y persistencia
- Abrir `Piso13 Realidad` sin Play: geometría, utilería, luces y configuración presentes y seleccionables.
- Guardar, cerrar y reabrir: conservar posiciones, materiales y referencias, sin materiales temporales perdidos.
- Mover teléfono, taza y CCTV: Play utiliza su Transform editado; `SetupCollider` no revierte posiciones.
- Cambiar spawn de jugador/enemigo: se respeta al iniciar y reiniciar.
- Mover una pared mediante su contenedor: el proxy y la lógica de colisión se actualizan coherentemente; mover solo el visual debe producir aviso o instrucción clara.
- Edición de punto CCTV y objetivo se refleja en la vista. IDs/cámaras sin duplicados.
- Convertidor con Undo, escena equivocada, modelos faltantes y segunda ejecución: no borrar contenido ni duplicar objetos; fallos deben ser diagnosticables.

## Gameplay y reinicios
- Una sola cámara principal activa, un AudioListener activo, un Game, UI, Menus y Audio; ausencia de fuentes o visuales duplicados tras iniciar/reiniciar.
- Sin desaparición del mundo al pulsar Play, sin bucles de limpieza que bloqueen el editor y sin reconstrucción de autoría.
- Café: beber, vaciar, autorrellenar y reportar; penalización de cafetera conservada.
- Teléfono: rangos, audio, congelación de movimiento y aparición del reloj al finalizar la llamada.
- Reloj oculto inicialmente; reporte habilita correctamente progresión de máscara.
- Persona visible en condiciones originales; máscara usa puntos editados incluso si son menos de nueve y conserva reglas de aparición/desaparición.
- Enemigo inactivo inicialmente, aparición tras advertencia y espera de 10 segundos antes de moverse, según implementación actual.
- Colisión y visión coherentes; enemigo no cruza paredes, puerta cerrada bloquea, apertura permite paso, navegación recalcula tras cambios dinámicos.
- Regiones conservan IDs de habitación y separación de pisos. Verificar puntos en límites y regiones superpuestas.
- Reintentar al menos tres veces después de reportar anomalías y durante una llamada: mismos objetos persistentes, estado restaurado, sin callbacks multiplicados ni audio residual.
- Pausa, captura y final de demo conservados; efectos temporales se limpian sin tocar autoría.

## Automatización y compilación
- EditMode: conversión de coordenadas/obstáculos, extracción de bindings, IDs, consultas de región, nodos inválidos e idempotencia del convertidor.
- PlayMode cuando la infraestructura existente lo permita: arranque, activación tardía y múltiples resets sin duplicados.
- Consola sin nuevos errores, Missing Scripts ni referencias faltantes.
- Compilar Windows usando el perfil/listado real del proyecto: incluir la escena objetivo y mantener todas las escenas existentes, sin reemplazar Build Settings globalmente. Revisar `DemoMenuBuild` antes de elegir comando de build.
- Prueba de humo en `Piso13.unity` y flujo hacia Realidad para garantizar que JSON, recursos compartidos e importadores siguen funcionando.

## Criterio de aceptación
El nivel y su configuración espacial se editan sin entrar en Play ni cambiar constantes. Esas modificaciones se conservan y son usadas por el juego; el runtime únicamente administra estado/comportamiento y efectos temporales. Mantener las reglas, el aspecto inicial y las otras escenas, sin cambios de paquetes ni fuentes externas.