using System;
using System.Collections.Generic;
using Il2CppSprocket.Vehicles;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(
    typeof(CannonSmokeSuppressor.CannonSmokeSuppressorMain),
    "Muzzle Smoke Suppressor",
    "1.2.2",
    "furryAxw")]
[assembly: MelonGame("HD", "Sprocket")]

namespace CannonSmokeSuppressor
{
    public sealed class CannonSmokeSuppressorMain : MelonMod
    {
        private const string CannonObjectName = "cannon";
        private const string EffectObjectName = "CannonMuzzleFlashEffect(Clone)";

        // Axis-aligned world-space box centered on each effect.
        private const float SuppressionBoxSize = 1.0f;
        private const int MaxEffectsPerArea = 5;
        private const int LatestEffectsToKeep = 5;
        private const float CannonDiscoveryInterval = 0.5f;
        private const float EffectScanInterval = 0.05f;

        private readonly Dictionary<int, Transform> trackedCannons = new();
        private readonly Dictionary<int, EffectRecord> trackedEffects = new();
        private readonly List<EffectRecord> currentEffects = new();
        private readonly List<EffectRecord> nearbyEffects = new();
        private readonly HashSet<int> seenEffectIds = new();
        private readonly HashSet<int> suppressedEffectIds = new();
        private readonly HashSet<int> retainedEffectIds = new();
        private readonly HashSet<int> liveCannonIds = new();
        private readonly List<int> staleCannonIds = new();
        private readonly List<int> staleEffectIds = new();
        private readonly List<Transform> hierarchyStack = new();
        private readonly List<EffectRecord> newestEffects = new(LatestEffectsToKeep);

        private float nextCannonDiscoveryTime;
        private float nextEffectScanTime;
        private long nextEffectSequence;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg(
                $"Enabled. Tracking '{CannonObjectName}' children and keeping the latest " +
                $"{LatestEffectsToKeep} muzzle smoke effects within a " +
                $"{SuppressionBoxSize:0.##} x {SuppressionBoxSize:0.##} x {SuppressionBoxSize:0.##} world-unit box.");
        }

        public override void OnUpdate()
        {
            float now = Time.unscaledTime;

            if (now >= nextCannonDiscoveryTime)
            {
                nextCannonDiscoveryTime = now + CannonDiscoveryInterval;
                DiscoverCannons();
            }

            if (now < nextEffectScanTime)
                return;

            nextEffectScanTime = now + EffectScanInterval;
            ScanEffects();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            trackedCannons.Clear();
            trackedEffects.Clear();
            nextCannonDiscoveryTime = 0.0f;
            nextEffectScanTime = 0.0f;
        }

        private void DiscoverCannons()
        {
            try
            {
                liveCannonIds.Clear();
                Scene activeScene = SceneManager.GetActiveScene();
                var vehicles = UnityEngine.Object.FindObjectsOfType<Vehicle>();

                foreach (var vehicle in vehicles)
                {
                    if (vehicle == null || vehicle.gameObject == null)
                        continue;

                    GameObject vehicleObject = vehicle.gameObject;
                    if (vehicleObject.scene.handle != activeScene.handle)
                        continue;

                    FindCannonsInVehicle(vehicle.transform);
                }

                staleCannonIds.Clear();
                foreach (int instanceId in trackedCannons.Keys)
                {
                    if (!liveCannonIds.Contains(instanceId))
                        staleCannonIds.Add(instanceId);
                }

                foreach (int instanceId in staleCannonIds)
                    trackedCannons.Remove(instanceId);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Cannon discovery failed: {ex}");
            }
        }

        private void FindCannonsInVehicle(Transform vehicleRoot)
        {
            if (vehicleRoot == null)
                return;

            hierarchyStack.Clear();
            hierarchyStack.Add(vehicleRoot);

            while (hierarchyStack.Count > 0)
            {
                int lastIndex = hierarchyStack.Count - 1;
                Transform current = hierarchyStack[lastIndex];
                hierarchyStack.RemoveAt(lastIndex);

                if (current == null || current.gameObject == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (string.Equals(currentObject.name, CannonObjectName, StringComparison.OrdinalIgnoreCase))
                {
                    int instanceId = currentObject.GetInstanceID();
                    liveCannonIds.Add(instanceId);
                    trackedCannons[instanceId] = current;
                    continue;
                }

                for (int childIndex = current.childCount - 1; childIndex >= 0; childIndex--)
                    hierarchyStack.Add(current.GetChild(childIndex));
            }
        }

        private void ScanEffects()
        {
            currentEffects.Clear();
            seenEffectIds.Clear();

            try
            {
                foreach (var pair in trackedCannons)
                {
                    Transform cannon = pair.Value;
                    if (cannon == null || cannon.gameObject == null || !cannon.gameObject.activeInHierarchy)
                        continue;

                    int childCount = cannon.childCount;
                    for (int childIndex = 0; childIndex < childCount; childIndex++)
                    {
                        Transform child = cannon.GetChild(childIndex);
                        if (child == null)
                            continue;

                        GameObject gameObject = child.gameObject;
                        if (gameObject == null ||
                            !gameObject.activeInHierarchy ||
                            !string.Equals(gameObject.name, EffectObjectName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        int instanceId = gameObject.GetInstanceID();
                        if (!seenEffectIds.Add(instanceId))
                            continue;

                        if (!trackedEffects.TryGetValue(instanceId, out var effect) ||
                            effect.Transform == null)
                        {
                            effect = new EffectRecord(child, ++nextEffectSequence);
                            trackedEffects[instanceId] = effect;
                        }

                        currentEffects.Add(effect);
                    }
                }

                RemoveStaleEffects();
            SuppressExcessEffects();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Muzzle smoke scan failed: {ex}");
            }
        }

        private void RemoveStaleEffects()
        {
            staleEffectIds.Clear();
            foreach (int instanceId in trackedEffects.Keys)
            {
                if (!seenEffectIds.Contains(instanceId))
                    staleEffectIds.Add(instanceId);
            }

            foreach (int instanceId in staleEffectIds)
                trackedEffects.Remove(instanceId);
        }

        private void SuppressExcessEffects()
        {
            suppressedEffectIds.Clear();
            float halfBoxSize = SuppressionBoxSize * 0.5f;

            for (int i = 0; i < currentEffects.Count; i++)
            {
                EffectRecord anchor = currentEffects[i];
                if (suppressedEffectIds.Contains(anchor.InstanceId) || !IsAlive(anchor.Transform))
                    continue;

                nearbyEffects.Clear();
                Vector3 anchorPosition = anchor.Transform.position;

                for (int j = 0; j < currentEffects.Count; j++)
                {
                    EffectRecord candidate = currentEffects[j];
                    if (suppressedEffectIds.Contains(candidate.InstanceId) || !IsAlive(candidate.Transform))
                        continue;

                    Vector3 offset = anchorPosition - candidate.Transform.position;
                    if (Mathf.Abs(offset.x) > halfBoxSize ||
                        Mathf.Abs(offset.y) > halfBoxSize ||
                        Mathf.Abs(offset.z) > halfBoxSize)
                    {
                        continue;
                    }

                    nearbyEffects.Add(candidate);
                }

                if (nearbyEffects.Count <= MaxEffectsPerArea)
                    continue;

                SelectRetainedEffects(nearbyEffects);

                foreach (var effect in nearbyEffects)
                {
                    if (retainedEffectIds.Contains(effect.InstanceId))
                        continue;

                    if (suppressedEffectIds.Add(effect.InstanceId))
                        Suppress(effect);
                }
            }
        }

        private void SelectRetainedEffects(List<EffectRecord> effects)
        {
            retainedEffectIds.Clear();
            newestEffects.Clear();

            foreach (var effect in effects)
                InsertNewest(effect, newestEffects);

            foreach (var effect in newestEffects)
                retainedEffectIds.Add(effect.InstanceId);
        }

        private static void InsertNewest(EffectRecord effect, List<EffectRecord> newest)
        {
            int index = 0;
            while (index < newest.Count && newest[index].Sequence > effect.Sequence)
                index++;

            newest.Insert(index, effect);

            if (newest.Count > LatestEffectsToKeep)
                newest.RemoveAt(newest.Count - 1);
        }

        private void Suppress(EffectRecord effect)
        {
            try
            {
                if (!IsAlive(effect.Transform))
                    return;

                effect.Transform.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to disable old muzzle smoke {effect.InstanceId}: {ex.Message}");
            }
        }

        private static bool IsAlive(Transform transform)
        {
            return transform != null && transform.gameObject != null;
        }

        private sealed class EffectRecord
        {
            public EffectRecord(Transform transform, long sequence)
            {
                Transform = transform;
                InstanceId = transform.gameObject.GetInstanceID();
                Sequence = sequence;
            }

            public Transform Transform { get; }
            public int InstanceId { get; }
            public long Sequence { get; }
        }
    }
}
