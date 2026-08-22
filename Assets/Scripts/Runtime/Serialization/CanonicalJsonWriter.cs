using System.Globalization;
using System.Text;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Serialization
{
    /// <summary>
    /// Produces the canonical JSON text for a CreatureDefinition. "Canonical" means:
    /// fixed key order (declared below, never dictionary-iteration order), fixed
    /// numeric formatting (invariant culture, always
    /// GenerationTolerances.QuantizationDecimalPlaces decimal places for float
    /// fields), fixed part ordering (by Id, ordinal — the caller is expected to pass
    /// an already-canonicalized definition; see JsonDnaSerializer).
    ///
    /// This guarantees the Sprint 1.3 exit gate: "Save -> load -> canonical-save
    /// produces byte-stable canonical JSON for supported definitions."
    ///
    /// Field name/nesting reference (the "exact JSON field names and nesting" this
    /// class exists to fix in place):
    /// <code>
    /// {
    ///   "schemaVersion": 1,
    ///   "symmetryMode": "None",
    ///   "bounds": { "maxX": 4.0000, "maxY": 4.0000, "maxZ": 4.0000 },
    ///   "generation": { "voxelsPerUnit": 16.0000 },
    ///   "parts": [
    ///     {
    ///       "id": "part_4f9a1c02",
    ///       "parentId": null,
    ///       "partType": "Body",
    ///       "transform": {
    ///         "position": { "x": 0.0000, "y": 0.0000, "z": 0.0000 },
    ///         "rotation": { "x": 0.0000, "y": 0.0000, "z": 0.0000, "w": 1.0000 },
    ///         "scale": { "x": 1.0000, "y": 1.0000, "z": 1.0000 }
    ///       },
    ///       "shape": { "type": "Sphere", "primarySize": 0.5000, "smoothBlendRadius": 0.1000 },
    ///       "appearance": {
    ///         "baseColor": { "r": 0.5000, "g": 0.5000, "b": 0.5000, "a": 1.0000 },
    ///         "noiseSeed": 0,
    ///         "noiseScale": 1.0000
    ///       },
    ///       "mirrorAcrossSymmetryPlane": false
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    internal static class CanonicalJsonWriter
    {
        public static string Write(CreatureDefinition definition)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "schemaVersion", definition.SchemaVersion, first: true);
            WriteField(sb, "symmetryMode", definition.SymmetryMode.ToString());
            WriteRawField(sb, "bounds", WriteBounds(definition.Bounds));
            WriteRawField(sb, "generation", WriteGeneration(definition.Generation));
            WriteRawField(sb, "parts", WriteParts(definition.Parts));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteBounds(BoundsDefinition bounds)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "maxX", bounds.MaxX, first: true);
            WriteField(sb, "maxY", bounds.MaxY);
            WriteField(sb, "maxZ", bounds.MaxZ);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteGeneration(GenerationSettings generation)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "voxelsPerUnit", generation.VoxelsPerUnit, first: true);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteParts(System.Collections.Generic.List<CreaturePart> parts)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(WritePart(parts[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string WritePart(CreaturePart part)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "id", part.Id, first: true);
            WriteNullableField(sb, "parentId", part.ParentId);
            WriteField(sb, "partType", part.PartType.ToString());
            WriteRawField(sb, "transform", WriteTransform(part.Transform));
            WriteRawField(sb, "shape", WriteShape(part.Shape));
            WriteRawField(sb, "appearance", WriteAppearance(part.Appearance));
            WriteField(sb, "mirrorAcrossSymmetryPlane", part.MirrorAcrossSymmetryPlane);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteTransform(TransformData t)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"position\":").Append(WriteVec3(t.Position)).Append(',');
            sb.Append("\"rotation\":").Append(WriteQuat(t.Rotation)).Append(',');
            sb.Append("\"scale\":").Append(WriteVec3(t.Scale));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteVec3(UnityEngine.Vector3 v)
        {
            return "{\"x\":" + Num(v.x) + ",\"y\":" + Num(v.y) + ",\"z\":" + Num(v.z) + "}";
        }

        private static string WriteQuat(UnityEngine.Quaternion q)
        {
            return "{\"x\":" + Num(q.x) + ",\"y\":" + Num(q.y) + ",\"z\":" + Num(q.z) + ",\"w\":" + Num(q.w) + "}";
        }

        private static string WriteShape(ShapeDefinition shape)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "type", shape.Type.ToString(), first: true);
            WriteField(sb, "primarySize", shape.PrimarySize);
            WriteField(sb, "smoothBlendRadius", shape.SmoothBlendRadius);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteAppearance(AppearanceDefinition appearance)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"baseColor\":").Append(WriteColor(appearance.BaseColor)).Append(',');
            WriteField(sb, "noiseSeed", appearance.NoiseSeed);
            WriteField(sb, "noiseScale", appearance.NoiseScale);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteColor(UnityEngine.Color c)
        {
            return "{\"r\":" + Num(c.r) + ",\"g\":" + Num(c.g) + ",\"b\":" + Num(c.b) + ",\"a\":" + Num(c.a) + "}";
        }

        // ---- low-level field writers -------------------------------------------------

        private static void WriteField(StringBuilder sb, string key, string value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static void WriteField(StringBuilder sb, string key, float value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(Num(value));
        }

        private static void WriteField(StringBuilder sb, string key, int value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteField(StringBuilder sb, string key, bool value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
        }

        private static void WriteNullableField(StringBuilder sb, string key, string value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":");
            sb.Append(value == null ? "null" : "\"" + Escape(value) + "\"");
        }

        private static void WriteRawField(StringBuilder sb, string key, string rawJsonValue, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(rawJsonValue);
        }

        private static string Num(float value)
        {
            // Fixed decimal-place formatting is the entire point of "canonical" here —
            // it must never vary between save operations regardless of platform
            // locale (hence InvariantCulture) or the specific float value's shortest
            // round-trip representation (hence a fixed "F<n>" format rather than "R"
            // or "G").
            return value.ToString("F" + Common.GenerationTolerances.QuantizationDecimalPlaces, CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
