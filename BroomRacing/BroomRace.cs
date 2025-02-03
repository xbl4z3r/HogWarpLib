using HogWarp.Lib;
using HogWarp.Lib.Commands;
using HogWarp.Lib.Game;
using HogWarp.Lib.Game.Data;
using HogWarp.Lib.System;
using Newtonsoft.Json;
using Buffer = HogWarp.Lib.System.Buffer;

namespace BroomRacing
{
    public class BroomRace : IPluginBase
    {
        public string Name => "RaceBuilder";
        public string Description => "Build Races & Race with others";
        private Server? _server;

        private struct RaceRings
        {
            public string Name;
            public FTransform[] Rings;
        }

        private struct RaceSetups
        {
            public string Name = "";
            public readonly List<Player> Players = new();
            public readonly Dictionary<Player, FTimespan> PlayerTimes = new();

            public RaceSetups()
            {
            }
        }

        private readonly string _racesFilePath = Path.Join("plugins", "BroomRacing", "races.json");

        private List<RaceRings> _races = new();
        private readonly List<RaceSetups> _activeRaces = new();

        public void Initialize(Server server)
        {
            _server = server;
            _server.PlayerLeaveEvent += PlayerLeave;
            _server.RegisterMessageHandler(Name, HandleMessage);
            _server.RegisterCommand("racebuilder", new CommandData()
            {
                Name = "racebuilder",
                Description = "Opens the race builder menu.",
                Mod = "BroomRacing",
                Permissions = CommandPermission.OPERATOR,
                Handlers = new HashSet<CommandDelegate> { (player, args) =>
                    {
                        var buffer = new Buffer(2);
                        var writer = new BufferWriter(buffer);
                        _server!.PlayerManager.SendTo(player, Name, 34, writer);
                    }
                }
            });
            _server.RegisterCommand("joinrace", new CommandData()
            {
                Name = "joinrace",
                Description = "Join a race",
                Mod = "BroomRacing",
                Permissions = CommandPermission.DEFAULT,
                Arguments = new HashSet<CommandArgument>
                {
                    new()
                    {
                        Name = "race_name",
                        Description = "The name of the race",
                        Required = true,
                    }
                },
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        if (args.First().Value == null) return;

                        _server!.Information($"Join race: {args.First().Value}");

                        var raceIndex = _races.FindIndex(race => race.Name == args.First().Value?.ToString());

                        if (raceIndex < 0)
                            player.SendMessage("Could not find race!");
                        else
                            SetupRace(player, raceIndex);
                    }
                }
            });
            _server.RegisterCommand("startrace", new CommandData()
            {
                Name = "startrace",
                Description = "Start the race",
                Mod = "BroomRacing",
                Permissions = CommandPermission.DEFAULT,
                Handlers = new HashSet<CommandDelegate>
                {
                    (player, args) =>
                    {
                        var activeRaceIndex =
                            _activeRaces.FindIndex(race => race.Players[0].DiscordId == player.DiscordId);

                        if (activeRaceIndex != -1)
                            SpawnRace(activeRaceIndex);
                        else
                            player.SendMessage("Race not found, or you are not the race host.");
                    }
                }
            });

            LoadRaces();
        }

        public void PlayerLeave(Player player)
        {
            _server!.Information("Player Left!");
            // Remove player from all active Races
            foreach (var r in _activeRaces)
            {
                foreach (var p in r.Players)
                {
                    if (p.DiscordId == player.DiscordId)
                    {
                        r.Players.Remove(p);
                        break;
                    }
                }
            }
        }

        public void HandleMessage(Player player, ushort opcode, Buffer buffer)
        {
            var reader = new BufferReader(buffer);

            switch (opcode)
            {
                case 32:
                {
                    RaceRings currentRace;

                    reader.Read(out currentRace.Name);
                    reader.ReadVarInt(out var raceSize);
                    raceSize &= 0xFFFF;

                    if (raceSize > 0)
                    {
                        currentRace.Rings = new FTransform[raceSize];

                        for (int i = 0; i < currentRace.Rings.Length; ++i)
                        {
                            reader.Read(out currentRace.Rings[i]);
                        }

                        _server!.Information($"Saving Race: {currentRace.Name}");
                        _races.Add(currentRace);
                        SaveRaces();
                    }
                    else
                        player.SendMessage($"Race failed to save, no race rings present.");

                    break;
                }
                case 33:
                    SendRaces(player);
                    break;
                case 34:
                {
                    reader.Read(out int selectedRace);
                    SetupRace(player, selectedRace);
                    break;
                }
                case 35:
                    reader.Read(out string raceName);
                    reader.Read(out FTimespan raceTime);
                    AddRaceTime(player, raceName, raceTime);
                    break;
                case 36:
                {
                    reader.Read(out int selectedRace);
                    DeleteRace(player, selectedRace);
                    break;
                }
            }
        }

        public void LoadRaces()
        {
            if (!File.Exists(_racesFilePath)) SaveRaces();
            _races = JsonConvert.DeserializeObject<List<RaceRings>>(File.ReadAllText(_racesFilePath))!;
            _server!.Information($"Loaded {_races.Count} races");
        }

        private void SaveRaces()
        {
            if(!Directory.Exists(Path.Join("plugins", "BroomRacing")))
                Directory.CreateDirectory(Path.Join("plugins", "BroomRacing"));
            File.WriteAllText(_racesFilePath, JsonConvert.SerializeObject(_races, Formatting.Indented));
        }

        private void SendRaces(Player player)
        {
            var buffer = new Buffer(10000);
            var writer = new BufferWriter(buffer);
            var isAdmin = _server!.IsOp(player);

            writer.Write(isAdmin);
            writer.WriteVarInt(Convert.ToUInt64(_races.Count));

            foreach (var race in _races)
            {
                writer.WriteString(race.Name);
            }

            _server!.Information($"Sending races...");
            _server!.PlayerManager.SendTo(player, Name, 33, writer);
        }

        private void DeleteRace(Player player, int selectedRace)
        {
            // just double check they are an admin, just in-case...
            if (!_server!.IsOp(player)) return;
            _server!.Information($"Race: {_races[selectedRace].Name} deleted");
            _races.RemoveAt(selectedRace);
            SaveRaces();
        }

        private void SetupRace(Player player, int selectedRace)
        {
            var race = _races[selectedRace];

            _server!.Information($"Setting up Race: {race.Name}");

            var raceIndex = _activeRaces.FindIndex(activeRace => activeRace.Name == race.Name);

            if (raceIndex == -1)
            {
                var raceSetup = new RaceSetups
                {
                    Name = race.Name
                };
                raceSetup.Players.Add(player);

                _activeRaces.Add(raceSetup);

                foreach (var serverPlayer in _server!.PlayerManager.Players)
                {
                    serverPlayer.SendMessage(serverPlayer.DiscordId == player.DiscordId
                        ? $"You are race Host type '/startrace' to begin."
                        : $"{race.Name} has been setup, type '/joinrace {race.Name}' to join.");
                }
            }
            else
            {
                var playerIndex = _activeRaces[raceIndex].Players.FindIndex(p => p.DiscordId == player.DiscordId);

                if (playerIndex != -1)
                    player.SendMessage("You are already in this race.");
                else
                {
                    _server!.Information($"Race exists, adding player...");
                    _activeRaces[raceIndex].Players.Add(player);
                    foreach (var racePlayer in _activeRaces[raceIndex].Players)
                    {
                        racePlayer.SendMessage($"{player.Name} has joined the race.");
                    }
                }
            }
        }

        private void SpawnRace(int activeRaceIndex)
        {
            var buffer = new Buffer(10000);
            var writer = new BufferWriter(buffer);

            var raceIndex = _races.FindIndex(race => race.Name == _activeRaces[activeRaceIndex].Name);
            var currentRace = _races[raceIndex];

            writer.WriteString(_races[raceIndex].Name);
            writer.WriteVarInt(Convert.ToUInt64(_races[raceIndex].Rings.Length));

            _server!.Information($"Building Race");

            foreach (var t in currentRace.Rings) writer.Write(t);

            foreach (var racePlayer in _activeRaces[activeRaceIndex].Players)
            {
                _server!.PlayerManager.SendTo(racePlayer, Name, 32, writer);
                _server!.Information(
                    $"Sending {_races[raceIndex].Name} to Player with {_races[raceIndex].Rings.Length} rings");
            }
        }

        private void AddRaceTime(Player player, string raceName, FTimespan raceTime)
        {
            _server!.Information(
                $"Race Index: {raceName}, Race Time: {raceTime.Minutes}:{raceTime.Seconds}:{raceTime.Milliseconds / 10}");

            var activeRaceIndex = _activeRaces.FindIndex(activeRace => activeRace.Name == raceName);

            if (activeRaceIndex == -1) return;
            _activeRaces[activeRaceIndex].PlayerTimes.Add(player, raceTime);
            var raceTimes = _activeRaces[activeRaceIndex].PlayerTimes;

            if (_activeRaces[activeRaceIndex].Players.Count != raceTimes.Count) return;
            var times = raceTimes.OrderBy(pair => pair.Value.Days)
                .ThenBy(pair => pair.Value.Hours)
                .ThenBy(pair => pair.Value.Minutes)
                .ThenBy(pair => pair.Value.Seconds)
                .ThenBy(pair => pair.Value.Milliseconds).ToList();

            foreach (var racePlayer in _activeRaces[activeRaceIndex].Players)
            {
                racePlayer.SendMessage("Race Times");
                foreach (var playerTimes in times)
                {
                    racePlayer.SendMessage(
                        $"{playerTimes.Key.Name} - {playerTimes.Value.Minutes}:{playerTimes.Value.Seconds}:{playerTimes.Value.Milliseconds / 10}");
                }
            }

            _activeRaces.RemoveAt(activeRaceIndex);
        }
    }
}