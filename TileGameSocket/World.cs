using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileGameSocket
{
    public class Tile
    {
        public int tileIndex;
        [NonSerialized] public Tuple<int, int> size;
        public Tuple<int, int> position;
        public int itemDrop;

        public Tile(int tileId, Tuple<int, int> pos, int dropId)
        {
            tileIndex = tileId;
            size = new Tuple<int, int>(40, 40);
            position = new Tuple<int, int>(pos.Item1 * size.Item1,
                                           pos.Item2 * size.Item2);
            itemDrop = dropId;
        }
    }

    public class Ore
    {
        public double deathCount;
        public double birthCount;
        public double initialChance;
        public int tileIndex;
        public int itemIndex;

        public Ore(double b, double d, double c, int i, int i2)
        {
            deathCount = d;
            birthCount = b;
            initialChance = c;
            tileIndex = i;
            itemIndex = i2;
        }
    }

    public class GameServer
    {
        public static GameServer Instance;

        public Tile[,] world;
        public int[,] biomeMap;
        public int[] heightMap;
        public Dictionary<int, GameClient> players;
        private int playerLength;

        private bool[,] caveMap;
        private List<bool[,]> oreMaps;
        private Ore[] ores = new[] { new Ore(1, 0, .005, 8, 16),
                                     new Ore(1, 0, .0075, 10, 18),
                                     new Ore(2, 2, .015, 9, 17),
                                     new Ore(2, 2, .0125, 11, 19),
                                     new Ore(2, 2, .005, 12, 20)};
        public int width;
        public int height;
        private Random rand;
        private LibNoise.Perlin noiseGenerator;

        public GameServer()
        {
            Instance = this;
        }

        public void GenerateWorld(int w, int h, DateTime start)
        {
            width = w;
            height = h;

            heightMap = new int[width];
            world = new Tile[width, height];
            biomeMap = new int[width, height];
            oreMaps = new List<bool[,]>();
            rand = new Random();
            noiseGenerator = new LibNoise.Perlin();
            players = new Dictionary<int, GameClient>();

            int heightSeed = (int)Math.Ceiling(rand.NextDouble() * 1000);
            int mountainSeed = (int)Math.Ceiling(rand.NextDouble() * 1000);
            int edgeSeed = (int)Math.Ceiling(rand.NextDouble() * 1000);
            int depthSeed = (int)Math.Ceiling(rand.NextDouble() * 1000);

            GenerateCaves();
            int[] b = GenerateBiomes();

            for (var x = 0; x < width; x++)
            {
                double flats = height / 2 - 20 + (noiseGenerator.GetValue((double)x / 400, heightSeed, 5) * 20);
                double mountains = height / 2 - 40 + (noiseGenerator.GetValue((double)x / 100, mountainSeed, 5) * 60);
                int heightMax = (int)Math.Ceiling(Math.Max((float)flats, (float)mountains));
                heightMap[x] = heightMax;

                for (var y = 0; y < height; y++)
                {
                    double biomeEdge = (int)Math.Floor(noiseGenerator.GetValue((double)y / 400, edgeSeed, 1) * 40);
                    int biomeIndex = (int)Math.Min(width - 1, Math.Max(0, Math.Floor(x + biomeEdge)));
                    biomeMap[x, y] = b[biomeIndex];

                    if (y < heightMax)
                        continue;

                    int depthEdge = (int)Math.Ceiling(noiseGenerator.GetValue((double)x / 400, depthSeed, 5) * 10);

                    if (y > height / 2 && !caveMap[x, y - height / 2] && y > heightMax + 20 + depthEdge)
                        continue;

                    bool isOre = false;
                    for (var i = 0; i < ores.Length; i++)
                    {
                        var ore = ores[i];
                        var oreMap = oreMaps[i];

                        if (y > height / 2 && oreMap[x, y - height / 2] && y > heightMax + 30 + depthEdge)
                        {
                            world[x, y] = new Tile(ore.tileIndex, new Tuple<int, int>(x, y), ore.itemIndex);
                            isOre = true;
                        }
                    }

                    if (isOre)
                        continue;

                    Tile tile = GetBiomeTile(biomeMap[x, y], new Tuple<int, int>(x, y), heightMax);
                    world[x, y] = tile;
                }
            }

            Console.WriteLine("Finished Generation: " + (DateTime.UtcNow - start).TotalSeconds + "s elapsed.");
        }

        private int[] GenerateBiomes()
        {
            int accumulatedWidth = 0;
            int[] biomes = new int[width];

            while (accumulatedWidth < width)
            {
                var start = accumulatedWidth;
                var biomeWidth = rand.Next(20, 40);
                var id = rand.Next(0, 4);
                accumulatedWidth += biomeWidth;

                for (var x = start; x < Math.Min(width, start + biomeWidth); x++)
                    biomes[x] = id;
            }

            return biomes;
        }

        private void GenerateCaves()
        {
            var startingChance = 0.525;
            var stepCount = 20;
            var deathLimit = 4;
            var birthLimit = 5;

            caveMap = new bool[width, height];

            for (var x = 0; x < width; x++)
                for (var y = 0; y < height / 2; y++)
                    caveMap[x, y] = rand.NextDouble() < startingChance;

            GenerateOre();

            for (var i = 0; i < stepCount; i++)
                caveMap = RunAutomataStep(caveMap, deathLimit, birthLimit, true);
        }

        private void GenerateOre()
        {
            foreach (Ore ore in ores)
            {
                bool[,] oreMap = new bool[width, height / 2];

                for (var x = 0; x < width; x++)
                    for (var y = 0; y < height / 2; y++)
                        oreMap[x, y] = rand.NextDouble() < ore.initialChance;

                oreMap = RunAutomataStep(oreMap, ore.deathCount, ore.birthCount, false);
                oreMaps.Add(oreMap);
            }
        }

        private bool[,] RunAutomataStep(bool[,] original, double dLimit, double bLimit, bool edgeIsNeighbor)
        {
            int originalWidth = original.GetLength(0);
            int originalHeight = original.GetLength(1);
            bool[,] newMap = new bool[originalWidth, originalHeight];

            for (var x = 0; x < originalWidth; x++)
            {
                for (var y = 0; y < originalHeight; y++)
                {
                    newMap[x, y] = original[x, y];
                    int aliveNeighbors = 0;
                    (int, int)[] neighbors = new[]
                    {
                        (x - 1, y - 1),
                        (x, y - 1),
                        (x + 1, y - 1),
                        (x - 1, y),
                        (x + 1, y),
                        (x - 1, y + 1),
                        (x, y + 1),
                        (x + 1, y + 1)
                    };

                    foreach ((int x2, int y2) in neighbors)
                    {
                        if (x2 < 0 || x2 >= originalWidth || y2 < 0 || y2 >= originalHeight)
                        {
                            if (edgeIsNeighbor)
                                aliveNeighbors++;

                            continue;
                        }

                        if (original[x2, y2])
                            aliveNeighbors++;
                    }

                    if (aliveNeighbors >= bLimit)
                        newMap[x, y] = true;
                    if (aliveNeighbors < dLimit)
                        newMap[x, y] = false;
                }
            }

            return newMap;
        }

        public int BiomeAtPos(Vector2 position)
        {
            Vector2 tilePos = new Vector2(Math.Floor(position.x / 40), Math.Floor(position.y / 40));
            return biomeMap[(int)Math.Min(width - 1, Math.Max(0, tilePos.x)), (int)Math.Min(height - 1, Math.Max(0, tilePos.y))];
        }

        private Tile GetBiomeTile(double biome, Tuple<int, int> position, int worldHeight)
        {
            if (biome == 0)
            {
                if (position.Item2 == worldHeight)
                    return new Tile(0, position, 0);

                if (position.Item2 > worldHeight + 20)
                    return new Tile(7, position, 3);

                return new Tile(1, position, 0);
            }

            if (biome == 1)
            {
                if (position.Item2 == worldHeight)
                    return new Tile(4, position, 1);

                return new Tile(5, position, 1);
            }

            if (biome == 2)
            {
                if (position.Item2 == worldHeight)
                    return new Tile(2, position, 2);

                return new Tile(3, position, 2);
            }

            if (biome == 3)
            {
                if (position.Item2 == worldHeight)
                    return new Tile(6, position, 0);

                if (position.Item2 > worldHeight + 20)
                    return new Tile(7, position, 3);

                return new Tile(1, position, 0);
            }

            return new Tile(0, position, 0);
        }

        public int AddPlayer(GameClient player)
        {
            int id = playerLength;
            players.Add(id, player);
            playerLength++;
            return id;
        }
    }
}
