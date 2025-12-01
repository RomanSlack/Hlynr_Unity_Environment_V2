using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Replay
{
    /// <summary>
    /// Efficiently parses replay JSONL files to extract only header/footer metadata.
    /// Skips all frame data for performance when populating replay selection menus.
    /// </summary>
    public static class ReplayMetadataParser
    {
        /// <summary>
        /// Parses only header and footer from JSONL, skipping all frame data.
        /// Uses streaming to minimize memory allocation.
        /// </summary>
        public static ReplayMetadata ParseMetadataOnly(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[ReplayMetadataParser] File not found: {filePath}");
                return null;
            }

            var metadata = new ReplayMetadata
            {
                filePath = filePath,
                fileName = Path.GetFileName(filePath),
                fileModified = File.GetLastWriteTime(filePath)
            };

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    // Read first line (header)
                    string headerLine = reader.ReadLine();
                    if (headerLine != null && headerLine.Contains("\"header\""))
                    {
                        var header = JsonUtility.FromJson<HeaderLine>(headerLine);
                        if (header != null)
                        {
                            metadata.episodeId = header.episode_id ?? "unknown";
                            metadata.startTime = header.start_time;
                        }
                    }

                    // Scan for footer and radar data
                    string lastFooterLine = null;
                    bool foundRadar = false;
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        // Check for footer (may appear multiple times, take last)
                        if (line.Contains("\"footer\""))
                        {
                            lastFooterLine = line;
                        }

                        // Check for radar data presence (handle both with and without spaces)
                        if (!foundRadar && (line.Contains("\"entity_id\":\"radar\"") || line.Contains("\"entity_id\": \"radar\"")))
                        {
                            foundRadar = true;
                        }
                    }

                    metadata.hasRadarData = foundRadar;

                    // Parse footer
                    if (lastFooterLine != null)
                    {
                        var footer = JsonUtility.FromJson<FooterLine>(lastFooterLine);
                        if (footer != null)
                        {
                            metadata.outcome = footer.outcome ?? "unknown";
                            metadata.duration = footer.duration;

                            if (footer.metrics != null)
                            {
                                metadata.steps = footer.metrics.steps;
                                metadata.finalDistance = footer.metrics.final_distance;
                                metadata.totalReward = footer.metrics.total_reward;
                                metadata.fuelUsed = footer.metrics.fuel_used;
                                metadata.volleyMode = footer.metrics.volley_mode;
                                metadata.missilesIntercepted = footer.metrics.missiles_intercepted;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayMetadataParser] Error parsing {filePath}: {e.Message}");
                return null;
            }

            return metadata;
        }

        /// <summary>
        /// Scans a directory for all JSONL replay files and parses their metadata.
        /// Returns list sorted by file modified date (newest first).
        /// </summary>
        public static List<ReplayMetadata> ScanDirectory(string directoryPath)
        {
            var results = new List<ReplayMetadata>();

            if (!Directory.Exists(directoryPath))
            {
                Debug.LogWarning($"[ReplayMetadataParser] Directory not found: {directoryPath}");
                return results;
            }

            var files = Directory.GetFiles(directoryPath, "*.jsonl");

            foreach (var file in files)
            {
                var metadata = ParseMetadataOnly(file);
                if (metadata != null)
                {
                    results.Add(metadata);
                }
            }

            // Sort by file modified date (newest first)
            results.Sort((a, b) => b.fileModified.CompareTo(a.fileModified));

            Debug.Log($"[ReplayMetadataParser] Found {results.Count} replay files in {directoryPath}");
            return results;
        }

        /// <summary>
        /// Gets the default replays directory path (StreamingAssets/Replays).
        /// </summary>
        public static string GetDefaultReplaysPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "Replays");
        }
    }
}
