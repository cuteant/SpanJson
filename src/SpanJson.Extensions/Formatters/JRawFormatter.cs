using SpanJson.Linq;
using SpanJson.Internal;

namespace SpanJson.Formatters
{
    public sealed class JRawFormatter<TValue> : JTokenFormatterBase<TValue>
        where TValue : JRaw
    {
        public static readonly JRawFormatter<TValue> Default = new JRawFormatter<TValue>();

        public override void Serialize(ref JsonWriter<byte> writer, TValue value, IJsonFormatterResolver<byte> resolver)
        {
            if (value is null || value.Value is null)
            {
                writer.WriteUtf8Null();
                return;
            }

            switch (value.Value)
            {
                case byte[] utf8Json:
                    writer.WriteUtf8Verbatim(utf8Json);
                    break;

                default:
                    writer.WriteUtf8Verbatim(TextEncodings.UTF8NoBOM.GetBytes(value.Value.ToString()));
                    break;
            }
        }

        public override void Serialize(ref JsonWriter<char> writer, TValue value, IJsonFormatterResolver<char> resolver)
        {
            if (value is null || value.Value is null)
            {
                writer.WriteUtf16Null();
                return;
            }

            switch (value.Value)
            {
                case byte[] utf8Json:
                    writer.WriteUtf16Verbatim(TextEncodings.Utf8.GetString(utf8Json));
                    break;

                default:
                    writer.WriteUtf16Verbatim(value.Value.ToString());
                    break;
            }
        }
    }
}
