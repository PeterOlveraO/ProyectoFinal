# Issue #26 — menús compactos

## Diseño

- Principal: placa pequeña superior con PKETER y compañero equipado; seis acciones en
  bandeja 3×2 inferior, sin panel ni imagen de fondo que cubra el mapa.
- Pausa: panel lateral derecho con continuar, EV, ajustes y abandonar.
- EV: panel lateral derecho, cuatro mejoras 2×2; nivel, coste, saldo y vuelta al origen.
  `Show`, `OpenSkills` y `CloseSkills` conservan el flujo de pausa existente:
  EV → partida reanuda; pausa → EV → pausa no reanuda.
- Se usa el `SafeAreaLayout` y el CanvasScaler 1920×1080 existentes. Tamaños mínimos
  de 48 píxeles de pantalla para tocar, texto de 14 píxeles, separación de 6 píxeles;
  no se reduce el panel entero para hacerlo caber. Landscape es la orientación del proyecto.
- El escudo transparente solo intercepta entrada; no oscurece ni oculta el mundo.
  Los paneles laterales llevan respaldo oscuro para contraste sobre cualquier terreno.
- La variante compacta es opt-in en `MenuPage`: colección, inicial, gacha, resultados,
  confirmaciones, ajustes y créditos institucionales mantienen su shell y callbacks.
  No se modifican `RunHud` ni `RoundManager`.

## Recursos y reproducción

`Assets/Imagenes/botones.jpg` y `referenciaui.jpg` son las fuentes proporcionadas.
La segunda solo inspira la composición mapa + controles; no se importa su logo,
tipografía, cielo ni branding. Los cuatro sprites RGBA de 178×55 extraen los marcos
verde, azul, ámbar y rojo de la primera. Se limpian letras/papel y se conserva el
marco biselado. TMP añade los textos del juego (no se generan imágenes).

Para repetir la extracción, desde la raíz:

```sh
uv run --with pillow python Tools/UI/extract_menu_buttons.py
```

Los PNG en `Resources/UI/CompactMenus` usan Sprite Single, Point, sin mipmaps,
sin compresión y borde nine-slice. Los JPG de referencia no se cargan en runtime.

## Validación

Assembly PlayMode: `Game.CompactMenus.Tests` (10 casos).

- Layout uGUI real en seis tamaños (568×320 hasta 2400×1080, además de 4:3),
  safe areas simuladas con notch, esquinas dentro de límites, área visible >50%,
  botones ≥48 px, texto ≥14 px sin overflow, sprites sliced y escudo transparente.
- Shell estándar/callbacks sin cambio; controles de acceso a pausa y EV táctiles.
- Navegación real del controlador y RoundManager con progresión real en archivo temporal:
  EV desde partida y desde pausa, invariantes de Time.timeScale/AudioListener/GameFlow,
  acceso a colección, gacha y créditos UAT/FIT/Pedro Olvera.
  La prueba no toca el guardado del jugador ni necesita cargar World.

En esta rama se comprobó la compilación standalone de Game.Runtime y de los tests
mediante Roslyn de Unity 6000.5.11f1 y las referencias existentes del proyecto, sin
abrir Unity ni mutar el Editor compartido. También `git diff --check`.
La compilación de tests emite CS1701 por las referencias netstandard 2.0/2.1 del
response file existente; no hay errores de compilación. Los cuatro PNG también se
regeneraron byte a byte y se comprobaron sus dimensiones, alfa y ajustes de importación.
**Los tests no se han ejecutado**: el coordinador debe correr este assembly en una
escena de test vacía y validar visualmente el mapa detrás de los tres menús al integrar.
Revisar también móvil físico (densidad y notch) y navegación teclado/gamepad.
