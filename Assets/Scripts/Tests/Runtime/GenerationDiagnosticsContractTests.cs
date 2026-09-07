using System;
using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class GenerationDiagnosticsContractTests
    {
        [Test]
        public void Timings_IsReadOnlyAndCannotBeMutatedThroughListDowncast()
        {
            var diagnostics = new GenerationDiagnostics();
            diagnostics.RecordTiming(GenerationStage.Validation, TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(diagnostics.Timings is System.Collections.Generic.List<StageTiming>);
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<StageTiming>)diagnostics.Timings)
                    .Add(new StageTiming(GenerationStage.SdfCompile, TimeSpan.Zero)));
            Assert.AreEqual(1, diagnostics.Timings.Count);
        }

        [Test]
        public void Issues_IsReadOnlyAndRejectsNullEntries()
        {
            var diagnostics = new GenerationDiagnostics();
            var issue = new ValidationIssue(
                ValidationSeverity.Error,
                ValidationCode.InvalidPartId,
                "part",
                "invalid");
            diagnostics.RecordIssue(issue);

            Assert.IsFalse(diagnostics.Issues is System.Collections.Generic.List<ValidationIssue>);
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<ValidationIssue>)diagnostics.Issues)
                    .Add(issue));
            Assert.Throws<ArgumentNullException>(() => diagnostics.RecordIssue(null));
            Assert.AreEqual(1, diagnostics.Issues.Count);
        }

        [Test]
        public void TimeStage_MarksDomainExceptionAsFailedAndRethrows()
        {
            var diagnostics = new GenerationDiagnostics();

            Assert.Throws<Common.DomainException>(() =>
                diagnostics.TimeStage(GenerationStage.SdfCompile, () =>
                    throw new Common.DomainException("bad domain")));

            Assert.AreEqual(GenerationStage.SdfCompile, diagnostics.FailedStage);
            Assert.IsFalse(diagnostics.Succeeded);
            Assert.AreEqual(1, diagnostics.Timings.Count);
        }

        [Test]
        public void TimeStage_MarksUnexpectedExceptionAsFailedAndRethrows()
        {
            var diagnostics = new GenerationDiagnostics();

            Assert.Throws<InvalidOperationException>(() =>
                diagnostics.TimeStage(GenerationStage.MeshExtraction, () =>
                    throw new InvalidOperationException("unexpected")));

            Assert.AreEqual(GenerationStage.MeshExtraction, diagnostics.FailedStage);
            Assert.IsFalse(diagnostics.Succeeded);
            Assert.AreEqual(1, diagnostics.Timings.Count);
        }

        [Test]
        public void TimeStage_WithoutTimingCollectionStillMarksUnexpectedExceptionAsFailed()
        {
            var diagnostics = new GenerationDiagnostics(collectTimings: false);

            Assert.Throws<InvalidOperationException>(() =>
                diagnostics.TimeStage(GenerationStage.AppearanceBake, () =>
                    throw new InvalidOperationException("unexpected")));

            Assert.AreEqual(GenerationStage.AppearanceBake, diagnostics.FailedStage);
            Assert.IsFalse(diagnostics.Succeeded);
            Assert.AreEqual(0, diagnostics.Timings.Count);
        }
    }
}
