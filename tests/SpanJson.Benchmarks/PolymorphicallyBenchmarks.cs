using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using SpanJson.Benchmarks.Serializers;
using SpanJson.Shared.Fixture;
using SpanJson.Shared.Models;
using SpanJson.Formatters;
using SpanJson.Linq;
using SpanJson.Resolvers;
using SpanJson.Serialization;

namespace SpanJson.Benchmarks
{
    [Config(typeof(MyConfig))]
    //[DisassemblyDiagnoser(printIL: true, recursiveDepth: 2)]
    public class PolymorphicallyBenchmarks
    {
        private static readonly Drawing Draw;
        static PolymorphicallyBenchmarks()
        {
            Draw = new Drawing
            {
                Shapes = new Shape[]
                {
                    new Square { Size = 10 },
                    new Square { Size = 20 },
                    new Circle { Radius = 5 }
                }
            };
            ExcludeNullsOriginalCaseResolver<char>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            ExcludeNullsOriginalCaseResolver<byte>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            IncludeNullsOriginalCaseResolver<char>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            IncludeNullsOriginalCaseResolver<byte>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
        }

        [Benchmark]
        public string JsonNetPolymorphicSerializerUtf16()
        {
            var serializerPool = JsonComplexSerializer.Instance.SerializerPool;
            return serializerPool.SerializeObject(Draw);
        }

        [Benchmark]
        public string SpanJsonPolymorphicSerializerUtf16()
        {
            var token = JToken.FromPolymorphicObject(Draw);
            return JsonSerializer.Generic.Utf16.Serialize(token);
        }

        [Benchmark]
        public byte[] JsonNetPolymorphicSerializerUtf8()
        {
            var serializerPool = JsonComplexSerializer.Instance.SerializerPool;
            return serializerPool.SerializeToByteArray(Draw);
        }

        [Benchmark]
        public byte[] SpanJsonPolymorphicSerializerUtf8()
        {
            var token = JToken.FromPolymorphicObject(Draw);
            return JsonSerializer.Generic.Utf8.Serialize(token);
        }

        public class Drawing
        {
            public Drawing()
            {
                Shapes = new List<Shape>();
            }

            [JsonPolymorphically]
            public IList<Shape> Shapes { get; set; }
        }

        public abstract class Shape
        {
            public int Id { get; set; }
        }

        public class Square : Shape
        {
            public int Size { get; set; }
        }

        public class Circle : Shape
        {
            public int Radius { get; set; }
        }
    }
}