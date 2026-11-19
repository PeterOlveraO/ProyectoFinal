# Logotipos de los créditos (issue #22)

Los PNG se incluyen localmente en `Assets/Resources/Credits` para que los
créditos funcionen sin conexión. Se descargaron sin modificar de las fuentes
institucionales siguientes:

| Recurso | Institución | Fuente oficial |
| --- | --- | --- |
| `UAT.png` | Universidad Autónoma de Tamaulipas | https://www.uat.edu.mx/PublishingImages/Logos/UAT-Logotipo-2024-Color.png |
| `FIT.png` | Facultad de Ingeniería Tampico | https://www.uat.edu.mx/PublishingImages/LogosFacultades/fit-logotipo.png |

El portal https://www.uat.edu.mx/ identifica el primer recurso como su logotipo.
El directorio https://www.uat.edu.mx/campus, ficha 24 de la lista pública
`Campus-Descripcion`, identifica el segundo como el logotipo de la Facultad de
Ingeniería Tampico.

Los archivos `.meta` configuran sprites individuales de rectángulo completo,
sin compresión ni mipmaps, con filtro bilineal. `CreditsView` conserva su
relación de aspecto y los presenta sobre un fondo blanco, sin recortarlos ni
alterar sus colores.

## Comprobación manual

1. Abrir el menú principal (elegir el Pokémon inicial si es una partida nueva).
2. Pulsar **Créditos** y comprobar el autor **Pedro Olvera**, ambos nombres
   completos y sus logotipos.
3. Pulsar **Volver** y comprobar que reaparece el menú principal.
4. Reabrir los créditos y repetir con **Escape** o el botón atrás del mando.
5. Revisar la pantalla en horizontal, tanto en 16:9 como en un teléfono
   panorámico y una tableta 4:3: textos completos, logotipos proporcionados
   y botón de regreso accesible.

## Verificación realizada

En Unity 6000.5.11f1, modo Play:

- Entrada y regreso mediante eventos de clic sobre los botones, comprobando
  que el raycast alcanza cada botón y que cambia la pantalla activa.
- Regreso con eventos del Input System para Escape y el botón este del mando.
- Foco en **Volver** al abrir y en **Jugar** al regresar.
- Textos completos y logotipos visibles en 1920×1080, 1024×768 y 2532×1170;
  capturas revisadas en `Captures/issue22-credits-*.png`.
- Consola sin errores ni advertencias.

Se utilizó una partida temporal para las pruebas y se restauró la resolución
original del editor al terminar. Las resoluciones de teléfono y tableta se
comprobaron en Game View, no en dispositivos físicos.
