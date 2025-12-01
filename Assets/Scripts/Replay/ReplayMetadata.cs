using System;

namespace Replay
{
    /// <summary>
    /// Lightweight metadata extracted from replay JSONL files for menu display.
    /// Only contains header/footer info - no frame data.
    /// </summary>
    [Serializable]
    public class ReplayMetadata
    {
        // File info
        public string filePath;
        public string fileName;
        public DateTime fileModified;

        // From HeaderLine
        public string episodeId;
        public double startTime;

        // From FooterLine
        public string outcome;
        public float duration;
        public int steps;
        public float finalDistance;
        public float totalReward;
        public float fuelUsed;
        public bool volleyMode;
        public int missilesIntercepted;

        // Derived
        public bool hasRadarData;

        // Helper properties
        public bool IsIntercepted => outcome?.ToLower() == "intercepted";
        public string DurationFormatted => $"{duration:F2}s";
        public string DistanceFormatted => $"{finalDistance:F1}m";
        public string OutcomeDisplay => outcome?.ToUpper() ?? "UNKNOWN";
    }
}
