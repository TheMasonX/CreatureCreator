using System;
using System.Collections.Generic;
using System.Globalization;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Serialization
{
    /// <summary>
    /// Default IDnaSerializer implementation. Serialize always canonicalizes first
    /// (so callers never need to remember to do it themselves); Deserialize parses
    /// structurally but performs no semantic validation — see IDnaSerializer's
    /// contract notes.
    /// </summary>
    public sealed class JsonDnaSerializer : IDnaSerializer
    {
        public string Serialize(CreatureDefinition definition)
        {
            CreatureDefinition canonical = DefinitionCanonicalizer.Canonicalize(definition);
            return CanonicalJsonWriter.Write(canonical);
        }

        public CreatureDefinition Deserialize(string json)
        {
            object root = MiniJsonReader.Parse(json);
            if (root is not Dictionary<string, object> obj)
            {
                throw new DnaDeserializationException("Root JSON value must be an object.");
            }

            int schemaVersion = (int)RequireNumber(obj, "schemaVersion");
            if (schemaVersion == 1)
            {
                throw new DnaDeserializationException(
                    "Schema version 1 is unsupported; flat v1 DNA cannot be migrated without changing author intent.");
            }
            if (schemaVersion != CreatureDefinition.CurrentSchemaVersion)
            {
                throw new DnaDeserializationException(
                    $"Schema version {schemaVersion} is unsupported (expected {CreatureDefinition.CurrentSchemaVersion}).");
            }

            var definition = new CreatureDefinition
            {
                SchemaVersion = schemaVersion,
                SymmetryMode = RequireEnum<SymmetryMode>(obj, "symmetryMode"),
                Bounds = ReadBounds(RequireObject(obj, "bounds")),
                Generation = ReadGeneration(RequireObject(obj, "generation")),
                Forward = ReadVec3(RequireObject(obj, "forward")),
                Body = ReadBody(RequireObject(obj, "body")),
                Parts = ReadParts(RequireArray(obj, "parts")),
            };

            return definition;
        }

        private static BodySpline ReadBody(Dictionary<string, object> obj)
        {
            var body = new BodySpline();
            foreach (object entry in RequireArray(obj, "samples"))
            {
                if (entry is not Dictionary<string, object> sampleObj)
                {
                    throw new DnaDeserializationException("Each entry in 'samples' must be an object.");
                }
                body.Samples.Add(new BodySample
                {
                    Id = RequireUInt(sampleObj, "id"),
                    Position = ReadVec3(RequireObject(sampleObj, "position")),
                    Radius = (float)RequireNumber(sampleObj, "radius"),
                });
            }
            body.Appearance = ReadOptionalBodyAppearance(obj);
            return body;
        }

        /// <summary>
        /// The body vertical-gradient appearance (CC-025) is an additive, optional
        /// schema field: existing v2 files without it load with the default flat
        /// gray model, so no migration or version bump is required. The canonical
        /// writer always emits it, making save-load-save byte-stable.
        /// </summary>
        private static BodyVerticalGradientAppearance ReadOptionalBodyAppearance(Dictionary<string, object> obj)
        {
            if (!obj.TryGetValue("appearance", out object value) || value == null)
            {
                return BodyVerticalGradientAppearance.CreateDefault();
            }
            if (value is not Dictionary<string, object> appearanceObj)
            {
                throw new DnaDeserializationException("Field 'appearance' must be an object or null.");
            }
            return new BodyVerticalGradientAppearance
            {
                TopGradient = ReadColorGradient(RequireArray(appearanceObj, "topGradient")),
                BottomGradient = ReadColorGradient(RequireArray(appearanceObj, "bottomGradient")),
                VerticalOffset = (float)RequireNumber(appearanceObj, "verticalOffset"),
            };
        }

        private static ColorGradient ReadColorGradient(List<object> array)
        {
            var gradient = new ColorGradient();
            foreach (object entry in array)
            {
                if (entry is not Dictionary<string, object> stopObj)
                {
                    throw new DnaDeserializationException("Each gradient stop must be an object.");
                }
                Dictionary<string, object> colorObj = RequireObject(stopObj, "color");
                gradient.Stops.Add(new GradientColorStop(
                    (float)RequireNumber(stopObj, "t"),
                    new UnityEngine.Color(
                        (float)RequireNumber(colorObj, "r"),
                        (float)RequireNumber(colorObj, "g"),
                        (float)RequireNumber(colorObj, "b"),
                        (float)RequireNumber(colorObj, "a"))));
            }
            return gradient;
        }

        private static BoundsDefinition ReadBounds(Dictionary<string, object> obj)
        {
            return new BoundsDefinition
            {
                MaxX = (float)RequireNumber(obj, "maxX"),
                MaxY = (float)RequireNumber(obj, "maxY"),
                MaxZ = (float)RequireNumber(obj, "maxZ"),
            };
        }

        private static GenerationSettings ReadGeneration(Dictionary<string, object> obj)
        {
            return new GenerationSettings
            {
                VoxelsPerUnit = (float)RequireNumber(obj, "voxelsPerUnit"),
            };
        }

        private static List<CreaturePart> ReadParts(List<object> array)
        {
            var result = new List<CreaturePart>(array.Count);
            foreach (object entry in array)
            {
                if (entry is not Dictionary<string, object> partObj)
                {
                    throw new DnaDeserializationException("Each entry in 'parts' must be an object.");
                }
                result.Add(ReadPart(partObj));
            }
            return result;
        }

        private static CreaturePart ReadPart(Dictionary<string, object> obj)
        {
            return new CreaturePart
            {
                Id = RequireString(obj, "id"),
                DisplayName = ReadOptionalString(obj, "displayName"),
                ParentId = ReadNullableString(obj, "parentId"),
                PartType = RequireEnum<PartType>(obj, "partType"),
                Transform = ReadTransform(RequireObject(obj, "transform")),
                Shape = ReadShape(RequireObject(obj, "shape")),
                Appearance = ReadAppearance(RequireObject(obj, "appearance")),
                MirrorAcrossSymmetryPlane = RequireBool(obj, "mirrorAcrossSymmetryPlane"),
                ParentAttachment = ReadNullableAnchor(obj, "parentAttachment"),
            };
        }

        private static BodySurfaceAnchor ReadNullableAnchor(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value) || value == null) return null;
            if (value is not Dictionary<string, object> anchorObj)
            {
                throw new DnaDeserializationException($"Field '{key}' must be an object or null.");
            }
            return new BodySurfaceAnchor
            {
                SegmentStartSampleId = RequireUInt(anchorObj, "segmentStartSampleId"),
                SegmentT = (float)RequireNumber(anchorObj, "segmentT"),
                RadialAngle = (float)RequireNumber(anchorObj, "radialAngle"),
                SurfaceOffset = (float)RequireNumber(anchorObj, "surfaceOffset"),
                Roll = (float)RequireNumber(anchorObj, "roll"),
            };
        }

        private static TransformData ReadTransform(Dictionary<string, object> obj)
        {
            return new TransformData
            {
                Position = ReadVec3(RequireObject(obj, "position")),
                Rotation = ReadQuat(RequireObject(obj, "rotation")),
                Scale = ReadVec3(RequireObject(obj, "scale")),
            };
        }

        private static UnityEngine.Vector3 ReadVec3(Dictionary<string, object> obj)
        {
            return new UnityEngine.Vector3(
                (float)RequireNumber(obj, "x"),
                (float)RequireNumber(obj, "y"),
                (float)RequireNumber(obj, "z"));
        }

        private static UnityEngine.Quaternion ReadQuat(Dictionary<string, object> obj)
        {
            return new UnityEngine.Quaternion(
                (float)RequireNumber(obj, "x"),
                (float)RequireNumber(obj, "y"),
                (float)RequireNumber(obj, "z"),
                (float)RequireNumber(obj, "w"));
        }

        private static ShapeDefinition ReadShape(Dictionary<string, object> obj)
        {
            return new ShapeDefinition
            {
                Type = RequireEnum<ShapeType>(obj, "type"),
                PrimarySize = (float)RequireNumber(obj, "primarySize"),
                SmoothBlendRadius = (float)RequireNumber(obj, "smoothBlendRadius"),
            };
        }

        private static AppearanceDefinition ReadAppearance(Dictionary<string, object> obj)
        {
            Dictionary<string, object> colorObj = RequireObject(obj, "baseColor");
            return new AppearanceDefinition
            {
                BaseColor = new UnityEngine.Color(
                    (float)RequireNumber(colorObj, "r"),
                    (float)RequireNumber(colorObj, "g"),
                    (float)RequireNumber(colorObj, "b"),
                    (float)RequireNumber(colorObj, "a")),
                NoiseSeed = (int)RequireNumber(obj, "noiseSeed"),
                NoiseScale = (float)RequireNumber(obj, "noiseScale"),
            };
        }

        // ---- tree-walk helpers --------------------------------------------------------

        private static object RequireField(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value))
            {
                throw new DnaDeserializationException($"Missing required field '{key}'.");
            }
            return value;
        }

        private static double RequireNumber(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value is double d) return d;
            throw new DnaDeserializationException($"Field '{key}' must be a number.");
        }

        private static bool RequireBool(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value is bool b) return b;
            throw new DnaDeserializationException($"Field '{key}' must be a boolean.");
        }

        private static uint RequireUInt(Dictionary<string, object> obj, string key)
        {
            double value = RequireNumber(obj, key);
            if (value < 0 || value > uint.MaxValue || value != Math.Floor(value))
            {
                throw new DnaDeserializationException($"Field '{key}' must be an unsigned integer.");
            }
            return (uint)value;
        }

        private static string RequireString(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value is string s) return s;
            throw new DnaDeserializationException($"Field '{key}' must be a string.");
        }

        private static string ReadNullableString(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value == null) return null;
            if (value is string s) return s;
            throw new DnaDeserializationException($"Field '{key}' must be a string or null.");
        }

        private static string ReadOptionalString(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value) || value == null) return null;
            if (value is string s) return s;
            throw new DnaDeserializationException($"Field '{key}' must be a string.");
        }

        private static Dictionary<string, object> RequireObject(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value is Dictionary<string, object> nested) return nested;
            throw new DnaDeserializationException($"Field '{key}' must be an object.");
        }

        private static List<object> RequireArray(Dictionary<string, object> obj, string key)
        {
            object value = RequireField(obj, key);
            if (value is List<object> list) return list;
            throw new DnaDeserializationException($"Field '{key}' must be an array.");
        }

        private static TEnum RequireEnum<TEnum>(Dictionary<string, object> obj, string key) where TEnum : struct
        {
            string raw = RequireString(obj, key);
            if (System.Enum.TryParse(raw, ignoreCase: false, out TEnum result))
            {
                return result;
            }
            throw new DnaDeserializationException(
                $"Field '{key}' has unrecognized value '{raw}' for enum {typeof(TEnum).Name}.");
        }
    }
}
