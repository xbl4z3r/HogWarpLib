using System.Diagnostics;
using HogWarp.Lib.Game;
using Serilog;
using System.Runtime.CompilerServices;
using HogWarp.Lib.Commands;
using HogWarp.Lib.System;
using Newtonsoft.Json;
using Buffer = HogWarp.Lib.System.Buffer;

[assembly: InternalsVisibleTo("HogWarp.Loader")]

namespace HogWarp.Lib
{
    public delegate void UpdateDelegate(float DeltaSeconds);
    public delegate void ShutdownDelegate();
    public delegate void PlayerJoinDelegate(Player player);
    public delegate void PlayerLeaveDelegate(Player player);
    public delegate void ChatDelegate(Player player, string message, ref bool cancel);
    public delegate void MessageDelegate(Player player, ushort opcode, Buffer buffer);

    public class Server
    {
        public readonly World World;
        public readonly PlayerManager PlayerManager;

        public event UpdateDelegate? UpdateEvent;
        public event ShutdownDelegate? ShutdownEvent;
        public event PlayerJoinDelegate? PlayerJoinEvent;
        public event PlayerLeaveDelegate? PlayerLeaveEvent;
        public event ChatDelegate? ChatEvent;

        private Dictionary<string, HashSet<MessageDelegate>> _messageHandlers = new();

        private Dictionary<string, CommandData> _commands = new();

        private readonly List<string> _opIds = new();
        private const string OpFile = "ops.json";

        internal Server(World world, PlayerManager playerManager)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/scripting.log").CreateLogger();

            World = world;
            PlayerManager = playerManager;
            
            if(!File.Exists(OpFile)) File.WriteAllText(OpFile, JsonConvert.SerializeObject(_opIds, Formatting.Indented));
            else _opIds = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(OpFile)) ?? new List<string>();
            
            if (!Directory.Exists("plugins")) Directory.CreateDirectory("plugins");
            PluginManager.LoadFromBase("plugins");
            PluginManager.InitializePlugins(this);

            RegisterCommand("help", new CommandData
            {
                Name = "help",
                Description = "List all commands",
                Mod = "Server",
                Permissions = CommandPermission.DEFAULT,
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        var commands = _commands.Select(x => x.Key);
                        foreach (var command in commands)
                        {
                            if (_commands[command].Permissions == CommandPermission.OPERATOR && !_opIds.Contains(player.DiscordId)) continue;
                            player.SendMessage($"/{command} - {_commands[command].Description}");
                        }
                    }
                }
            });
            RegisterCommand("whoami", new CommandData
            {
                Name = "whoami",
                Description = "View information about yourself",
                Mod = "Server",
                Permissions = CommandPermission.DEFAULT,
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        player.SendMessage($"You are {player.Name} ({player.DiscordId})");
                    }
                }
            });
            RegisterCommand("op", new CommandData
            {
                Name = "op",
                Description = "Make a player an operator",
                Mod = "Server",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to make an operator",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        if (args.First().Value == null)
                        {
                            player.SendMessage("Usage: /op <player_id>");
                            return;
                        }

                        if (_opIds.Contains(args.First().Value))
                        {
                            player.SendMessage("Player is already an operator.");
                            return;
                        }
                        
                        var target = args.First().Value?.ToString();
                        var targetPlayer = PlayerManager.Players.FirstOrDefault(p => p.DiscordId == target);
                        if (targetPlayer == null)
                        {
                            player.SendMessage($"Could not find player with ID {target}");
                            return;
                        }

                        _opIds.Add(args.First().Value?.ToString() ?? string.Empty);
                        File.WriteAllText(OpFile, JsonConvert.SerializeObject(_opIds, Formatting.Indented));
                        player.SendMessage($"{targetPlayer.Name} is now an operator.");
                    }
                }
            });
            RegisterCommand("deop", new CommandData
            {
                Name = "deop",
                Description = "Remove operator status from a player",
                Mod = "Server",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to remove operator status from",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        if (args.First().Value == null)
                        {
                            player.SendMessage("Usage: /deop <player_id>");
                            return;
                        }

                        if (!_opIds.Contains(args.First().Value))
                        {
                            player.SendMessage("Player is not an operator.");
                            return;
                        }

                        _opIds.Remove(args.First().Value?.ToString() ?? string.Empty);
                        File.WriteAllText(OpFile, JsonConvert.SerializeObject(_opIds, Formatting.Indented));
                        player.SendMessage("Player is no longer an operator.");
                    }
                }
            });
            
            Log.Information("Server initialized.");
        }

        public void RegisterMessageHandler(string modName, MessageDelegate messageDelegate)
        {
            if (!_messageHandlers.TryGetValue(modName, out HashSet<MessageDelegate>? handlers))
            {
                handlers = new HashSet<MessageDelegate>();
                _messageHandlers.Add(modName, handlers);
            }

            handlers.Add(messageDelegate);
        }
        
        public void RegisterCommand(string command, CommandData commandData)
        {
            var mod = commandData.Mod;
            Log.Information($"[{mod}] Registering command: {command}");
            _commands.Add(command, commandData);
        }

        public void Debug(string message)
        {
            var caller = new StackFrame(1).GetMethod();
            Log.Debug(caller == null ? message : $"[{caller.DeclaringType?.Name}] {message}");
        }

        public void Information(string message)
        {
            var caller = new StackFrame(1).GetMethod();
            Log.Information(caller == null ? message : $"[{caller.DeclaringType?.Name}] {message}");
        }

        public void Warning(string message)
        {
            var caller = new StackFrame(1).GetMethod();
            Log.Warning(caller == null ? message : $"[{caller.DeclaringType?.Name}] {message}");    
        }

        public void Error(string message)
        {
            var caller = new StackFrame(1).GetMethod();
            Log.Error(caller == null ? message : $"[{caller.DeclaringType?.Name}] {message}");
        }

        public void Fatal(string message)
        {
            var caller = new StackFrame(1).GetMethod();
            Log.Fatal(caller == null ? message : $"[{caller.DeclaringType?.Name}] {message}");
        }

        public void UnregisterMessageHandler(string modName, MessageDelegate messageDelegate)
        {
            if (_messageHandlers.TryGetValue(modName, out var handlers))
            {
                handlers.Remove(messageDelegate);
            }
        }
        
        public void UnregisterCommand(string command)
        {
            _commands.Remove(command, out _);
        }

        internal void OnUpdate(float deltaSeconds)
        {
            if (UpdateEvent == null)
                return;

            foreach (UpdateDelegate handler in UpdateEvent!.GetInvocationList())
            {
                try
                {
                    handler?.Invoke(deltaSeconds);
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }
        }

        internal void OnShutdown()
        {
            if (ShutdownEvent == null)
                return;

            foreach (ShutdownDelegate handler in ShutdownEvent!.GetInvocationList())
            {
                try
                {
                    handler?.Invoke();
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }
        }

        internal void OnPlayerJoin(Player player)
        {
            if (PlayerJoinEvent == null)
                return;

            foreach (PlayerJoinDelegate handler in PlayerJoinEvent!.GetInvocationList())
            {
                try
                {
                    handler?.Invoke(player);
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }
        }

        internal void OnPlayerLeave(Player player)
        {
            if (PlayerLeaveEvent == null)
                return;

            foreach (PlayerLeaveDelegate handler in PlayerLeaveEvent!.GetInvocationList())
            {
                try
                {
                    handler?.Invoke(player);
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }

        }

        internal void OnChat(Player player, string message, out bool cancel)
        {
            cancel = false;
            
            if(message.StartsWith("/"))
            {
                var split = message.Split(' ');
                var command = split[0].Substring(1);
                var args = split.Skip(1).ToArray();
                
                var success = false;

                if(_commands.TryGetValue(command, out var commandData))
                {
                    if (commandData.Permissions == CommandPermission.OPERATOR && !_opIds.Contains(player.DiscordId))
                    {
                        player.SendMessage("You are not allowed to use this command.");
                        cancel = true;
                        return;
                    }
                    
                    var arguments = commandData.Arguments;
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        var arg = arguments.ElementAt(i);
                        if (args.Length <= i)
                        {
                            if (arg.Required)
                            {
                                player.SendMessage($"Usage: /{command} {string.Join(" ", arguments.Select(x => x.Name))}");
                                cancel = true;
                                return;
                            }
                            continue;
                        }

                        arg.Value = args[i];
                    }
                    
                    try
                    {
                        var handlers = commandData.Handlers;
                        foreach (var h in handlers)
                        {
                            try
                            {
                                h.Invoke(player, arguments);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                Warning(ex.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Warning(ex.ToString());
                    }
                }

                if (!success) player.SendMessage("Unknown command. Type /help for a list of commands.");
                cancel = true;
            }

            if (ChatEvent == null)
                return;
            
            foreach (ChatDelegate handler in ChatEvent!.GetInvocationList())
            {
                try
                {
                    handler?.Invoke(player, message, ref cancel);
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }
        }

        internal void OnMessage(Player player, string modName, ushort opcode, Lib.System.Buffer buffer)
        {
            if (!_messageHandlers.TryGetValue(modName, out var handlers)) return;
            foreach (var h in handlers)
            {
                try
                {
                    h.Invoke(player, opcode, buffer);
                }
                catch (Exception ex)
                {
                    Warning(ex.ToString());
                }
            }
        }
        
        public bool IsOp(Player player)
        {
            return _opIds.Contains(player.DiscordId);
        }
    }
}
