using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class LocalRunSaveSlot
    {
        public int Number { get; }
        public bool Exists { get; }
        public bool IsCompatible { get; }
        public string Description { get; }
        public RunIdentity Identity { get; }
        public long? Day { get; }
        internal LocalRunSaveSlot(int number, bool exists, bool compatible, string description,
            RunIdentity identity = null, long? day = null)
        { Number = number; Exists = exists; IsCompatible = compatible; Description = description; Identity = identity; Day = day; }
    }

    /// <summary>Three local slots. The Unity host supplies its persistent-data directory; the simulation has no file policy.</summary>
    public sealed class LocalRunSaveStore
    {
        public const int SlotCount = 3;
        private readonly string directory;
        public LocalRunSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute local save directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }

        public void Save(int slot, RegionalWorldState world) => SaveText(slot, WorldSaveCodec.Save(world));

        public void SaveText(int slot, string json)
        {
            var path = SlotPath(slot);
            if (json == null || json.Length == 0 || json.Length > WorldSaveCodec.MaximumJsonCharacters)
                throw new WorldSaveException("Save data is empty or exceeds the supported size.");
            Directory.CreateDirectory(directory);
            // CreateNew prevents an existing file being followed or reused as a temporary save.
            var temporary = Path.Combine(directory, ".slot-" + slot + "-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public RegionalWorldState Load(int slot, ContentCatalog content) => WorldSaveCodec.Load(content, ReadText(slot));

        public string ReadText(int slot)
        {
            var path = SlotPath(slot);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > WorldSaveCodec.MaximumJsonCharacters * 4L)
                throw new WorldSaveException("Save file exceeds the supported size.");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            var text = reader.ReadToEnd();
            if (text.Length > WorldSaveCodec.MaximumJsonCharacters)
                throw new WorldSaveException("Save file exceeds the supported size.");
            return text;
        }

        public IReadOnlyList<LocalRunSaveSlot> List(ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var slots = new List<LocalRunSaveSlot>();
            for (var number = 1; number <= SlotCount; number++)
            {
                if (!File.Exists(SlotPath(number)))
                {
                    slots.Add(new LocalRunSaveSlot(number, false, false, "Empty slot"));
                    continue;
                }
                try
                {
                    var snapshot = Load(number, content).Snapshot();
                    var identity = snapshot.RunIdentity;
                    slots.Add(new LocalRunSaveSlot(number, true, true,
                        "Day " + snapshot.Day + " | " + identity.ComparisonGroup +
                        (snapshot.Challenge == null ? " | Sandbox" : " | " + snapshot.Challenge.Status), identity, snapshot.Day));
                }
                catch (Exception exception) when (exception is WorldSaveException || exception is IOException ||
                    exception is UnauthorizedAccessException || exception is DecoderFallbackException)
                {
                    slots.Add(new LocalRunSaveSlot(number, true, false, "Cannot load: " + exception.Message));
                }
            }
            return slots.AsReadOnly();
        }

        public int? LatestCompatibleSlot(ContentCatalog content) => List(content).Where(s => s.IsCompatible)
            .OrderByDescending(s => File.GetLastWriteTimeUtc(SlotPath(s.Number))).ThenBy(s => s.Number)
            .Select(s => (int?)s.Number).FirstOrDefault();

        private string SlotPath(int slot)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(directory, "slot-" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
        }
    }
}
