using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Focused contract tests for TSK-0138 (hardening the ValidationResult public
    /// contract): genuinely read-only Issues, a documented total order, typed
    /// structural locations, and a cached IsValid. Preserves the existing behavior
    /// that validation is deterministic and order-independent.
    /// </summary>
    [TestFixture]
    public class ValidationResultContractTests
    {
        private static ValidationIssue Issue(
            ValidationSeverity severity,
            ValidationCode code,
            string message,
            string partId = null,
            ValidationIssueLocation? location = null)
            => new ValidationIssue(severity, code, message, partId, location);

        // ---- F-201: Issues must be genuinely read-only after construction ----

        [Test]
        public void Issues_CannotBeMutatedThroughReadOnlyView()
        {
            var result = new ValidationResult(new[]
            {
                Issue(ValidationSeverity.Error, ValidationCode.MissingBody, "first"),
                Issue(ValidationSeverity.Error, ValidationCode.InvalidForward, "second"),
            });

            IReadOnlyList<ValidationIssue> view = result.Issues;
            Assert.AreEqual(2, view.Count);

            // The returned view must reject mutation even through the mutable
            // IList<T> facet it exposes, so no caller can alter the collection.
            IList<ValidationIssue> mutableFacet = view as IList<ValidationIssue>;
            Assert.IsNotNull(mutableFacet, "the read-only view must still expose IList<T> so mutation attempts can be observed and rejected");
            Assert.Throws<NotSupportedException>(() => mutableFacet.Add(Issue(ValidationSeverity.Info, ValidationCode.InvalidBounds, "x")));
            Assert.Throws<NotSupportedException>(() => mutableFacet.Clear());
            Assert.Throws<NotSupportedException>(() => mutableFacet.RemoveAt(0));

            Assert.AreEqual(2, view.Count, "no element may be added, removed, or cleared through the read-only view");
            Assert.AreEqual(ValidationCode.MissingBody, view[0].Code, "existing elements remain readable by index");
        }

        // ---- F-202: deterministic documented total order ----

        [Test]
        public void Issues_UseMessageAsFinalTieBreak()
        {
            // Identical PartId (null), Code, and Severity -> Message is the final
            // tie-break in the documented total order.
            var result = new ValidationResult(new[]
            {
                Issue(ValidationSeverity.Error, ValidationCode.InvalidBodySample, "sample 'zebra' has bad data"),
                Issue(ValidationSeverity.Error, ValidationCode.InvalidBodySample, "sample 'alpha' has bad data"),
                Issue(ValidationSeverity.Error, ValidationCode.InvalidBodySample, "sample 'mango' has bad data"),
            });

            string[] messages = result.Issues.Select(i => i.Message).ToArray();
            CollectionAssert.AreEqual(
                new[] { "sample 'alpha' has bad data", "sample 'mango' has bad data", "sample 'zebra' has bad data" },
                messages);
        }

        [Test]
        public void Issues_TotalOrderIsDeterministicAcrossConstruction()
        {
            ValidationIssue[] issues =
            {
                Issue(ValidationSeverity.Error, ValidationCode.InvalidForward, "part forward", "part_1"),
                Issue(ValidationSeverity.Error, ValidationCode.MissingBody, "body", null),
                Issue(ValidationSeverity.Warning, ValidationCode.InvalidForward, "part forward", "part_1"),
                Issue(ValidationSeverity.Error, ValidationCode.InvalidForward, "part forward b", "part_1"),
                Issue(ValidationSeverity.Error, ValidationCode.InvalidBounds, "bounds", null),
            };

            ValidationResult first = new ValidationResult(issues);
            ValidationResult second = new ValidationResult(issues.Reverse());

            Assert.AreEqual(first.Issues.Count, second.Issues.Count, "order must not depend on input order");
            for (int i = 0; i < first.Issues.Count; i++)
            {
                Assert.AreEqual(first.Issues[i].PartId, second.Issues[i].PartId);
                Assert.AreEqual(first.Issues[i].Code, second.Issues[i].Code);
                Assert.AreEqual(first.Issues[i].Severity, second.Issues[i].Severity);
                Assert.AreEqual(first.Issues[i].Message, second.Issues[i].Message);
            }
        }

        // ---- F-203: typed structural location supplements the Message ----

        [Test]
        public void NullBodySampleIssue_CarriesTypedBodySampleIndex()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(null); // null sample at index 1
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });

            ValidationResult result = DefinitionValidator.Validate(definition);

            ValidationIssue issue = result.Issues.FirstOrDefault(i =>
                i.Code == ValidationCode.InvalidBodySample && i.Message.Contains("at index 1"));
            Assert.IsNotNull(issue, "expected a null-body-sample finding embedding the index in its Message");
            Assert.IsTrue(issue.Location.HasValue, "the finding must carry a typed location");
            Assert.AreEqual(ValidationLocationKind.BodySample, issue.Location.Value.Kind);
            Assert.AreEqual(1, issue.Location.Value.Index);
        }

        [Test]
        public void NullLimbJointIssue_CarriesTypedLimbJointIndex()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.5f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 1f), Radius = 0.5f });
            definition.AddPart(new CreaturePart
            {
                Id = "leg",
                ParentId = CreatureDefinition.BodyId,
                PartType = PartType.Limb,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                Limb = new LimbChain { Joints = new List<LimbJoint> { null } }, // null joint at index 0
            });

            ValidationResult result = DefinitionValidator.Validate(definition);

            ValidationIssue issue = result.Issues.FirstOrDefault(i =>
                i.Code == ValidationCode.InvalidLimbChain && i.Message.Contains("null limb joint at index 0"));
            Assert.IsNotNull(issue, "expected a null-limb-joint finding embedding the index in its Message");
            Assert.IsTrue(issue.Location.HasValue, "the finding must carry a typed location");
            Assert.AreEqual(ValidationLocationKind.LimbJoint, issue.Location.Value.Kind);
            Assert.AreEqual(0, issue.Location.Value.Index);
        }

        [Test]
        public void IssueLocation_ExposesNullWhenNotProvided()
        {
            ValidationIssue unscoped = Issue(ValidationSeverity.Error, ValidationCode.MissingBody, "body");
            Assert.IsFalse(unscoped.Location.HasValue, "definition-wide findings carry no typed structural location");
        }

        // ---- F-204: IsValid is computed once at construction and cached ----

        [Test]
        public void IsValid_IsCachedAndReflectsErrorPresenceOnly()
        {
            var valid = new ValidationResult(new[]
            {
                Issue(ValidationSeverity.Info, ValidationCode.InvalidBounds, "awareness only"),
                Issue(ValidationSeverity.Warning, ValidationCode.LegFootChainMissingAnkleJoint, "heads up"),
            });
            Assert.IsTrue(valid.IsValid, "Warning/Info issues must not block generation");
            Assert.IsTrue(valid.IsValid, "IsValid must be stable across reads (cached)");

            var invalid = new ValidationResult(new[]
            {
                Issue(ValidationSeverity.Error, ValidationCode.MissingBody, "blocks generation"),
                Issue(ValidationSeverity.Info, ValidationCode.InvalidBounds, "non-blocking awareness"),
            });
            Assert.IsFalse(invalid.IsValid);
            Assert.IsFalse(invalid.IsValid, "IsValid must remain false across reads (cached)");

            Assert.IsTrue(ValidationResult.Valid().IsValid, "the empty result must be valid");
        }
    }
}
