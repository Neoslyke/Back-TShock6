using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace Back
{
    [ApiVersion(2, 1)]
    public class Back : TerrariaPlugin
    {
        public override string Name => "Back";
        public override string Author => "Neoslyke, Melton";
        public override Version Version => new Version(2, 1, 0);
        public override string Description => "Teleports you back to the last death location";

        private readonly Dictionary<string, Vector2> playerDeathData = new();
        private readonly HashSet<string> autoBackEnabled = new();
        private readonly HashSet<string> pendingTeleport = new();

        public Back(Main game) : base(game)
        { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("back.back", BackCommand, "back"));
            Commands.ChatCommands.Add(new Command("back.auto", BackAutoCommand, "backauto"));
            ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == BackCommand || c.CommandDelegate == BackAutoCommand);
                ServerApi.Hooks.NetGetData.Deregister(this, OnNetGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }

            base.Dispose(disposing);
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player != null && !string.IsNullOrEmpty(player.Name))
            {
                autoBackEnabled.Remove(player.Name);
                playerDeathData.Remove(player.Name);
                pendingTeleport.Remove(player.Name);
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            foreach (var playerName in pendingTeleport.ToList())
            {
                var tsPlayer = TShock.Players.FirstOrDefault(p => p != null && p.Name == playerName);
                if (tsPlayer != null && tsPlayer.Active && !tsPlayer.Dead)
                {
                    if (playerDeathData.TryGetValue(playerName, out var deathPosition))
                    {
                        tsPlayer.Teleport(deathPosition.X, deathPosition.Y);
                        tsPlayer.SendSuccessMessage("I've come to bargain!");
                    }
                    pendingTeleport.Remove(playerName);
                }
            }
        }

        private void OnNetGetData(GetDataEventArgs args)
        {
            if (args.MsgID == PacketTypes.PlayerSpawn)
            {
                if (args.Msg == null)
                    return;

                using (BinaryReader br = new(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    byte playerID = br.ReadByte();
                    var player = Main.player[playerID];

                    if (player == null || string.IsNullOrEmpty(player.name))
                        return;

                    if (autoBackEnabled.Contains(player.name) && playerDeathData.ContainsKey(player.name))
                    {
                        pendingTeleport.Add(player.name);
                    }
                }
            }
            else if (args.MsgID == PacketTypes.PlayerDeathV2)
            {
                if (args.Msg == null)
                    return;

                using (BinaryReader br = new(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    byte playerID = br.ReadByte();
                    var player = Main.player[playerID];

                    if (player == null || string.IsNullOrEmpty(player.name))
                        return;

                    playerDeathData[player.name] = new Vector2(player.position.X, player.position.Y);
                }
            }
        }

        private void BackCommand(CommandArgs args)
        {
            var player = args.Player;
            if (player == null || !player.Active || !player.RealPlayer) return;
            if (player.Dead)
            {
                player.SendErrorMessage("You can't use this command while dead.");
                return;
            }

            if (playerDeathData.TryGetValue(player.Name, out var deathPosition))
            {
                player.Teleport(deathPosition.X, deathPosition.Y);
                player.SendSuccessMessage("You have been teleported back to your death location.");
            }
            else
            {
                player.SendErrorMessage("No death location found.");
            }
        }

        private void BackAutoCommand(CommandArgs args)
        {
            var player = args.Player;
            if (player == null || !player.Active || !player.RealPlayer) return;

            if (autoBackEnabled.Contains(player.Name))
            {
                autoBackEnabled.Remove(player.Name);
                player.SendInfoMessage("Auto-back disabled.");
            }
            else
            {
                autoBackEnabled.Add(player.Name);
                player.SendInfoMessage("Auto-back enabled.");
            }
        }
    }
}