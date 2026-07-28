using System;
using CannonSmokeSuppressor;

if (!SmokeAccumulationOutputMap.TryValidateCapturedEvidence(
        out string failure,
        out int sampleCount))
{
    Console.Error.WriteLine($"Smoke accumulation output map failed: {failure}");
    return 1;
}

Console.WriteLine(
    $"Smoke accumulation output map passed: {sampleCount} captured samples");

if (!SmokeNativeExpressionMap.TryValidateCapturedEvidence(
        out failure,
        out sampleCount))
{
    Console.Error.WriteLine(
        $"Smoke native expression map failed: {failure}");
    return 1;
}

Console.WriteLine(
    $"Smoke native expression map passed: {sampleCount} captured samples");
return 0;
