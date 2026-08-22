using ProceduralCreature.Definition;

namespace ProceduralCreature.Serialization
{
    /// <summary>
    /// Serializes/deserializes canonical CreatureDefinition JSON with schema/version
    /// handling and deterministic formatting (implementation guide §11). Depends only
    /// on the Definition model, never on generated state (§1.2 dependency rules).
    /// </summary>
    public interface IDnaSerializer
    {
        /// <summary>
        /// Canonicalizes <paramref name="definition"/> before writing. Throws
        /// DomainException if the definition is not finite/valid — callers should
        /// validate first (DefinitionValidator) so a bad definition never reaches
        /// serialization in the first place.
        /// </summary>
        string Serialize(CreatureDefinition definition);

        /// <summary>
        /// Parses JSON into a CreatureDefinition. Does NOT validate the result —
        /// callers must run DefinitionValidator on the returned definition before
        /// using it for generation (§14: "Add load command that validates before
        /// replacing current canonical state").
        /// </summary>
        /// <exception cref="DnaDeserializationException">
        /// Thrown when the JSON is structurally malformed (not valid JSON, or missing
        /// a required field). This is distinct from semantic validation — a
        /// structurally valid JSON document with a NaN transform deserializes fine
        /// and is caught by DefinitionValidator instead.
        /// </exception>
        CreatureDefinition Deserialize(string json);
    }
}
