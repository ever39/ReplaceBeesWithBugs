using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ReplaceBeesWithBugs
{
    [BepInPlugin("ReplaceBeesWithHoardingBugs", "Replace bees with hoarding bugs", "1.0.0")]
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
            maxBugAmount = Config.Bind("General", "Maximum bug amount per spawn cycle", 5);

            new Harmony("ReplaceBeesWithHoardingBugs").PatchAll(typeof(StartOfRoundPatches));
            mls.LogInfo("patch applied, bees will be replaced with hoarding bugs");
        }
    }

    public class StartOfRoundPatches
    {
        static bool isHost = false;
        static EnemyType hoardingBugEnemyType;
        static int beeHiveAmount = 0;
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

            if (!isHost)
            {
                return;
            }

            foreach (SpawnableEnemyWithRarity enemy in newLevel.Enemies)
            {
                if (enemy.enemyType.enemyPrefab.GetComponent<HoarderBugAI>() != null)
                {
                    hoardingBugEnemyType = enemy.enemyType;
                    beeHiveAmount = 0;
                    Plugin.mls.LogInfo("got hoarding bug enemy type");
                }
            }
        }

        [HarmonyPatch(typeof(RedLocustBees), nameof(RedLocustBees.SpawnHiveClientRpc))]
        [HarmonyPostfix]
        static void ReplaceBeesWithHoardingBugs()
        {
            if (!isHost)
            {
                Plugin.mls.LogWarning("NOT HOST, returning");
                return;
            }

            bugSpawnCooldown = UnityEngine.Random.Range(20, 120);

            if (!Plugin.killBeesOnRoundStart.Value)
            {
                Plugin.mls.LogInfo("Not killing bees, not spawning initial bugs");
                return;
            }

            try
            {
                RedLocustBees[] bees = Object.FindObjectsOfType<RedLocustBees>();
                if (bees != null)
                {
                    foreach (RedLocustBees bee in bees)
                    {
                        Vector3 beePos = bee.transform.position;
                        ((EnemyAI)bee).enemyType.canDie = true;
                        ((EnemyAI)bee).KillEnemyClientRpc(true);
                        ((EnemyAI)bee).KillEnemyOnOwnerClient(true);

                        RoundManager.Instance.SpawnEnemyGameObject(beePos, 0, UnityEngine.Random.Range(minBugs, maxBugs), hoardingBugEnemyType);
                        beeHiveAmount++;
                        Plugin.mls.LogInfo($"Spawned initial hoarding bug to replace dead bees at position {beePos} ({beeHiveAmount})");
                    }
                }
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

                if (bugSpawnCooldown < 0 && Plugin.spawnBugsOvertime.Value)
                {
                    GameObject[] hives = GameObject.FindGameObjectsWithTag("PhysicsProp");

                    if (hives.Length == 0 | hives == null)
                    {
                        Plugin.mls.LogInfo("NO HIVES, returning");
                        bugSpawnCooldown = UnityEngine.Random.Range(20, 120);
                        return;
                    }

                    foreach (GameObject hive in hives)
                    {
                        if (hive.name == "RedLocustHive(Clone)" && !hive.GetComponent<PhysicsProp>().isInShipRoom)
                        {
                            Vector3 hivePos = hive.transform.position;
                            RoundManager.Instance.SpawnEnemyGameObject(hivePos, 0, UnityEngine.Random.Range(minBugs, maxBugs), hoardingBugEnemyType);
                        }
                    }

                    bugSpawnCooldown = UnityEngine.Random.Range(20, 120);
                }
            }
        }
    }
}
