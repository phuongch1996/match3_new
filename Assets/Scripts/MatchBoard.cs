using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownGarden
{
    // Independent rules model. Coordinates use a top-left origin.
    public sealed class MatchBoard
    {
        public const int N = 8, Colors = 5;
        public readonly int Width,Height;
        public readonly int[,] Color;
        public readonly int[,] Power; // 1: row, 2: column, 3: bomb, 4: rainbow, 5: propeller
        public readonly bool[,] Ice;
        public readonly bool[,] Active;
        public readonly int[,] Boxes;
        public readonly int[,] MetalCrates;
        public readonly int[,] Vases;
        public readonly int[,] FlowerPots;
        public readonly int[,] Logs;
        public readonly int[,] Lanterns;
        public readonly int[,] PuzzleBoxes;
        public readonly int[,] GiftBoxes;
        public readonly int[,] Flowers;
        public readonly int[,] ChaliceBoxes;
        public readonly int[,] DragonBoxes;
        public readonly int[,] Birdhouses;
        public readonly int[,] Gnomes;
        public readonly int[,] Safes;
        public readonly int[,] Masks;
        public readonly int[,] Turtles;
        public readonly int[,] Snowmen;
        public readonly int[,] SteamBombs;
        public readonly int[,] Knights;
        public readonly int[,] TurtleHouses;
        public readonly int[,] Clocks;
        // Native WallItemModel: non-fallable/non-swappable, near-match damage,
        // multi-hit group member. A layer reaches zero only after its final hit.
        public readonly int[,] Walls;
        public readonly int[,] WallGroup;
        // Column/MetalColumn are native static items made of one master and
        // directional fake cells. Every cell in a group shares one layer value.
        public readonly int[,] Columns;
        public readonly int[,] ColumnGroup;
        public readonly int[,] ColumnDirection;
        public readonly int[,] MetalColumns;
        public readonly int[,] MetalColumnGroup;
        public readonly int[,] MetalColumnDirection;
        // Remaining native ItemType families that are static board blockers.
        // The third dimension is indexed by NativeObstacleKind. Variants retain
        // the original TiledId so the renderer can select the matching art.
        public enum NativeObstacleKind
        {
            Armory, Barrel, Cannon, Gem, JellyMaker, Lightbulb, Lily, Lyre,
            MagicCards, MagicGlobe, MagicOrb, Magnet, Starfish, Tablet, Target, Volcano
        }
        public const int NativeObstacleCount = 16;
        public readonly int[,,] NativeObstacles;
        public readonly int[,] NativeObstacleVariant;
        public readonly int[] Collected = new int[Colors];
        public int IceCleared, BoxesCleared, MetalCratesCleared, VasesCleared, FlowerPotsCleared, LogsCleared, LanternsCleared, PuzzleBoxesCleared, GiftBoxesCleared, FlowersCleared, ChaliceBoxesCleared, DragonBoxesCleared, BirdhousesCleared, GnomesCleared, SafesCleared, MasksCleared, TurtlesCleared, SnowmenCleared, SteamBombsCleared, KnightsCleared, TurtleHousesCleared, ClocksCleared, WallsCleared, ColumnsCleared, MetalColumnsCleared, Score;
        readonly Random random;
        int[] fillColors = {0,1,2,3,4};
        Queue<int>[] predefined;
        public struct Cell : IEquatable<Cell>
        {
            public int x,y;
            public Cell(int x,int y) { this.x=x; this.y=y; }
            public bool Equals(Cell c) { return x==c.x && y==c.y; }
            public override bool Equals(object o) { return o is Cell && Equals((Cell)o); }
            public override int GetHashCode() { return x^(y<<16); }
        }
        public sealed class Match
        {
            public HashSet<Cell> cells = new HashSet<Cell>();
            public Dictionary<Cell,int> creates = new Dictionary<Cell,int>();
            public readonly HashSet<Cell> expandedPowers = new HashSet<Cell>();
            public readonly List<Flight> flights = new List<Flight>();
            public readonly HashSet<Cell> adjacentDamageSources = new HashSet<Cell>();
            public readonly HashSet<Cell> hitBoxes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitMetalCrates = new HashSet<Cell>();
            public readonly HashSet<Cell> hitVases = new HashSet<Cell>();
            public readonly HashSet<Cell> hitFlowerPots = new HashSet<Cell>();
            public readonly HashSet<Cell> hitLogs = new HashSet<Cell>();
            public readonly HashSet<Cell> hitLanterns = new HashSet<Cell>();
            public readonly HashSet<Cell> hitPuzzleBoxes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitGiftBoxes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitFlowers = new HashSet<Cell>();
            public readonly HashSet<Cell> hitChaliceBoxes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitDragonBoxes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitBirdhouses = new HashSet<Cell>();
            public readonly HashSet<Cell> hitGnomes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitSafes = new HashSet<Cell>();
            public readonly HashSet<Cell> hitMasks = new HashSet<Cell>();
            public readonly HashSet<Cell> hitTurtles = new HashSet<Cell>();
            public readonly HashSet<Cell> hitSnowmen = new HashSet<Cell>();
            public readonly HashSet<Cell> hitSteamBombs = new HashSet<Cell>();
            public readonly HashSet<Cell> hitKnights = new HashSet<Cell>();
            public readonly HashSet<Cell> hitTurtleHouses = new HashSet<Cell>();
            public readonly HashSet<Cell> hitClocks = new HashSet<Cell>();
            public readonly HashSet<Cell> hitWalls = new HashSet<Cell>();
            public readonly HashSet<Cell> hitNativeObstacles = new HashSet<Cell>();
            public readonly HashSet<int> hitColumnGroups = new HashSet<int>();
            public readonly HashSet<int> hitMetalColumnGroups = new HashSet<int>();
            public readonly HashSet<Cell> damagedBoxes = new HashSet<Cell>();
        }
        public struct Flight
        {
            public Cell from,to;
            public int payload;
            public Flight(Cell from,Cell to,int payload) { this.from=from; this.to=to; this.payload=payload; }
        }
        public MatchBoard(int seed,int level,int width=N,int height=N,bool[,] activeMask=null)
        {
            if(width<3 || height<3 || width>32 || height>32) throw new ArgumentOutOfRangeException("Board dimensions must be between 3 and 32.");
            Width=width; Height=height;
            Color=new int[width,height]; Power=new int[width,height]; Ice=new bool[width,height];
            Active=new bool[width,height]; Boxes=new int[width,height]; MetalCrates=new int[width,height]; Vases=new int[width,height]; FlowerPots=new int[width,height]; Logs=new int[width,height]; Lanterns=new int[width,height]; PuzzleBoxes=new int[width,height]; GiftBoxes=new int[width,height]; Flowers=new int[width,height]; ChaliceBoxes=new int[width,height]; DragonBoxes=new int[width,height]; Birdhouses=new int[width,height]; Gnomes=new int[width,height]; Safes=new int[width,height]; Masks=new int[width,height]; Turtles=new int[width,height]; Snowmen=new int[width,height]; SteamBombs=new int[width,height]; Knights=new int[width,height]; TurtleHouses=new int[width,height]; Clocks=new int[width,height]; Walls=new int[width,height]; WallGroup=new int[width,height];
            Columns=new int[width,height]; ColumnGroup=new int[width,height]; ColumnDirection=new int[width,height];
            MetalColumns=new int[width,height]; MetalColumnGroup=new int[width,height]; MetalColumnDirection=new int[width,height];
            NativeObstacles=new int[NativeObstacleCount,width,height]; NativeObstacleVariant=new int[width,height];
            predefined=new Queue<int>[width];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) Active[x,y]=activeMask==null || activeMask[x,y];
            for(int x=0;x<width;x++) predefined[x]=new Queue<int>();
            random=new Random(seed);
            FillStable();
            Cell first,second;
            if(!FindMove(out first,out second)) Shuffle();
            if(level>1)
                for(int y=2;y<Math.Min(6,height);y++) for(int x=2;x<Math.Min(6,width);x++)
                    Ice[x,y]=level>=4 || (x+y)%2==0;
        }
        public static bool Inside(int x,int y) { return x>=0 && y>=0 && x<N && y<N; }
        public bool IsInside(int x,int y) { return x>=0 && y>=0 && x<Width && y<Height; }
        public bool IsCell(int x,int y) { return IsInside(x,y) && Active[x,y]; }
        // Replaced color is retained for effects/combinations, but a special is not a match tile.
        public int NativeObstacleAt(int x,int y) { if(!IsInside(x,y)) return 0; for(int kind=0;kind<NativeObstacleCount;kind++) if(NativeObstacles[kind,x,y]>0) return NativeObstacles[kind,x,y]; return 0; }
        public int NativeObstacleKindAt(int x,int y) { if(!IsInside(x,y)) return -1; for(int kind=0;kind<NativeObstacleCount;kind++) if(NativeObstacles[kind,x,y]>0) return kind; return -1; }
        int MatchColor(int x,int y) { return IsCell(x,y) && Boxes[x,y]==0 && MetalCrates[x,y]==0 && Vases[x,y]==0 && FlowerPots[x,y]==0 && Logs[x,y]==0 && Lanterns[x,y]==0 && PuzzleBoxes[x,y]==0 && GiftBoxes[x,y]==0 && Flowers[x,y]==0 && ChaliceBoxes[x,y]==0 && DragonBoxes[x,y]==0 && Birdhouses[x,y]==0 && Gnomes[x,y]==0 && Safes[x,y]==0 && Masks[x,y]==0 && Turtles[x,y]==0 && Snowmen[x,y]==0 && SteamBombs[x,y]==0 && Knights[x,y]==0 && TurtleHouses[x,y]==0 && Clocks[x,y]==0 && Walls[x,y]==0 && Columns[x,y]==0 && MetalColumns[x,y]==0 && NativeObstacleAt(x,y)==0 && Power[x,y]==0?Color[x,y]:-1; }
        public bool IsMovable(Cell c) { return IsCell(c.x,c.y) && Boxes[c.x,c.y]==0 && MetalCrates[c.x,c.y]==0 && Vases[c.x,c.y]==0 && FlowerPots[c.x,c.y]==0 && Logs[c.x,c.y]==0 && Lanterns[c.x,c.y]==0 && PuzzleBoxes[c.x,c.y]==0 && GiftBoxes[c.x,c.y]==0 && Flowers[c.x,c.y]==0 && ChaliceBoxes[c.x,c.y]==0 && DragonBoxes[c.x,c.y]==0 && Birdhouses[c.x,c.y]==0 && Gnomes[c.x,c.y]==0 && Safes[c.x,c.y]==0 && Masks[c.x,c.y]==0 && Turtles[c.x,c.y]==0 && Snowmen[c.x,c.y]==0 && SteamBombs[c.x,c.y]==0 && Knights[c.x,c.y]==0 && TurtleHouses[c.x,c.y]==0 && Clocks[c.x,c.y]==0 && Walls[c.x,c.y]==0 && Columns[c.x,c.y]==0 && MetalColumns[c.x,c.y]==0 && NativeObstacleAt(c.x,c.y)==0 && Color[c.x,c.y]>=0; }
        public void SetWall(Cell c,int layers=1,int groupId=0)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Wall must be on an active cell with at least one layer.");
            Walls[c.x,c.y]=layers; WallGroup[c.x,c.y]=groupId; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetMetalCrate(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Metal crate must be on an active cell with at least one layer.");
            MetalCrates[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetVase(Cell c)
        {
            if(!IsCell(c.x,c.y)) throw new ArgumentOutOfRangeException("Vase must be on an active cell.");
            Vases[c.x,c.y]=1; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetFlowerPot(Cell c,int layers=3)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("FlowerPot must be on an active cell with at least one layer.");
            FlowerPots[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetLog(Cell c,int layers=3)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Log must be on an active cell with at least one layer.");
            Logs[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetLantern(Cell c,int layers=2)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Lantern must be on an active cell with at least one layer.");
            Lanterns[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetPuzzleBox(Cell c,int layers=2)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("PuzzleBox must be on an active cell with at least one layer.");
            PuzzleBoxes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetGiftBox(Cell c,int layers=4)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("GiftBox must be on an active cell with at least one layer.");
            GiftBoxes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetFlowers(Cell c,int layers=4)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Flowers must be on an active cell with at least one layer.");
            Flowers[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetChaliceBox(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("ChaliceBox piece must be on an active cell with at least one layer.");
            ChaliceBoxes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetDragonBox(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("DragonBox piece must be on an active cell with at least one layer.");
            DragonBoxes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetBirdhouse(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Birdhouse must be on an active cell with at least one layer.");
            Birdhouses[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetGnome(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Gnome must be on an active cell with at least one layer.");
            Gnomes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetSafe(Cell c,int layers=2)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Safe must be on an active cell with at least one layer.");
            Safes[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetMask(Cell c,int layers=2)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Mask must be on an active cell with at least one layer.");
            Masks[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetTurtle(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Turtle must be on an active cell with at least one layer.");
            Turtles[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetSnowman(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Snowman piece must be on an active cell with at least one layer.");
            Snowmen[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetSteamBomb(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("SteamBomb piece must be on an active cell with at least one layer.");
            SteamBombs[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetKnight(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Knight piece must be on an active cell with at least one layer.");
            Knights[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetTurtleHouse(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("TurtleHouse piece must be on an active cell with at least one layer.");
            TurtleHouses[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetClock(Cell c,int layers=1)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Clock piece must be on an active cell with at least one layer.");
            Clocks[c.x,c.y]=layers; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        public void SetNativeObstacle(Cell c,NativeObstacleKind kind,int layers,int variant=0)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Native obstacle must be on an active cell with at least one layer.");
            for(int i=0;i<NativeObstacleCount;i++) NativeObstacles[i,c.x,c.y]=0;
            NativeObstacles[(int)kind,c.x,c.y]=layers; NativeObstacleVariant[c.x,c.y]=variant;
            Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
        static int NativeLayerCount(int tile)
        {
            // Values mirror the native TiledId -> layer lookup. Armory's 20-hit
            // base is intentional: it is the source's multi-stage anchor tile.
            switch(tile)
            {
                case 150:return 5; case 96: case 97:return 3; case 59: case 60:return 1;
                case 161: case 162: case 172:return 1; case 171:return 7;
                case 115: case 116: case 117: case 125: case 126: case 127: case 136: case 137:return 1;
                case 118: case 119: case 128: case 129: case 135:return 5; case 98:return 2; case 29:return 1;
                case 141: case 142: case 143: case 152: case 153:return 1;
                case 144: case 145: case 151: case 154: case 155:return 4; case 173:return 3;
                case 61: case 62: case 63: case 64: case 72: case 74:return 1;
                case 71: case 73: case 81: case 82: case 83: case 84: case 85:return 5;
                case 95: case 146:return 1; case 147: case 148: case 149:return 2;
                case 56: case 57: case 58:return 1; case 110:return 3;
                case 158: case 159: case 160: case 168: case 169: case 170: case 179: case 180:return 1;
                case 178:return 20; default:return 0;
            }
        }
        static bool TryNativeObstacle(int tile,out NativeObstacleKind kind)
        {
            switch(tile)
            {
                case 158: case 159: case 160: case 168: case 169: case 170: case 178: case 179: case 180: kind=NativeObstacleKind.Armory; return true;
                case 150: kind=NativeObstacleKind.Barrel; return true; case 96: case 97: kind=NativeObstacleKind.Cannon; return true;
                case 59: case 60: kind=NativeObstacleKind.Gem; return true; case 161: case 162: case 171: case 172: kind=NativeObstacleKind.JellyMaker; return true;
                case 115: case 116: case 117: case 118: case 119: case 125: case 126: case 127: case 128: case 129: case 135: case 136: case 137: kind=NativeObstacleKind.Lightbulb; return true;
                case 98: kind=NativeObstacleKind.Lily; return true; case 29: kind=NativeObstacleKind.Lyre; return true;
                case 141: case 142: case 143: case 144: case 145: case 151: case 152: case 153: case 154: case 155: kind=NativeObstacleKind.MagicCards; return true;
                case 173: kind=NativeObstacleKind.MagicGlobe; return true;
                case 61: case 62: case 63: case 64: case 71: case 72: case 73: case 74: case 81: case 82: case 83: case 84: case 85: kind=NativeObstacleKind.MagicOrb; return true;
                case 95: kind=NativeObstacleKind.Magnet; return true; case 146: kind=NativeObstacleKind.Starfish; return true;
                case 147: case 148: case 149: kind=NativeObstacleKind.Tablet; return true; case 56: case 57: case 58: kind=NativeObstacleKind.Target; return true;
                case 110: kind=NativeObstacleKind.Volcano; return true; default: kind=default(NativeObstacleKind); return false;
            }
        }
        static bool IsNativeTile(int tile) { NativeObstacleKind kind; return TryNativeObstacle(tile,out kind); }
        // Native ColumnItemModel/MetalColumnItemModel are static, non-swappable
        // master items with directional fake cells. Register the whole footprint
        // under one group so a hit on any segment damages the master once.
        public void SetColumn(Cell master,int direction,int length=1,int layers=1,bool metal=false,int groupId=0)
        {
            if(length<1 || layers<1 || direction<1 || direction>4) throw new ArgumentOutOfRangeException("Column direction, length and layers must be positive.");
            int dx=direction==1?-1:direction==2?1:0, dy=direction==3?1:direction==4?-1:0;
            var cells=new List<Cell>();
            for(int i=0;i<length;i++)
            {
                int x=master.x+dx*i,y=master.y+dy*i;
                if(!IsCell(x,y) || Boxes[x,y]>0 || Walls[x,y]>0) throw new ArgumentException("Column footprint must fit active, unobstructed cells.");
                cells.Add(new Cell(x,y));
            }
            if(groupId==0) groupId=NextStaticGroup();
            foreach(var c in cells)
            {
                if(metal) { MetalColumns[c.x,c.y]=layers; MetalColumnGroup[c.x,c.y]=groupId; MetalColumnDirection[c.x,c.y]=direction; }
                else { Columns[c.x,c.y]=layers; ColumnGroup[c.x,c.y]=groupId; ColumnDirection[c.x,c.y]=direction; }
                Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
            }
        }
        int nextStaticGroup=1;
        int NextStaticGroup() { return nextStaticGroup++; }
        static bool IsColumnTile(int tile) { return tile>=43 && tile<=47; }
        static bool IsMetalColumnTile(int tile) { return tile>=105 && tile<=109; }
        static int ColumnDirectionFromTile(int tile)
        {
            if(tile==44 || tile==106) return 1; // Left
            if(tile==45 || tile==107) return 3; // Up
            if(tile==46 || tile==108) return 4; // Down
            if(tile==47 || tile==109) return 2; // Right
            return 0;
        }
        static int DirectionDeltaX(int direction) { return direction==1?-1:direction==2?1:0; }
        static int DirectionDeltaY(int direction) { return direction==3?1:direction==4?-1:0; }
        public bool CanSwap(Cell a,Cell b) { return Adjacent(a,b) && IsMovable(a) && IsMovable(b); }
        public static bool Adjacent(Cell a,Cell b) { return Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)==1; }
        public static int ColorFromTiledId(int tiledId)
        {
            switch(tiledId) { case 1:return 1; case 2:return 2; case 3:return 0; case 4:return 3; case 5:return 4; default:throw new ArgumentException("Unsupported match color tile: "+tiledId); }
        }
        public static MatchBoard FromOriginal(OriginalLevelDefinition definition,int seed)
        {
            if(definition==null || definition.width<3 || definition.width>32 || definition.height<3 || definition.height>32 || definition.tiles==null || definition.tiles.Length!=definition.width*definition.height || definition.boardCount!=1 || definition.moves<=0 || definition.boxGoal<=0)
                throw new ArgumentException("Original level must have one board and a complete tile grid.");
            if(definition.colors==null || definition.colors.Length<3 || definition.colors.Distinct().Count()!=definition.colors.Length)
                throw new ArgumentException("Original color palette is missing or invalid.");
            if(definition.active!=null && definition.active.Length!=definition.width*definition.height)
                throw new ArgumentException("Active mask must contain width*height entries.");
            for(int rawY=0;rawY<definition.height;rawY++) for(int x=0;x<definition.width;x++)
            {
                int index=x+definition.width*rawY, tile=definition.tiles[index];
                bool active=definition.active==null?tile!=0:definition.active[index];
                if(!active && tile!=0) throw new ArgumentException("Inactive cells must use tile id 0.");
                if(tile!=0 && !(tile>=1 && tile<=5) && tile!=24 && tile!=25 && tile!=36 && tile!=26 && tile!=27 && tile!=37 && tile!=38 && tile!=39 && tile!=48 && tile!=49 && tile!=65 && tile!=66 && tile!=67 && tile!=68 && tile!=69 && tile!=70 && tile!=75 && tile!=76 && tile!=77 && tile!=78 && tile!=79 && tile!=80 && tile!=93 && tile!=94 && tile!=103 && tile!=104 && tile!=99 && tile!=100 && tile!=138 && tile!=140 && tile!=157 && tile!=163 && !(tile==41 || tile==42 || tile==51 || tile==52) && !(tile==91 || tile==92 || tile==101 || tile==102) && !(tile>=21 && tile<=23) && !(tile>=43 && tile<=47) && !(tile>=53 && tile<=55) && !(tile>=86 && tile<=90) && !(tile>=105 && tile<=109) && !IsNativeTile(tile)) throw new NotSupportedException("Tile "+tile+" is not supported by this original-level adapter.");
            }
            foreach(var refill in definition.predefined ?? new OriginalRefill[0])
            {
                if(refill.x<0 || refill.x>=definition.width || refill.ids==null) throw new ArgumentException("Invalid predefined column.");
                foreach(int tile in refill.ids) ColorFromTiledId(tile);
            }
            var activeMask=new bool[definition.width,definition.height];
            for(int rawY=0;rawY<definition.height;rawY++) for(int x=0;x<definition.width;x++)
            {
                int index=x+definition.width*rawY;
                activeMask[x,definition.height-1-rawY]=definition.active==null?definition.tiles[index]!=0:definition.active[index];
            }
            var board=new MatchBoard(seed,1,definition.width,definition.height,activeMask);
            board.fillColors=definition.colors.Select(ColorFromTiledId).ToArray();
            for(int y=0;y<board.Height;y++) for(int x=0;x<board.Width;x++)
            {
                int tile=definition.tiles[x+board.Width*y],viewY=board.Height-1-y;
                board.Power[x,viewY]=0; board.Ice[x,viewY]=false; board.Walls[x,viewY]=0; board.WallGroup[x,viewY]=0; board.MetalCrates[x,viewY]=0; board.Vases[x,viewY]=0; board.FlowerPots[x,viewY]=0; board.Logs[x,viewY]=0; board.Lanterns[x,viewY]=0; board.PuzzleBoxes[x,viewY]=0; board.GiftBoxes[x,viewY]=0; board.Flowers[x,viewY]=0; board.ChaliceBoxes[x,viewY]=0; board.DragonBoxes[x,viewY]=0; board.Birdhouses[x,viewY]=0; board.Gnomes[x,viewY]=0; board.Safes[x,viewY]=0; board.Masks[x,viewY]=0; board.Turtles[x,viewY]=0; board.Snowmen[x,viewY]=0; board.SteamBombs[x,viewY]=0; board.Knights[x,viewY]=0; board.TurtleHouses[x,viewY]=0; board.Clocks[x,viewY]=0;
                board.NativeObstacleVariant[x,viewY]=0; for(int nativeKind=0;nativeKind<NativeObstacleCount;nativeKind++) board.NativeObstacles[nativeKind,x,viewY]=0;
                board.Columns[x,viewY]=0; board.ColumnGroup[x,viewY]=0; board.ColumnDirection[x,viewY]=0;
                board.MetalColumns[x,viewY]=0; board.MetalColumnGroup[x,viewY]=0; board.MetalColumnDirection[x,viewY]=0;
                if(!board.Active[x,viewY]) { board.Color[x,viewY]=-1; continue; }
                if(tile>=21 && tile<=23) { board.Boxes[x,viewY]=tile-20; board.Color[x,viewY]=-1; }
                else if(tile>=53 && tile<=55) { board.SetWall(new Cell(x,viewY),5,tile-52); }
                else if(tile>=86 && tile<=88) { board.SetMetalCrate(new Cell(x,viewY),tile-85); }
                else if(tile==26) { board.SetVase(new Cell(x,viewY)); }
                else if(tile==89) { board.SetFlowerPot(new Cell(x,viewY),3); }
                else if(tile==90) { board.SetLog(new Cell(x,viewY),3); }
                else if(tile==36) { board.SetLantern(new Cell(x,viewY),2); }
                else if(tile==157) { board.SetPuzzleBox(new Cell(x,viewY),2); }
                else if(tile==140) { board.SetGiftBox(new Cell(x,viewY),4); }
                else if(tile==163) { board.SetFlowers(new Cell(x,viewY),4); }
                else if(tile==41 || tile==42 || tile==51 || tile==52) { board.SetChaliceBox(new Cell(x,viewY),1); }
                else if(tile==91 || tile==92 || tile==102) { board.SetDragonBox(new Cell(x,viewY),1); }
                else if(tile==101) { board.SetDragonBox(new Cell(x,viewY),4); }
                else if(tile==27) { board.SetBirdhouse(new Cell(x,viewY),1); }
                else if(tile==37 || tile==99 || tile==100) { board.SetGnome(new Cell(x,viewY),tile==37?1:tile==100?2:3); }
                else if(tile==24) { board.SetSafe(new Cell(x,viewY),2); }
                else if(tile==138) { board.SetMask(new Cell(x,viewY),2); }
                else if(tile==25) { board.SetTurtle(new Cell(x,viewY),1); }
                else if(tile==65 || tile==66 || tile==76) { board.SetSnowman(new Cell(x,viewY),1); }
                else if(tile==75) { board.SetSnowman(new Cell(x,viewY),5); }
                else if(tile==67 || tile==68 || tile==78) { board.SetSteamBomb(new Cell(x,viewY),1); }
                else if(tile==77) { board.SetSteamBomb(new Cell(x,viewY),8); }
                else if(tile==69 || tile==70 || tile==80) { board.SetKnight(new Cell(x,viewY),1); }
                else if(tile==79) { board.SetKnight(new Cell(x,viewY),5); }
                else if(tile==93 || tile==94 || tile==104) { board.SetTurtleHouse(new Cell(x,viewY),1); }
                else if(tile==103) { board.SetTurtleHouse(new Cell(x,viewY),5); }
                else if(tile==38 || tile==39 || tile==49) { board.SetClock(new Cell(x,viewY),1); }
                else if(tile==48) { board.SetClock(new Cell(x,viewY),8); }
                else if(IsNativeTile(tile)) { NativeObstacleKind nativeKind; TryNativeObstacle(tile,out nativeKind); board.SetNativeObstacle(new Cell(x,viewY),nativeKind,NativeLayerCount(tile),tile); }
                else if(IsColumnTile(tile) || IsMetalColumnTile(tile)) board.Color[x,viewY]=-1;
                else board.Color[x,viewY]=ColorFromTiledId(tile);
            }
            board.LoadColumnsFromOriginal(definition.tiles);
            // Native AddPredefined traverses entries/Ids in stored order and queues by X.
            foreach(var refill in definition.predefined ?? new OriginalRefill[0])
                foreach(int tile in refill.ids) board.predefined[refill.x].Enqueue(ColorFromTiledId(tile));
            board.AssignWallGroups();
            if(definition.boxGoal>board.RemainingBoxes) throw new ArgumentException("Original Box goal exceeds the supported boxes in this level.");
            return board;
        }
        void LoadColumnsFromOriginal(int[] rawTiles)
        {
            bool[,] used=new bool[Width,Height];
            for(int rawY=0;rawY<Height;rawY++) for(int x=0;x<Width;x++)
            {
                int tile=rawTiles[x+Width*rawY];
                if(!IsCell(x,Height-1-rawY)) continue;
                if(tile!=43 && tile!=105) continue;
                bool metal=tile==105;
                int direction=0,best=0;
                // Prefer the direction whose fake cells form the longest contiguous run.
                for(int candidate=1;candidate<=4;candidate++)
                {
                    int dx=DirectionDeltaX(candidate),dy=DirectionDeltaY(candidate),count=0;
                    for(int step=1;;step++)
                    {
                        int nx=x+dx*step,ny=rawY+dy*step;
                        if(nx<0 || nx>=Width || ny<0 || ny>=Height) break;
                        int next=rawTiles[nx+Width*ny];
                        if(ColumnDirectionFromTile(next)!=candidate || (metal?!IsMetalColumnTile(next):!IsColumnTile(next))) break;
                        count++;
                    }
                    if(count>best) { best=count; direction=candidate; }
                }
                if(direction==0)
                {
                    // A malformed or cropped source can omit the orientation run;
                    // keep the master as a one-cell static item.
                    direction=2;
                }
                var master=new Cell(x,Height-1-rawY);
                int length=1+best;
                int group=NextStaticGroup();
                for(int i=0;i<length;i++)
                {
                    int rawX=x+DirectionDeltaX(direction)*i,rawCellY=rawY+DirectionDeltaY(direction)*i;
                    if(rawX<0 || rawX>=Width || rawCellY<0 || rawCellY>=Height) break;
                    int viewY=Height-1-rawCellY;
                    if(used[rawX,viewY]) continue;
                    used[rawX,viewY]=true;
                    if(metal) { MetalColumns[rawX,viewY]=1; MetalColumnGroup[rawX,viewY]=group; MetalColumnDirection[rawX,viewY]=direction; }
                    else { Columns[rawX,viewY]=1; ColumnGroup[rawX,viewY]=group; ColumnDirection[rawX,viewY]=direction; }
                    Color[rawX,viewY]=-1; Power[rawX,viewY]=0;
                }
            }
            // Preserve isolated fake cells as static one-cell remnants instead of
            // turning unsupported source tiles into falling jewels.
            for(int rawY=0;rawY<Height;rawY++) for(int x=0;x<Width;x++)
            {
                int tile=rawTiles[x+Width*rawY];
                if(!IsColumnTile(tile) && !IsMetalColumnTile(tile)) continue;
                int vy=Height-1-rawY;
                if(!IsCell(x,vy)) continue;
                if((IsMetalColumnTile(tile) && MetalColumns[x,vy]>0) || (IsColumnTile(tile) && Columns[x,vy]>0)) continue;
                int group=NextStaticGroup(),direction=ColumnDirectionFromTile(tile); if(direction==0) direction=2;
                if(IsMetalColumnTile(tile)) { MetalColumns[x,vy]=1; MetalColumnGroup[x,vy]=group; MetalColumnDirection[x,vy]=direction; }
                else { Columns[x,vy]=1; ColumnGroup[x,vy]=group; ColumnDirection[x,vy]=direction; }
                Color[x,vy]=-1; Power[x,vy]=0;
            }
        }
        void AssignWallGroups()
        {
            int next=1;
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                if(Walls[x,y]<=0 || WallGroup[x,y]!=0) continue;
                var queue=new Queue<Cell>(); queue.Enqueue(new Cell(x,y)); WallGroup[x,y]=next;
                while(queue.Count>0)
                {
                    var c=queue.Dequeue();
                    foreach(var d in new[]{new Cell(1,0),new Cell(-1,0),new Cell(0,1),new Cell(0,-1)})
                    {
                        int nx=c.x+d.x,ny=c.y+d.y;
                        if(IsCell(nx,ny) && Walls[nx,ny]>0 && WallGroup[nx,ny]==0)
                        { WallGroup[nx,ny]=next; queue.Enqueue(new Cell(nx,ny)); }
                    }
                }
                next++;
            }
        }
        public int PredefinedRemaining(int column) { return predefined[column].Count; }
        public void Swap(Cell a,Cell b)
        {
            if(!CanSwap(a,b)) throw new InvalidOperationException("Only adjacent movable jewels can be swapped.");
            int c=Color[a.x,a.y]; Color[a.x,a.y]=Color[b.x,b.y]; Color[b.x,b.y]=c;
            c=Power[a.x,a.y]; Power[a.x,a.y]=Power[b.x,b.y]; Power[b.x,b.y]=c;
        }
        void FillStable()
        {
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                if(!Active[x,y] || Boxes[x,y]>0) { Color[x,y]=-1; Power[x,y]=0; continue; }
                int c;
                do { c=fillColors[random.Next(fillColors.Length)]; }
                while((x>=2 && Color[x-1,y]==c && Color[x-2,y]==c) ||
                      (y>=2 && Color[x,y-1]==c && Color[x,y-2]==c) ||
                      (x>=1 && y>=1 && Color[x-1,y]==c && Color[x,y-1]==c && Color[x-1,y-1]==c));
                Color[x,y]=c; Power[x,y]=0;
            }
        }
        public Match FindMatches(Cell? preferred=null)
        {
            var runs=new List<List<Cell>>();
            var squares=new HashSet<List<Cell>>();
            for(int y=0;y<Height;y++)
                for(int x=0;x<Width;)
                {
                    int color=MatchColor(x,y);
                    int end=x+1;
                    while(end<Width && color>=0 && MatchColor(end,y)==color) end++;
                    if(end-x>=3) { var run=new List<Cell>(); for(int k=x;k<end;k++) run.Add(new Cell(k,y)); runs.Add(run); }
                    x=end;
                }
            for(int x=0;x<Width;x++)
                for(int y=0;y<Height;)
                {
                    int color=MatchColor(x,y);
                    int end=y+1;
                    while(end<Height && color>=0 && MatchColor(x,end)==color) end++;
                    if(end-y>=3) { var run=new List<Cell>(); for(int k=y;k<end;k++) run.Add(new Cell(x,k)); runs.Add(run); }
                    y=end;
                }
            // A square is its own valid match, regardless of same-color tiles outside it.
            // Only cells belonging to a matching shape are cleared; unrelated tails remain.
            for(int y=0;y<Height-1;y++) for(int x=0;x<Width-1;x++)
            {
                int color=MatchColor(x,y);
                if(color>=0 && MatchColor(x+1,y)==color && MatchColor(x,y+1)==color && MatchColor(x+1,y+1)==color)
                {
                    var square=new List<Cell>{new Cell(x,y),new Cell(x+1,y),new Cell(x,y+1),new Cell(x+1,y+1)};
                    squares.Add(square); runs.Add(square);
                }
            }
            var result=new Match();
            foreach(var run in runs) result.cells.UnionWith(run);
            result.adjacentDamageSources.UnionWith(result.cells);
            // Merge overlapping shapes: one connected match creates at most one special.
            while(runs.Count>0)
            {
                var group=new HashSet<Cell>(runs[0]);
                var members=new List<List<Cell>>{runs[0]}; runs.RemoveAt(0);
                bool changed=true;
                while(changed)
                {
                    changed=false;
                    for(int i=runs.Count-1;i>=0;i--)
                        if(runs[i].Any(c=>group.Contains(c)))
                        { group.UnionWith(runs[i]); members.Add(runs[i]); runs.RemoveAt(i); changed=true; }
                }
                var lines=members.Where(r=>!squares.Contains(r)).ToList();
                bool crossing=lines.Any(h=>h[0].y==h[1].y && lines.Any(v=>v[0].x==v[1].x && h.Any(c=>v.Contains(c))));
                var four=lines.FirstOrDefault(r=>r.Count==4);
                // Preserve existing stronger patterns: five-line > T/L > four-line > square.
                int type=lines.Any(r=>r.Count>=5)?4:crossing?3:four!=null?(four[0].y==four[1].y?1:2):members.Any(squares.Contains)?5:0;
                if(type>0)
                {
                    // The native MatchGroup has an explicit specialItemCreationCell.
                    // A swap passes the drop cell as preferred; preserve it whenever it
                    // belongs to this match. This is what makes A -> B create at B.
                    Cell? selected=SelectSpecialCreationCell(group,preferred);
                    if(selected.HasValue) result.creates[selected.Value]=type;
                }
            }
            return result;
        }
        Cell? SelectSpecialCreationCell(HashSet<Cell> group,Cell? preferred)
        {
            if(preferred.HasValue && group.Contains(preferred.Value) && Power[preferred.Value.x,preferred.Value.y]==0)
                return preferred.Value;
            var available=group.Where(c=>Power[c.x,c.y]==0).OrderBy(c=>c.y*Width+c.x).ToList();
            return available.Count==0?(Cell?)null:available[available.Count/2];
        }
        public Match Activate(Cell a,int rainbowColor=-1)
        {
            var m=new Match(); m.cells.Add(a);
            Expand(m,rainbowColor);
            return m;
        }
        public Match Combine(Cell a,Cell b)
        {
            var m=new Match();
            int pa=Power[a.x,a.y],pb=Power[b.x,b.y];
            int preferredColor=-1;
            m.cells.Add(a); m.cells.Add(b);
            if(pa==4 && pb==4)
            {
                m.expandedPowers.Add(a); m.expandedPowers.Add(b);
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) m.cells.Add(new Cell(x,y));
            }
            else if(pa==4 || pb==4)
            {
                Cell rainbow=pa==4?a:b;
                Cell target=pa==4?b:a;
                int type=Power[target.x,target.y];
                // Native ElectroBallSpecialStrategy.Start selects the most common
                // eligible MatchItem color, independent of the partner's replaced color.
                int color=type>0?MostCommonColor():Color[target.x,target.y];
                preferredColor=color;
                // The combined rainbow is consumed once; do not also activate its own color.
                m.expandedPowers.Add(rainbow);
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                    if(Color[x,y]==color && (type==0 || Power[x,y]==0))
                    {
                        m.cells.Add(new Cell(x,y));
                        // Only ordinary match items are converted. Other specials
                        // can still chain when a subsequent explosion hits them.
                        if(type>0 && Power[x,y]==0) Power[x,y]=type;
                    }
            }
            else if(pa==5 && pb==5)
            {
                // Native SpinnerSpinnerCombo launches three propellers. The two
                // originals are consumed at the drop cell and each remote target
                // is cleared once, without recursively launching another propeller.
                m.expandedPowers.Add(a); m.expandedPowers.Add(b);
                AddArea(m,b,5);
                var targets=ChoosePropellerTargets(m,MostCommonColor(),3);
                foreach(var destination in targets)
                {
                    m.flights.Add(new Flight(b,destination,0));
                    m.cells.Add(destination);
                }
            }
            else if((pa==5 && pb>=1 && pb<=3) || (pb==5 && pa>=1 && pa<=3))
            {
                // Carry the partner to a remote target, preserving rocket direction.
                // Both original specials are consumed rather than detonating at the launch site.
                m.expandedPowers.Add(a); m.expandedPowers.Add(b);
                int payload=pa==5?pb:pa;
                AddArea(m,b,5);
                var destination=ChoosePropellerTarget(m,MostCommonColor());
                if(destination.HasValue)
                {
                    m.flights.Add(new Flight(b,destination.Value,payload));
                    AddArea(m,destination.Value,payload);
                }
            }
            else if(pa>0 && pb>0 && pa!=5 && pb!=5)
            {
                int radius=(pa==3 && pb==3)?3:1;
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                    if((pa==3 && pb==3)?(Math.Abs(x-b.x)<=radius && Math.Abs(y-b.y)<=radius):
                       (pa==3 || pb==3)?(Math.Abs(x-b.x)<=radius || Math.Abs(y-b.y)<=radius):(x==b.x || y==b.y)) m.cells.Add(new Cell(x,y));
            }
            Expand(m,preferredColor);
            return m;
        }
        void AddArea(Match m,Cell center,int power)
        {
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                if(IsCell(x,y) && ((power==1 && y==center.y) || (power==2 && x==center.x) ||
                   (power==3 && Math.Abs(x-center.x)<=1 && Math.Abs(y-center.y)<=1) ||
                   (power==5 && Math.Abs(x-center.x)+Math.Abs(y-center.y)<=1))) m.cells.Add(new Cell(x,y));
        }
        List<Cell> ChoosePropellerTargets(Match m,int color,int count)
        {
            var result=new List<Cell>();
            var excluded=new HashSet<Cell>();
            for(int i=0;i<count;i++)
            {
                var target=ChoosePropellerTarget(m,color,excluded);
                if(!target.HasValue) break;
                result.Add(target.Value); excluded.Add(target.Value);
            }
            return result;
        }
        Cell? ChoosePropellerTarget(Match m,int color) { return ChoosePropellerTarget(m,color,null); }
        Cell? ChoosePropellerTarget(Match m,int color,HashSet<Cell> excluded)
        {
            for(int priority=0;priority<3;priority++)
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                {
                    var next=new Cell(x,y);
                    if(!IsCell(x,y) || (Color[x,y]<0 && Boxes[x,y]==0) || m.cells.Contains(next) || (excluded!=null && excluded.Contains(next))) continue;
                    if(priority==0?(!Ice[x,y] && Boxes[x,y]==0):priority==1?Color[x,y]!=color:false) continue;
                    return next;
                }
            return null;
        }
        public void Expand(Match m,int rainbowColor=-1)
        {
            m.cells.RemoveWhere(c=>!IsCell(c.x,c.y));
            var queue=new Queue<Cell>(m.cells.OrderBy(c=>c.y*Width+c.x));
            var visited=m.expandedPowers;
            while(queue.Count>0)
            {
                var c=queue.Dequeue(); if(!visited.Add(c)) continue;
                int p=Power[c.x,c.y];
                if(p==0) continue;
                int target=rainbowColor>=0?rainbowColor:MostCommonColor();
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                    if(IsCell(x,y) && ((p==1 && y==c.y) || (p==2 && x==c.x) ||
                       (p==3 && Math.Abs(x-c.x)<=1 && Math.Abs(y-c.y)<=1) || (p==4 && Color[x,y]==target) ||
                       (p==5 && Math.Abs(x-c.x)+Math.Abs(y-c.y)<=1)))
                    { var next=new Cell(x,y); if(m.cells.Add(next)) queue.Enqueue(next); }
                if(p==5)
                {
                    // Hit a distant ice tile first; otherwise target a common-color jewel.
                    var destination=ChoosePropellerTarget(m,target);
                    if(destination.HasValue && m.cells.Add(destination.Value))
                    {
                        m.flights.Add(new Flight(c,destination.Value,0));
                        queue.Enqueue(destination.Value);
                    }
                }
            }
            PrepareBoxHits(m);
        }
        void PrepareBoxHits(Match m)
        {
            foreach(var c in m.cells) if(IsCell(c.x,c.y))
            {
                if(Boxes[c.x,c.y]>0) m.hitBoxes.Add(c);
                if(MetalCrates[c.x,c.y]>0) m.hitMetalCrates.Add(c);
                if(Vases[c.x,c.y]>0) m.hitVases.Add(c);
                if(FlowerPots[c.x,c.y]>0) m.hitFlowerPots.Add(c);
                if(Logs[c.x,c.y]>0) m.hitLogs.Add(c);
                if(Lanterns[c.x,c.y]>0) m.hitLanterns.Add(c);
                if(PuzzleBoxes[c.x,c.y]>0) m.hitPuzzleBoxes.Add(c);
                if(GiftBoxes[c.x,c.y]>0) m.hitGiftBoxes.Add(c);
                if(Flowers[c.x,c.y]>0) m.hitFlowers.Add(c);
                if(ChaliceBoxes[c.x,c.y]>0) m.hitChaliceBoxes.Add(c);
                if(DragonBoxes[c.x,c.y]>0) m.hitDragonBoxes.Add(c);
                if(Birdhouses[c.x,c.y]>0) m.hitBirdhouses.Add(c);
                if(Gnomes[c.x,c.y]>0) m.hitGnomes.Add(c);
                if(Safes[c.x,c.y]>0) m.hitSafes.Add(c);
                if(Masks[c.x,c.y]>0) m.hitMasks.Add(c);
                if(Turtles[c.x,c.y]>0) m.hitTurtles.Add(c);
                if(Snowmen[c.x,c.y]>0) m.hitSnowmen.Add(c);
                if(SteamBombs[c.x,c.y]>0) m.hitSteamBombs.Add(c);
                if(Knights[c.x,c.y]>0) m.hitKnights.Add(c);
                if(TurtleHouses[c.x,c.y]>0) m.hitTurtleHouses.Add(c);
                if(Clocks[c.x,c.y]>0) m.hitClocks.Add(c);
                if(NativeObstacleAt(c.x,c.y)>0) m.hitNativeObstacles.Add(c);
                if(Walls[c.x,c.y]>0) m.hitWalls.Add(c);
                RecordColumnHit(m,c);
            }
            foreach(var source in m.adjacentDamageSources)
                for(int d=0;d<4;d++)
                {
                    int x=source.x+(d==0?1:d==1?-1:0),y=source.y+(d==2?1:d==3?-1:0);
                    if(IsCell(x,y))
                    {
                        if(Boxes[x,y]>0) m.hitBoxes.Add(new Cell(x,y));
                        if(MetalCrates[x,y]>0) m.hitMetalCrates.Add(new Cell(x,y));
                        if(Vases[x,y]>0) m.hitVases.Add(new Cell(x,y));
                        if(FlowerPots[x,y]>0) m.hitFlowerPots.Add(new Cell(x,y));
                        if(Logs[x,y]>0) m.hitLogs.Add(new Cell(x,y));
                        if(Lanterns[x,y]>0) m.hitLanterns.Add(new Cell(x,y));
                        if(PuzzleBoxes[x,y]>0) m.hitPuzzleBoxes.Add(new Cell(x,y));
                        if(GiftBoxes[x,y]>0) m.hitGiftBoxes.Add(new Cell(x,y));
                        if(Flowers[x,y]>0) m.hitFlowers.Add(new Cell(x,y));
                        if(ChaliceBoxes[x,y]>0) m.hitChaliceBoxes.Add(new Cell(x,y));
                        if(DragonBoxes[x,y]>0) m.hitDragonBoxes.Add(new Cell(x,y));
                        if(Birdhouses[x,y]>0) m.hitBirdhouses.Add(new Cell(x,y));
                        if(Gnomes[x,y]>0) m.hitGnomes.Add(new Cell(x,y));
                        if(Safes[x,y]>0) m.hitSafes.Add(new Cell(x,y));
                        if(Masks[x,y]>0) m.hitMasks.Add(new Cell(x,y));
                        if(Turtles[x,y]>0) m.hitTurtles.Add(new Cell(x,y));
                        if(Snowmen[x,y]>0) m.hitSnowmen.Add(new Cell(x,y));
                        if(SteamBombs[x,y]>0) m.hitSteamBombs.Add(new Cell(x,y));
                        if(Knights[x,y]>0) m.hitKnights.Add(new Cell(x,y));
                        if(TurtleHouses[x,y]>0) m.hitTurtleHouses.Add(new Cell(x,y));
                        if(Clocks[x,y]>0) m.hitClocks.Add(new Cell(x,y));
                        if(NativeObstacleAt(x,y)>0) m.hitNativeObstacles.Add(new Cell(x,y));
                        if(Walls[x,y]>0) m.hitWalls.Add(new Cell(x,y));
                        RecordColumnHit(m,new Cell(x,y));
                    }
                }
            m.cells.UnionWith(m.hitBoxes);
        }
        void RecordColumnHit(Match m,Cell c)
        {
            if(!IsCell(c.x,c.y)) return;
            if(Columns[c.x,c.y]>0 && ColumnGroup[c.x,c.y]>0) m.hitColumnGroups.Add(ColumnGroup[c.x,c.y]);
            if(MetalColumns[c.x,c.y]>0 && MetalColumnGroup[c.x,c.y]>0) m.hitMetalColumnGroups.Add(MetalColumnGroup[c.x,c.y]);
        }
        int MostCommonColor()
        {
            int[] counts=new int[Colors];
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) { int color=MatchColor(x,y); if(color>=0) counts[color]++; }
            return Array.IndexOf(counts,counts.Max());
        }
        public void Clear(Match m,int combo)
        {
            PrepareBoxHits(m);
            foreach(var c in m.hitBoxes)
                if(m.damagedBoxes.Add(c) && Boxes[c.x,c.y]>0)
                {
                    Boxes[c.x,c.y]--; Score+=100;
                    if(Boxes[c.x,c.y]==0) BoxesCleared++;
                }
            foreach(var c in m.hitMetalCrates)
                if(IsCell(c.x,c.y) && MetalCrates[c.x,c.y]>0)
                {
                    MetalCrates[c.x,c.y]--; Score+=100;
                    if(MetalCrates[c.x,c.y]==0) MetalCratesCleared++;
                }
            foreach(var c in m.hitVases)
                if(IsCell(c.x,c.y) && Vases[c.x,c.y]>0)
                {
                    Vases[c.x,c.y]=0; VasesCleared++; Score+=100;
                }
            foreach(var c in m.hitFlowerPots)
                if(IsCell(c.x,c.y) && FlowerPots[c.x,c.y]>0)
                {
                    FlowerPots[c.x,c.y]--; Score+=100;
                    if(FlowerPots[c.x,c.y]==0) FlowerPotsCleared++;
                }
            foreach(var c in m.hitLogs)
                if(IsCell(c.x,c.y) && Logs[c.x,c.y]>0)
                {
                    Logs[c.x,c.y]--; Score+=100;
                    if(Logs[c.x,c.y]==0) LogsCleared++;
                }
            foreach(var c in m.hitLanterns)
                if(IsCell(c.x,c.y) && Lanterns[c.x,c.y]>0)
                {
                    Lanterns[c.x,c.y]--; Score+=100;
                    if(Lanterns[c.x,c.y]==0) LanternsCleared++;
                }
            foreach(var c in m.hitPuzzleBoxes)
                if(IsCell(c.x,c.y) && PuzzleBoxes[c.x,c.y]>0)
                {
                    PuzzleBoxes[c.x,c.y]--; Score+=100;
                    if(PuzzleBoxes[c.x,c.y]==0) PuzzleBoxesCleared++;
                }
            foreach(var c in m.hitGiftBoxes)
                if(IsCell(c.x,c.y) && GiftBoxes[c.x,c.y]>0)
                {
                    GiftBoxes[c.x,c.y]--; Score+=100;
                    if(GiftBoxes[c.x,c.y]==0) GiftBoxesCleared++;
                }
            foreach(var c in m.hitFlowers)
                if(IsCell(c.x,c.y) && Flowers[c.x,c.y]>0)
                {
                    Flowers[c.x,c.y]--; Score+=100;
                    if(Flowers[c.x,c.y]==0) FlowersCleared++;
                }
            foreach(var c in m.hitChaliceBoxes)
                if(IsCell(c.x,c.y) && ChaliceBoxes[c.x,c.y]>0)
                {
                    ChaliceBoxes[c.x,c.y]--; Score+=100;
                    if(ChaliceBoxes[c.x,c.y]==0) ChaliceBoxesCleared++;
                }
            foreach(var c in m.hitDragonBoxes)
                if(IsCell(c.x,c.y) && DragonBoxes[c.x,c.y]>0)
                {
                    DragonBoxes[c.x,c.y]--; Score+=100;
                    if(DragonBoxes[c.x,c.y]==0) DragonBoxesCleared++;
                }
            foreach(var c in m.hitBirdhouses)
                if(IsCell(c.x,c.y) && Birdhouses[c.x,c.y]>0)
                {
                    Birdhouses[c.x,c.y]--; Score+=100;
                    if(Birdhouses[c.x,c.y]==0) BirdhousesCleared++;
                }
            foreach(var c in m.hitGnomes)
                if(IsCell(c.x,c.y) && Gnomes[c.x,c.y]>0)
                {
                    Gnomes[c.x,c.y]--; Score+=100;
                    if(Gnomes[c.x,c.y]==0) GnomesCleared++;
                }
            foreach(var c in m.hitSafes)
                if(IsCell(c.x,c.y) && Safes[c.x,c.y]>0)
                {
                    Safes[c.x,c.y]--; Score+=100;
                    if(Safes[c.x,c.y]==0) SafesCleared++;
                }
            foreach(var c in m.hitMasks)
                if(IsCell(c.x,c.y) && Masks[c.x,c.y]>0)
                {
                    Masks[c.x,c.y]--; Score+=100;
                    if(Masks[c.x,c.y]==0) MasksCleared++;
                }
            foreach(var c in m.hitTurtles)
                if(IsCell(c.x,c.y) && Turtles[c.x,c.y]>0)
                {
                    Turtles[c.x,c.y]--; Score+=100;
                    if(Turtles[c.x,c.y]==0) TurtlesCleared++;
                }
            foreach(var c in m.hitSnowmen)
                if(IsCell(c.x,c.y) && Snowmen[c.x,c.y]>0)
                {
                    Snowmen[c.x,c.y]--; Score+=100;
                    if(Snowmen[c.x,c.y]==0) SnowmenCleared++;
                }
            foreach(var c in m.hitSteamBombs)
                if(IsCell(c.x,c.y) && SteamBombs[c.x,c.y]>0)
                {
                    SteamBombs[c.x,c.y]--; Score+=100;
                    if(SteamBombs[c.x,c.y]==0) SteamBombsCleared++;
                }
            foreach(var c in m.hitKnights)
                if(IsCell(c.x,c.y) && Knights[c.x,c.y]>0)
                {
                    Knights[c.x,c.y]--; Score+=100;
                    if(Knights[c.x,c.y]==0) KnightsCleared++;
                }
            foreach(var c in m.hitTurtleHouses)
                if(IsCell(c.x,c.y) && TurtleHouses[c.x,c.y]>0)
                {
                    TurtleHouses[c.x,c.y]--; Score+=100;
                    if(TurtleHouses[c.x,c.y]==0) TurtleHousesCleared++;
                }
            foreach(var c in m.hitClocks)
                if(IsCell(c.x,c.y) && Clocks[c.x,c.y]>0)
                {
                    Clocks[c.x,c.y]--; Score+=100;
                    if(Clocks[c.x,c.y]==0) ClocksCleared++;
                }
            foreach(var c in m.hitNativeObstacles)
            {
                int kind=NativeObstacleKindAt(c.x,c.y);
                if(IsCell(c.x,c.y) && kind>=0 && NativeObstacles[kind,c.x,c.y]>0)
                {
                    NativeObstacles[kind,c.x,c.y]--; Score+=100;
                    if(NativeObstacles[kind,c.x,c.y]==0) NativeObstacleVariant[c.x,c.y]=0;
                }
            }
            foreach(var c in m.hitWalls)
                if(IsCell(c.x,c.y) && Walls[c.x,c.y]>0)
                {
                    Walls[c.x,c.y]--; Score+=100;
                    if(Walls[c.x,c.y]==0)
                    {
                        int group=WallGroup[c.x,c.y];
                        if(group>0)
                            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                                if(WallGroup[x,y]==group && Walls[x,y]>0) { Walls[x,y]=0; WallsCleared++; }
                        WallsCleared++;
                    }
                }
            foreach(int group in m.hitColumnGroups)
            {
                bool damaged=false,cleared=false;
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                    if(ColumnGroup[x,y]==group && Columns[x,y]>0) { Columns[x,y]--; damaged=true; }
                if(!damaged) continue;
                Score+=100;
                bool any=false;
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) if(ColumnGroup[x,y]==group && Columns[x,y]>0) any=true;
                if(!any)
                {
                    for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                        if(ColumnGroup[x,y]==group) { Columns[x,y]=0; ColumnGroup[x,y]=0; ColumnDirection[x,y]=0; }
                    ColumnsCleared++; cleared=true;
                }
            }
            foreach(int group in m.hitMetalColumnGroups)
            {
                bool damaged=false;
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                    if(MetalColumnGroup[x,y]==group && MetalColumns[x,y]>0) { MetalColumns[x,y]--; damaged=true; }
                if(!damaged) continue;
                Score+=100;
                bool any=false;
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) if(MetalColumnGroup[x,y]==group && MetalColumns[x,y]>0) any=true;
                if(!any)
                {
                    for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                        if(MetalColumnGroup[x,y]==group) { MetalColumns[x,y]=0; MetalColumnGroup[x,y]=0; MetalColumnDirection[x,y]=0; }
                    MetalColumnsCleared++;
                }
            }
            foreach(var c in m.cells)
            {
                if(!IsCell(c.x,c.y) || Boxes[c.x,c.y]>0) continue;
                int color=Color[c.x,c.y];
                if(color<0) continue;
                Collected[color]++; Score+=60*Math.Max(1,combo);
                if(Ice[c.x,c.y]) { Ice[c.x,c.y]=false; IceCleared++; Score+=100; }
                if(m.creates.ContainsKey(c)) Power[c.x,c.y]=m.creates[c];
                else { Color[c.x,c.y]=-1; Power[c.x,c.y]=0; }
            }
        }
        bool IsGravityBlocker(int x,int y)
        {
            return !IsCell(x,y) || Boxes[x,y]>0 || MetalCrates[x,y]>0 || Vases[x,y]>0 || FlowerPots[x,y]>0 || Logs[x,y]>0 || Lanterns[x,y]>0 || PuzzleBoxes[x,y]>0 || GiftBoxes[x,y]>0 || Flowers[x,y]>0 || ChaliceBoxes[x,y]>0 || DragonBoxes[x,y]>0 || Birdhouses[x,y]>0 || Gnomes[x,y]>0 || Safes[x,y]>0 || Masks[x,y]>0 || Turtles[x,y]>0 || Snowmen[x,y]>0 || SteamBombs[x,y]>0 || Knights[x,y]>0 || TurtleHouses[x,y]>0 || Clocks[x,y]>0 || Walls[x,y]>0 || Columns[x,y]>0 || MetalColumns[x,y]>0 || NativeObstacleAt(x,y)>0;
        }
        bool IsFallableItem(int x,int y)
        {
            return IsCell(x,y) && !IsGravityBlocker(x,y) && Color[x,y]>=0;
        }
        // Returns original row per destination; negative origins represent incoming pieces.
        // Native FallManager performs straight falls, then cross falls, then top refills.
        public int[,] Collapse()
        {
            int[,] from=new int[Width,Height];
            for(int x=0;x<Width;x++)
            {
                int bottom=Height-1;
                while(bottom>=0)
                {
                    if(IsGravityBlocker(x,bottom)) { from[x,bottom]=bottom; bottom--; continue; }
                    int top=bottom;
                    while(top>=0 && !IsGravityBlocker(x,top)) top--;
                    int dest=bottom;
                    for(int y=bottom;y>top;y--) if(Color[x,y]>=0)
                    {
                        Color[x,dest]=Color[x,y]; Power[x,dest]=Power[x,y]; from[x,dest]=y; dest--;
                    }
                    for(int y=dest;y>top;y--)
                    {
                        // Keep holes open until cross-fall has had a chance to use them.
                        Color[x,y]=-1; Power[x,y]=0; from[x,y]=y;
                    }
                    bottom=top;
                }
            }

            // CalculateCrossFalls: after straight compression, a jewel may enter a
            // lower empty cell diagonally from the adjacent column. Process bottom-up
            // so a source is reserved once and cannot be claimed by two destinations.
            for(int y=Height-1;y>=0;y--) for(int x=0;x<Width;x++)
            {
                if(IsGravityBlocker(x,y) || Color[x,y]>=0) continue;
                int sourceX=-1,sourceY=y-1;
                foreach(int candidateX in new[]{x-1,x+1})
                {
                    if(candidateX<0 || candidateX>=Width || sourceY<0) continue;
                    if(!IsFallableItem(candidateX,sourceY)) continue;
                    sourceX=candidateX; break;
                }
                if(sourceX<0) continue;
                Color[x,y]=Color[sourceX,sourceY]; Power[x,y]=Power[sourceX,sourceY]; from[x,y]=sourceY;
                Color[sourceX,sourceY]=-1; Power[sourceX,sourceY]=0;
            }

            // AddTopBoardRemainingItemsToQueue: only segments connected to the top
            // board edge receive new items; this also fills holes created when a
            // source jewel leaves its column during cross-fall.
            for(int x=0;x<Width;x++)
            {
                int top=0;
                while(top<Height && !IsGravityBlocker(x,top)) top++;
                int empty=0;
                for(int y=top-1;y>=0;y--) if(Color[x,y]<0) empty++;
                for(int y=top-1;y>=0;y--) if(Color[x,y]<0)
                {
                    Color[x,y]=NextColor(x); Power[x,y]=0; from[x,y]=y-empty;
                }
            }
            return from;
        }
        int NextColor(int column) { return predefined[column].Count>0?predefined[column].Dequeue():fillColors[random.Next(fillColors.Length)]; }
        public bool FindMove(out Cell a,out Cell b)
        {
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                a=new Cell(x,y);
                if(!IsMovable(a)) continue;
                if(Power[x,y]>0) { b=a; return true; }
                for(int d=0;d<2;d++)
                {
                    b=new Cell(x+(d==0?1:0),y+(d==1?1:0));
                    if(!IsMovable(b)) continue;
                    Swap(a,b); bool valid=FindMatches().cells.Count>0; Swap(a,b);
                    if(valid) return true;
                }
            }
            a=b=new Cell(-1,-1); return false;
        }
        public bool Shuffle()
        {
            Cell a,b;
            for(int attempt=0;attempt<100;attempt++) { FillStable(); if(FindMove(out a,out b)) return true; }
            return false;
        }
        public int RemainingBoxes { get { int count=0; foreach(int layers in Boxes) if(layers>0) count++; return count; } }
        public int RemainingMetalCrates { get { int count=0; foreach(int layers in MetalCrates) if(layers>0) count++; return count; } }
        public int RemainingVases { get { int count=0; foreach(int layers in Vases) if(layers>0) count++; return count; } }
        public int RemainingFlowerPots { get { int count=0; foreach(int layers in FlowerPots) if(layers>0) count++; return count; } }
        public int RemainingLogs { get { int count=0; foreach(int layers in Logs) if(layers>0) count++; return count; } }
        public int RemainingLanterns { get { int count=0; foreach(int layers in Lanterns) if(layers>0) count++; return count; } }
        public int RemainingPuzzleBoxes { get { int count=0; foreach(int layers in PuzzleBoxes) if(layers>0) count++; return count; } }
        public int RemainingGiftBoxes { get { int count=0; foreach(int layers in GiftBoxes) if(layers>0) count++; return count; } }
        public int RemainingFlowers { get { int count=0; foreach(int layers in Flowers) if(layers>0) count++; return count; } }
        public int RemainingChaliceBoxes { get { int count=0; foreach(int layers in ChaliceBoxes) if(layers>0) count++; return count; } }
        public int RemainingDragonBoxes { get { int count=0; foreach(int layers in DragonBoxes) if(layers>0) count++; return count; } }
        public int RemainingBirdhouses { get { int count=0; foreach(int layers in Birdhouses) if(layers>0) count++; return count; } }
        public int RemainingGnomes { get { int count=0; foreach(int layers in Gnomes) if(layers>0) count++; return count; } }
        public int RemainingSafes { get { int count=0; foreach(int layers in Safes) if(layers>0) count++; return count; } }
        public int RemainingMasks { get { int count=0; foreach(int layers in Masks) if(layers>0) count++; return count; } }
        public int RemainingTurtles { get { int count=0; foreach(int layers in Turtles) if(layers>0) count++; return count; } }
        public int RemainingSnowmen { get { int count=0; foreach(int layers in Snowmen) if(layers>0) count++; return count; } }
        public int RemainingSteamBombs { get { int count=0; foreach(int layers in SteamBombs) if(layers>0) count++; return count; } }
        public int RemainingKnights { get { int count=0; foreach(int layers in Knights) if(layers>0) count++; return count; } }
        public int RemainingTurtleHouses { get { int count=0; foreach(int layers in TurtleHouses) if(layers>0) count++; return count; } }
        public int RemainingClocks { get { int count=0; foreach(int layers in Clocks) if(layers>0) count++; return count; } }
        public int RemainingNativeObstacles(NativeObstacleKind kind) { int count=0; int k=(int)kind; for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) if(NativeObstacles[k,x,y]>0) count++; return count; }
        public int RemainingNativeObstaclesTotal { get { int count=0; for(int kind=0;kind<NativeObstacleCount;kind++) for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) if(NativeObstacles[kind,x,y]>0) count++; return count; } }
        public int RemainingIce { get { int count=0; foreach(bool ice in Ice) if(ice) count++; return count; } }
    }
}
