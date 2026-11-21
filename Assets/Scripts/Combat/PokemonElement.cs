namespace Game.Combat
{
    // Presentation and audio only; never used for damage or type effectiveness.
    public enum PokemonElement
    {
        Normal, Electric, Fire, Water, Grass, Ice, Psychic, Ghost, Rock, Ground, Dragon, Fighting, Steel
    }

    public static class PokemonElementResolver
    {
        // Dual-type species use their first canonical type, except Lapras deliberately
        // uses Ice for a distinct visual identity (its other type is Water).
        public static PokemonElement Resolve(string speciesId)
        {
            switch (speciesId == null ? "" : speciesId.Trim().ToLowerInvariant())
            {
                case "jolteon": return PokemonElement.Electric;
                case "charizard": case "flareon": return PokemonElement.Fire;
                case "vaporeon": case "blastoise": case "gyarados": return PokemonElement.Water;
                case "venusaur": return PokemonElement.Grass;
                case "lapras": return PokemonElement.Ice;
                case "mew": return PokemonElement.Psychic;
                case "gengar": return PokemonElement.Ghost;
                case "aerodactyl": return PokemonElement.Rock;
                case "garchomp": case "dragonite": return PokemonElement.Dragon;
                case "lucario": return PokemonElement.Fighting;
                case "snorlax": case "ditto": case "kangaskhan": case "tauros": return PokemonElement.Normal;
                default: return PokemonElement.Normal;
            }
        }
    }
}
