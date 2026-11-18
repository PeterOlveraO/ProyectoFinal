# Validación de las issues 7–13

## Entorno

- Unity `6000.5.11f1`, Linux Editor, escena `Assets/Scenes/World.unity`.
- Integración realizada en `integration/issues-7-13`; pruebas y capturas mediante MCP Unity.
- Las sesiones de QA usan `ProgressionService` con `MemoryStore`. Las compras de prueba no escriben el progreso del usuario.

## Regresiones ejecutadas

| Integración | EditMode | PlayMode |
| --- | ---: | ---: |
| Estado original | 35/35 | — |
| Apuntado, catálogo, decoración e infraestructura CI | 55/55 | 8/8 |
| Lago y colocación de tiendas | 67/67 | 10/10 |
| Compras reales de bebidas y patrones de disparo | 79/79 | 27/27 |
| Integración completa, retratos y animaciones | **89/89** | **27/27** |

Además, los 28 tests Python de validación de resultados e IPA pasan. El
verificador de extracción comprueba 178 coincidencias con la hoja fuente y
1.080 frames generados, incluidos los dos grupos correctos de Jolteon y Lucario.
La consola del Editor quedó sin errores ni advertencias durante la revisión final.

## Recorridos físicos del mapa

Se generaron cinco puestos de bebidas y tres de disparo sobre el mapa real,
con el lago de radio 11 y 98 decoraciones. Para cada puesto se reconstruyó un
camino desde el spawn y se recorrió con el `CharacterController` del jugador,
mediante pasos horizontales de 0,1 y gravedad. Solo se reposicionó al jugador
al inicio de cada recorrido; no se teletransportó entre nodos.

| Puesto | Suelo X/Y/Z | Pasos de cuadrícula | Resultado |
| --- | --- | ---: | --- |
| Fuerza | 89,5 / 0 / 116,5 | 45 | Llegó |
| Vitalidad | 64,5 / 0 / 42,5 | 54 | Llegó |
| Sprint | 132,5 / 3 / 60,5 | 72 | Llegó |
| Escudo | 28,5 / 3,5 / 97,5 | 69 | Llegó |
| Bebida ráfaga | 158,5 / 0,5 / 155,5 | 153 | Llegó |
| Disparo ráfaga | 91,5 / 0,5 / 74,5 | 17 | Llegó |
| Abanico | 130,5 / 3,5 / 104,5 | 74 | Llegó |
| Perforante | 1,5 / 0 / 2,5 | 157 | Llegó |

El jugador se detuvo a aproximadamente 0,52 unidades del borde físico al
intentar atravesar muestras de extras, árboles y props. Ninguna raíz física
de decoración estaba rotada y ningún centro estaba sobre el agua.

Tras ampliar la búsqueda de edificios, se generaron **91 decoraciones, incluidas
13 de `Buildings`**. Se repitieron los ocho recorridos físicos: todos llegaron,
con las mismas posiciones y longitudes de la tabla. Se comprobó también el
bloqueo físico de `building_18`.

## Compra mediante el HUD

Se invocó el evento del botón `BuyShot` visible, después `EquipBasic` y otra vez
`BuyShot`: saldo 500 → 440 → 440, ráfaga adquirida y equipada. Las 1.000 monedas
del almacenamiento temporal permanecieron intactas.

## Hallazgos de QA

- La primera auditoría de identidad confundió `poke_114` con Jolteon y
  `poke_092` con Lucario. La captura real y la búsqueda por píxeles revelaron
  que son Meowth y Swampert. Los grupos auténticos de la hoja comienzan en
  `(512,1024)` y `(512,4256)`, respectivamente.
- El selector superior cortaba «PERFORANTE». Se ajustó el texto TMP conservando
  los tamaños de los botones; la captura final muestra completos nombre y precio.
- El radio inicial de colocación no encontraba edificios principales en el
  terreno nuevo. Se amplió la búsqueda a 70 bloques conservando las comprobaciones
  de accesibilidad y suelo; los ocho recorridos finales volvieron a pasar.

## Identidad y orientación finales

Se comprobaron en partida `jolteon → species_jolteon`,
`lucario → species_lucario` y `aerodactyl → poke_140`. El pool enemigo conserva
178 sprites. Al mover el CharacterController en los cuatro ejes relativos a la
cámara, Jolteon pasó a `front_0`, `back_0` o `side_0` con el reflejo apropiado;
al detenerse conservó la vista.

Al iniciar la revisión final, el menú del usuario seguía mostrando **29 monedas
y la selección inicial pendiente**, confirmando que las sesiones temporales de
QA no eligieron un inicial en su guardado.

## Evidencia publicada

- [Selección inicial](evidence/qa-final-starter-identities.png)
- [HUD con etiquetas completas](evidence/qa-final-hud-jolteon.png)
- [Cuatro orientaciones de Jolteon en juego](evidence/qa-jolteon-directions.png)
- [Edificios y decoración](evidence/qa-final-buildings.png)
- [Lago grande](evidence/qa-integration-lake-overview.png)

## Capturas locales adicionales

En `Captures/Issues7-13/`:

- `issues-baseline-menu.png`
- `integration-grass-jolteon.png` — evidencia de la identificación incorrecta.
- `integration-lake-overview.png`
- `integration-shot-offer.png` — evidencia del texto truncado.

## IPA

El workflow bloquea la exportación si faltan suites requeridas o falla alguna
prueba. El paquete final se valida como ZIP con `Payload/*.app`, bundle
`com.peterolvera.proyectofinal` y ejecutable arm64.

### Resultado final en `main`

- [PR #21](https://github.com/PeterOlveraO/ProyectoFinal/pull/21) fusionado;
  issues #7–#13 cerradas.
- Commit compilado: `6fbd3d0f8350215b5fa458018864df13d0560d8b`.
- [Pipeline final aprobado](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44235657392):
  **89/89 EditMode + 27/27 PlayMode**, exportación Unity y compilación Xcode.
  Los XML descargados pasaron también el validador local de suites completas.
- [Artefacto `ProyectoFinal-ipa`](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44235657392/artifacts/14397864525),
  ID `14397864525`.
- Archivo `ProyectoFinal-unsigned.ipa`: **43.824.537 bytes**.
- SHA-256 del IPA:
  `cc29c53a012cc611c5f5f2a05c7c6865783b3b9d1ea91aac20a525d79a91a8f4`.
- Validación local del archivo descargado: ZIP/CRC, aplicación única, bundle y
  ejecutable Mach-O arm64 correctos.

La [CI de integración](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44233138604)
también aprobó las 116 pruebas y produjo un IPA validado localmente, sobre
`3cb98a8628c608c48edfa24e4bcbad085c2acd6c`.

### Pipelines individuales

Las siete ramas completaron tests, exportación y compilación. Cada IPA se
descargó y pasó `ci/validate_ipa.py`; los hashes están registrados en su PR.

| Issue | Pipeline aprobado | PR con evidencia |
| --- | --- | --- |
| #7 | [44230235243](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44230235243) | [#15](https://github.com/PeterOlveraO/ProyectoFinal/pull/15) |
| #8 | [44232284380](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44232284380) | [#20](https://github.com/PeterOlveraO/ProyectoFinal/pull/20) |
| #9 | [44230942725](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44230942725) | [#18](https://github.com/PeterOlveraO/ProyectoFinal/pull/18) |
| #10 | [44231931315](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44231931315) | [#19](https://github.com/PeterOlveraO/ProyectoFinal/pull/19) |
| #11 | [44232397397](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44232397397) | [#14](https://github.com/PeterOlveraO/ProyectoFinal/pull/14) |
| #12 | [44231931734](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44231931734) | [#16](https://github.com/PeterOlveraO/ProyectoFinal/pull/16) |
| #13 | [44230651402](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44230651402) | [#17](https://github.com/PeterOlveraO/ProyectoFinal/pull/17) |

Los IPA están **sin firmar**. La instalación y prueba física en iPhone requieren
firmarlos y aún no se han realizado.
