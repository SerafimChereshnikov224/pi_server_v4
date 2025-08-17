namespace PiServer.version_2.interpreter.core
{
    public class VariableScope
    {
        private readonly Dictionary<string, string> _variables = new();
        private readonly VariableScope? _parent;

        public VariableScope(VariableScope? parent = null)
        {
            _parent = parent;
        }

        public void Bind(string name, string value)
        {
            _variables[name] = value;
        }

        public string? Resolve(string name)
        {
            if (_variables.TryGetValue(name, out var value))
                return value;
            return _parent?.Resolve(name);
        }

        public Dictionary<string, string> GetAllVariables()
        {
            var result = _parent?.GetAllVariables() ?? new Dictionary<string, string>();
            foreach (var kv in _variables)
                result[kv.Key] = kv.Value;
            return result;
        }

        public VariableScope CreateChild()
        {
            return new VariableScope(this);
        }
    }
}