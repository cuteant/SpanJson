﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using Newtonsoft.Json;
using SpanJson.Document;
using Xunit;

namespace SpanJson.Tests
{
    public static class JsonElementWriteTests
    {
        private const string CompiledNewline = @"
";

        private static readonly JsonDocumentOptions s_options =
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
            };

        private static readonly bool s_replaceNewlines =
            !StringComparer.Ordinal.Equals(CompiledNewline, Environment.NewLine);

        //[Fact]
        //public static void CheckByPassingNullWriter()
        //{
        //    using (JsonDocument doc = JsonDocument.Parse("true", default))
        //    {
        //        JsonElement root = doc.RootElement;
        //        AssertExtensions.Throws<ArgumentNullException>("writer", () => root.WriteTo(null));
        //    }
        //}

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumber(bool indented)
        {
            WriteSimpleValue(indented, "42");
        }

        [Theory]
        [InlineData("12E-3", false)]
        [InlineData("1e6", false)]
        [InlineData("1e6", true)]
        [InlineData("1e+6", false)]
        [InlineData("1e+6", true)]
        [InlineData("1e-6", false)]
        [InlineData("1e-6", true)]
        [InlineData("-1e6", false)]
        [InlineData("-1e6", true)]
        [InlineData("-1e+6", true)]
        //[InlineData("-1e+6", true)]
        [InlineData("-1e-6", false)]
        [InlineData("-1e-6", true)]
        public static void WriteNumberScientific(string value, bool indented)
        {
            WriteSimpleValue(indented, value);
        }

        [Theory]
        [InlineData("1.2E+3", false)]
        [InlineData("5.012e-20", false)]
        [InlineData("5.012e-20", true)]
        [InlineData("5.012e20", false)]
        [InlineData("5.012e20", true)]
        [InlineData("5.012e+20", false)]
        [InlineData("5.012e+20", true)]
        [InlineData("-5.012e-20", false)]
        [InlineData("-5.012e-20", true)]
        [InlineData("-5.012e20", false)]
        [InlineData("-5.012e20", true)]
        [InlineData("-5.012e+20", false)]
        [InlineData("-5.012e+20", true)]
        public static void WriteNumberDecimalScientific(string value, bool indented)
        {
            WriteSimpleValue(indented, value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberOverprecise(bool indented)
        {
            // This value is a reference "potential interoperability problem" from
            // https://tools.ietf.org/html/rfc7159#section-6
            const string PrecisePi = "3.141592653589793238462643383279";

            // To confirm that this test is doing what it intends, one could
            // confirm the printing precision of double, like
            //
            //double precisePi = double.Parse(PrecisePi);
            //Assert.NotEqual(PrecisePi, precisePi.ToString(JsonTestHelper.DoubleFormatString));

            WriteSimpleValue(indented, PrecisePi);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberTooLargeScientific(bool indented)
        {
            // This value is a reference "potential interoperability problem" from
            // https://tools.ietf.org/html/rfc7159#section-6
            const string OneQuarticGoogol = "1e400";

            // This just validates we write the literal number 1e400 even though it is too
            // large to be represented by System.Double and would be converted to
            // PositiveInfinity instead (or throw if using double.Parse on frameworks
            // older than .NET Core 3.0).
            WriteSimpleValue(indented, OneQuarticGoogol);
        }

        //[Theory]
        //[InlineData("6.022e+23", "6,022e+23")]
        //[InlineData("6.022e+23", "6.022f+23")]
        //[InlineData("6.022e+23", "6.022e+ 3")]
        //[InlineData("6.022e+23", "6e022e+23")]
        //[InlineData("6.022e+23", "6.022e+f3")]
        //[InlineData("1", "-")]
        //[InlineData("12", "+2")]
        //[InlineData("12", "1e")]
        //[InlineData("12", "1.")]
        //[InlineData("12", "02")]
        //[InlineData("123", "1e+")]
        //[InlineData("123", "1e-")]
        //[InlineData("0.12", "0.1e")]
        //[InlineData("0.123", "0.1e+")]
        //[InlineData("0.123", "0.1e-")]
        //[InlineData("10", "+0")]
        //[InlineData("101", "-01")]
        //[InlineData("12", "1a")]
        //[InlineData("10", "00")]
        //[InlineData("11", "01")]
        //[InlineData("10.5e-012", "10.5e-0.2")]
        //[InlineData("10.5e012", "10.5.012")]
        //[InlineData("0.123", "0.-23")]
        //[InlineData("12345", "hello")]
        //public static void WriteCorruptedNumber(string parseJson, string overwriteJson)
        //{
        //    if (overwriteJson.Length != parseJson.Length)
        //    {
        //        throw new InvalidOperationException("Invalid test, parseJson and overwriteJson must have the same length");
        //    }

        //    byte[] utf8Data = Encoding.UTF8.GetBytes(parseJson);

        //    using (JsonDocument document = JsonDocument.Parse(utf8Data))
        //    //using (MemoryStream stream = new MemoryStream(Array.Empty<byte>()))
        //    using (Utf8JsonWriter writer = new Utf8JsonWriter(16))
        //    {
        //        // Use fixed and the older version of GetBytes-in-place because of the NetFX build.
        //        unsafe
        //        {
        //            fixed (byte* dataPtr = utf8Data)
        //            fixed (char* inputPtr = overwriteJson)
        //            {
        //                // Overwrite the number in the memory buffer still referenced by the document.
        //                // If it doesn't hit a 100% overlap then we're not testing what we thought we were.
        //                Assert.Equal(
        //                    utf8Data.Length,
        //                    Encoding.UTF8.GetBytes(inputPtr, overwriteJson.Length, dataPtr, utf8Data.Length));
        //            }
        //        }

        //        JsonElement rootElement = document.RootElement;

        //        Assert.Equal(overwriteJson, rootElement.GetRawText());

        //        AssertExtensions.Throws<ArgumentException>(
        //            "utf8FormattedNumber",
        //            () => rootElement.WriteTo(writer));
        //    }
        //}

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteAsciiString(bool indented)
        {
            WriteSimpleValue(indented, "\"pizza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEscapedString(bool indented)
        {
            WriteSimpleValue(indented, "\"p\\u0069zza\"", "\"pizza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNonAsciiString(bool indented)
        {
            // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
            // therefore is ingested as the UTF-8 sequence [ C3 AD ].
            //
            // When writing it back out, the writer turns it into a JSON string
            // using escaped codepoint syntax.
            //
            // The subtlety of the input vs output is the number of backslashes (and
            // the hex casing is different to show the difference more aggressively).
            WriteSimpleValue(indented, "\"p\u00CDzza\"", "\"p\\u00cdzza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEscapedNonAsciiString(bool indented)
        {
            // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
            // therefore is ingested as the UTF-8 sequence [ C3 AD ].
            //
            // When writing it back out, the writer turns it into a JSON string
            // using escaped codepoint syntax.
            //
            // The subtlety of the input vs output is the number of backslashes (and
            // the hex casing is different to show the difference more aggressively).
            //
            // The U+007A (lowercase z) is just to make sure nothing weird happens
            // between the de-escape and the UTF-8.
            WriteSimpleValue(indented, "\"p\u00CDz\\u007Aa\"", "\"p\\u00cdzza\"");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteTrue(bool indented)
        {
            WriteSimpleValue(indented, "true");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteFalse(bool indented)
        {
            WriteSimpleValue(indented, "false");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNull(bool indented)
        {
            WriteSimpleValue(indented, "null");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyArray(bool indented)
        {
            WriteComplexValue(
                indented,
                "[        ]",
                "[]",
                "[]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyObject(bool indented)
        {
            WriteComplexValue(
                indented,
                "{     }",
                "{}",
                "{}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyCommentedArray(bool indented)
        {
            WriteComplexValue(
                indented,
                "[ /* \"No values here\" */    ]",
                "[]",
                "[]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyCommentedObject(bool indented)
        {
            WriteComplexValue(
                indented,
                "{ /* Technically empty */ }",
                "{}",
                "{}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteSimpleArray(bool indented)
        {
            WriteComplexValue(
                indented,
                @"[ 2, 4, 
6                       , 0


, 1       ]",
                @"[
  2,
  4,
  6,
  0,
  1
]",
                "[2,4,6,0,1]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteSimpleObject(bool indented)
        {
            WriteComplexValue(
                indented,
                @"{ ""r""   : 2,
// Comments make everything more interesting.
            ""d"":
2
}",
                @"{
  ""r"": 2,
  ""d"": 2
}",
                "{\"r\":2,\"d\":2}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEverythingArray(bool indented)
        {
            WriteComplexValue(
                indented,
                @"

[
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @""",
// Array with an array (primes)
[ 2, 3, 5, 7, /*9,*/ 11],
{ ""obj"": [ 21, { ""deep obj"": [
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @"""

], ""more deep"": false },
12 ], ""second property"": null }]
",
                @"[
  ""Once upon a midnight dreary"",
  42,
  1e400,
  3.141592653589793238462643383279,
  false,
  true,
  null,
  ""Escaping is not required"",
  ""Some things get lost in the m\u00eal\u00e9e"",
  [
    2,
    3,
    5,
    7,
    11
  ],
  {
    ""obj"": [
      21,
      {
        ""deep obj"": [
          ""Once upon a midnight dreary"",
          42,
          1e400,
          3.141592653589793238462643383279,
          false,
          true,
          null,
          ""Escaping is not required"",
          ""Some things get lost in the m\u00eal\u00e9e""
        ],
        ""more deep"": false
      },
      12
    ],
    ""second property"": null
  }
]",
                "[\"Once upon a midnight dreary\",42,1e400,3.141592653589793238462643383279," +
                    "false,true,null,\"Escaping is not required\"," +
                    "\"Some things get lost in the m\\u00eal\\u00e9e\",[2,3,5,7,11]," +
                    "{\"obj\":[21,{\"deep obj\":[\"Once upon a midnight dreary\",42,1e400," +
                    "3.141592653589793238462643383279,false,true,null,\"Escaping is not required\"," +
                    "\"Some things get lost in the m\\u00eal\\u00e9e\"],\"more deep\":false},12]," +
                    "\"second property\":null}]");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEverythingObject(bool indented)
        {
            WriteComplexValue(
                indented,
                "{" +
                    "\"int\": 42," +
                    "\"quadratic googol\": 1e400," +
                    "\"precisePi\": 3.141592653589793238462643383279," +
                    "\"lit0\": null,\"lit1\":  false,/*guess next*/\"lit2\": true," +
                    "\"ascii\": \"pizza\"," +
                    "\"escaped\": \"p\\u0069zza\"," +
                    "\"utf8\": \"p\u00CDzza\"," +
                    "\"utf8ExtraEscape\": \"p\u00CDz\\u007Aa\"," +
                    "\"arr\": [\"hello\", \"sa\\u0069lor\", 21, \"blackjack!\" ]," +
                    "\"obj\": {" +
                        "\"arr\": [ 1, 3, 5, 7, /*9,*/ 11] " +
                    "}}",
                @"{
  ""int"": 42,
  ""quadratic googol"": 1e400,
  ""precisePi"": 3.141592653589793238462643383279,
  ""lit0"": null,
  ""lit1"": false,
  ""lit2"": true,
  ""ascii"": ""pizza"",
  ""escaped"": ""pizza"",
  ""utf8"": ""p\u00cdzza"",
  ""utf8ExtraEscape"": ""p\u00cdzza"",
  ""arr"": [
    ""hello"",
    ""sailor"",
    21,
    ""blackjack!""
  ],
  ""obj"": {
    ""arr"": [
      1,
      3,
      5,
      7,
      11
    ]
  }
}",
                "{\"int\":42,\"quadratic googol\":1e400,\"precisePi\":3.141592653589793238462643383279," +
                    "\"lit0\":null,\"lit1\":false,\"lit2\":true,\"ascii\":\"pizza\",\"escaped\":\"pizza\"," +
                    "\"utf8\":\"p\\u00cdzza\",\"utf8ExtraEscape\":\"p\\u00cdzza\"," +
                    "\"arr\":[\"hello\",\"sailor\",21,\"blackjack!\"]," +
                    "\"obj\":{\"arr\":[1,3,5,7,11]}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void ReadWriteEscapedPropertyNames(bool indented)
        {
            const string jsonIn = " { \"p\\u0069zza\": 1, \"hello\\u003c\\u003e\": 2, \"normal\": 3 }";

            WriteComplexValue(
                indented,
                jsonIn,
                @"{
  ""pizza"": 1,
  ""hello\u003c\u003e"": 2,
  ""normal"": 3
}",
                "{\"pizza\":1,\"hello\\u003c\\u003e\":2,\"normal\":3}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "ectoplasm",
                "42",
                @"{
  ""ectoplasm"": 42
}",
                "{\"ectoplasm\":42}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberAsPropertyWithLargeName(bool indented)
        {
            var charArray = new char[300];
            charArray.AsSpan().Fill('a');
            charArray[0] = (char)0xEA;
            var propertyName = new string(charArray);

            WritePropertyValueBothForms(
                indented,
                propertyName,
                "42",
                @"{
  ""\u00EA" + propertyName.Substring(1) + @""": 42
}",
                $"{{\"\\u00EA{propertyName.Substring(1)}\":42}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberScientificAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "m\u00EAl\u00E9e",
                "1e6",
                @"{
  ""m\u00eal\u00e9e"": 1e6
}",
                "{\"m\\u00eal\\u00e9e\":1e6}");
        }

        [Fact]
        public static void WriteValueSurrogatesEscapeString()
        {
            string unicodeString = "\uD800\uDC00\uD803\uDE6D \uD834\uDD1E\uDBFF\uDFFF";
            string json = $"[\"{unicodeString}\"]";
            string expectedStr = GetEscapedExpectedString(unicodeString, Newtonsoft.Json.StringEscapeHandling.EscapeNonAscii);

            using (JsonDocument doc = JsonDocument.Parse(json, s_options))
            {
                JsonElement target = doc.RootElement[0];

                var writer = new Utf8JsonWriter(1024, new JsonWriterOptions { EscapeHandling = JsonEscapeHandling.EscapeNonAscii });
                target.WriteTo(ref writer);
                JsonTestHelper.AssertContents(expectedStr, writer.ToByteArray());
            }
        }

        private static string GetEscapedExpectedString(string value, Newtonsoft.Json.StringEscapeHandling escaping)
        {
            using (TextWriter stringWriter = new StringWriter())
            using (var json = new JsonTextWriter(stringWriter)
            {
                StringEscapeHandling = escaping
            })
            {
                json.WriteValue(value);
                json.Flush();
                return stringWriter.ToString();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberOverpreciseAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "test property",
                "3.141592653589793238462643383279",
                @"{
  ""test property"": 3.141592653589793238462643383279
}",
                "{\"test property\":3.141592653589793238462643383279}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNumberTooLargeAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                // Arabic "kabir" => "big"
                "\u0643\u0628\u064A\u0631",
                "1e400",
                @"{
  ""\u0643\u0628\u064A\u0631"": 1e400
}",
                "{\"\\u0643\\u0628\\u064A\\u0631\":1e400}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "dinner",
                "\"pizza\"",
                @"{
  ""dinner"": ""pizza""
}",
                "{\"dinner\":\"pizza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEscapedStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "dinner",
                "\"p\\u0069zza\"",
                @"{
  ""dinner"": ""pizza""
}",
                "{\"dinner\":\"pizza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNonAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "lunch",
                "\"p\u00CDzza\"",
                @"{
  ""lunch"": ""p\u00cdzza""
}",
                "{\"lunch\":\"p\\u00cdzza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEscapedNonAsciiStringAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "lunch",
                "\"p\u00CDz\\u007Aa\"",
                @"{
  ""lunch"": ""p\u00cdzza""
}",
                "{\"lunch\":\"p\\u00cdzza\"}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteTrueAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                " boolean ",
                "true",
                @"{
  "" boolean "": true
}",
                "{\" boolean \":true}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteFalseAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                " boolean ",
                "false",
                @"{
  "" boolean "": false
}",
                "{\" boolean \":false}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteNullAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "someProp",
                "null",
                @"{
  ""someProp"": null
}",
                "{\"someProp\":null}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyArrayAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "arr",
                "[        ]",
                @"{
  ""arr"": []
}",
                "{\"arr\":[]}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyObjectAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "obj",
                "{       }",
                @"{
  ""obj"": {}
}",
                "{\"obj\":{}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyCommentedArrayAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "arr",
                "[   /* 5 */     ]",
                @"{
  ""arr"": []
}",
                "{\"arr\":[]}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEmptyCommentedObjectAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "obj",
                "{ /* Technically empty */ }",
                @"{
  ""obj"": {}
}",
                "{\"obj\":{}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteSimpleArrayAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "valjean",
                "[ 2, 4, 6, 0, 1 /* Did you know that there's an asteroid: 24601 Valjean? */ ]",
                @"{
  ""valjean"": [
    2,
    4,
    6,
    0,
    1
  ]
}",
                "{\"valjean\":[2,4,6,0,1]}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteSimpleObjectAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "bestMinorCharacter",
                @"{ ""r""   : 2,
// Comments make everything more interesting.
            ""d"":
2
}",
                @"{
  ""bestMinorCharacter"": {
    ""r"": 2,
    ""d"": 2
  }
}",
                "{\"bestMinorCharacter\":{\"r\":2,\"d\":2}}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEverythingArrayAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "data",
                @"

[
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @""",
// Array with an array (primes)
[ 2, 3, 5, 7, /*9,*/ 11],
{ ""obj"": [ 21, { ""deep obj"": [
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @"""

], ""more deep"": false },
12 ], ""second property"": null }]
",
                @"{
  ""data"": [
    ""Once upon a midnight dreary"",
    42,
    1e400,
    3.141592653589793238462643383279,
    false,
    true,
    null,
    ""Escaping is not required"",
    ""Some things get lost in the m\u00eal\u00e9e"",
    [
      2,
      3,
      5,
      7,
      11
    ],
    {
      ""obj"": [
        21,
        {
          ""deep obj"": [
            ""Once upon a midnight dreary"",
            42,
            1e400,
            3.141592653589793238462643383279,
            false,
            true,
            null,
            ""Escaping is not required"",
            ""Some things get lost in the m\u00eal\u00e9e""
          ],
          ""more deep"": false
        },
        12
      ],
      ""second property"": null
    }
  ]
}",

                "{\"data\":[\"Once upon a midnight dreary\",42,1e400,3.141592653589793238462643383279," +
                    "false,true,null,\"Escaping is not required\"," +
                    "\"Some things get lost in the m\\u00eal\\u00e9e\",[2,3,5,7,11]," +
                    "{\"obj\":[21,{\"deep obj\":[\"Once upon a midnight dreary\",42,1e400," +
                    "3.141592653589793238462643383279,false,true,null,\"Escaping is not required\"," +
                    "\"Some things get lost in the m\\u00eal\\u00e9e\"],\"more deep\":false},12]," +
                    "\"second property\":null}]}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteEverythingObjectAsProperty(bool indented)
        {
            WritePropertyValueBothForms(
                indented,
                "data",
                "{" +
                    "\"int\": 42," +
                    "\"quadratic googol\": 1e400," +
                    "\"precisePi\": 3.141592653589793238462643383279," +
                    "\"lit0\": null,\"lit1\":  false,/*guess next*/\"lit2\": true," +
                    "\"ascii\": \"pizza\"," +
                    "\"escaped\": \"p\\u0069zza\"," +
                    "\"utf8\": \"p\u00CDzza\"," +
                    "\"utf8ExtraEscape\": \"p\u00CDz\\u007Aa\"," +
                    "\"arr\": [\"hello\", \"sa\\u0069lor\", 21, \"blackjack!\" ]," +
                    "\"obj\": {" +
                        "\"arr\": [ 1, 3, 5, 7, /*9,*/ 11] " +
                    "}}",
                @"{
  ""data"": {
    ""int"": 42,
    ""quadratic googol"": 1e400,
    ""precisePi"": 3.141592653589793238462643383279,
    ""lit0"": null,
    ""lit1"": false,
    ""lit2"": true,
    ""ascii"": ""pizza"",
    ""escaped"": ""pizza"",
    ""utf8"": ""p\u00cdzza"",
    ""utf8ExtraEscape"": ""p\u00cdzza"",
    ""arr"": [
      ""hello"",
      ""sailor"",
      21,
      ""blackjack!""
    ],
    ""obj"": {
      ""arr"": [
        1,
        3,
        5,
        7,
        11
      ]
    }
  }
}",
                "{\"data\":" +
                    "{\"int\":42,\"quadratic googol\":1e400,\"precisePi\":3.141592653589793238462643383279," +
                    "\"lit0\":null,\"lit1\":false,\"lit2\":true,\"ascii\":\"pizza\",\"escaped\":\"pizza\"," +
                    "\"utf8\":\"p\\u00cdzza\",\"utf8ExtraEscape\":\"p\\u00cdzza\"," +
                    "\"arr\":[\"hello\",\"sailor\",21,\"blackjack!\"]," +
                    "\"obj\":{\"arr\":[1,3,5,7,11]}}}");
        }

        [Fact]
        public static void WriteIncredibleDepth()
        {
            const int TargetDepth = 500;
            JsonDocumentOptions optionsCopy = s_options;
            optionsCopy.MaxDepth = TargetDepth + 1;
            const int SpacesPre = 12;
            const int SpacesSplit = 85;
            const int SpacesPost = 4;

            byte[] jsonIn = new byte[SpacesPre + TargetDepth + SpacesSplit + TargetDepth + SpacesPost];
            jsonIn.AsSpan(0, SpacesPre).Fill((byte)' ');
            Span<byte> openBrackets = jsonIn.AsSpan(SpacesPre, TargetDepth);
            openBrackets.Fill((byte)'[');
            jsonIn.AsSpan(SpacesPre + TargetDepth, SpacesSplit).Fill((byte)' ');
            Span<byte> closeBrackets = jsonIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit, TargetDepth);
            closeBrackets.Fill((byte)']');
            jsonIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit + TargetDepth).Fill((byte)' ');

            using (JsonDocument doc = JsonDocument.Parse(jsonIn, optionsCopy))
            {
                var writer = new Utf8JsonWriter(jsonIn.Length);
                doc.RootElement.WriteTo(ref writer);

                ReadOnlySpan<byte> formatted = writer.ToByteArray();

                Assert.Equal(TargetDepth + TargetDepth, formatted.Length);
                Assert.True(formatted.Slice(0, TargetDepth).SequenceEqual(openBrackets), "OpenBrackets match");
                Assert.True(formatted.Slice(TargetDepth).SequenceEqual(closeBrackets), "CloseBrackets match");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WritePropertyOutsideObject(bool skipValidation)
        {
            using (var doc = JsonDocument.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
            {
                JsonElement root = doc.RootElement;
                var options = new JsonWriterOptions
                {
                    SkipValidation = skipValidation,
                };

                const string CharLabel = "char";
                byte[] byteUtf8 = Encoding.UTF8.GetBytes("byte");
                var writer = new Utf8JsonWriter(1024, options);

                if (skipValidation)
                {
                    foreach (JsonElement val in root.EnumerateArray())
                    {
                        writer.WritePropertyName(CharLabel);
                        val.WriteTo(ref writer);
                        writer.WritePropertyName(CharLabel.AsSpan());
                        val.WriteTo(ref writer);
                        writer.WritePropertyName(byteUtf8);
                        val.WriteTo(ref writer);
                        writer.WritePropertyName(JsonEncodedText.Encode(CharLabel));
                        val.WriteTo(ref writer);
                    }

                    var buffer = writer.ToByteArray();

                    JsonTestHelper.AssertContents(
                        "\"char\":null,\"char\":null,\"byte\":null,\"char\":null," +
                            "\"char\":false,\"char\":false,\"byte\":false,\"char\":false," +
                            "\"char\":true,\"char\":true,\"byte\":true,\"char\":true," +
                            "\"char\":\"hi\",\"char\":\"hi\",\"byte\":\"hi\",\"char\":\"hi\"," +
                            "\"char\":5,\"char\":5,\"byte\":5,\"char\":5," +
                            "\"char\":{},\"char\":{},\"byte\":{},\"char\":{}," +
                            "\"char\":[],\"char\":[],\"byte\":[],\"char\":[]",
                        buffer);
                }
                else
                {
                    foreach (JsonElement val in root.EnumerateArray())
                    {
                        try
                        {
                            writer.WritePropertyName(CharLabel);
                            Assert.True(false);
                        }
                        catch (Exception ex)
                        {
                            Assert.IsType<InvalidOperationException>(ex);
                        }
                        try
                        {
                            writer.WritePropertyName(CharLabel.AsSpan());
                            Assert.True(false);
                        }
                        catch (Exception ex)
                        {
                            Assert.IsType<InvalidOperationException>(ex);
                        }
                        try
                        {
                            writer.WritePropertyName(byteUtf8);
                            Assert.True(false);
                        }
                        catch (Exception ex)
                        {
                            Assert.IsType<InvalidOperationException>(ex);
                        }
                        try
                        {
                            writer.WritePropertyName(JsonEncodedText.Encode(CharLabel));
                            Assert.True(false);
                        }
                        catch (Exception ex)
                        {
                            Assert.IsType<InvalidOperationException>(ex);
                        }
                    }

                    var buffer = writer.ToByteArray();

                    JsonTestHelper.AssertContents("", buffer);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WriteValueInsideObject(bool skipValidation)
        {
            using (var doc = JsonDocument.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
            {
                JsonElement root = doc.RootElement;
                var options = new JsonWriterOptions
                {
                    SkipValidation = skipValidation,
                };

                var writer = new Utf8JsonWriter(1024, options);
                writer.WriteStartObject();

                if (skipValidation)
                {
                    foreach (JsonElement val in root.EnumerateArray())
                    {
                        val.WriteTo(ref writer);
                    }

                    writer.WriteEndObject();

                    var buffer = writer.ToByteArray();
                    JsonTestHelper.AssertContents(
                        "{null,false,true,\"hi\",5,{},[]}",
                        buffer);
                }
                else
                {
                    foreach (JsonElement val in root.EnumerateArray())
                    {
                        try
                        {
                            val.WriteTo(ref writer);
                            Assert.True(false);
                        }
                        catch (Exception ex)
                        {
                            Assert.IsType<InvalidOperationException>(ex);
                        }
                    }

                    writer.WriteEndObject();

                    var buffer = writer.ToByteArray();
                    JsonTestHelper.AssertContents("{}", buffer);
                }
            }
        }

#if !NET452
        [Theory]
        [InlineData(false, "\"message\"", "\"message\"", true)]
        [InlineData(true, "\"message\"", "\"message\"", true)]
        [InlineData(false, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
        [InlineData(true, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
        [InlineData(false, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
        [InlineData(true, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
        public static void WriteWithRelaxedEscaper(bool indented, string jsonIn, string jsonOut, bool matchesRelaxedEscaping)
        {
            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                {
                    var options = new JsonWriterOptions
                    {
                        Indented = indented,
                        EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                    };

                    var writer = new Utf8JsonWriter(1024, options);

                    target.WriteTo(ref writer);

                    if (matchesRelaxedEscaping)
                    {
                        JsonTestHelper.AssertContents(jsonOut, writer.ToByteArray());
                    }
                    else
                    {
                        JsonTestHelper.AssertContentsNotEqual(jsonOut, writer.ToByteArray());
                    }
                }

                {
                    var options = new JsonWriterOptions
                    {
                        Indented = indented,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    };

                    var writer = new Utf8JsonWriter(1024, options);

                    target.WriteTo(ref writer);

                    JsonTestHelper.AssertContents(jsonOut, writer.ToByteArray());
                }
            }
        }
#endif

        private static void WriteSimpleValue(bool indented, string jsonIn, string jsonOut = null)
        {
            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                };

                var writer = new Utf8JsonWriter(1024, options);
                target.WriteTo(ref writer);
                var buffer = writer.ToByteArray();
                JsonTestHelper.AssertContents(jsonOut ?? jsonIn, buffer);
            }
        }

        private static void WriteComplexValue(
            bool indented,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            byte[] bufferOutput;
            var options = new JsonWriterOptions
            {
                Indented = indented,
                EscapeHandling = JsonEscapeHandling.EscapeNonAscii
            };

            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                var writer = new Utf8JsonWriter(1024, options);

                target.WriteTo(ref writer);

                bufferOutput = writer.ToByteArray();
                if (indented && s_replaceNewlines)
                {
                    JsonTestHelper.AssertContents(
                        expectedIndent.Replace(CompiledNewline, Environment.NewLine),
                        bufferOutput);
                }

                JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, bufferOutput);
            }

            // After reading the output and writing it again, it should be byte-for-byte identical.
            {
                string bufferString = Encoding.UTF8.GetString(bufferOutput);

                using (JsonDocument doc2 = JsonDocument.Parse(bufferString, s_options))
                {
                    var writer = new Utf8JsonWriter(1024, options);
                    try
                    {
                        doc2.RootElement.WriteTo(ref writer);
                        Assert.True(writer.WrittenSpan.SequenceEqual(bufferOutput));
                    }
                    finally
                    {
                        writer.Dispose();
                    }
                }
            }
        }

        private static void WritePropertyValueBothForms(
            bool indented,
            string propertyName,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            WritePropertyValue(
                indented,
                propertyName,
                jsonIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                propertyName.AsSpan(),
                jsonIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                Encoding.UTF8.GetBytes(propertyName ?? ""),
                jsonIn,
                expectedIndent,
                expectedMinimal);

            WritePropertyValue(
                indented,
                JsonEncodedText.Encode(propertyName.AsSpan(), JsonEscapeHandling.EscapeNonAscii),
                jsonIn,
                expectedIndent,
                expectedMinimal);
        }

        private static void WritePropertyValue(
            bool indented,
            string propertyName,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            string temp = $" [  {jsonIn}  ]";
            using (JsonDocument doc = JsonDocument.Parse(temp, s_options))
            {
                JsonElement target = doc.RootElement[0];

                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                };

                var writer = new Utf8JsonWriter(1024, options);

                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                target.WriteTo(ref writer);
                writer.WriteEndObject();

                var buffer = writer.ToByteArray();
                if (indented && s_replaceNewlines)
                {
                    JsonTestHelper.AssertContents(
                        expectedIndent.Replace(CompiledNewline, Environment.NewLine),
                        buffer);
                }

                JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private static void WritePropertyValue(
            bool indented,
            ReadOnlySpan<char> propertyName,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                };

                var writer = new Utf8JsonWriter(1024, options);

                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                target.WriteTo(ref writer);
                writer.WriteEndObject();

                var buffer = writer.ToByteArray();
                if (indented && s_replaceNewlines)
                {
                    JsonTestHelper.AssertContents(
                        expectedIndent.Replace(CompiledNewline, Environment.NewLine),
                        buffer);
                }

                JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private static void WritePropertyValue(
            bool indented,
            ReadOnlySpan<byte> propertyName,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                };

                var writer = new Utf8JsonWriter(1024, options);

                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                target.WriteTo(ref writer);
                writer.WriteEndObject();

                var buffer = writer.ToByteArray();
                if (indented && s_replaceNewlines)
                {
                    JsonTestHelper.AssertContents(
                        expectedIndent.Replace(CompiledNewline, Environment.NewLine),
                        buffer);
                }

                JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }

        private static void WritePropertyValue(
            bool indented,
            JsonEncodedText propertyName,
            string jsonIn,
            string expectedIndent,
            string expectedMinimal)
        {
            using (JsonDocument doc = JsonDocument.Parse($" [  {jsonIn}  ]", s_options))
            {
                JsonElement target = doc.RootElement[0];

                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    EscapeHandling = JsonEscapeHandling.EscapeNonAscii
                };

                var writer = new Utf8JsonWriter(1024, options);

                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                target.WriteTo(ref writer);
                writer.WriteEndObject();

                var buffer = writer.ToByteArray();
                if (indented && s_replaceNewlines)
                {
                    JsonTestHelper.AssertContents(
                        expectedIndent.Replace(CompiledNewline, Environment.NewLine),
                        buffer);
                }

                JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
            }
        }
    }
    public static class WriterHelpers
    {
        // Normalize comparisons against Json.NET.
        // Includes uppercasing the \u escaped hex characters and escaping forward slash to "\/" instead of "\u002f".
        public static string NormalizeToJsonNetFormat(this string json)
        {
            var sb = new StringBuilder(json.Length);
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] == '\\')
                {
                    sb.Append(json[i++]);

                    if (i < json.Length - 1 && json[i] == 'u')
                    {
                        sb.Append(json[i++]);

                        if (i < json.Length - 4)
                        {
                            string temp = json.Substring(i, 4).ToLowerInvariant();
                            sb.Append(temp);
                            i += 4;
                        }
                    }
                    if (i < json.Length - 1 && json[i] == '/')
                    {
                        // Convert / to u002f
                        i++;
                        sb.Append("u002f");
                    }
                }
                else
                {
                    sb.Append(json[i++]);
                }
            }

            return sb.ToString();
        }
    }
}
