namespace Tests
{
    using System.Collections.Concurrent;
    using CODA.NtdsAudit;

    internal static class TestHelpers
    {
        private static readonly string RepositoryRoot = FindRepositoryRoot();
        private static readonly string TestDataDirectory = Path.Combine(RepositoryRoot, "Tests", "testdata");

        internal static string NtdsPath { get; set; } = Path.Combine(TestDataDirectory, "ntds.dit");
        internal static string PwdumpPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "ad.lst");
        internal static string SystemHivePath { get; set; } = Path.Combine(TestDataDirectory, "SYSTEM");
        internal static string WordlistPath { get; set; } = Path.Combine(TestDataDirectory, "wordlist.txt");
        internal static string OuFilterPath { get; set; } = Path.Combine(TestDataDirectory, "oufilters.txt");
        internal static DateTime BaseDateTime = new(2026, 01, 23, 14, 0, 32, DateTimeKind.Utc);
        internal static string LMHash = "AAD3B435B51404EEAAD3B435B51404EE"; //Empty LM Hash.
        internal static string NTHash = "DAE57D78FEC919471799CE0FAE8236B9"; //Pa55w0rd!
        internal static string DefaultPassword = "Pa55w0rd!";

        private static readonly ConcurrentDictionary<(bool Dump, bool History, string Wordlist, string OuFilter), NtdsAuditor> AuditorCache = new();

        internal static NtdsAuditor GetAuditor(bool dump, bool history, string wordlist = "", string ouFilter = "")
        {
            var key = (dump, history, wordlist, ouFilter);
            if (AuditorCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            cached = new NtdsAuditor(NtdsPath, dump, history, SystemHivePath, wordlist, ouFilter);
            stopwatch.Stop();
            AuditorCache[key] = cached;
            if (NtdsAuditor.ShowDebugOutput)
                Console.WriteLine($"Test auditor cache miss: dump={dump}, history={history}, wordlist={wordlist.Length > 0}, OU={ouFilter.Length > 0} in {stopwatch.Elapsed}");
            return cached;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "NtdsAudit.sln")) ||
                    Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "NtdsAudit.sln")) ||
                    Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the NtdsAudit repository root.");
        }
    }
}
