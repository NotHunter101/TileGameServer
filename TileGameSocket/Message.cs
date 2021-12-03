using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileGameSocket
{
    public class BiomeMessage
    {
        public string type;
        public int biome;

        public BiomeMessage(int biome)
        {
            this.type = "biome";
            this.biome = biome;
        }
    }

    public class ChunkMessage
    {
        public string type;
        public Tile[] tiles;
        public Vector2 position;

        public ChunkMessage(List<Tile> tiles, Vector2 position)
        {
            this.type = "chunk";
            this.tiles = tiles.ToArray();
            this.position = position;
        }
    }

    public class WorldDataMessage
    {
        public class Player
        {
            public Vector2 position;
            public int id;
        }


        public string type;
        public int[] heightMap;
        public Vector2 size;
        public Vector2 spawnPosition;
        public int localId;
        public Player[] players;

        public WorldDataMessage(GameServer world, int localId)
        {
            this.type = "world_data_";
            this.heightMap = world.heightMap;
            this.size = new Vector2(world.width, world.height);

            Vector2 spawnBlock = new Vector2(Math.Floor((double)world.width / 2), 0);
            for (var y = 0; y < world.height; y++)
            {
                if (world.world[(int)spawnBlock.x, y] != null)
                {
                    spawnBlock.y = y;
                    break;
                }
            }

            this.spawnPosition = new Vector2(spawnBlock.x * 40, spawnBlock.y * 40 - 90);
            GameServer.Instance.players[localId].position = this.spawnPosition;

            players = new Player[GameServer.Instance.players.Count];
            for (var i = 0; i < players.Length; i++)
            {
                var dictValue = GameServer.Instance.players.ElementAt(i);
                players[i] = new Player();
                players[i].position = dictValue.Value.position;
                players[i].id = dictValue.Key;
            }

            this.localId = localId;
        }
    }

    public class PlayerJoinedMessage
    {
        public string type;
        public int playerId;

        public PlayerJoinedMessage(int playerId)
        {
            this.type = "player_joined";
            this.playerId = playerId;
        }
    }

    public class PlayerLeftMessage
    {
        public string type;
        public int playerId;

        public PlayerLeftMessage(int playerId)
        {
            this.type = "player_left";
            this.playerId = playerId;
        }
    }

    public class SetPlayerPositionMessage
    {
        public string type;
        public Vector2 position;
        public int playerId;

        public SetPlayerPositionMessage(Vector2 position, int playerId)
        {
            this.type = "set_player_position";
            this.position = position;
            this.playerId = playerId;
        }
    }
}
