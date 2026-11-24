using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Combat.Config
{
    // Newtonsoft's default integer conversion rounds fractional tokens. Protocol integers
    // must reject that coercion before DTO validation loses the original token type.
    public sealed class NullableIntegerConverter : JsonConverter<int?>
    {
        public override bool CanWrite=>false;
        public override int? ReadJson(JsonReader reader,Type type,int? existing,bool hasExisting,JsonSerializer serializer)
        {
            if(reader.TokenType==JsonToken.Null)return null;
            if(reader.TokenType!=JsonToken.Integer)throw new JsonSerializationException(reader.Path+": Expected an integer or null.");
            try{return checked((int)Convert.ToInt64(reader.Value,CultureInfo.InvariantCulture));}
            catch(Exception error) when(error is OverflowException||error is FormatException)
            {throw new JsonSerializationException(reader.Path+": Integer is outside the supported range.",error);}
        }
        public override void WriteJson(JsonWriter writer,int? value,JsonSerializer serializer)=>writer.WriteValue(value);
    }
}
