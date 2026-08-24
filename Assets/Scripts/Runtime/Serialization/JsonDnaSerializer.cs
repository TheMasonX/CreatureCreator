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
        ///
        /// The vertical blend is a curve (CC-034). New files carry
        /// <c>verticalCurve</c>; files saved before CC-034 carry
        /// <c>verticalOffset</c>, which migrates exactly (not approximately) to the
        /// equivalent 3-key piecewise-linear curve via
        /// <see cref="CurveAdapter.FromLegacyOffset"/>.
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

            UnityEngine.AnimationCurve verticalCurve;
            if (appearanceObj.TryGetValue("verticalCurve", out object curveValue) && curveValue != null)
            {
                verticalCurve = ReadCurve(curveValue, "verticalCurve");
            }
            else if (appearanceObj.TryGetValue("verticalOffset", out object offsetValue) && offsetValue is double legacyOffset)
            {
                verticalCurve = CurveAdapter.FromLegacyOffset((float)legacyOffset);
            }
            else
            {
                verticalCurve = CurveAdapter.Linear();
            }

            return new BodyVerticalGradientAppearance
            {
                TopGradient = ReadGradient(RequireField(appearanceObj, "topGradient"), "topGradient"),
                BottomGradient = ReadGradient(RequireField(appearanceObj, "bottomGradient"), "bottomGradient"),
                VerticalCurve = verticalCurve,
            };
        }

        /// <summary>
        /// Reads the vertical-blend curve (CC-034) from its canonical key list.
        /// Newly constructed keys default to free tangents, so
        /// <see cref="UnityEngine.AnimationCurve.Evaluate"/> uses exactly the
        /// numeric in/out tangents from the file.
        /// </summary>
        private static UnityEngine.AnimationCurve ReadCurve(object value, string name)
        {
            if (value is not Dictionary<string, object> obj)
            {
                throw new DnaDeserializationException($"Field '{name}' must be a curve object.");
            }

            var keys = new List<UnityEngine.Keyframe>();
            foreach (object entry in RequireArray(obj, "keys"))
            {
                if (entry is not Dictionary<string, object> keyObj)
                {
                    throw new DnaDeserializationException("Each curve key must be an object.");
                }
                var keyframe = new UnityEngine.Keyframe(
                    (float)RequireNumber(keyObj, "time"),
                    (float)RequireNumber(keyObj, "value"),
                    (float)RequireNumber(keyObj, "inTangent"),
                    (float)RequireNumber(keyObj, "outTangent"));
                keys.Add(keyframe);
            }
            if (keys.Count == 0)
            {
                throw new DnaDeserializationException($"{name} curve must contain at least one key.");
            }
            return new UnityEngine.AnimationCurve(keys.ToArray());
        }

        /// <summary>
        /// Reads a gradient from either the current canonical form (an object with
        /// mode + colorKeys + alphaKeys) or the legacy pre-CC-025-refactor form (an
        /// array of { t, color } stops). Legacy arrays convert to a Unity Gradient
        /// with the same color stops and per-stop alpha, so older saved creature
        /// files keep loading.
        /// </summary>
        private static UnityEngine.Gradient ReadGradient(object value, string name)
        {
            if (value is List<object> legacy)
            {
                return ReadLegacyGradient(legacy, name);
            }

            if (value is not Dictionary<string, object> obj)
            {
                throw new DnaDeserializationException(
                    $"Field '{name}' must be a gradient object or a legacy array of stops.");
            }

            UnityEngine.GradientMode mode = RequireEnum<UnityEngine.GradientMode>(obj, "mode");
            var colorKeys = new List<UnityEngine.GradientColorKey>();
            foreach (object entry in RequireArray(obj, "colorKeys"))
            {
                if (entry is not Dictionary<string, object> keyObj)
                {
                    throw new DnaDeserializationException("Each gradient color key must be an object.");
                }
                colorKeys.Add(new UnityEngine.GradientColorKey(
                    ReadColor(RequireObject(keyObj, "color")),
                    (float)RequireNumber(keyObj, "time")));
            }

            var alphaKeys = new List<UnityEngine.GradientAlphaKey>();
            foreach (object entry in RequireArray(obj, "alphaKeys"))
            {
                if (entry is not Dictionary<string, object> keyObj)
                {
                    throw new DnaDeserializationException("Each gradient alpha key must be an object.");
                }
                alphaKeys.Add(new UnityEngine.GradientAlphaKey(
                    (float)RequireNumber(keyObj, "alpha"),
                    (float)RequireNumber(keyObj, "time")));
            }

            var gradient = new UnityEngine.Gradient();
            gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
            gradient.mode = mode;
            return gradient;
        }

        private static UnityEngine.Gradient ReadLegacyGradient(List<object> legacy, string name)
        {
            var colorKeys = new List<UnityEngine.GradientColorKey>();
            var alphaKeys = new List<UnityEngine.GradientAlphaKey>();
            foreach (object entry in legacy)
            {
                if (entry is not Dictionary<string, object> stopObj)
                {
                    throw new DnaDeserializationException($"Each {name} stop must be an object.");
                }
                UnityEngine.Color color = ReadColor(RequireObject(stopObj, "color"));
                float t = (float)RequireNumber(stopObj, "t");
                colorKeys.Add(new UnityEngine.GradientColorKey(color, t));
                alphaKeys.Add(new UnityEngine.GradientAlphaKey(color.a, t));
            }
            if (colorKeys.Count == 0)
            {
                throw new DnaDeserializationException($"{name} gradient must contain at least one stop.");
            }
            // Unity's Gradient always stores at least two color/alpha keys; a
            // single-stop legacy gradient becomes a solid color.
            if (colorKeys.Count == 1)
            {
                float padTime = colorKeys[0].time < 0.5f ? 1f : 0f;
                colorKeys.Add(new UnityEngine.GradientColorKey(colorKeys[0].color, padTime));
                alphaKeys.Add(new UnityEngine.GradientAlphaKey(alphaKeys[0].alpha, padTime));
            }
            var gradient = new UnityEngine.Gradient();
            gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
            return gradient;
        }

        private static UnityEngine.Color ReadColor(Dictionary<string, object> obj)
        {
            return new UnityEngine.Color(
                (float)RequireNumber(obj, "r"),
                (float)RequireNumber(obj, "g"),
                (float)RequireNumber(obj, "b"),
                (float)RequireNumber(obj, "a"));
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
                Limb = ReadNullableLimbChain(obj, "limbChain"),
                MeshGeometry = ReadNullableMeshGeometry(obj, "meshGeometry"),
            };
        }

        /// <summary>
        /// Reads an optional mesh-asset geometry source (CC-031). A missing or null
        /// field yields null (the part is a plain primitive/limb); pre-CC-031 v2
        /// files load unchanged, so this additive field needs no schema version
        /// bump. The attachment is optional and defaults to identity when absent.
        /// </summary>
        private static MeshGeometry ReadNullableMeshGeometry(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value) || value == null) return null;
            if (value is not Dictionary<string, object> meshObj)
            {
                throw new DnaDeserializationException($"Field '{key}' must be an object or null.");
            }

            var mesh = new MeshGeometry
            {
                MeshAssetKey = ReadOptionalString(meshObj, "meshAssetKey"),
            };

            if (meshObj.TryGetValue("attachment", out object attachmentValue)
                && attachmentValue is Dictionary<string, object> attachmentObj)
            {
                mesh.Attachment = new GeometryAttachment
                {
                    Offset = ReadVec3(RequireObject(attachmentObj, "offset")),
                    Orientation = ReadQuat(RequireObject(attachmentObj, "orientation")),
                    Scale = ReadVec3(RequireObject(attachmentObj, "scale")),
                };
            }
            return mesh;
        }

        /// <summary>
        /// Reads an optional limb chain (CC-018). A missing or null field yields a
        /// null chain (the part is a plain primitive); pre-CC-018 v2 files load
        /// unchanged, so this additive field needs no schema version bump. Joints
        /// keep their authored order; the thickness profile is optional and
        /// defaults to the standard tapering profile when absent. Tangents are not
        /// read in v1 — the profile is linear — but a future additive tangent
        /// field would be read here without breaking existing files.
        /// </summary>
        private static LimbChain ReadNullableLimbChain(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value) || value == null) return null;
            if (value is not Dictionary<string, object> limbObj)
            {
                throw new DnaDeserializationException($"Field '{key}' must be an object or null.");
            }

            var chain = new LimbChain
            {
                Thickness = ReadOptionalThicknessProfile(limbObj, "thicknessProfile"),
            };
            foreach (object entry in RequireArray(limbObj, "joints"))
            {
                if (entry is not Dictionary<string, object> jointObj)
                {
                    throw new DnaDeserializationException("Each limb joint must be an object.");
                }
                chain.Joints.Add(new LimbJoint
                {
                    Id = RequireUInt(jointObj, "id"),
                    Position = ReadVec3(RequireObject(jointObj, "position")),
                });
            }
            return chain;
        }

        private static ThicknessProfile ReadOptionalThicknessProfile(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value) || value == null)
            {
                return ThicknessProfile.CreateDefault();
            }
            if (value is not Dictionary<string, object> profileObj)
            {
                throw new DnaDeserializationException($"Field '{key}' must be an object or null.");
            }

            var profile = new ThicknessProfile();
            foreach (object entry in RequireArray(profileObj, "keys"))
            {
                if (entry is not Dictionary<string, object> keyObj)
                {
                    throw new DnaDeserializationException("Each thickness key must be an object.");
                }
                profile.Keys.Add(new ThicknessKey
                {
                    T = (float)RequireNumber(keyObj, "t"),
                    Value = (float)RequireNumber(keyObj, "value"),
                });
            }
            return profile;
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
            return new AppearanceDefinition
            {
                BaseColor = ReadColor(RequireObject(obj, "baseColor")),
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
