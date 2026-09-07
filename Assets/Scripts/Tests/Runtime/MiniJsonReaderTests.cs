using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Pure grammar fixture for <see cref="MiniJsonReader"/> (TSK-0136). Tests the
    /// strict JSON grammar boundaries in isolation — duplicate object member names,
    /// raw control characters inside strings, the strict numeric grammar, and
    /// escaped-string decoding. This is intentionally separate from
    /// <see cref="JsonDnaSerializerTests"/>, which stays focused on DNA semantics and
    /// schema migration. No Unity scene or engine state is required; these are plain
    /// [Test] cases against the internal reader.
    /// </summary>
    [TestFixture]
    public class MiniJsonReaderTests
    {
        // --- F-303: duplicate object member names must be rejected (no last-write-wins).

        [Test]
        public void Parse_DuplicateObjectMember_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"a\":1,\"a\":2}"));
        }

        [Test]
        public void Parse_DuplicateObjectMember_ThrowsRegardlessOfValueType()
        {
            // Even when the second value differs in type, the duplicate key is still an error.
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"key\":1,\"key\":\"different\"}"));
        }

        [Test]
        public void Parse_DuplicateObjectMember_NestedObjectAlsoRejected()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"outer\":{\"x\":true,\"x\":false}}"));
        }

        [Test]
        public void Parse_DuplicateErrorIsDeterministic_MessageNamesTheKey()
        {
            DnaDeserializationException exception = Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"first\":0,\"dup\":1,\"dup\":2}"));
            StringAssert.Contains("'dup'", exception.Message);
        }

        // --- F-304: raw control characters (< 0x20) inside strings must be rejected.

        [Test]
        public void Parse_RawNewlineInString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"line1" + '\n' + "line2\""));
        }

        [Test]
        public void Parse_RawTabInString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"col1" + '\t' + "col2\""));
        }

        [Test]
        public void Parse_RawCarriageReturnInString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"a" + '\r' + "b\""));
        }

        [Test]
        public void Parse_RawNulInString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"a" + '\u0000' + "b\""));
        }

        [Test]
        public void Parse_RawLowControlCharacterInString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"a" + '\u0001' + "b\""));
        }

        [Test]
        public void Parse_RawControlCharacterInObjectValueString_Throws()
        {
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"name\":\"a" + '\u0002' + "b\"}"));
        }

        [Test]
        public void Parse_ControlCharacterError_IsDeterministic()
        {
            DnaDeserializationException exception = Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("\"x" + '\u0003' + "y\""));
            StringAssert.Contains("control character", exception.Message);
        }

        // --- Valid escapes must still parse (contrast with raw control chars above).

        [Test]
        public void Parse_EscapedControlCharacters_AreAllowed()
        {
            // JSON text whose value is a newline produced via the \n escape is valid.
            object parsed = MiniJsonReader.Parse("\"\\n\"");
            Assert.AreEqual("\n", parsed);
        }

        [Test]
        public void Parse_EscapedString_DecodesAllStandardEscapes()
        {
            // JSON: "\n\t\"\\\/\b\f\r\u00e9"
            const string json = "\"\\n\\t\\\"\\\\\\/\\b\\f\\r\\u00e9\"";
            string expected = "\n\t\"\\/\b\f\r\u00e9";
            Assert.AreEqual(expected, (string)MiniJsonReader.Parse(json));
        }

        [Test]
        public void Parse_UnicodeEscape_SurrogateSafeBasicPlane()
        {
            // \u0041 == 'A'
            Assert.AreEqual("A", (string)MiniJsonReader.Parse("\"\\u0041\""));
        }

        [Test]
        public void Parse_EmptyString_IsValid()
        {
            Assert.AreEqual(string.Empty, (string)MiniJsonReader.Parse("\"\""));
        }

        // --- F-305: strict JSON numeric grammar (valid forms).

        private static double ParseNumber(string json)
        {
            object value = MiniJsonReader.Parse(json);
            Assert.IsInstanceOf<double>(value, "Expected a double for '" + json + "'.");
            return (double)value;
        }

        [TestCase("0", 0.0)]
        [TestCase("-0", 0.0)]
        [TestCase("123", 123.0)]
        [TestCase("-123", -123.0)]
        [TestCase("0.5", 0.5)]
        [TestCase("-0.5", -0.5)]
        [TestCase("123.456", 123.456)]
        [TestCase("1e5", 100000.0)]
        [TestCase("1E5", 100000.0)]
        [TestCase("1e+5", 100000.0)]
        [TestCase("1e-3", 0.001)]
        [TestCase("1.5e2", 150.0)]
        [TestCase("1.25E-2", 0.0125)]
        public void Parse_ValidNumber_Parses(string json, double expected)
        {
            Assert.AreEqual(expected, ParseNumber(json), 1e-9);
        }

        [Test]
        public void Parse_ExponentOverflowToInfinity_Throws()
        {
            Assert.Throws<DnaDeserializationException>(() => MiniJsonReader.Parse("1e999"));
            Assert.Throws<DnaDeserializationException>(() => MiniJsonReader.Parse("-1e999"));
        }

        [Test]
        public void Parse_NumberInsideObject_Parses()
        {
            object root = MiniJsonReader.Parse("{\"radius\":0.75}");
            var obj = (Dictionary<string, object>)root;
            Assert.AreEqual(0.75, (double)obj["radius"], 1e-9);
        }

        // --- F-305: strict JSON numeric grammar (invalid forms must throw).

        [TestCase("01")]
        [TestCase("-01")]
        [TestCase("00")]
        [TestCase("1.")]
        [TestCase(".5")]
        [TestCase("+1")]
        [TestCase("-")]
        [TestCase("1e")]
        [TestCase("1e+")]
        [TestCase("1e-")]
        [TestCase("1.2.3")]
        [TestCase("1a")]
        [TestCase("0x1F")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("e5")]
        public void Parse_InvalidNumber_Throws(string json)
        {
            Assert.Throws<DnaDeserializationException>(() => MiniJsonReader.Parse(json));
        }

        [Test]
        public void Parse_InvalidNumberInsideObject_Throws()
        {
            // "1x" is a valid 1 followed by trailing junk where a ',' or '}' was expected.
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"a\":1x}"));
            // Leading-zero integer inside an object is also rejected.
            Assert.Throws<DnaDeserializationException>(
                () => MiniJsonReader.Parse("{\"a\":01}"));
        }

        // --- Trailing junk after a complete value is rejected.

        [Test]
        public void Parse_TrailingContent_Throws()
        {
            Assert.Throws<DnaDeserializationException>(() => MiniJsonReader.Parse("1 2"));
            Assert.Throws<DnaDeserializationException>(() => MiniJsonReader.Parse("true x"));
        }
    }
}
