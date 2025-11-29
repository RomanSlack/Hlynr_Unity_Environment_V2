using System;
using UnityEngine;

namespace Replay
{
    // New format types

    [Serializable] public sealed class HeaderLine
    {
        public string type;         // "header"
        public string episode_id;
        public double start_time;   // Absolute start time (for reference)
        public Metadata metadata;
    }

    [Serializable] public sealed class Metadata
    {
        // Currently empty in the data, but can be extended
    }

    [Serializable] public sealed class StateLine
    {
        public string type;         // "state"
        public float timestamp;     // Relative time from episode start (in seconds)
        public string entity_id;    // "interceptor" or "missile"
        public EntityState state;
    }

    [Serializable] public sealed class EntityState
    {
        public float[] position;    // [x,y,z]
        public float fuel;          // for interceptor
        public float[] action;      // for interceptor [6 values]
    }

    [Serializable] public sealed class FooterLine
    {
        public string type;         // "footer"
        public string episode_id;
        public double end_time;     // Absolute end time (for reference)
        public float duration;      // Episode duration in seconds
        public string outcome;      // "intercepted", "miss", etc.
        public Metrics metrics;
    }

    [Serializable] public sealed class Metrics
    {
        public float total_reward;
        public int steps;
        public float final_distance;
        public float fuel_used;
        public bool volley_mode;
        public int missiles_intercepted;  // -1 means null
        public int volley_size;           // -1 means null
    }

    // Internal format used by ReplayDirector
    [Serializable] public sealed class TimestepLine
    {
        public float t;
        public AgentState interceptor;
        public AgentState missile;
    }

    [Serializable] public sealed class AgentState
    {
        public float[] p;        // [x,y,z] position
        public float[] q;        // [w,x,y,z] quaternion (will default to identity if not provided)
        public float[] v;        // velocity (not in new format, will be zero)
        public float[] w;        // angular velocity (not in new format, will be zero)
        public string  status;   // "active"
        public float[] u;        // actions (from interceptor)
        public float   fuel_kg;  // fuel
    }

    // ==================== Radar Data Types ====================

    /// <summary>
    /// Raw radar state line from JSONL (entity_id == "radar")
    /// </summary>
    [Serializable] public sealed class RadarStateLine
    {
        public string type;         // "state"
        public float timestamp;
        public string entity_id;    // "radar"
        public RadarStateData state;
    }

    [Serializable] public sealed class RadarStateData
    {
        public OnboardRadarState onboard;
        public GroundRadarState ground;
        public FusionState fusion;
    }

    [Serializable] public sealed class OnboardRadarState
    {
        public float[] position;           // Interceptor position
        public float[] forward_vector;     // Seeker look direction
        public float beam_width_deg;       // Total beam width (e.g., 120)
        public float beam_angle_to_target_deg;
        public float half_beam_width_deg;
        public bool in_beam;               // Target within seeker FOV
        public float range_to_target;
        public float max_range;
        public bool detected;
        public string detection_reason;    // "detected", "outside_beam", "poor_signal"
        public float quality;              // 0-1 signal quality
    }

    [Serializable] public sealed class GroundRadarState
    {
        public float[] position;           // Ground station position
        public bool enabled;
        public float max_range;
        public float min_elevation_deg;
        public float max_elevation_deg;
        public float range_to_target;
        public float elevation_deg;
        public bool detected;
        public string detection_reason;
        public float quality;
    }

    [Serializable] public sealed class FusionState
    {
        public float datalink_quality;     // Communication quality
        public float fusion_confidence;    // Combined track confidence
        public bool both_detected;         // Both radars see target
        public bool any_detected;          // At least one radar sees target
    }

    /// <summary>
    /// Processed radar frame for replay (matches TimestepLine timing)
    /// </summary>
    [Serializable] public sealed class RadarFrame
    {
        public float t;
        public OnboardRadarState onboard;
        public GroundRadarState ground;
        public FusionState fusion;
    }
}
