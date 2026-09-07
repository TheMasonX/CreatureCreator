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
            WriteRawField(sb, "forward", WriteVec3(definition.Forward));
            WriteRawField(sb, "body", WriteBody(definition.Body));
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

        private static string WriteBody(BodySpline body)
        {
            var sb = new StringBuilder();
            sb.Append("{\"samples\":[");
            for (int i = 0; i < body.Samples.Count; i++)
            {
                if (i > 0) sb.Append(',');
                BodySample sample = body.Samples[i];
                sb.Append("{\"id\":").Append(sample.Id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"position\":").Append(WriteVec3(sample.Position));
                sb.Append(",\"radius\":").Append(Num(sample.Radius)).Append('}');
            }
            sb.Append("],\"appearance\":").Append(WriteBodyAppearance(body.Appearance));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteBodyAppearance(BodyVerticalGradientAppearance appearance)
        {
            if (appearance == null) return "null";
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"topGradient\":").Append(WriteGradient(appearance.TopGradient)).Append(',');
            sb.Append("\"bottomGradient\":").Append(WriteGradient(appearance.BottomGradient)).Append(',');
            sb.Append("\"verticalCurve\":").Append(WriteCurve(appearance.VerticalCurve));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteCurve(UnityEngine.AnimationCurve curve)
        {
            if (curve == null) return "null";
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"keys\":[");
            UnityEngine.Keyframe[] keys = curve.keys;
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"time\":").Append(Num(keys[i].time));
                    sb.Append(",\"value\":").Append(Num(keys[i].value));
                    sb.Append(",\"inTangent\":").Append(Num(keys[i].inTangent));
                    sb.Append(",\"outTangent\":").Append(Num(keys[i].outTangent)).Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string WriteGradient(UnityEngine.Gradient gradient)
        {
            if (gradient == null) return "null";
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "mode", gradient.mode.ToString(), first: true);
            sb.Append(",\"colorKeys\":[");
            UnityEngine.GradientColorKey[] colorKeys = gradient.colorKeys;
            if (colorKeys != null)
            {
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"time\":").Append(Num(colorKeys[i].time));
                    sb.Append(",\"color\":").Append(WriteColor(colorKeys[i].color)).Append('}');
                }
            }
            sb.Append("],\"alphaKeys\":[");
            UnityEngine.GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
            if (alphaKeys != null)
            {
                for (int i = 0; i < alphaKeys.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"time\":").Append(Num(alphaKeys[i].time));
                    sb.Append(",\"alpha\":").Append(Num(alphaKeys[i].alpha)).Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string WritePart(CreaturePart part)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            WriteField(sb, "id", part.Id, first: true);
            WriteNullableField(sb, "displayName", part.DisplayName);
            WriteNullableField(sb, "parentId", part.ParentId);
            WriteField(sb, "partType", part.PartType.ToString());
            WriteRawField(sb, "transform", WriteTransform(part.Transform));
            WriteRawField(sb, "shape", WriteShape(part.Shape));
            WriteRawField(sb, "appearance", WriteAppearance(part.Appearance));
            WriteField(sb, "mirrorAcrossSymmetryPlane", part.MirrorAcrossSymmetryPlane);
            WriteRawField(sb, "parentAttachment", WriteNullableAnchor(part.ParentAttachment));
            WriteRawField(sb, "limbChain", WriteNullableLimbChain(part.Limb));
            WriteRawField(sb, "meshGeometry", WriteNullableMeshGeometry(part.MeshGeometry));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteNullableMeshGeometry(MeshGeometry mesh)
        {
            if (mesh == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{\"meshAssetKey\":");
            sb.Append('"').Append(Escape(mesh.MeshAssetKey ?? string.Empty)).Append('"');
            sb.Append(",\"attachment\":");
            sb.Append(WriteGeometryAttachment(mesh.Attachment));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteGeometryAttachment(GeometryAttachment attachment)
        {
            if (attachment == null) return "null";
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"offset\":").Append(WriteVec3(attachment.Offset)).Append(',');
            sb.Append("\"orientation\":").Append(WriteQuat(attachment.Orientation)).Append(',');
            sb.Append("\"scale\":").Append(WriteVec3(attachment.Scale));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteNullableLimbChain(LimbChain limb)
        {
            if (limb == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{\"joints\":[");
            if (limb.Joints != null)
            {
                for (int i = 0; i < limb.Joints.Count; i++)
                {
                    LimbJoint joint = limb.Joints[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":");
                    sb.Append(joint == null ? "null" : joint.Id.ToString(CultureInfo.InvariantCulture));
                    sb.Append(",\"position\":");
                    sb.Append(joint == null ? "null" : WriteVec3(joint.Position));
                    sb.Append('}');
                }
            }
            sb.Append("],\"blendRadius\":");
            sb.Append(Num(limb.BlendRadius));
            sb.Append(",\"thicknessProfile\":");
            sb.Append(WriteThicknessProfile(limb.Thickness));
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteThicknessProfile(ThicknessProfile profile)
        {
            if (profile == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{\"keys\":[");
            if (profile.Keys != null)
            {
                for (int i = 0; i < profile.Keys.Count; i++)
                {
                    ThicknessKey key = profile.Keys[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"t\":");
                    sb.Append(key == null ? "null" : Num(key.T));
                    sb.Append(",\"value\":");
                    sb.Append(key == null ? "null" : Num(key.Value));
                    sb.Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string WriteNullableAnchor(BodySurfaceAnchor anchor)
        {
            if (anchor == null) return "null";
            return "{\"segmentStartSampleId\":" +
                anchor.SegmentStartSampleId.ToString(CultureInfo.InvariantCulture) +
                ",\"segmentT\":" + Num(anchor.SegmentT) +
                ",\"radialAngle\":" + Num(anchor.RadialAngle) +
                ",\"surfaceOffset\":" + Num(anchor.SurfaceOffset) +
                ",\"roll\":" + Num(anchor.Roll) + "}";
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
            WriteField(sb, "radius", shape.Radius);
            WriteField(sb, "capsuleAxis", shape.CapsuleAxis.ToString());
            WriteField(sb, "capsuleHeight", shape.CapsuleHeight);
            sb.Append(",\"ellipsoidRadii\":").Append(WriteVec3(shape.EllipsoidRadii));
            WriteRawField(sb, "boxHalfExtents", WriteVec3(shape.BoxHalfExtents));
            WriteField(sb, "smoothBlendRadius", shape.SmoothBlendRadius);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteAppearance(AppearanceDefinition appearance)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"baseColor\":").Append(WriteColor(appearance.BaseColor));
            WriteField(sb, "noiseSeed", appearance.NoiseSeed);
            WriteField(sb, "noiseScale", appearance.NoiseScale);
            WriteNullableField(sb, "materialKey",
                string.IsNullOrWhiteSpace(appearance.MaterialKey) ? null : appearance.MaterialKey);
            sb.Append('}');
            return sb.ToString();
        }

        private static string WriteColor(UnityEngine.Color c)
        {
            return "{\"r\":" + Num(c.r) + ",\"g\":" + Num(c.g) + ",\"b\":" + Num(c.b) + ",\"a\":" + Num(c.a) + "}";
        }

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
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
