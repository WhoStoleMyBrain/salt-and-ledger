using System;
using System.IO;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Host-owned file policy. The simulation parses and validates the immutable configuration.</summary>
    public static class LocalWorldGenerationConfiguration
    {
        public static WorldGenerationConfiguration Load(string path, WorldGenerationConfiguration bundledDefault)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (bundledDefault == null) throw new ArgumentNullException(nameof(bundledDefault));
            if (!File.Exists(path)) return bundledDefault;
            if (new FileInfo(path).Length > 128 * 1024)
                throw new InvalidOperationException("world-generation.json must be at most 128 KiB.");
            return WorldGenerationConfiguration.Parse(File.ReadAllText(path));
        }
    }
}
