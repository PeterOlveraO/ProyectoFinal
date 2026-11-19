using Game.Pokemon;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Crea los enemigos como sprites 2D: ya no hay prisma triangular ni malla 3D.
    ///
    /// Cada enemigo recibe su propio Pokémon (sprite billboard) extraído de
    /// <c>Pokemones.png</c> y conserva ese sprite durante toda la partida: el personaje
    /// es fijo y solo cambia de vista (espejado) según la dirección en la que camina.
    /// </summary>
    public static class EnemyTriangleFactory
    {
        public static GameObject Create(int zone)
        {
            return Create(zone, PokemonSpriteLibrary.NextForEnemy());
        }

        public static GameObject Create(int zone, Sprite sprite)
        {
            var go = new GameObject("Enemy_Z" + zone);

            go.AddComponent<EnemyTriangle>();
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // El sprite es el único visual del enemigo.
            PokemonEnemyVisual.Attach(go, sprite);

            return go;
        }
    }
}
