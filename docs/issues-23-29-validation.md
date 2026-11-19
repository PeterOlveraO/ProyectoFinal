# Validación de las issues 23–29

## Alcance y ramas

Base: `8e8c03d365283f0c227c14e57ca36796e4094772` de `main`.

| Issues | Rama | Responsabilidad |
| --- | --- | --- |
| #23, #27, #28 | `feat/issues-23-27-28-tycoon-economy` | Disparo único, construcción de vendedores y economía |
| #24 | `feat/issue-24-flora` | Flora, colisiones y rutas |
| #25, #29 | `feat/issues-25-29-boss-difficulty` | Jefes y dificultad por ronda |
| #26 | `feat/issue-26-compact-menus` | Menús compactos con las imágenes proporcionadas |

Los cuatro subagentes de implementación usan GPT-6.1 Sol. La integración y
revisión se realizan en `integration/issues-23-29`.

## Comprobaciones requeridas

- Pruebas EditMode y PlayMode completas, incluidas las nuevas regresiones.
- Compras atómicas, bloqueo previo a construcción y reinicio por partida.
- Un jefe por ronda especial, sin refuerzos, con avance normal al derrotarlo.
- Progresión de vida, daño y velocidad en rondas iniciales y avanzadas.
- Flora sin edificios decorativos y rutas de acceso a vendedores.
- Menús que conservan el fondo visible, controles táctiles y pausa.
- Workflow de integración aprobado antes de actualizar `main`.
- Nueva ejecución del workflow sobre el commit final de `main`.
- Descarga del IPA de esa ejecución y validación local de ZIP/CRC,
  `Payload/*.app`, identificador de aplicación y ejecutable Mach-O arm64.

## Estado de la validación

En curso. Esta sección se completa con resultados observados, enlaces a las
ejecuciones y el hash del artefacto final; no representa todavía una aprobación.

El intento inicial de abrir un Editor aislado mediante Unity CLI no pudo
continuar por falta de una licencia local válida. Los workflows de GitHub
utilizan la configuración de licencia ya existente del repositorio.
