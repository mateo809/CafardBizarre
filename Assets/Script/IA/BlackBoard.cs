using UnityEngine;
using System.Collections.Generic;

namespace BehaviorTree.Core
{
    public class Blackboard
    {
        private readonly Dictionary<string, object> _data = new();

        public void Set<T>(string key, T value) => _data[key] = value;

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T t) return t;
            return defaultValue;
        }

        public bool Has(string key) => _data.ContainsKey(key);

        public const string PlayerTransform = "player_transform";
        public const string TargetPosition = "target_position";
        public const string IsPlayerVisible = "is_player_visible";
        public const string IsAtDestination = "is_at_destination";
        public const string TaskDuration = "task_duration";
        public const string TaskTimer = "task_timer";
        public const string CurrentTask = "current_task";
        public const string AttackRange = "attack_range";
        public const string VacuumForce = "vacuum_force";
        public const string PatrolPoints = "patrol_points";
        public const string PatrolIndex = "patrol_index";
    }
}