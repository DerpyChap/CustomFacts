using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

[JsonObject]
public class Fact
{
    [JsonRequired] public string fact;
    [JsonRequired] public string author;
    public int up = 0;
    public int down = 0;
    public int delta = 0;
    public long message_id = 0;
    public float time = 0.0f;
}

// 😐
[JsonObject]
public class DownloadedFacts
{
    [JsonRequired] public List<Fact> facts;
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
        private const string FACTS_FILENAME = "facts.json";
        private const string API_URL = "https://guardelo.la/api/facts/all/";
        public ConfigEntry<bool> basegameFacts;
        public ConfigEntry<bool> loadFactsFile;
        public ConfigEntry<bool> downloadFacts;
        public ConfigEntry<int> minimumUpvotes;
        public ConfigEntry<int> minimumDelta;

        private List<Fact> AllFacts = new List<Fact>();
        private bool firstRun = true;

        private void Awake()
        {
            var customFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "facts.cfg"), true);
            basegameFacts = customFile.Bind("General", "Enable Base Game Facts", true, "Controls if facts from the base game should be included in the rotation.");
            loadFactsFile = customFile.Bind("General", "Load Facts From File", true, "Controls if facts should be loaded from the mod's curated facts.json file.");
            downloadFacts = customFile.Bind("Web", "Download Latest Facts", true, "If enabled, will download the latest facts from the #real-fake-trombone-facts channel on the Modding Discord.");
            minimumUpvotes = customFile.Bind("Web", "Mininum Upvotes", 1, "The minimum amount of upvotes needed before a fact from the Discord is added to the rotation.");
            minimumDelta = customFile.Bind("Web", "Minimum Vote Delta", 3, "The minimum vote delta (the difference between upvotes and downvotes) before a fact is added to the rotation.");

            Instance = this;

            if (loadFactsFile.Value == true) LoadFacts();
            if (downloadFacts.Value == true) StartCoroutine(DownloadFacts());

            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll();
        }

        private IEnumerator DownloadFacts()
        {
            Logger.LogInfo("Downloading facts...");
            using (UnityWebRequest request = UnityWebRequest.Get(API_URL))
            {
                yield return request.SendWebRequest();

                if (request.isHttpError || request.isNetworkError)
                {
                    Logger.LogError("Failed to download facts from the API!");
                }
                else
                {
                    Logger.LogDebug("Downloaded facts!");
                    var downloaded = JsonConvert.DeserializeObject<DownloadedFacts>(request.downloadHandler.text);
                    var count = 0;
                    foreach (var fact in downloaded.facts)
                    {
                        if (fact.delta < minimumDelta.Value || fact.up < minimumUpvotes.Value) continue;
                        if (AllFacts.Any(a => a.message_id == fact.message_id)) continue;
                        count++;
                        AllFacts.Add(fact);
                    }
                    Logger.LogInfo($"Adding {count} facts from the interwebs...");
                }
            }

        }

        private void LoadFacts()
        {
            var customFactsFilePath = Path.Combine(Paths.ConfigPath, FACTS_FILENAME);

            if (!File.Exists(customFactsFilePath))
            {
                return;
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
            if (!Instance.firstRun || !Instance.basegameFacts.Value) return;

            Instance.Logger.LogDebug($"Adding {___tfacts.Length} default facts.");
            foreach (var f in ___tfacts){
                Fact singlefact = new Fact
                {
                    author = null,
                    fact = f
                };
                Instance.AllFacts.Add(singlefact);
            }
            Instance.firstRun = false;
        }

        [HarmonyPatch(typeof(LoadController), SETFACT_METHOD_NAME)]
        private static bool Prefix(LoadController __instance)
        {
            var count = Instance.AllFacts.Count();
            if (count == 0)
            {
                Instance.Logger.LogWarning("There are no facts to load!");
                __instance.facttext.text = "<size=20%>But nobody came...</size>";
                __instance.facttext.resizeTextMinSize = 1;
            }
            else
            {
                var index = Random.Range(0, Instance.AllFacts.Count());

                Instance.Logger.LogDebug($"Loading fact at index: {index}");
                Instance.Logger.LogDebug(Instance.AllFacts[index]);
                __instance.facttext.text = Instance.AllFacts[index].fact;
                __instance.facttext.resizeTextMinSize = 1;
            }

            return false;
        }
    }
}
