using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ReplaceBeesWithBugs
{
    [BepInPlugin("ReplaceBeesWithHoardingBugs", "Replace bees with hoarding bugs", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource mls;

        private void Awake()
        {
            mls = Logger;
            new Harmony("ReplaceBeesWithHoardingBugs").PatchAll(typeof(StartOfRoundPatches));
            mls.LogInfo("Patch applied, bees will be replaced with hoarding bugs");
        }
    }

    public class StartOfRoundPatches
    {
        static EnemyType hoardingBugEnemyType;
        static int beeHiveAmount = 0;
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
        [HarmonyPostfix]
        static void GetHoardingBugEnemyType(ref SelectableLevel newLevel)
        {
            if (!GameNetworkManager.Instance.isHostingGame)
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
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                Plugin.mls.LogWarning("NOT HOST, returning");
                return;
            }

            try
            {
                RedLocustBees[] bees = Object.FindObjectsOfType<RedLocustBees>();
                foreach (RedLocustBees bee in bees)
                {
                    Vector3 beePos = bee.transform.position;
                    ((EnemyAI)bee).enemyType.canDie = true;
                    ((EnemyAI)bee).KillEnemyClientRpc(true);
                    ((EnemyAI)bee).KillEnemyOnOwnerClient(true);
                    RoundManager.Instance.SpawnEnemyGameObject(beePos, 0, 1, hoardingBugEnemyType);
                    beeHiveAmount++;
                    Plugin.mls.LogInfo($"Spawned hoarding bug to replace dead bees at position {beePos} ({beeHiveAmount})");
                }
            }
            catch
            {
                return;
            }
        }
    }
}
