# Auditoría visual de decoración — issue 12

Revisión de los 112 PNG reales en `Assets/Resources/Decor` mediante cuatro hojas de contacto, con fondo gris, miniaturas y nombres. Las imágenes son sprites originales, no prefabs. La clasificación indica lo que contiene cada recorte, no garantiza que su aspecto en cámara esté validado. Las hojas temporales utilizadas están en `/tmp/opencode/{Buildings,Extras,Trees,Props}-issue12.png`.

## Buildings (20)

| IDs | Identificación y decisión |
| --- | --- |
| 00–01 | Torres urbanas; edificio. |
| 02 | Casa roja con piezas sueltas a la derecha e inferior; excluida por recorte compuesto. |
| 03–04 | Bloque gris y centro Pokémon; edificio. |
| 05–07 | Monumento rocoso, nave y edificio blanco/amarillo; edificio. |
| 08 | Edificio naranja con pieza suelta inferior; excluido. |
| 09–10 | Bloque gris y gran fachada; edificio. |
| 11 | Fachada roja con piezas laterales y superiores; excluida. |
| 12–19 | Casa roja, torre azul, edificios urbanos y estaciones/tiendas; edificio. |

## Extras (16)

| IDs | Identificación y decisión |
| --- | --- |
| 00 | Plantilla de setos/piezas lineales, no objeto completo; excluida. |
| 01 | Faro; edificio. |
| 02–03 | Centros Pokémon blanco y azul; edificio. |
| 04–05 | Barcos; perfil de orilla, varados sobre tierra y a distancia del agua. |
| 06–11 | Centros, casas y nave; edificio. |
| 12–14 | Estatuas sobre pedestal; objeto sólido. |
| 15 | Tren aislado sin vía/contexto; excluido. |

## Trees (44)

00–15: árboles redondos y coníferas de distintos verdes. 16–22: árboles de copa abierta, verdes y otoñales. 23: recorte vacío, excluido. 24–31: coníferas. 32–35: árboles de varias copas. 36–39: árboles redondos. 40–43: arbustos/coníferas compactas. Todos salvo 23 usan perfil de árbol con colisión estrecha en tronco.

## Props (32)

| IDs | Identificación y decisión |
| --- | --- |
| 01, 05, 07–08, 14, 16–17 | Árboles y palmera; perfil árbol. |
| 02–04 | Árboles con tocones o troncos separados; excluidos como recortes compuestos. |
| 09–12 | Árboles con bayas/piezas sueltas o varios árboles; excluidos como compuestos. |
| 13 | Puestos de mercado múltiples; excluido como compuesto. |
| 18 | Fardos y herramientas recortados juntos; excluido. |
| 20 | Roca; objeto sólido. |
| 22 | Múltiples segmentos de valla; excluido. |
| 23 | Camión y cajas/piezas separadas; excluido. |
| 25 | Entrada azul de metro; edificio. |
| 26 | Piezas de suelo; excluidas, no son objetos completos. |
| 27, 30, 33, 35, 37, 40, 43 | Edificios/tiendas; perfil edificio. |
| 31 | Estatua; objeto sólido. |
| 38 | Plantilla fragmentada de suelo; excluida. |
| 44 | Fragmentos de suelo/arcada; excluidos, no son objeto completo. |
| 45 | Cartel con poste recortado; excluido por fragmentación/contexto incierto. |

## Criterios y límites

Catálogo por nombres explícitos: no aparecen PNG excluidos al añadir archivos a Resources. Los perfiles fijan tamaño visible máximo, semihuella y altura del `BoxCollider` 3D estático; el árbol usa tronco, los edificios y barcos una base compacta. El billboard es hijo visual y no rota la raíz física. Se rechazan posiciones sin terreno llano dentro de la huella y margen, junto al lago, cerca del spawn/tiendas o en corredores cardinales de rampas; existe separación adicional entre objetos. Se permiten menos instancias que los máximos configurados.

El contrato de agua usa únicamente `WorldTerrain.IsLakeAtWorld`, no nombres de objetos ni colliders de lagos. El barco es terrestre y necesita agua a unos 2–7 m más allá de su base; si la geometría del nuevo lago no ofrece un sitio seguro, no aparece. Los corredores cardinales y el anillo de tiendas son reservas conservadoras, no una demostración formal de conectividad global. Issue 13 debe confirmar que `IsLakeAtWorld` representa toda la superficie peligrosa y que `BlockTopCenter`, `Map`, `BlockSize`, `Settings` y `SpawnPosition` mantienen sus unidades/semántica; se requiere revisión visual y prueba de recorridos al integrar.
