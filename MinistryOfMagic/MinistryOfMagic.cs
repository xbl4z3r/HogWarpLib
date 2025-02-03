using HogWarp.Lib;
using HogWarp.Lib.Commands;
using HogWarp.Lib.Game;
using Newtonsoft.Json;

namespace MinistryOfMagic
{
    public class MinistryOfMagic : IPluginBase
    {
        public string Name => "MinistryOfMagic";
        public string Description => "Manage Your Players & Server";
        private Server? _server;

        private readonly string _mutedFilePath = Path.Join("plugins", "MinistryOfMagic", "muted.json");
        private readonly string _bannedFilePath = Path.Join("plugins", "MinistryOfMagic", "banned.json");
        
        private List<string> _muted = new();
        private List<string> _banned = new();
        
        public void Initialize(Server server)
        {
            _server = server;
            LoadMuted();
            LoadBanned();
            _server.RegisterCommand("clearchat", new CommandData()
            {
                Name = "clearchat",
                Description = "Clears the chat for all players.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Handlers = new HashSet<CommandDelegate>() { ClearChat }
            });
            _server.RegisterCommand("kick", new CommandData()
            {
                Name = "kick",
                Description = "Kicks a player from the server.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to kick",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>() { Kick }
            });
            _server.RegisterCommand("ban", new CommandData()
            {
                Name = "ban",
                Description = "Bans a player from the server.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to ban",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>() { Ban }
            });
            _server.RegisterCommand("unban", new CommandData()
            {
                Name = "unban",
                Description = "Unbans a player from the server.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to unban",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>() { Unban }
            });
            _server.RegisterCommand("mute", new CommandData()
            {
                Name = "mute",
                Description = "Mutes a player.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to mute",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>() { Mute }
            });
            _server.RegisterCommand("unmute", new CommandData()
            {
                Name = "unmute",
                Description = "Unmutes a player.",
                Mod = "MinistryOfMagic",
                Permissions = CommandPermission.OPERATOR,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "player_id",
                        Description = "The player to unmute",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>() { Unmute }
            });
            _server.PlayerJoinEvent += player =>
            {
                if (_banned.Contains(player.DiscordId))
                {
                    player.Kick();
                }
            };
            _server.ChatEvent += (Player player, string _, ref bool cancel) =>
            {
                if (!_muted.Contains(player.DiscordId)) return;
                player.SendMessage("You are muted.");
                cancel = true;
            };
        }

        public void ClearChat(Player player, HashSet<CommandArgument> args)
        {
            if (!_server!.IsOp(player))
            {
                player.SendMessage("You are not allowed to use this command.");
                return;
            }
            foreach (var p in _server!.PlayerManager.Players)
            {
                for (int i = 0; i < 100; i++)
                {
                    p.SendMessage("");
                }
                player.SendMessage("Chat cleared.");
            }
        }
        
        public void Kick(Player player, HashSet<CommandArgument> args)
        {
            if (args.First().Value == null)
            {
                player.SendMessage("Usage: /kick <player>");
                return;
            }

            var target = args.First().Value?.ToString();
            var targetPlayer = _server!.PlayerManager.Players.FirstOrDefault(p => p.Name == target);
            if (targetPlayer == null)
            {
                player.SendMessage($"{target} is not online.");
                return;
            }

            targetPlayer.Kick();
            player.SendMessage($"{target} has been kicked.");
        }
        
        public void Ban(Player player, HashSet<CommandArgument> args)
        {
            if (args.First().Value == null)
            {
                player.SendMessage("Usage: /ban <player>");
                return;
            }

            var target = args.First().Value?.ToString();
            var targetPlayer = _server!.PlayerManager.Players.FirstOrDefault(p => p.Name == target);
            if (targetPlayer == null)
            {
                player.SendMessage($"{target} is not online.");
                return;
            }

            _banned.Add(targetPlayer.DiscordId);
            targetPlayer.Kick();
            player.SendMessage($"{target} has been banned.");
        }
        
        public void Unban(Player player, HashSet<CommandArgument> args)
        {
            if (args.First().Value == null)
            {
                player.SendMessage("Usage: /unban <player>");
                return;
            }

            var target = args.First().Value?.ToString();
            if (!_banned.Contains(target))
            {
                player.SendMessage($"{target} is not banned.");
                return;
            }

            _banned.Remove(target);
            player.SendMessage($"{target} has been unbanned.");
        }
        
        public void Mute(Player player, HashSet<CommandArgument> args)
        {
            if (args.First().Value == null)
            {
                player.SendMessage("Usage: /mute <player>");
                return;
            }

            var target = args.First().Value?.ToString();
            var targetPlayer = _server!.PlayerManager.Players.FirstOrDefault(p => p.Name == target);
            if (targetPlayer == null)
            {
                player.SendMessage($"{target} is not online.");
                return;
            }

            _muted.Add(targetPlayer.DiscordId);
            player.SendMessage($"{target} has been muted.");
        }
        
        public void Unmute(Player player, HashSet<CommandArgument> args)
        {
            if (args.First().Value == null)
            {
                player.SendMessage("Usage: /unmute <player>");
                return;
            }

            var target = args.First().Value?.ToString();
            if (!_muted.Contains(target))
            {
                player.SendMessage($"{target} is not muted.");
                return;
            }

            _muted.Remove(target);
            player.SendMessage($"{target} has been unmuted.");
        }
        
        public void LoadMuted()
        {
            if (File.Exists(_mutedFilePath))
            {
                _muted = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(_mutedFilePath))!;
                _server!.Information("Loaded muted players.");
            }
            else
            {
                _server!.Warning("No muted players exist! Creating new file...");
                SaveMuted();
            }
        }
        
        public void LoadBanned()
        {
            if (File.Exists(_bannedFilePath))
            {
                _banned = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(_bannedFilePath))!;
                _server!.Information("Loaded banned players.");
            }
            else
            {
                _server!.Warning("No banned players exist! Creating new file...");
                SaveBanned();
            }
        }

        public void SaveMuted()
        {
            if(!Directory.Exists(Path.Join("plugins", "MinistryOfMagic")))
                Directory.CreateDirectory(Path.Join("plugins", "MinistryOfMagic"));
            File.WriteAllText(_mutedFilePath, JsonConvert.SerializeObject(_muted));
        }
        
        public void SaveBanned()
        {
            if(!Directory.Exists(Path.Join("plugins", "MinistryOfMagic")))
                Directory.CreateDirectory(Path.Join("plugins", "MinistryOfMagic"));
            File.WriteAllText(_bannedFilePath, JsonConvert.SerializeObject(_banned));
        }
    }
}