# Escala física de los Pokémon

`Assets/Resources/PokemonPhysicalSizes.json` contiene las medidas de las **190 familias
de sprites**, correspondientes a **161 especies / 164 formas**. Cubre los 178 sprites
del grupo de enemigos, los 12 retratos adicionales, el catálogo jugable y los jefes.

## Descarga Linux

[Versión portable con escala Pokédex](https://github.com/PeterOlveraO/ProyectoFinal/releases/tag/v2026.11.27-linux-pokemon-scale).
Extrae el paquete completo y ejecuta `PKETER/Jugar.sh`.

![Comparativa renderizada en Unity](https://github.com/PeterOlveraO/ProyectoFinal/releases/download/v2026.11.27-linux-pokemon-scale/comparativa.png)

## Fuente e identificación

- Fuente de datos: [PokéAPI, pokemon.csv](https://github.com/PokeAPI/pokeapi/blob/e4f981e5cb03c1fe33d436b1ee8e1197fbb0af84/data/v2/csv/pokemon.csv).
  La copia local `pokemon.csv` queda fijada a ese commit y verificada por SHA-256.
- Altura estándar de la Pokédex: decímetros / 10 → metros. Peso: hectogramos / 10 → kg.
  Son medidas de referencia de especie, no medias calculadas sobre una población.
- Comprobaciones con la Pokédex oficial:
  [Ditto: 0,3 m](https://www.pokemon.com/el/pokedex/ditto),
  [Gyarados: 6,5 m](https://www.pokemon.com/el/pokedex/gyarados),
  [Rayquaza: 7 m](https://www.pokemon.com/el/pokedex/rayquaza).
- `identities.json` identifica visualmente cada `poke_*` usando sus celdas en la hoja
  original. Su número de archivo **no es** su número de Pokédex. Los `species_*`
  usan el nombre explícito del archivo.
- Se distinguen las cuatro formas de Deoxys, Shaymin Cielo, Rotom Corte y Wormadam
  Tronco Arena. Las variantes de sexo, pose y color de la misma medida comparten datos.
- El generador exige cobertura exacta de fuentes, retratos, catálogo y animaciones;
  también rechaza identificaciones contradictorias de una misma celda.

## Convención de representación

**1 metro = 1 unidad Unity.** El jugador, los enemigos y los jefes usan la misma
conversión. No se comprimen las diferencias de tamaño ni se aplica un aumento por
rango de jefe. Ejemplos: Azurill 0,2; Ditto 0,3; Charizard 1,7; Snorlax 2,1;
Lugia 5,2; Gyarados 6,5; Rayquaza 7.

En estos billboards 2D, la altura indicada corresponde a la envolvente vertical
máxima de píxeles visibles de los seis fotogramas. Los fotogramas comparten escala,
conservan proporciones y apoyan su borde inferior en el suelo. El ancho se calcula
desde el arte, sin el antiguo límite uniforme que encogía las especies anchas.
La fuente estática usa sus propios límites alfa; los alias recortados y acolchados
alcanzan la misma medida.

Es una convención visual: la medida Pokédex de cuerpos serpentinos puede describir
longitud corporal, y la pose/envergadura del sprite no reconstruye anatomía 3D.
No se inventan medidas de anchura. El peso queda como dato de referencia.
Sombras y posición de barras de vida acompañan la escala; el tamaño del actor
raíz, colisiones, estadísticas, movimiento y datos de progreso siguen independientes.

## Regenerar y comprobar

```sh
# Sin red, usando la instantánea local:
python3 -B Tools/PokemonAnimations/sizes.py
python3 -B Tools/PokemonAnimations/sizes.py --check

# Solo si se necesita recuperar la instantánea fijada:
python3 -B Tools/PokemonAnimations/sizes.py --fetch
```

Las pruebas `PokemonVisualMetricsTests` comprueban las 190 alturas, los 1.140
fotogramas, apoyo, sombras y conservación de tamaño/barra en reinicios normal-jefe.

Verificación local en Unity 6000.5.11f1:

- 25/25 pruebas de `Game.DirectionalSprites.Tests` aprobadas.
- Cambio entre los 28 Pokémon jugables verificado en un jugador reutilizado:
  altura, apoyo a -0,5, sombra proporcional y un solo billboard.
- Captura GPU revisada en `Builds/Linux/QA-pokemon-scale/comparativa.png`.
- Generador reproducible y comprobación de GUID/referencias sin errores.
- Ejecutable Linux compilado con 0 errores y 0 advertencias; arranque de 12 segundos,
  mundo cargado y cierre limpio. Los datos de escala empaquetados coinciden con el JSON
  fuente y el ensamblado incluye el nuevo sistema.
