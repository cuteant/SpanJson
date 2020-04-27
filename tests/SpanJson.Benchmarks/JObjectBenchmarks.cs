using BenchmarkDotNet.Attributes;
using SpanJson.Formatters;
using SpanJson.Linq;
using SpanJson.Resolvers;
using SpanJson.Tests;
using NJObject = Newtonsoft.Json.Linq.JObject;

namespace SpanJson.Benchmarks
{
    [Config(typeof(MyConfig))]
    //[DisassemblyDiagnoser(printIL: true, recursiveDepth: 2)]
    public class JObjectBenchmarks
    {
        private static readonly NJObject JsonObj;
        private static readonly JObject SpanObj;
        static JObjectBenchmarks()
        {
            ExcludeNullsOriginalCaseResolver<char>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            ExcludeNullsOriginalCaseResolver<byte>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            IncludeNullsOriginalCaseResolver<char>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            IncludeNullsOriginalCaseResolver<byte>.TryRegisterGlobalCustomrResolver(JsonDynamicResolver.Instance);
            JsonObj = NJObject.Parse(TestSR.BasicJson);
            SpanObj = JObject.Parse(TestSR.BasicJson);
        }

        [Benchmark]
        public string SpanJObjectToString()
        {
            return SpanObj.ToString();
        }

        [Benchmark]
        public string JsonNetJObjectToString()
        {
            return JsonObj.ToString();
        }
    }
}