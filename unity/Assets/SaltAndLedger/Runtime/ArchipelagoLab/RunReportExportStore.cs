using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class RunReportExportResult
    {
        public string SavePath { get; }
        public string ReportPath { get; }
        internal RunReportExportResult(string savePath, string reportPath)
        { SavePath = savePath; ReportPath = reportPath; }
    }

    /// <summary>Explicit diagnostic exports, separate from the three save slots. Every export creates a new pair.</summary>
    public sealed class RunReportExportStore
    {
        private readonly string directory;
        private readonly ContentCatalog content;

        public RunReportExportStore(string directory, ContentCatalog content)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute export directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public RunReportExportResult Export(string rawSaveJson, RegionalWorldSnapshot snapshot,
            IEnumerable<string> recentEvents = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrEmpty(rawSaveJson) || rawSaveJson.Length > WorldSaveCodec.MaximumJsonCharacters)
                throw new WorldSaveException("Save data is empty or exceeds the supported size.");
            // Validate the companion pair before creating anything. Loading is detached and cannot replace the active host.
            var restored = WorldSaveCodec.Load(content, rawSaveJson).Snapshot();
            if (RunDiagnosticReport.StateFingerprint(restored) != RunDiagnosticReport.StateFingerprint(snapshot))
                throw new WorldSaveException("The raw save and diagnostic snapshot describe different states.");
            var report = RunDiagnosticReport.Create(content, snapshot, recentEvents);
            var stem = "run-d" + snapshot.Day.ToString("D6", CultureInfo.InvariantCulture) +
                "-r" + snapshot.Revision.ToString("D6", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            var savePath = Path.Combine(directory, stem + ".save.json");
            var reportPath = Path.Combine(directory, stem + ".report.json");
            Directory.CreateDirectory(directory);
            var createdSave = false;
            var createdReport = false;
            try
            {
                WriteNew(savePath, rawSaveJson, ref createdSave);
                WriteNew(reportPath, report, ref createdReport);
                return new RunReportExportResult(savePath, reportPath);
            }
            catch
            {
                if (createdReport) File.Delete(reportPath);
                if (createdSave) File.Delete(savePath);
                throw;
            }
        }

        private static void WriteNew(string path, string text, ref bool created)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                var bytes = new UTF8Encoding(false, true).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }
}
