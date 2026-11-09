# Resultado de validación — Piso13 Realidad

## Implementación entregada
- Escena persistente guardada y editable sin Play.
- Referencias, volúmenes lógicos, límites, regiones, nueve vistas CCTV, diez nodos de patrulla y spawns editables.
- Arranque por bindings, reutilización de modelos/objetos y reinicio sin reconstrucción destructiva.
- Inspector de bindings con ayuda, validación y actualización de lista espacial.
- Guía: `Assets/Piso13/Documentation/Realidad-Editor.md`.
- Pruebas reutilizables: menú `Piso 13 / Realidad / Pruebas de autoría`.

## Verificado en Edit Mode
**PASS: 2782 comprobaciones**, ejecutadas también después de cerrar/reabrir la escena:
- Referencias serializadas y ausencia de Missing Scripts.
- Una cámara y un AudioListener activos.
- Geometría y estados iniciales persistentes.
- Datos de paredes, muebles, piso12, cámaras y objetivos desde la autoría.
- Spawns transitables y nodos con proyección Safe transitable cercana.
- Separación de pisos y bloqueo de puerta cerrada; paso al abrir.
- Conversión de bounds y movimiento de un proxy a su representación lógica.
- Segunda ejecución del convertidor no duplica ni sobrescribe contenido.
- Construcción del Inspector de autoría.

La primera versión de la prueba exigía que cada nodo crudo fuese transitable. Patrulla_0 usa una posición original próxima a mobiliario; se corrigió la prueba para utilizar la proyección Safe del navegador existente, sin mover el nivel arbitrariamente.

## Verificado en Play Mode
**PASS: 47 comprobaciones**. El bootstrap temporal salió de Play y fue retirado.
- Arranque con mundo enlazado y componentes únicos de Game/UI/Audio/Menus.
- Enemigo y reloj ocultos inicialmente; un AudioListener y un solo disco de líquido.
- Activación de persona y reloj reutilizando objetos guardados.
- Advertencia activa el enemigo; reinicio lo desactiva.
- Consumo y autorrellenado del café mediante corutinas. La prueba redujo temporalmente refillDelay a 0.1 s dentro de Play para verificar el ciclo sin una espera prolongada; no modificó la configuración guardada.
- Llamada contestada y congelación de movimiento.
- Intercambio de cama/guardia persistentes para el final.
- Tres reinicios después de reportar reloj y durante una llamada: instancias y número de objetos conservados, estados/corutinas restaurados, sin duplicados.
- Pausa, estado de captura y reintento desde captura.

## Compilación
**Windows 64: Succeeded, 0 errores, 492 advertencias.**

Salida: `Builds/RealidadValidation/Piso13.exe`.
Reporte de build local: `Library/RealidadWindowsBuildReport.txt`.
Se utilizaron todas las escenas habilitadas del proyecto, sin reemplazar Build Settings ni alterar paquetes o Project Settings.

Las advertencias revisadas incluyen shaders compute `ConvGeneric` y variantes TRANSPOSE_OUTPUT/NON_UNIFORM_CONVGROUP_PER_OC. No se modificaron fuentes de paquetes para silenciarlas. El build exitoso no implica que esas advertencias hayan sido resueltas.

## Merge
Se resolvió el conflicto de P13RealidadWorld.cs combinando las bases de iluminación editables con los valores nocturnos del repo. Se integraron esos valores en el convertidor y en la escena existente sin regenerarla. Git comprobado sin archivos sin fusionar; únicamente el archivo conflictivo se marcó resuelto, sin commit automático ni sobrescritura de los demás cambios del usuario.

## Revisión visual y límites
Las capturas multiángulo muestran dos bloques de piso separados, sin superposición global. Los techos y la iluminación nocturna oscura ocultan gran parte de los interiores en esas capturas: no se presenta esto como comprobación detallada de visibilidad de cada objeto desde gameplay.

Pendiente de una pasada manual completa:
- Recorrido visual de todos los interiores con linterna y cámaras.
- Persecución prolongada y espera inicial de 10 segundos bajo todas las condiciones de percepción.
- Audición de clips completos, llamada normal completa y cinemática final de extremo a extremo.
- Flujo jugable completo desde Turno_Piso13/Piso13 hasta Realidad; la compilación incluyó esas escenas, pero no reemplaza esa prueba manual.
- Ejecución del EXE fuera del editor.

La escena quedó guardada y el editor fuera de Play Mode. No se modificaron modelos ni materiales originales compartidos.
