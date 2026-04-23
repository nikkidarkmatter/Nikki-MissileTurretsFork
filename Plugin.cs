using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using LethalLib.Modules;
using Unity.Netcode;
using UnityEngine;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;
using Object = UnityEngine.Object;

namespace MissileTurret
{
    [BepInPlugin("Finnerex.MissileTurret", "MissileTurret", "1.3.9")]
    [BepInDependency(LethalLib.Plugin.ModGUID)] 
    public class Plugin : BaseUnityPlugin
    {    
        public static SpawnableMapObject MissileTurretMapObj;
        public static GameObject MissileTurretPrefab;
        public static GameObject MissilePrefab;

        public static ManualLogSource TheLogger;
        
        // Configs
        public int MaxTurrets;
        public int MinTurrets;
        public static AnimationCurve curve;

        private void Awake()
        {
            TheLogger = Logger;
            
            Logger.LogInfo("Missile Turret Loading???");

            // Configs
            Configure();

            // Harmony patch
            this.Patch();

            // fuh real inniting
                
            string modPath = Path.GetDirectoryName(Info.Location);
            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(modPath, "missileturretassetbundle"));

            if (bundle is null)
            {
                Logger.LogError("Failed to load assets");
                return;
            }
            
            MissileTurretPrefab = bundle.LoadAsset<GameObject>("MissileTurret");
            MissilePrefab = bundle.LoadAsset<GameObject>("Missile");

            // initialize the prefabs
            MissileTurretAI ai = MissileTurretPrefab.AddComponent<MissileTurretAI>();
            
            ai.rod = MissileTurretPrefab.transform.Find("missileTurret/Mount/Rod");
            ai.rail = ai.rod.Find("Rod.001");
            ai.missile = ai.rail.Find("Cylinder").gameObject;
            
            ai.acquireTargetAudio = ai.rod.GetComponent<AudioSource>();
            ai.disableAudio = ai.rod.Find("DisableSound").GetComponent<AudioSource>();
            ai.enableAudio = ai.rod.Find("EnableSound").GetComponent<AudioSource>();
            ai.laser = ai.rod.Find("LaserLight").gameObject;

            
            MissilePrefab.AddComponent<MissileAI>();
            
            Utilities.FixMixerGroups(MissileTurretPrefab);
            Utilities.FixMixerGroups(MissilePrefab);
            
            NetworkPrefabs.RegisterNetworkPrefab(MissileTurretPrefab);
            NetworkPrefabs.RegisterNetworkPrefab(MissilePrefab);


            if (MinTurrets >= MaxTurrets)
            {
                curve = new AnimationCurve(new Keyframe(0, MinTurrets, 0f, 0f),
                new Keyframe(1, MaxTurrets, 0f, 0f));
            }   // Added in case MinTurrets and MaxTurrets are equal to force it to honor it instead of generating a weird curve
            else
            {
                curve = new AnimationCurve(new Keyframe(0, MinTurrets, 0.267f, 0.267f, 0, 0.246f),
                new Keyframe(1, MaxTurrets, 61, 61, 0.015f * MaxTurrets, 0));
            }   // Old spawn curve takes over elsewise

            MissileTurretMapObj = new SpawnableMapObject
            {
                prefabToSpawn = MissileTurretPrefab,
                spawnFacingAwayFromWall = true,
                numberToSpawn = curve
            };

            MapObjects.RegisterMapObject(MissileTurretMapObj, Levels.LevelTypes.All, _ => curve);

            Logger.LogInfo("Missile Turret Loaded!!!");

        }

        private void Patch()
        {
            Harmony harmony = new Harmony("Finnerex.MissileTurret");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnMapObjects")]
        [HarmonyPriority(Priority.Low)]
        public static class Patch_SpawnMapObjects
        {
            static void Prefix()
            {
                RandomMapObject[] array = FindObjectsOfType<RandomMapObject>();

                foreach (RandomMapObject randomMapObject in array)
                {
                    // if it doesn't contain MissileTurret, skip
                    if (!randomMapObject.spawnablePrefabs.Any(x => x.name.StartsWith("MissileTurret")))
                    {
                        continue;
                    }
                    // if it contains vanilla TurretContainer, skip
                    if (randomMapObject.spawnablePrefabs.Any(x => x.name.StartsWith("TurretContainer")))
                    {
                        continue;
                    }
                    // if it has MissileTurret and doesn't contain TurretContainer, remove MissileTurret
                    else
                    {
                        randomMapObject.spawnablePrefabs.RemoveAll(x => x.name.StartsWith("MissileTurret"));
                    }
                    // A missile turret has fallen into a vent in Lego City
                    // Quick, build the rescue patcher
                    // https://media.tenor.com/x5XHcKYpO3wAAAPo/hey-a-man-has-fallen-into-a-river-in-lego-city.mp4
                }
            }
        }

        private void Configure()
        {
            
            MaxTurrets = Config.Bind<int>(new ConfigDefinition("Spawn Options", "Max Turrets"), 6,
                new ConfigDescription("Maximum number of turrets that can be spawned")).Value;
            MinTurrets = Config.Bind<int>(new ConfigDefinition("Spawn Options", "Min Turrets"), 0,
                new ConfigDescription("Minimum number of turrets that can be spawned")).Value;
            
            
            MissileAI.MaxSpeed = Config.Bind<float>(new ConfigDefinition("Missile Options", "Max Speed"), 0.7f,
                new ConfigDescription("Maximum speed of a missile")).Value * 100;
            MissileAI.MaxTurnSpeed = Config.Bind<float>(new ConfigDefinition("Missile Options", "Turn Rate"), 0.6f,
                new ConfigDescription("How fast the missile can turn")).Value;
            MissileAI.Acceleration = Config.Bind<float>(new ConfigDefinition("Missile Options", "Acceleration"), 0.6f,
                new ConfigDescription("Acceleration of the missile")).Value * 100;
            
            MissileAI.KillRange = Config.Bind<float>(new ConfigDefinition("Missile Options", "Explosive Kill Range"), 2f,
                new ConfigDescription("Distance from explosion to kill")).Value;
            MissileAI.DamageRange = Config.Bind<float>(new ConfigDefinition("Missile Options", "Explosive Damage Range"), 5f,
                new ConfigDescription("Distance from explosion to damage")).Value;
            
            
            MissileTurretAI.RotationRange = Config.Bind<float>(
                new ConfigDefinition("Missile Turret Options", "Rotation Range"),45f,
                new ConfigDescription("The angle the turret\'s search is restricted to in degrees left & right")).Value;
            
            MissileTurretAI.RotationSpeed = Config.Bind<float>(
                new ConfigDefinition("Missile Turret Options", "Rotation Rate"), 0.25f,
                new ConfigDescription("The speed at which the turret rotates")).Value * 100;
            
            MissileTurretAI.ReloadTimeSeconds = Config.Bind<float>(
                new ConfigDefinition("Missile Turret Options", "Reload Time"), 6f,
                new ConfigDescription("The time it takes for the turret to reload in seconds")).Value;
            
            MissileTurretAI.ChargeTimeSeconds = Config.Bind<float>(
                new ConfigDefinition("Missile Turret Options", "Charge Time"), 0.5f,
                new ConfigDescription("The time it takes for the turret to shoot at a target in seconds")).Value;

        }
        
        
    }
}
