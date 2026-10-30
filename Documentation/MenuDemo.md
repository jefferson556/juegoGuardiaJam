# Menú de la demo Piso 13

El menú de producción usa la portada existente, fondo oscuro, texto blanco, botones rojos y un rojo más brillante al pasar el ratón o seleccionar con teclado/mando. La referencia es `VIDEO-2026-10-16-18-59-12.mov`; las pestañas y ajustes también se contrastaron con el código del menú original de Piso 13.

## Comportamiento

- **Iniciar demo** comienza la escena `Turno_Piso13`. Si ya existe una partida en esta sesión, pide confirmación antes de reiniciarla.
- **Continuar demo** aparece después de iniciar. Volver al menú conserva la escena, la posición y el progreso en memoria; continuar reanuda esa misma sesión, también desde `Piso13 Realidad`.
- **Opciones** está disponible en el título y en la pausa. Los ajustes persisten al cerrar el juego.
- **Salir** llama a `Application.Quit()` en la aplicación compilada. En el editor termina Play Mode.

**Alcance de Continuar:** conserva la partida mientras la aplicación sigue abierta. No se añadió un sistema de checkpoints en disco; cerrar la aplicación termina esa sesión. Los ajustes de opciones sí se guardan en disco.

## Opciones

- **Controles:** teclado/mando, símbolos automáticos/PS5/Xbox, sensibilidad, reasignación de acciones y restauración. Las acciones disponibles corresponden a los controles de las escenas Unity existentes. Escape cancela la reasignación; el intercambio evita duplicar las asignaciones principales.
- **Video:** brillo, prioridad Rendimiento/Equilibrado/Calidad, ventana 1152 × 720 o 1440 × 900 y pantalla completa. El tamaño de ventana se aplica en el juego compilado. Brillo usa exposición de URP; prioridad cambia escala de render y sombras sobre una copia temporal del perfil, sin modificar el recurso original.
- **Sonido:** volumen general, música, efectos, ambiente, voces/diálogos, prueba de efectos/ambiente y restauración. `DemoAudio.mixer` separa los canales respetando los fundidos de los sonidos existentes.

Las escenas de producción y su orden en Build Settings se conservan. La herramienta antigua de preparación de Piso13 continúa disponible manualmente; abrir el editor ya no ejecuta automáticamente esa preparación del prototipo.

## Abrir y verificar

1. Abrir la carpeta con Unity **6000.5.5f1**, la versión registrada en `ProjectSettings/ProjectVersion.txt`.
2. Esperar la importación de paquetes y modelos. Los archivos grandes de Git LFS están incluidos materializados en el ZIP.
3. Seleccionar **Turno 03:13 → Abrir demo** y pulsar Play.
4. Para las comprobaciones automáticas, detener Play y ejecutar **Turno 03:13 → Pruebas → Verificar menu demo**.
5. Consultar `Documentation/Validation/DemoMenuSmoke.txt`. Solo un informe **PASS** confirma que terminó la prueba. Los informes de compilación C# independiente se encuentran en `Documentation/Validation/Menu/`; no equivalen a una prueba de juego completa.

## Resultado de la validación — 30 de octubre de 2026

Unity **6000.5.5f1** completó el recorrido automático con **PASS: 40 comprobaciones, etapa 12**. Se comprobaron el menú inicial, la aparición de Continuar, inicio y reinicio, pausa, conservación de posición, opciones, volumen por canales, guardado de ajustes y reasignación/restauración de controles, tanto en Turno_Piso13 como en Piso13 Realidad. Las capturas están en `Documentation/Validation/`.

Durante la prueba apareció un error del servicio de cuenta de Unity (`UnityConnectWebRequestException`, intercambio de token). Se registra en el informe separado de los fallos del juego; no se declara reparado. Cualquier otro error o excepción sigue haciendo fallar la prueba.

La compilación independiente de los seis ensamblados también terminó sin errores; sus advertencias existentes se conservan en los informes.

Validación manual complementaria en la plataforma de destino: verificar el hover con ratón, navegar los tres paneles, reasignar un control, iniciar, pausar, volver al menú, continuar sin perder posición, reiniciar con confirmación y cerrar una aplicación compilada con Salir. Probar también resolución/pantalla completa y un mando físico en la plataforma de destino.

El ZIP es un proyecto fuente de Unity; no es una aplicación compilada. No contiene `Library`, `Temp`, `.git` ni archivos de autenticación. La copia local anterior y su ZIP se conservan por separado.
