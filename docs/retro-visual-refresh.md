# Renovación visual 2.5D

## Dirección

Referencia principal: SoulSilver; claridad de FireRed y profundidad ambiental de Black 2.
Terreno 3D escalonado, sprites billboard y cámara ortográfica conservan la identidad del juego.
Interfaz petróleo (`#223940`, `#29474e`), crema (`#f5efd6`) y dorado (`#e9c46a`);
acentos verdes, azules y coral. Los motivos procedurales nuevos usan filtro Point.

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

## Validación local — 21 de noviembre de 2026

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

## Evidencia

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

## Revisión y entrega

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
