using UnityEngine;
using ProceduralCreature.Common;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Small quantization helpers shared by canonicalization and by anything that
    /// needs to compare "would these two values be the same once canonicalized"
    /// without doing a full DefinitionCanonicalizer pass (e.g. editor UI deciding
    /// whether a drag actually changed anything worth committing).
    /// </summary>
    public static class QuantizeUtil
    {
        public static float Quantize(float value) => GenerationTolerances.Quantize(value);

        public static Vector3 Quantize(Vector3 value) => new Vector3(
            GenerationTolerances.Quantize(value.x),
            GenerationTolerances.Quantize(value.y),
            GenerationTolerances.Quantize(value.z));
    }
}
