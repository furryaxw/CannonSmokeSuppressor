using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;
using ExhaustEffect = Il2CppSprocket.Vehicles.Exhausts.ExhaustEffect;
using MuzzleFlashEffect = Il2CppSprocket.Vehicles.Fires.MuzzleFlashEffect;

[assembly: MelonInfo(
    typeof(CannonSmokeSuppressor.SmokeAccumulationSuppressorMod),
    "Smoke Accumulation Suppressor",
    "2.0.0",
    "furryAxw")]
[assembly: MelonGame("HD", "Sprocket")]

namespace CannonSmokeSuppressor
{
    public sealed class SmokeAccumulationSuppressorMod : MelonMod
    {
        private const string DiagnosticPrefix =
            "[DEBUG-smoke-accumulation-suppressor-2.0.0]";
        private readonly HashSet<string> loggedOutputMappings =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> loggedFailures =
            new(StringComparer.Ordinal);

        internal static SmokeAccumulationSuppressorMod? Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            if (SmokeAccumulationOutputMap.TryValidateCapturedEvidence(
                    out string failure,
                    out int sampleCount))
            {
                LoggerInstance.Msg(
                    $"{DiagnosticPrefix} output-map passed samples={sampleCount}");
            }
            else
            {
                LoggerInstance.Error(
                    $"{DiagnosticPrefix} output-map failed samples={sampleCount}," +
                    $"failure={failure}");
            }

            LoggerInstance.Msg(
                $"{DiagnosticPrefix} enabled " +
                "nativeOutputs=ExhaustSmoke/System-(5)," +
                "MediumCannonFire/System-(14)," +
                "normalSmokeRetained=true,scenePolling=false");
        }

        public override void OnDeinitializeMelon()
        {
            Instance = null;
            loggedOutputMappings.Clear();
            loggedFailures.Clear();
        }

        internal void SuppressAccumulationOutput(
            Component effect,
            string category)
        {
            try
            {
                if (effect == null)
                    return;

                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                Renderer? renderer = visualEffect.GetComponent<Renderer>();
                if (renderer == null)
                    return;

                string assetName = visualEffect.visualEffectAsset.name ?? string.Empty;
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    return;

                bool changed = false;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material? material = materials[materialIndex];
                    string materialName = material?.name ?? string.Empty;
                    if (!SmokeAccumulationOutputMap.IsAccumulationOutput(
                            assetName,
                            materialName))
                    {
                        continue;
                    }

                    materials[materialIndex] = null;
                    changed = true;
                    string mappingKey = $"{assetName}|{materialName}";
                    if (loggedOutputMappings.Add(mappingKey))
                    {
                        LoggerInstance.Msg(
                            $"{DiagnosticPrefix} suppressed " +
                            $"category={category},asset={assetName}," +
                            $"materialIndex={materialIndex},material={materialName}," +
                            "normalSmokeRetained=true");
                    }
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
            catch (Exception exception)
            {
                string failureKey = $"{category}|{exception.GetType().FullName}";
                if (loggedFailures.Add(failureKey))
                {
                    LoggerInstance.Error(
                        $"{DiagnosticPrefix} suppress-failed " +
                        $"category={category},error={exception}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(ExhaustEffect), nameof(ExhaustEffect.PlayEffect))]
    internal static class ExhaustAccumulationSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ExhaustEffect __instance)
        {
            SmokeAccumulationSuppressorMod.Instance?
                .SuppressAccumulationOutput(__instance, "engine-exhaust");
        }
    }

    [HarmonyPatch(typeof(MuzzleFlashEffect), nameof(MuzzleFlashEffect.Setup))]
    internal static class MuzzleAccumulationSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MuzzleFlashEffect __instance)
        {
            SmokeAccumulationSuppressorMod.Instance?
                .SuppressAccumulationOutput(__instance, "muzzle-flash");
        }
    }
}
