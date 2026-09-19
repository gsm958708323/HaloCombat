using System;
using System.Collections.Generic;
using Combat.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>
    /// Unity mints a MonoScript only for the class whose name matches the class Unity picked as
    /// the file's primary class. A ScriptableObject class without one is created happily in
    /// memory, but serialises as m_Script: 0 and comes back null after the next domain reload —
    /// which is exactly how the Arena ended up with timelines whose payloads baked to null
    /// effects and dealt no damage. Keep every effect class in a file of its own.
    /// </summary>
    public sealed class EffectAssetScriptTests
    {
        [Test]
        public void EveryEffectAssetClassCanBeSerialised()
        {
            var offenders = new List<string>();
            int scanned = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("Combat.", StringComparison.Ordinal)) continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    if (type.IsAbstract || !typeof(EffectAsset).IsAssignableFrom(type)) continue;
                    scanned++;
                    if (!CanBeSerialised(type)) offenders.Add(type.Name + " (" + name + ")");
                }
            }

            Assert.Greater(scanned, 0, "No EffectAsset class was found, so this guard is not running.");
            Assert.IsEmpty(
                offenders,
                "These EffectAsset classes have no MonoScript, so an asset using one lands on disk "
                    + "with m_Script: 0 and bakes to a null effect after a domain reload. Move each "
                    + "class into its own .cs file named after it: " + string.Join(", ", offenders));
        }

        static bool CanBeSerialised(Type assetType)
        {
            var probe = ScriptableObject.CreateInstance(assetType);
            if (probe == null) return false;
            var script = MonoScript.FromScriptableObject(probe);
            bool ok = script != null && script.GetClass() == assetType;
            UnityEngine.Object.DestroyImmediate(probe);
            return ok;
        }
    }
}
