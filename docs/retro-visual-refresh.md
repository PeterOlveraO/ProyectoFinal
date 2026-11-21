# Renovación visual 2.5D

## Dirección

Referencia principal: SoulSilver; claridad de FireRed y profundidad ambiental de Black 2.
Terreno 3D escalonado, sprites billboard y cámara ortográfica conservan la identidad del juego.
La revisión final usa mapa visible, paneles oscuros compactos y los botones originales
del menú principal: marco ilustrado de color, interior petróleo y acento lateral.
Texto crema (`#f5efd6`) y foco dorado (`#e9c46a`).
Los motivos procedurales nuevos usan filtro Point.

## Implementación

- Césped con variación discreta; costados terrosos con remate verde solo en el nivel superior.
- Caminos conectados al centro, pequeñas flores y vegetación agrupada.
- Agua con crestas finas, animación UV escalonada y dos bandas dentro de la huella del lago.
- Estaciones con bases de piedra, símbolos de oferta, señales de construcción y precios por proximidad.
- Sombras de contacto sobre el mapa de alturas; una sombra reutilizada por personaje.
- Marcos pixelados, HUD compacto, contraste de estados adquiridos y contenido largo desplazable.
- Proyectiles definidos, estelas cortas e impactos reutilizados con límite de 32 efectos.
- Limpieza mediante la API de Unity de nueve subassets con scripts ausentes del perfil de volumen.

No se modificaron precios, progresión, daño, cadencia, alcance ni geometría de colisión.
Los sprites y el arte importado existentes conservan su procedencia en `credits-assets.md`.
Los nuevos motivos de piedra, señales, sombra, marcos y VFX se generan por código.

## Validación de la primera entrega — 21 de noviembre de 2026

Entorno: Unity **6000.5.11f1**, URP **17.5.0**, Cinemachine **3.1.7**, escena `World`.

| Comprobación | Resultado |
| --- | --- |
| EditMode | **143/143**, 0 fallos, 0 omitidos |
| PlayMode | **60/60**, 0 fallos, 0 omitidos |
| Validadores Python de CI | **28/28** |
| Checker de GUID, metas y scripts ausentes | **0 errores**, 3 advertencias preexistentes de nombre de clase/archivo |
| Consola tras recompilar y probar | **0 errores** |
| Whitespace del código y tests cambiados | Correcto |
| Compra de fuerza en partida temporal | 2000 → 1800 puntos; estado «ADQUIRIDA» |
| Proyectil real contra enemigo creado por la factoría | Vida 10 → 5; captura de proyectil y de impacto |
| Colección y gacha en 1024×768 | Texto y acciones legibles; listas desplazables |
| Menú, HUD, lago, acantilados y pausa en 1920×1080 | Revisión visual completada |

Jobs locales de Unity: EditMode `3ef3ba07c06e4155b3db883048f0b16a`,
PlayMode `ff91f70421e24fed9983184bd3451373`.
Después se corrigió únicamente el salto de línea de símbolos de las estaciones;
se volvió a compilar y revisar su captura.

Las nuevas pruebas cubren el límite/reutilización del pool, referencias destruidas,
pausa/derrota y ausencia de sombras duplicadas al reinicializar enemigos.
El proyecto actual usa una escena; el pool de impactos no se validó con descarga de escenas aditivas.
Las capturas de combate son frames pausados, no una medición de fluidez ni de rendimiento en iPhone.

## Evidencia de la primera entrega

Capturas de Game View, con UI, en [`evidence/visual-refresh/`](evidence/visual-refresh/).
Las imágenes de 1920×1080 se redujeron a 1200 px de ancho para este documento.
La colección y el gacha conservan sus 1024×768.

| Antes | Después |
| --- | --- |
| ![Centro anterior](evidence/visual-refresh/before-world.png) | ![Centro renovado](evidence/visual-refresh/after-main.png) |

- [Estación adquirida](evidence/visual-refresh/after-shop.png)
- [Agua y orilla](evidence/visual-refresh/after-lake.png)
- [Acantilado y flora](evidence/visual-refresh/after-grove.png)
- [Proyectil](evidence/visual-refresh/after-projectile.png) e [impacto](evidence/visual-refresh/after-impact.png)
- [Colección 4:3](evidence/visual-refresh/after-collection-4x3.png) y [gacha 4:3](evidence/visual-refresh/after-gacha-4x3.png)
- [Pausa](evidence/visual-refresh/after-pause.png)

## Revisión de la primera entrega

Participaron GPT-6.1 Terra (dirección artística, VFX y revisión), GPT-6.1 Sol (UI)
y GPT-6 Luna (entorno), mediante delegación nativa de OpenCode.
Se emplearon las skills de arte, UI uGUI, C#, VFX y build de Unity.
La revisión final de capturas no identificó defectos visuales bloqueadores:
paleta 4/5, unidad 4/5, profundidad 3/5, silueta 4/5, UI 4/5 y feedback 3/5.

Los 1087 archivos con cambios locales previos coinciden byte a byte con el respaldo inicial.
La progresión real y las preferencias de juego se conservaron; el QA utilizó un guardado temporal.
Game View volvió a Full HD y se retiró la resolución de QA.

El workflow existente `iOS Build` ejecuta tests, exporta Xcode, compila y valida un
**IPA sin firmar**. Su resultado y artefacto se consultan en GitHub Actions para la rama
`feat/retro-visual-polish`. La instalación requiere firma; no se realizó validación física en iPhone.

## Revisión final: interfaz, audio y elementos

La corrección solicitada toma el menú principal como referencia para todas las pantallas:

- Mapa visible y paneles compactos; todas las acciones reutilizan el diseño original de
  botones del menú principal, con sus marcos verdes, azules, ámbar o rojos e interior petróleo.
- Nombre del juego solo en principal y créditos.
- Iniciales y su resultado: retrato, nombre y acciones. Sin clases ni estadísticas redundantes.
- Clases solo en inventario y gacha, incluido el resultado de una tirada.
- Insignias F/A/S y cuatro barras IV sobre una escala común de 31.
  Las clases producen respectivamente **0, 15 y 31 IV** en cada stat.
  Probabilidades leídas de configuración: básica **75/20/5%**, especial **40/45/15%**.
- Ajustes con General/Música/Efectos y calidad; créditos, pausa, EV, liberación y derrota
  reducidos a información útil y acciones.
- Pueblo Lavanda como tema predominante, gritos originales de las 18 especies,
  tema de jefe, 24 efectos y mezcla separada:
  [composición y validación de audio](audio-composition.md).
- Proyectiles de 32 px con dos fotogramas, contorno, paletas internas, orientación,
  destello de salida e impactos reutilizables. No cambian daño, alcance, velocidad,
  cadencia, radio de colisión ni patrones de equipo.
- Carteles con pictogramas de 24 px: **1 · MÁQUINA/ARSENAL** muestra construcción y su
  precio; **2 · PERK/DISPARO** muestra el producto y su precio independiente.
  Los perks adquiridos conservan su icono y añaden una marca verde.
  Pesa, corazón, bota, escudo y munición identifican los cinco perks.
  Los arsenales representan la trayectoria de cada patrón en lugar de letras o flechas TMP.

| Identidad visual | Pokémon |
| --- | --- |
| Eléctrico | Jolteon |
| Fuego | Charizard, Flareon |
| Agua | Blastoise, Vaporeon, Gyarados |
| Planta | Venusaur |
| Hielo | Lapras |
| Psíquico | Mew |
| Fantasma | Gengar |
| Roca | Aerodactyl |
| Dragón | Dragonite, Garchomp |
| Lucha | Lucario |
| Normal | Snorlax, Ditto, Kangaskhan, Tauros |

### Validación final local

- **143/143 EditMode y 83/83 PlayMode**, sin fallos ni omitidos.
- Tras abreviar las últimas etiquetas: **10/10** pruebas de menús/navegación.
  Al recuperar los marcos originales en todos los botones se repitió PlayMode: **82/82**.
- Validadores Python de CI: **28/28**.
- Una única instancia de EventSystem y AudioListener; 15 fuentes de audio en runtime.
- Compilación sin errores de C#.
- Capturas de todas las pantallas en 1920×1080 y 1024×768, usando guardados temporales.
- La galería incluye HUD, bebidas, disparos, pausa, EV, derrota y ambas adquisiciones.
- [Galería final](elemental-gallery.md). Las capturas anteriores documentan únicamente
  la primera entrega; no representan la UI definitiva.

Jobs locales: EditMode `96bfa5b7714c464bbdcde125a498c177`,
PlayMode `b037d93c987c4785b5590d8b8dc43c78`,
menús `6766beee6ecc427db060a2807dfaa9a1`.
PlayMode con los botones originales compartidos: `aa54c1ce3fb5491e8a5922e0bb402b30`.
PlayMode final, con continuidad de Pueblo Lavanda y los 18 gritos:
`e8c75abf3e624c3ca519e373f7822b97`.
Después de los carteles: **83/83 PlayMode** (`3c5144317bcc4f84a552ee860fd8ef7f`)
y **47/47 EditMode** de economía, colocación y disparos (`58935dac77ae4d20815c0324a6f11a0f`).
QA de las nueve tiendas: **46 capturas**, cero desbordamientos TMP, compra bloqueada
antes de construir, cobros separados y rechazo de compras repetidas de un perk.
Al terminar se verificaron el JSON de progresión y todas las claves `pketer.*` contra
el respaldo: sin diferencias. Los 1087 archivos de trabajo previo conservaron sus hashes.

Los iconos de `StationIconArt` son dibujos originales definidos en máscaras de 16×16
sobre insignias RGBA de 24×24, a 32 PPU, sin compresión ni mipmaps, filtro Point.
Se comparten mediante caché y usan contorno petróleo, brillos crema y acentos de color.
No incorporan fuentes ni imágenes externas.

La entrega final se publica en `main` y su workflow `ios-build.yml` se ejecuta
explícitamente con `workflow_dispatch`, ya que el push a `main` no lo activa.
