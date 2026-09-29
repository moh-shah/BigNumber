using System;
using Newtonsoft.Json;

namespace Moshah.BigNumbers
{
    /// <summary>
    /// Optional Newtonsoft.Json glue for BigNumber — the only file in this module with an external
    /// dependency (BigNumber.cs itself has none). (De)serializes as the letter-suffix string ("1.5B"),
    /// which is why config JSON should author BigNumber fields as strings, not raw numbers.
    /// Register globally via JsonSerializerSettings.Converters, or drop [JsonConverter(typeof(BigNumberJsonConverter))]
    /// on individual fields.
    /// </summary>
    public class BigNumberJsonConverter : JsonConverter<BigNumber>
    {
        public override void WriteJson(JsonWriter writer, BigNumber value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString(4));
        }

        public override BigNumber ReadJson(JsonReader reader, Type objectType, BigNumber existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    return BigNumber.Parse((string)reader.Value);
                case JsonToken.Integer:
                case JsonToken.Float:
                    return new BigNumber(Convert.ToDouble(reader.Value));
                case JsonToken.Null:
                    return BigNumber.Zero;
                default:
                    throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing a BigNumber.");
            }
        }
    }
}
