using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Combat.Config
{
    // Json.NET otherwise rounds fractional DTO identities and accepts numeric strings
    // before domain validation can see their original type. Integral legacy .0 values
    // remain valid; serialization still uses the ordinary integer writer.
    internal sealed class WholeIntegerConverter : JsonConverter
    {
        public override bool CanWrite=>false;
        public override bool CanConvert(Type type)=>type==typeof(int)||type==typeof(int?);
        public override object ReadJson(JsonReader reader,Type type,object existing,JsonSerializer serializer)
        {
            if(reader.TokenType==JsonToken.Null&&type==typeof(int?))return null;
            if((reader.TokenType==JsonToken.Integer||reader.TokenType==JsonToken.Float)&&
                decimal.TryParse(Convert.ToString(reader.Value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out decimal number)&&
                number>=int.MinValue&&number<=int.MaxValue&&decimal.Truncate(number)==number)return (int)number;
            throw new JsonSerializationException("Expected a whole32-bit integer at "+reader.Path+".");
        }
        public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)=>throw new NotSupportedException();
    }
}
