using System;
using UnityEngine;

namespace Replay
{
    // New format types

    [Serializable] public sealed class HeaderLine
    {
        public string type;         // "header"
        public string episode_id;
        public float start_time;
        public Metadata metadata;
    }

    [Serializable] public sealed class Metadata
    {
        // Currently empty in the data, but can be extended
    }

    [Serializable] public sealed class StateLine
    {
        public string type;         // "state"
        public float timestamp;
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
        public float end_time;
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
        public int? missiles_intercepted;
        public int? volley_size;
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
}
