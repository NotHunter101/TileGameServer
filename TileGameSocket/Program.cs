using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Linq;

namespace TileGameSocket
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var start = DateTime.UtcNow;
            Console.WriteLine("Start Generation!");
            var world = new GameServer();
            var task = Task.Run(() => world.GenerateWorld(1750, 900, DateTime.UtcNow));

            var wssv = new WebSocketServer("ws://192.168.0.2:5000");
            wssv.AddWebSocketService<GameClient>("/");
            wssv.Start();
            Console.ReadKey(true);
            wssv.Stop();
        }
    }

    public class Vector2
    {
        public double x;
        public double y;

        public Vector2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class GameClient : WebSocketBehavior
    {
        public Vector2 position;
        private bool[,] chunksDiscovered;
        private int chunksWidth;
        private int chunksHeight;
        private int biome;
        private int playerID;

        protected override void OnOpen()
        {
            playerID = GameServer.Instance.AddPlayer(this);
            Console.WriteLine($"Client Connected From: {Context.UserEndPoint} (ID: {playerID})");

            chunksWidth = (int)Math.Ceiling((double)GameServer.Instance.width / 50);
            chunksHeight = (int)Math.Ceiling((double)GameServer.Instance.height / 50);
            chunksDiscovered = new bool[chunksWidth, chunksHeight];

            var outMessage = new WorldDataMessage(GameServer.Instance, playerID);
            string json = JsonConvert.SerializeObject(outMessage);
            Send(json);

            var outMessage2 = new PlayerJoinedMessage(playerID);
            string json2 = JsonConvert.SerializeObject(outMessage2);
            Broadcast(json2);

            UpdatePos();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine($"Client Disconnected (ID: {playerID})");
            GameServer.Instance.players.Remove(playerID);

            var outMessage = new PlayerLeftMessage(playerID);
            string json = JsonConvert.SerializeObject(outMessage);
            Broadcast(json);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            dynamic message = JsonConvert.DeserializeObject<dynamic>(e.Data);
            if (message.type == "SetPosition")
            {
                position = new Vector2((double)message.position[0], (double)message.position[1]);
                UpdatePos();
            }
            else if (message.type == "SetTile")
            {
                var tilePos = new Tuple<int, int>((int)message.position[0], (int)message.position[1]);
                Tile tile = null;

                if (message.tile != null)
                    tile = new Tile((int)message.tile.tile_index, tilePos, (int)message.tile.item_drop);
                GameServer.Instance.world[message.position[0], message.position[1]] = tile;
                Broadcast(e.Data);
            }
        }

        private void UpdatePos()
        {
            RecalculateChunks();

            biome = GameServer.Instance.BiomeAtPos(position);
            var outMessage = new BiomeMessage(biome);
            string json = JsonConvert.SerializeObject(outMessage);
            Send(json);

            var outMessage2 = new SetPlayerPositionMessage(position, playerID);
            string json2 = JsonConvert.SerializeObject(outMessage2);
            Broadcast(json2);
        }

        private void RecalculateChunks()
        {
            Vector2 index = new Vector2(Math.Floor(position.x / 40 / 50), Math.Floor(position.y / 40 / 50));

            var chunks = new Vector2[]
            {
                    new Vector2(index.x - 1, index.y - 1),
                    new Vector2(index.x, index.y - 1),
                    new Vector2(index.x + 1, index.y - 1),
                    new Vector2(index.x - 1, index.y),
                    new Vector2(index.x, index.y),
                    new Vector2(index.x + 1, index.y),
                    new Vector2(index.x - 1, index.y + 1),
                    new Vector2(index.x, index.y + 1),
                    new Vector2(index.x + 1, index.y + 1),
            };

            var tiles = new List<Tile>();
            foreach (var chunk in chunks)
            {
                if (chunk.x < 0 || chunk.x >= chunksWidth || chunk.y < 0 || chunk.y >= chunksHeight)
                    continue;

                if (chunksDiscovered[(int)chunk.x, (int)chunk.y])
                    continue;

                chunksDiscovered[(int)chunk.x, (int)chunk.y] = true;
                Vector2 origin = new Vector2(Math.Floor(chunk.x * 50), Math.Floor(chunk.y * 50));

                for (var x = origin.x; x < origin.x + 50; x++)
                {
                    for (var y = origin.y; y < origin.y + 50; y++)
                    {
                        if (x < 0 || x >= GameServer.Instance.width || y < 0 || y >= GameServer.Instance.height)
                            continue;

                        tiles.Add(GameServer.Instance.world[(int)x, (int)y]);
                    }
                }
            }

            if (tiles.Count != 0)
            {
                var outMessage = new ChunkMessage(tiles, position);
                string json = JsonConvert.SerializeObject(outMessage);
                Send(json);
            }
        }

        private void Broadcast(string message)
        {
            foreach (var session in Sessions.Sessions)
            {
                if (session == this || session.State != WebSocketState.Open)
                    continue;

                session.Context.WebSocket.Send(message);
            }
        }
    }
}
