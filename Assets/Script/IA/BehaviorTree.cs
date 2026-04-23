using System.Collections.Generic;

namespace BehaviorTree.Core
{
    public enum NodeStatus { Running, Success, Failure }

    public abstract class BTNode
    {
        public string Name { get; protected set; }
        protected NodeStatus _status = NodeStatus.Failure;

        protected BTNode(string name) => Name = name;

        public NodeStatus Tick()
        {
            _status = OnTick();
            return _status;
        }

        protected abstract NodeStatus OnTick();
        public virtual void Reset() { }
    }

    public class Selector : BTNode
    {
        private readonly List<BTNode> _children;

        public Selector(string name, List<BTNode> children) : base(name)
            => _children = children;

        protected override NodeStatus OnTick()
        {
            foreach (var child in _children)
            {
                var result = child.Tick();
                if (result != NodeStatus.Failure) return result;
            }
            return NodeStatus.Failure;
        }

        public override void Reset()
        {
            foreach (var c in _children) c.Reset();
        }
    }

    public class Sequence : BTNode
    {
        private readonly List<BTNode> _children;

        public Sequence(string name, List<BTNode> children) : base(name)
            => _children = children;

        protected override NodeStatus OnTick()
        {
            foreach (var child in _children)
            {
                var result = child.Tick();
                if (result != NodeStatus.Success) return result;
            }
            return NodeStatus.Success;
        }

        public override void Reset()
        {
            foreach (var c in _children) c.Reset();
        }
    }

    public class Inverter : BTNode
    {
        private readonly BTNode _child;

        public Inverter(string name, BTNode child) : base(name) => _child = child;

        protected override NodeStatus OnTick()
        {
            var r = _child.Tick();
            return r switch
            {
                NodeStatus.Success => NodeStatus.Failure,
                NodeStatus.Failure => NodeStatus.Success,
                _ => NodeStatus.Running
            };
        }
    }

    public class Repeater : BTNode
    {
        private readonly BTNode _child;
        private readonly int _times;
        private int _count;

        public Repeater(string name, BTNode child, int times = -1) : base(name)
        {
            _child = child;
            _times = times;
        }

        protected override NodeStatus OnTick()
        {
            while (_times < 0 || _count < _times)
            {
                var r = _child.Tick();
                if (r == NodeStatus.Running) return NodeStatus.Running;
                _child.Reset();
                if (_times > 0) _count++;
            }
            _count = 0;
            return NodeStatus.Success;
        }

        public override void Reset() { _count = 0; _child.Reset(); }
    }

    public class ActionNode : BTNode
    {
        private readonly System.Func<NodeStatus> _action;

        public ActionNode(string name, System.Func<NodeStatus> action) : base(name)
            => _action = action;

        protected override NodeStatus OnTick() => _action();
    }

    public class ConditionNode : BTNode
    {
        private readonly System.Func<bool> _condition;

        public ConditionNode(string name, System.Func<bool> condition) : base(name)
            => _condition = condition;

        protected override NodeStatus OnTick()
            => _condition() ? NodeStatus.Success : NodeStatus.Failure;
    }

    public class WaitNode : BTNode
    {
        private readonly float _duration;
        private float _elapsed;

        public WaitNode(string name, float duration) : base(name) => _duration = duration;

        protected override NodeStatus OnTick()
        {
            _elapsed += UnityEngine.Time.deltaTime;
            if (_elapsed >= _duration)
            {
                _elapsed = 0f;
                return NodeStatus.Success;
            }
            return NodeStatus.Running;
        }

        public override void Reset() => _elapsed = 0f;
    }
}