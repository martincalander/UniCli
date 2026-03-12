using System;
using System.Reflection;

namespace UniCli.Server.Editor
{
    internal struct CommandCapabilityInfo
    {
        public bool InteractiveOnly { get; set; }
        public bool RequiresGraphics { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class CommandCapabilityAttribute : Attribute
    {
        public bool InteractiveOnly { get; }
        public bool RequiresGraphics { get; }

        public CommandCapabilityAttribute(bool interactiveOnly = false, bool requiresGraphics = false)
        {
            InteractiveOnly = interactiveOnly;
            RequiresGraphics = requiresGraphics;
        }

        public static CommandCapabilityInfo Resolve(Type handlerType)
        {
            var capability = handlerType.GetCustomAttribute<CommandCapabilityAttribute>();
            if (capability == null)
                return default;

            return new CommandCapabilityInfo
            {
                InteractiveOnly = capability.InteractiveOnly,
                RequiresGraphics = capability.RequiresGraphics
            };
        }
    }
}
