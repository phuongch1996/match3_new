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
        // Native WallItemModel: non-fallable/non-swappable, near-match damage,
        // multi-hit group member. A layer reaches zero only after its final hit.
        public readonly int[,] Walls;
        public readonly int[,] WallGroup;
        public readonly int[] Collected = new int[Colors];
        public int IceCleared, BoxesCleared, WallsCleared, Score;
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
            public readonly HashSet<Cell> hitWalls = new HashSet<Cell>();
            public readonly HashSet<Cell> damagedBoxes = new HashSet<Cell>();
        }
        public struct Flight
        {
            public Cell from,to;
            public int payload;
            public Flight(Cell from,Cell to,int payload) { this.from=from; this.to=to; this.payload=payload; }
        }
        public MatchBoard(int seed,int level,int width=N,int height=N)
        {
            if(width<3 || height<3 || width>32 || height>32) throw new ArgumentOutOfRangeException("Board dimensions must be between 3 and 32.");
            Width=width; Height=height;
            Color=new int[width,height]; Power=new int[width,height]; Ice=new bool[width,height];
            Active=new bool[width,height]; Boxes=new int[width,height]; Walls=new int[width,height]; WallGroup=new int[width,height]; predefined=new Queue<int>[width];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) Active[x,y]=true;
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
        int MatchColor(int x,int y) { return IsCell(x,y) && Boxes[x,y]==0 && Walls[x,y]==0 && Power[x,y]==0?Color[x,y]:-1; }
        public bool IsMovable(Cell c) { return IsCell(c.x,c.y) && Boxes[c.x,c.y]==0 && Walls[c.x,c.y]==0 && Color[c.x,c.y]>=0; }
        public void SetWall(Cell c,int layers=1,int groupId=0)
        {
            if(!IsCell(c.x,c.y) || layers<1) throw new ArgumentOutOfRangeException("Wall must be on an active cell with at least one layer.");
            Walls[c.x,c.y]=layers; WallGroup[c.x,c.y]=groupId; Color[c.x,c.y]=-1; Power[c.x,c.y]=0;
        }
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
            foreach(int tile in definition.tiles)
                if(!(tile>=1 && tile<=5) && !(tile>=21 && tile<=23) && !(tile>=53 && tile<=55)) throw new NotSupportedException("Tile "+tile+" is not supported by this original-level adapter.");
            foreach(var refill in definition.predefined ?? new OriginalRefill[0])
            {
                if(refill.x<0 || refill.x>=definition.width || refill.ids==null) throw new ArgumentException("Invalid predefined column.");
                foreach(int tile in refill.ids) ColorFromTiledId(tile);
            }
            var board=new MatchBoard(seed,1,definition.width,definition.height);
            board.fillColors=definition.colors.Select(ColorFromTiledId).ToArray();
            for(int y=0;y<board.Height;y++) for(int x=0;x<board.Width;x++)
            {
                int tile=definition.tiles[x+board.Width*y],viewY=board.Height-1-y;
                board.Power[x,viewY]=0; board.Ice[x,viewY]=false; board.Walls[x,viewY]=0; board.WallGroup[x,viewY]=0;
                if(tile>=21 && tile<=23) { board.Boxes[x,viewY]=tile-20; board.Color[x,viewY]=-1; }
                else if(tile>=53 && tile<=55) { board.SetWall(new Cell(x,viewY),5,tile-52); }
                else board.Color[x,viewY]=ColorFromTiledId(tile);
            }
            // Native AddPredefined traverses entries/Ids in stored order and queues by X.
            foreach(var refill in definition.predefined ?? new OriginalRefill[0])
                foreach(int tile in refill.ids) board.predefined[refill.x].Enqueue(ColorFromTiledId(tile));
            board.AssignWallGroups();
            if(definition.boxGoal>board.RemainingBoxes) throw new ArgumentException("Original Box goal exceeds the supported boxes in this level.");
            return board;
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
                if(Walls[c.x,c.y]>0) m.hitWalls.Add(c);
            }
            foreach(var source in m.adjacentDamageSources)
                for(int d=0;d<4;d++)
                {
                    int x=source.x+(d==0?1:d==1?-1:0),y=source.y+(d==2?1:d==3?-1:0);
                    if(IsCell(x,y))
                    {
                        if(Boxes[x,y]>0) m.hitBoxes.Add(new Cell(x,y));
                        if(Walls[x,y]>0) m.hitWalls.Add(new Cell(x,y));
                    }
                }
            m.cells.UnionWith(m.hitBoxes);
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
        // Returns original row per destination; negative origins represent incoming pieces.
        public int[,] Collapse()
        {
            int[,] from=new int[Width,Height];
            for(int x=0;x<Width;x++)
            {
                int bottom=Height-1;
                while(bottom>=0)
                {
                    if(!Active[x,bottom] || Boxes[x,bottom]>0 || Walls[x,bottom]>0) { from[x,bottom]=bottom; bottom--; continue; }
                    int top=bottom;
                    while(top>=0 && Active[x,top] && Boxes[x,top]==0 && Walls[x,top]==0) top--;
                    int dest=bottom;
                    for(int y=bottom;y>top;y--) if(Color[x,y]>=0)
                    {
                        Color[x,dest]=Color[x,y]; Power[x,dest]=Power[x,y]; from[x,dest]=y; dest--;
                    }
                    int empty=dest-top;
                    for(int y=dest;y>top;y--)
                    {
                        // Predefined refills only enter from the top; isolated lower segments
                        // do not manufacture pieces through stationary boxes or absent cells.
                        if(top>=0) { Color[x,y]=-1; Power[x,y]=0; from[x,y]=y; }
                        else { Color[x,y]=NextColor(x); Power[x,y]=0; from[x,y]=y-empty; }
                    }
                    bottom=top;
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
        public int RemainingIce { get { int count=0; foreach(bool ice in Ice) if(ice) count++; return count; } }
    }
}
