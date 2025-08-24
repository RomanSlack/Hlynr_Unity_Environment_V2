using System;
using UnityEngine;

namespace Replay
{
    // Exact shapes per your backend agent.

    [Serializable] public sealed class HeaderLine
    {
        public float t;
        public Meta meta;
        public Scene scene;
    }

    [Serializable] public sealed class Meta
    {
        public string ep_id;
        public int?   seed;
        public string coord_frame;   // "ENU_RH"
        public float  dt_nominal;    // 0.01
        public string scenario;
    }

    [Serializable] public sealed class Scene
    {
        public InterceptorCfg interceptor_0;
        public ThreatCfg      threat_0;
    }

    [Serializable] public sealed class InterceptorCfg
    {
        public float mass_kg;
        public float[] max_torque;      // [8000,8000,2000]
        public float sensor_fov_deg;
        public float max_thrust_n;
    }

    [Serializable] public sealed class ThreatCfg
    {
        public string type;             // "ballistic"
        public float mass_kg;
        public float[] aim_point;       // [x,y,z]
    }

    [Serializable] public sealed class TimestepLine
    {
        public float t;
        public Agents agents;
    }

    [Serializable] public sealed class Agents
    {
        public AgentState interceptor_0;
        public AgentState threat_0;
    }

    [Serializable] public sealed class AgentState
    {
        public float[] p;        // [x,y,z] (m), ENU
        public float[] q;        // [w,x,y,z], WORLD->BODY, ENU, unit
        public float[] v;        // [vx,vy,vz] (m/s), ENU
        public float[] w;        // [wx,wy,wz] (rad/s), ENU
        public string  status;   // "active"|"destroyed"|"finished"
        public float[] u;        // [pitchY, yawZ, rollX, thrust, aux, aux]  (actions)
        public float   fuel_kg;  // remaining fuel (optional)
    }

    [Serializable] public sealed class SummaryLine
    {
        public float t;
        public Summary summary;
    }

    [Serializable] public sealed class Summary
    {
        public string outcome;          // "hit"|"miss"|"timeout"
        public float  episode_duration;
        public string notes;
    }
}
