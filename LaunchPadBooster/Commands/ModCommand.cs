using System;
using Util.Commands;

namespace LaunchPadBooster.Commands {
    public interface IModCommand { }

    public abstract class ModCommand : CommandBase, IModCommand {
        public abstract string Name { get; }
    }

    public class ModCommandBasic : ModCommand {
        public override string Name { get; }
        public override string HelpText { get; }
        public override string[] Arguments { get; }
        public override bool IsLaunchCmd { get; }
        public override bool Hidden { get; set; }

        private readonly Func<string[], string> _execute;

        public ModCommandBasic(Func<string[], string> execute, string name, string helpText = null, string[] arguments = null, bool isLaunchCmd = false, bool hidden = false) {
            this.Name = name;
            this.HelpText = helpText;
            this.Arguments = arguments;
            this.IsLaunchCmd = isLaunchCmd;
            this.Hidden = hidden;
            _execute = execute;
        }

        public override string Execute(string[] args) => _execute?.Invoke(args);
    }
}