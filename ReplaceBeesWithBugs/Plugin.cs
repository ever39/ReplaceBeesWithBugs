using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ReplaceBeesWithBugs
{
    [BepInPlugin("ReplaceBeesWithHoardingBugs", "Replace bees with hoarding bugs", "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource mls;
        public static ConfigEntry<bool> killBeesOnRoundStart;
        public static ConfigEntry<bool> spawnBugsOvertime;
        public static ConfigEntry<int> minBugAmount;
        public static ConfigEntry<int> maxBugAmount;

        private void Awake()
        {
            mls = Logger;
            killBeesOnRoundStart = Config.Bind("General", "Kill bees on round start?", true, "Also spawns initial hoarding bugs!");
            spawnBugsOvertime = Config.Bind("General", "Spawn bugs over time", true, "Spawns bugs overtime based on a random interval between 30 and 120 seconds");
            minBugAmount = Config.Bind("General", "Minimum bug amount per spawn cycle", 1);
            maxBugAmount = Config.Bind("General", "Maximum bug amount per spawn cycle", 3);

            new Harmony("ReplaceBeesWithHoardingBugs").PatchAll(typeof(StartOfRoundPatches));
            mls.LogInfo("patch applied, bees will be replaced with hoarding bugs");
        }
    }

    public class StartOfRoundPatches
    {
        static bool isHost = false;
        static EnemyType hoardingBugEnemyType;
        static float bugSpawnCooldown;
        static int maxBugs;
        static int minBugs;
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
        [HarmonyPostfix]
        static void GetHoardingBugEnemyType(ref SelectableLevel newLevel)
        {
            isHost = GameNetworkManager.Instance.isHostingGame;
            maxBugs = Plugin.maxBugAmount.Value;
            minBugs = Plugin.minBugAmount.Value;
            bugSpawnCooldown = Random.Range(30, 120);

            if (!isHost)
            {
                return;
            }

            foreach (SpawnableEnemyWithRarity enemy in newLevel.Enemies)
            {
                if (enemy.enemyType.enemyPrefab.GetComponent<HoarderBugAI>() != null)
                {
                    hoardingBugEnemyType = enemy.enemyType;
                    Plugin.mls.LogInfo("got hoarding bug enemy type");
                }
            }
        }

        [HarmonyPatch(typeof(RedLocustBees), nameof(RedLocustBees.SpawnHiveClientRpc))]
        [HarmonyPostfix]
        static void ReplaceBeesWithHoardingBugs(ref RedLocustBees __instance)
        {
            if (!isHost)
            {
                Plugin.mls.LogWarning("NOT HOST, returning");
                return;
            }

            if (!Plugin.killBeesOnRoundStart.Value)
            {
                Plugin.mls.LogInfo("Not killing bees, not spawning initial bugs");
                return;
            }

            try
            {
                RedLocustBees bee = __instance;
                Vector3 beePos = bee.transform.position;
                int randomBugAmount = Random.Range(minBugs, maxBugs);

                bee.enemyType.canDie = true;
                bee.KillEnemyClientRpc(true);
                bee.KillEnemyOnOwnerClient(true);

                for (var i = 1; i <= randomBugAmount; i++)
                    RoundManager.Instance.SpawnEnemyGameObject(beePos, 0, 1, hoardingBugEnemyType);

                Plugin.mls.LogInfo($"Spawned initial hoarding bug(s) to replace dead bees at position {beePos})");
            }
            catch
            {
                return;
            }
        }

        [HarmonyPatch(typeof(RoundManager), "Update")]
        [HarmonyPostfix]
        static void RandomlySpawnBugsFromHives()
        {
            if (isHost)
            {
                bugSpawnCooldown -= Time.deltaTime;

                if (bugSpawnCooldown < 0 && Plugin.spawnBugsOvertime.Value && !StartOfRound.Instance.inShipPhase)
                {
                    Plugin.mls.LogInfo("attempting to spawn bugs randomly!");
                    GameObject[] hives = GameObject.FindGameObjectsWithTag("PhysicsProp");
                    int randomBugAmount = Random.Range(minBugs, maxBugs);

                    if (hives.Length == 0 | hives == null)
                    {
                        Plugin.mls.LogInfo("NO HIVES, returning");
                        bugSpawnCooldown = Random.Range(30, 120);
                        return;
                    }

                    foreach (GameObject hive in hives)
                    {
                        if (hive.name == "RedLocustHive(Clone)" && !hive.GetComponent<PhysicsProp>().isInShipRoom)
                        {
                            Vector3 hivePos = hive.transform.position;
                            for(var i = 1; i<=randomBugAmount; i++)
                                RoundManager.Instance.SpawnEnemyGameObject(hivePos, 0, 1, hoardingBugEnemyType);
                        }
                    }

                    Plugin.mls.LogInfo("spawned bugs at respective hives, resetting cooldown");
                    bugSpawnCooldown = Random.Range(30, 120);
                }
            }
        }
    }
}
