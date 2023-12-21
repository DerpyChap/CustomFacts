using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;

[JsonObject]
public class Fact
{
    [JsonRequired] public string text;
    [JsonRequired] public string author;
}

namespace CustomFacts
{
    [HarmonyPatch]
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Plugin Instance;
        
        private const string PLUGIN_GUID = "derpychap.realfaketrombonefacts";
        private const string PLUGIN_NAME = "Real Fake Trombone Facts";
        private const string PLUGIN_VERSION = "1.0.0";
        private const string SETFACT_METHOD_NAME = "setFact";
        private const string CONFIG_FILENAME = "facts.json";
        private const string RESOURCE_NAME = "CustomFacts.Resources.facts.json";

        private List<Fact> AllFacts = new List<Fact>();
        private bool firstRun = true;

        private void Awake()
        {
            Instance = this;

            LoadFacts();

            if (AllFacts.Count() == 0)
            {
                Logger.LogWarning("No custom facts to load!");
                Destroy(this);
                return;
            }

            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll();
        }

        private void LoadFacts()
        {
            var customFactsFilePath = Path.Combine(Paths.ConfigPath, CONFIG_FILENAME);

            if (!File.Exists(customFactsFilePath))
            {
                Logger.LogDebug("Config file not found. Creating...");
                WriteResourceToFile(RESOURCE_NAME, customFactsFilePath);
                Logger.LogDebug("Done!");
            }

            Logger.LogDebug("Loading custom facts...");
            var facts = JsonConvert.DeserializeObject<List<Fact>>(File.ReadAllText(customFactsFilePath));

            AllFacts.AddRange(facts);

            Logger.LogDebug($"Finished loading {AllFacts.Count()} custom facts!");
        }

        private void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            resource.CopyTo(file);
        }

        [HarmonyPatch(typeof(LoadController), MethodType.Constructor)]
        private static void Postfix(string[] ___tfacts)
        {
            if (!Instance.firstRun) return;

            Instance.Logger.LogDebug($"Adding {___tfacts.Length} default facts.");
            foreach (var f in ___tfacts){
                Fact singlefact = new Fact();
                singlefact.author = "holywow";
                singlefact.text = f;
                Instance.AllFacts.Add(singlefact);
            }
            Instance.firstRun = false;
        }

        [HarmonyPatch(typeof(LoadController), SETFACT_METHOD_NAME)]
        private static bool Prefix(LoadController __instance)
        {
            var index = Random.Range(0, Instance.AllFacts.Count());

            Instance.Logger.LogDebug($"Loading fact at index: {index}");
            Instance.Logger.LogDebug(Instance.AllFacts[index]);
            __instance.facttext.text = Instance.AllFacts[index].text;
            __instance.facttext.resizeTextMinSize = 1;

            return false;
        }
    }
}
