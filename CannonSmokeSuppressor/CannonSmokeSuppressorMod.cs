using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;
using ExhaustEffect = Il2CppSprocket.Vehicles.Exhausts.ExhaustEffect;
using MuzzleFlashEffect = Il2CppSprocket.Vehicles.Fires.MuzzleFlashEffect;

[assembly: MelonInfo(
    typeof(CannonSmokeSuppressor.CannonSmokeSuppressorMain),
    "Muzzle Smoke Suppressor",
    "2.5.0",
    "furryAxw")]
[assembly: MelonGame("HD", "Sprocket")]

namespace CannonSmokeSuppressor
{
    public sealed class CannonSmokeSuppressorMain : MelonMod
    {
        private const string DiagnosticPrefix =
            "[CannonSmokeSuppressor]";
        private readonly HashSet<string> loggedFailures =
            new(StringComparer.Ordinal);
        private bool nativeWritesEnabled;

        internal static CannonSmokeSuppressorMain? Instance
        {
            get;
            private set;
        }

        public override void OnInitializeMelon()
        {
            Instance = this;
            if (SmokeAccumulationOutputMap.TryValidateCapturedEvidence(
                    out string failure,
                    out int sampleCount))
            {
                LoggerInstance.Msg(
                    $"{DiagnosticPrefix} output-map passed " +
                    $"samples={sampleCount}," +
                    "muzzleAccumulationMaterialConfirmed=false");
            }
            else
            {
                LoggerInstance.Error(
                    $"{DiagnosticPrefix} output-map failed " +
                    $"samples={sampleCount},failure={failure}");
            }

            bool expressionMapPassed =
                SmokeNativeExpressionMap.TryValidateCapturedEvidence(
                    out failure,
                    out sampleCount);
            if (expressionMapPassed)
            {
                LoggerInstance.Msg(
                    $"{DiagnosticPrefix} expression-map passed " +
                    $"samples={sampleCount}," +
                    "source=compiled-VisualEffectAsset");
            }
            else
            {
                LoggerInstance.Error(
                    $"{DiagnosticPrefix} expression-map failed " +
                    $"samples={sampleCount},failure={failure}");
            }

            bool nativeGuardPassed =
                VfxNativeExpressionOverride.TryInitialize(
                    out string nativeGuardResult);
            nativeWritesEnabled =
                expressionMapPassed && nativeGuardPassed;
            if (nativeWritesEnabled)
            {
                LoggerInstance.Msg(
                    $"{DiagnosticPrefix} native-guard passed " +
                    nativeGuardResult);
                LoggerInstance.Msg(
                    $"{DiagnosticPrefix} enabled " +
                    "engine=ExhaustSmoke/valueIndex-174/" +
                    "System-(4)->System-(5)," +
                    "muzzle=MediumCannonFire/System-(9)/Count/" +
                    "expression-148/valueIndex-299-300/(7,7)->(0,0)," +
                    "muzzleExpressions=valueIndex-289-and-294/unchanged," +
                    "normalSmokeRetained=true," +
                    "scenePolling=false");
            }
            else
            {
                LoggerInstance.Error(
                    $"{DiagnosticPrefix} activation failed " +
                    $"expressionMapPassed={expressionMapPassed}," +
                    $"nativeGuardPassed={nativeGuardPassed}," +
                    $"guard={nativeGuardResult},writesDisabled=true");
            }
        }

        public override void OnDeinitializeMelon()
        {
            nativeWritesEnabled = false;
            loggedFailures.Clear();
            Instance = null;
        }

        internal void SuppressEngineExpression(
            Component effect,
            string category)
        {
            try
            {
                if (!nativeWritesEnabled || effect == null)
                    return;

                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                if (!VfxNativeExpressionOverride.TryDisableMappedExpression(
                        visualEffect,
                        out _,
                        out string failure))
                {
                    LogFailure(category, failure);
                }
            }
            catch (Exception exception)
            {
                LogFailure(category, exception.ToString());
            }
        }

        internal void SuppressMuzzleCount(
            Component effect,
            string category)
        {
            try
            {
                if (!nativeWritesEnabled || effect == null)
                    return;

                VisualEffect? visualEffect =
                    effect.GetComponentInChildren<VisualEffect>(true);
                if (visualEffect == null || visualEffect.visualEffectAsset == null)
                    return;

                if (!VfxNativeExpressionOverride.TryZeroMappedFloat2Expression(
                        visualEffect,
                        out _,
                        out string failure))
                {
                    LogFailure(category, failure);
                }
            }
            catch (Exception exception)
            {
                LogFailure(category, exception.ToString());
            }
        }

        private void LogFailure(string category, string failure)
        {
            string key = $"{category}|{failure}";
            if (!loggedFailures.Add(key))
                return;

            LoggerInstance.Error(
                $"{DiagnosticPrefix} failed " +
                $"category={category},error={failure}");
        }
    }

    [HarmonyPatch(typeof(ExhaustEffect), nameof(ExhaustEffect.PlayEffect))]
    internal static class ExhaustNativeExpressionSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ExhaustEffect __instance)
        {
            CannonSmokeSuppressorMain.Instance?
                .SuppressEngineExpression(
                    __instance,
                    "engine-exhaust");
        }
    }

    [HarmonyPatch(typeof(MuzzleFlashEffect), nameof(MuzzleFlashEffect.Setup))]
    internal static class MuzzleNativeCountSuppressionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MuzzleFlashEffect __instance)
        {
            CannonSmokeSuppressorMain.Instance?
                .SuppressMuzzleCount(
                    __instance,
                    "muzzle-flash");
        }
    }
}
