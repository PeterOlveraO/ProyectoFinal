namespace Game.Core
{
    /// <summary>
    /// Estado global de la partida. Mientras <see cref="Blocked"/> está activo el jugador
    /// no se mueve ni dispara: lo usan el menú principal, la pausa y la configuración.
    ///
    /// Es una bandera estática sencilla para que los sistemas de juego no dependan de la UI.
    /// </summary>
    public static class GameFlow
    {
        /// <summary>True cuando hay un menú abierto y la partida debe ignorar la entrada.</summary>
        public static bool Blocked { get; set; }

        /// <summary>True cuando el jugador ha perdido y se muestra la pantalla de derrota.</summary>
        public static bool Over { get; set; }

        /// <summary>True cuando el jugador puede moverse y atacar.</summary>
        public static bool Playing => !Blocked && !Over;
    }
}
