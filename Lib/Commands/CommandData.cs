using HogWarp.Lib.Game;

namespace HogWarp.Lib.Commands
{
    public delegate void CommandDelegate(Player player, HashSet<CommandArgument> args);
    
    public sealed class CommandData
    {
        public required string Name;
        public string Description = "No description provided";
        public string Mod = "Unknown";
        public HashSet<CommandArgument> Arguments = new();
        public required CommandPermission Permissions;
        public required HashSet<CommandDelegate> Handlers;
    }
}