using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    // This field belongs to the disk editor shard only. It is never part of the portable DTO.
    internal static class SkillProjectDiskLayout
    {
        const string Positions = "nodePositions";
        static readonly string[] GraphCollections = {"skills", "timelines", "projectiles", "areas", "buffs"};

        internal static IEnumerable<((string Kind, string GraphId, string NodeId) Key, JObject Node)> Nodes(JObject document)
        {
            foreach (string kind in GraphCollections)
            {
                if (!(document[kind] is JArray graphs)) continue;
                foreach (var graph in graphs)
                {
                    if (!(graph is JObject entry) || !(entry["id"] is JValue graphId) || graphId.Type != JTokenType.String ||
                        !(entry["nodes"] is JArray nodes)) continue;
                    foreach (var item in nodes)
                    {
                        if (item is JObject node && node["id"] is JValue id && id.Type == JTokenType.String)
                            yield return ((kind, (string)graphId, (string)id), node);
                    }
                }
            }
        }

        internal static HashSet<(string, string, string)> Restore(JObject document, bool sharded)
        {
            var positions = new Dictionary<(string, string, string), JObject>();
            if (sharded && document["editor"] is JObject editor && editor.Property(Positions) is JProperty field)
            {
                if (!(field.Value is JArray entries)) throw Invalid("Expected an array.");
                foreach (var row in entries)
                {
                    if (!(row is JObject entry) || entry.Count != 5 || entry["kind"]?.Type != JTokenType.String ||
                        entry["graphId"]?.Type != JTokenType.String || entry["nodeId"]?.Type != JTokenType.String ||
                        !IsCoordinate(entry["x"]) || !IsCoordinate(entry["y"]))
                        throw Invalid("Expected a graph identity, node identity and finite x/y coordinates.");
                    var key = ((string)entry["kind"], (string)entry["graphId"], (string)entry["nodeId"]);
                    if (positions.ContainsKey(key)) throw Invalid("Duplicate layout for " + key + ".");
                    positions.Add(key, new JObject { ["x"] = entry["x"], ["y"] = entry["y"] });
                }
                field.Remove();
                if (editor.Count == 0) document.Remove("editor");
            }

            var missing = new HashSet<(string, string, string)>();
            foreach (var entry in Nodes(document))
            {
                if (positions.TryGetValue(entry.Key, out var position)) entry.Node["position"] = position;
                if (entry.Node.Property("position") == null) missing.Add(entry.Key);
            }
            return missing;
        }

        internal static void Extract(JObject document, HashSet<(string, string, string)> missing)
        {
            var positions = new JArray();
            foreach (var entry in Nodes(document))
            {
                var field = entry.Node.Property("position");
                if (field == null) continue;
                if (missing == null || !missing.Contains(entry.Key))
                {
                    positions.Add(new JObject
                    {
                        ["kind"] = entry.Key.Kind, ["graphId"] = entry.Key.GraphId, ["nodeId"] = entry.Key.NodeId,
                        ["x"] = field.Value["x"], ["y"] = field.Value["y"]
                    });
                }
                field.Remove();
            }
            if (positions.Count == 0) return;
            if (!(document["editor"] is JObject editor)) document["editor"] = editor = new JObject();
            editor[Positions] = positions;
        }

        internal static void Strip(JObject document)
        {
            document.Remove("editor");
            foreach (var entry in Nodes(document)) entry.Node.Remove("position");
        }

        static bool IsCoordinate(JToken token)
        {
            if (token?.Type != JTokenType.Float && token?.Type != JTokenType.Integer) return false;
            float value = (float)token;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static ContentValidationException Invalid(string reason) => new ContentValidationException("$.editor." + Positions, reason);
    }
}
