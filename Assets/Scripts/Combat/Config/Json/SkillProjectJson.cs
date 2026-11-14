using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Combat.Config
{
    public static class SkillProjectJson
    {
        public static SkillProjectDocument Read(string text) => SkillJsonCodec.Read<SkillProjectDocument>(text);
        public static string Write(SkillProjectDocument value) => SkillJsonCodec.Write(value);
    }

    internal static class SkillJsonCodec
    {
        static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None, MaxDepth = 64, FloatParseHandling = FloatParseHandling.Double
        };

        public static T Read<T>(string text) where T : class
        {
            try
            {
                var token = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                return token.ToObject<T>(JsonSerializer.Create(Settings))
                    ?? throw new ContentValidationException("$", "Document is required.");
            }
            catch (JsonException error) { throw new ContentValidationException("$", error.Message); }
        }

        public static string Write(object value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

        public static string Fingerprint(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }
    }
}
