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
        // Darkrai shares the shadow-shaped Ghost presentation; this is not type effectiveness.
        public static PokemonElement Resolve(string speciesId)
        {
            switch (speciesId == null ? "" : speciesId.Trim().ToLowerInvariant())
            {
                case "jolteon": return PokemonElement.Electric;
                case "charizard": case "flareon": case "ho-oh": return PokemonElement.Fire;
                case "vaporeon": case "blastoise": case "gyarados": return PokemonElement.Water;
                case "venusaur": return PokemonElement.Grass;
                case "lapras": return PokemonElement.Ice;
                case "mew": case "mewtwo": case "lugia": case "celebi": return PokemonElement.Psychic;
                case "gengar": case "darkrai": return PokemonElement.Ghost;
                case "aerodactyl": case "tyranitar": return PokemonElement.Rock;
                case "garchomp": case "dragonite": case "salamence": case "rayquaza": return PokemonElement.Dragon;
                case "metagross": case "jirachi": return PokemonElement.Steel;
                case "lucario": return PokemonElement.Fighting;
                case "snorlax": case "ditto": case "kangaskhan": case "tauros": return PokemonElement.Normal;
                default: return PokemonElement.Normal;
            }
        }
    }
}
