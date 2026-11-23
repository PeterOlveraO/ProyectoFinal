using System;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// One immutable Resources material shared by world Pokémon. Unity owns its lifetime;
    /// billboards only borrow it. The asset also keeps its shader referenced in players.
    /// UI, contact shadows and territory fog keep their own materials.
    /// </summary>
    internal static class PokemonSpriteMaterial
    {
        static Material _shared;

        internal static Material Shared
        {
            get
            {
                if (_shared == null) _shared = Resources.Load<Material>("PokemonWorldSprite");
                if (_shared == null)
                    throw new InvalidOperationException(
                        "Missing Resources/PokemonWorldSprite material (Assets/Resources/PokemonWorldSprite.mat).");
                return _shared;
            }
        }
    }
}
