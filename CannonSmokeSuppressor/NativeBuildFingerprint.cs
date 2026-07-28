using System;
using System.IO;
using System.Security.Cryptography;

namespace CannonSmokeSuppressor
{
    internal readonly record struct NativeFileFingerprint(
        string Name,
        long Length,
        string Sha256);

    internal static class NativeBuildFingerprint
    {
        private static readonly NativeFileFingerprint[] UnityPlayerBuilds =
        {
            new(
                "Unity 2022.3.62f2",
                31_006_120,
                "43FB0AEA1B80C74963DAE88EC7FC1C3C1993D893CE94D12972000FCC9B653AD1")
        };

        private static readonly NativeFileFingerprint[] GameAssemblyBuilds =
        {
            new(
                "Sprocket 0.2.53.1",
                62_211_072,
                "948C5FB4D580034DE753A784B6AD11E7896C61CA998E3E4F2E7524BEC69BEF02"),
            new(
                "Sprocket 0.2.53.2",
                62_212_608,
                "30EB5D6DA29BCCDDC441DB62257455D24929B1C8B35E963FBF8FAFB19826E9B8")
        };

        internal static bool TryMatchUnityPlayer(
            string path,
            out NativeFileFingerprint matched,
            out string result) =>
            TryMatchKnownFile(path, UnityPlayerBuilds, out matched, out result);

        internal static bool TryMatchGameAssembly(
            string path,
            out NativeFileFingerprint matched,
            out string result) =>
            TryMatchKnownFile(path, GameAssemblyBuilds, out matched, out result);

        private static bool TryMatchKnownFile(
            string path,
            NativeFileFingerprint[] knownBuilds,
            out NativeFileFingerprint matched,
            out string result)
        {
            matched = default;
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                result = $"missing path={path}";
                return false;
            }

            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            string actualSha256 = Convert.ToHexString(
                sha256.ComputeHash(stream));

            foreach (NativeFileFingerprint candidate in knownBuilds)
            {
                if (file.Length == candidate.Length &&
                    string.Equals(
                        actualSha256,
                        candidate.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matched = candidate;
                    result =
                        $"build={candidate.Name},length={file.Length}," +
                        $"sha256={actualSha256}";
                    return true;
                }
            }

            string expected = string.Join(
                " or ",
                Array.ConvertAll(
                    knownBuilds,
                    candidate =>
                        $"{candidate.Name}/length={candidate.Length}/" +
                        $"sha256={candidate.Sha256}"));
            result =
                $"expected={expected},actual=length={file.Length}/" +
                $"sha256={actualSha256}";
            return false;
        }
    }
}
