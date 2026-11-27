# Disparos animados de PKETER

## Descarga y vista previa

[Versión Linux y GIF de las animaciones](https://github.com/PeterOlveraO/ProyectoFinal/releases/tag/v2026.11.27-linux-vfx).
Extrae el paquete completo y ejecuta `PKETER/Jugar.sh`.

## Dirección visual

Pixel art de contorno oscuro, centro luminoso y estelas discretas. La forma identifica
el arma; el color y la animación interior identifican el elemento del Pokémon.
Se revisa a 1280 × 720, cámara ortográfica de tamaño 8 e inclinación de 30°/45°.

| Modalidad | Vuelo | Impacto |
| --- | --- | --- |
| Básico | Cometa elemental con estela decreciente | Destello y onda breve |
| Ráfaga | Dardos y chevrones sucesivos | Tres golpes escalonados |
| Abanico | Cuchillas crecientes | Corte circular amplio |
| Perforante | Lanza con filamento helicoidal | Aro de compresión estrecho |
| Precisión | Aguja larga con marcas laterales | Destello diagonal |
| Búmeran | Hoja giratoria; giro inverso al regresar | Dos cortes opuestos y pulso de retorno |
| Explosivo | Núcleo con fragmentos orbitales | Expansión central y onda sobre el suelo |

Los 13 elementos tienen ocho fotogramas: energía normal, rayo, llama, agua,
hojas, cristales de hielo, espiral psíquica, espectro, roca, arena, dragón, puño y acero.
Las trayectorias, colisiones, daño y alcance siguen en `AttackProjectile`.

## Límites y recursos

- Atlas procedural compartido: 256 × 736, RGBA32, 184 fotogramas de 32 × 32,
  Point, sin mipmaps, pivote central, borde transparente mínimo de 2 píxeles.
- Un material compartido `Resources/PokemonWorldSprite`, con profundidad y niebla URP.
- Siete renderers por proyectil; tres estelas con historial circular de 16 muestras.
- Impactos: pool de 32 efectos, hasta seis fragmentos por efecto; duración máxima 0,46 s.
- La onda explosiva termina en el radio real de 2 m. Desvanecer al llegar al alcance
  no muestra una explosión que no haya causado daño.

## Comprobación local

Unity 6000.5.11f1, URP 17.5.0, Linux/Mesa Intel ADL:

- 76 pruebas PlayMode aprobadas (combate, patrones, pausa, reutilización y capturas).
- 91 combinaciones de arma/elemento con atlas y material compartidos.
- 20 proyectiles durante 240 ticks: 0 bytes administrados por tick y media de
  0,073 ms de actualización en Editor. No es una medición de GPU ni de teléfono.
- Atlas: 53 colores, alfa real; memoria de textura reportada por Unity: 1.508.312 bytes.
- Capturas de salida, vuelo, impacto, multitud y terreno real revisadas.

La prueba explícita `Game.Tests.PlayMode.ShotVisualTests.CaptureAnimationBoards`
genera imágenes en `Temp/CombatVfxReview`. Las evidencias de esta revisión están
en `Builds/Linux/QA-shot-vfx`.

## Procedencia

Arte original calculado por `Assets/Scripts/Combat/ShotAnimationAtlas.cs`, con código
asistido por IA. No se incorporaron imágenes generadas por modelos ni paquetes de terceros.
Las capturas son renderizados del juego. Véase `manifest.json`.
