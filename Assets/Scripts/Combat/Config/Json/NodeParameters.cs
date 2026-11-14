using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    internal sealed class NodeParameters
    {
        readonly JObject _values;
        readonly string _path;
        public NodeParameters(SkillNodeDocument node, string path, params string[] allowed)
        {
            _path = path + ".parameters";
            _values = node.Parameters ?? throw new ContentValidationException(_path, "Object is required.");
            var names = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (var property in _values.Properties())
                if (!names.Contains(property.Name)) throw new ContentValidationException(_path + "." + property.Name, "Unknown parameter.");
        }
        public float Number(string name, float fallback, float min, float max)
        {
            if (!_values.TryGetValue(name, out var value)) return fallback;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) Fail(name, "Number is required.");
            double number = value.Value<double>();
            if (double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max)
                Fail(name, "Expected finite number in [" + min + ", " + max + "].");
            return (float)number;
        }
        public int Integer(string name, int fallback, int min, int max)
        {
            float number = Number(name, fallback, min, max);
            if (number != Math.Floor(number)) Fail(name, "Integer is required.");
            return (int)number;
        }
        public bool Boolean(string name, bool fallback)
        {
            if (!_values.TryGetValue(name, out var value)) return fallback;
            if (value.Type != JTokenType.Boolean) Fail(name, "Boolean is required.");
            return value.Value<bool>();
        }
        public string Choice(string name, string fallback, params string[] allowed)
        {
            if (!_values.TryGetValue(name, out var value)) return fallback;
            if (value.Type != JTokenType.String || Array.IndexOf(allowed, value.Value<string>()) < 0)
                Fail(name, "Expected one of: " + string.Join(", ", allowed));
            return value.Value<string>();
        }
        public string Text(string name,string fallback="")
        {
            if(!_values.TryGetValue(name,out var value))return fallback;
            if(value.Type!=JTokenType.String||string.IsNullOrWhiteSpace(value.Value<string>())||value.Value<string>().Length>128)Fail(name,"Expected nonempty identity up to128 characters.");
            return value.Value<string>();
        }
        void Fail(string name, string reason) => throw new ContentValidationException(_path + "." + name, reason);
    }
}
