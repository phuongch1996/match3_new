using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CrownGarden;
using C = CrownGarden.MatchBoard.Cell;
using B = CrownGarden.MatchBoard;

public static class BuildGame
{
    static int assertions;
    static void Check(bool ok,string message)
    { assertions++; if(!ok) throw new Exception("RULE CHECK FAILED: "+message); }
    static B Fixture()
    {
        var b=new B(1,1);
        for(int y=0;y<8;y++) for(int x=0;x<8;x++) { b.Color[x,y]=(x+2*y)%5; b.Power[x,y]=0; b.Ice[x,y]=false; }
        return b;
    }
    static B EmptyFixture()
    {
        var b=Fixture();
        for(int y=0;y<8;y++) for(int x=0;x<8;x++) b.Color[x,y]=-1;
        return b;
    }
    static void Square(B b,int x,int y,int color=0)
    { for(int yy=y;yy<y+2;yy++) for(int xx=x;xx<x+2;xx++) b.Color[xx,yy]=color; }
    static void VerifySquares()
    {
        for(int color=0;color<B.Colors;color++) for(int y=0;y<7;y++) for(int x=0;x<7;x++)
        {
            var b=EmptyFixture(); Square(b,x,y,color); var at=new C(x+1,y+1);
            var m=b.FindMatches(at);
            Check(m.cells.Count==4 && m.creates.Count==1 && m.creates[at]==5,"2x2 creates one propeller at preferred cell, all positions/colors");
            b.Ice[at.x,at.y]=true; b.Clear(m,1);
            Check(b.Power[at.x,at.y]==5 && b.Color[at.x,at.y]==color && b.IceCleared==1,"propeller survives creation and ice clears");
        }
        {
            var b=EmptyFixture(); Square(b,2,2); b.Color[1,2]=0; b.Color[4,3]=0; b.Color[4,4]=0;
            var m=b.FindMatches(new C(3,3));
            Check(m.creates.Count==1 && m.creates[new C(3,3)]==5,"extra same-color tiles do not suppress square");
            Check(m.cells.Contains(new C(1,2)) && m.cells.Contains(new C(4,3)),"qualifying lines merge into square match");
            Check(!m.cells.Contains(new C(4,4)),"unmatched same-color tail is preserved");
        }
        {
            var b=EmptyFixture(); Square(b,2,2); Square(b,3,2);
            var m=b.FindMatches(new C(3,3));
            Check(m.cells.Count==6 && m.creates.Count==1 && m.creates[new C(3,3)]==5,"overlapping squares make one propeller; parallel triples are not a T/L");
            b=EmptyFixture(); Square(b,0,0); Square(b,5,5); m=b.FindMatches();
            Check(m.cells.Count==8 && m.creates.Count==2,"independent squares create independent specials");
            b=EmptyFixture(); for(int i=0;i<4;i++) b.Color[i,i]=0;
            Check(b.FindMatches().cells.Count==0,"diagonal four is not a square");
        }
        {
            var b=EmptyFixture(); Square(b,0,0); b.Color[1,0]=1; b.Color[2,0]=0;
            Check(b.FindMatches().cells.Count==0,"almost-square is not a match");
            C a,c; Check(b.FindMove(out a,out c),"hint sees square-producing swap");
            b.Swap(a,c); Check(b.FindMatches().creates.Count==1,"square hint is playable");
            b=EmptyFixture(); Square(b,2,2); b.Color[3,3]=1; b.Color[4,3]=0;
            b.Swap(new C(4,3),new C(3,3));
            var m=b.FindMatches(new C(3,3));
            Check(m.creates[new C(3,3)]==5,"swapping into square places propeller at destination");
        }
        {
            var b=EmptyFixture(); Square(b,2,2);
            for(int x=1;x<=4;x++) b.Color[x,2]=0;
            var m=b.FindMatches(new C(2,2));
            Check(m.creates.Count==1 && m.creates[new C(2,2)]==1,"square plus four-line retains rocket priority");
            b.Color[5,2]=0; m=b.FindMatches(new C(2,2));
            Check(m.creates.Count==1 && m.creates[new C(2,2)]==4,"square plus five-line retains rainbow priority");
            b=EmptyFixture(); for(int y=2;y<=4;y++) for(int x=2;x<=4;x++) b.Color[x,y]=0;
            m=b.FindMatches(new C(3,3));
            Check(m.creates.Count==1 && m.creates[new C(3,3)]==3,"crossing triples plus squares retain bomb priority");
        }
        {
            var b=Fixture(); b.Power[3,3]=5; b.Ice[7,7]=true;
            var m=b.Activate(new C(3,3));
            Check(m.cells.Count==6 && m.cells.Contains(new C(7,7)),"propeller clears cross plus distant ice target");
            b.Expand(m); Check(m.cells.Count==6,"expanding propeller twice does not add another target");
            b=Fixture(); b.Power[3,3]=5; b.Ice[7,7]=true; b.Power[7,7]=1;
            m=b.Activate(new C(3,3));
            Check(m.cells.Count==13,"propeller target triggers chained rocket");
            b=EmptyFixture(); Square(b,2,2); for(int y=2;y<4;y++) for(int x=2;x<4;x++) b.Power[x,y]=5;
            Check(b.FindMatches().creates.Count==0,"existing specials do not create a new special without a normal spawn cell");
        }
    }
    static void VerifyColumns()
    {
        var tiles=Enumerable.Repeat(1,7*5).ToArray();
        tiles[0]=21; // one box keeps the original-level adapter's goal contract valid
        int master=1+7*2; tiles[master]=43; tiles[master+1]=47; tiles[master+2]=47; tiles[master+3]=47;
        var definition=new OriginalLevelDefinition { width=7,height=5,moves=10,boxGoal=1,boardCount=1,tiles=tiles,colors=new[]{1,2,3,4},predefined=new OriginalRefill[0] };
        var board=B.FromOriginal(definition,77);
        Check(board.Columns[1,2]==1 && board.Columns[4,2]==1,"column crusher builds one directional footprint");
        Check(board.ColumnGroup[1,2]>0 && board.ColumnGroup[1,2]==board.ColumnGroup[4,2],"column fake cells share the master group");
        Check(board.ColumnDirection[1,2]==2 && !board.IsMovable(new C(2,2)),"column preserves right direction and blocks swapping");
        board.Color[0,1]=board.Color[0,2]=board.Color[0,3]=0;
        var hit=board.FindMatches(new C(0,2)); board.Clear(hit,1);
        Check(board.ColumnsCleared==1 && board.Columns.Cast<int>().All(v=>v==0),"near match destroys the complete column group");

        board=Fixture(); board.SetColumn(new C(2,2),3,3,1,true);
        Check(board.MetalColumns[2,2]==1 && board.MetalColumns[2,4]==1 && !board.IsMovable(new C(2,3)),"metal column keeps its upward static footprint");
        hit=board.Activate(new C(1,2)); hit.adjacentDamageSources.Add(new C(1,2)); board.Clear(hit,1);
        Check(board.MetalColumnsCleared==1 && board.MetalColumns.Cast<int>().All(v=>v==0),"near match destroys the complete metal column group");
    }
    public static void VerifyRules()
    {
        assertions=0;
        VerifySquares();
        VerifyPropellerCombos();
        VerifyColumns();
        VerifyOriginalBoard();
        var b=Fixture();
        Check(b.FindMatches().cells.Count==0,"stable fixture");
        for(int x=0;x<4;x++) b.Color[x,0]=3;
        var m=b.FindMatches(new C(2,0));
        Check(m.cells.Count==4,"four jewels matched");
        Check(m.creates.Count==1 && m.creates[new C(2,0)]==1,"horizontal four creates row rocket at swap destination");
        b.Ice[2,0]=true; b.Clear(m,1);
        Check(b.Power[2,0]==1 && b.Color[2,0]==3,"created rocket survives clearing");
        Check(!b.Ice[2,0] && b.IceCleared==1,"ice under new rocket clears");
        b.Collapse();
        for(int x=0;x<8;x++) for(int y=0;y<8;y++) Check(b.Color[x,y]>=0,"collapse fills every hole");
        b=Fixture(); for(int y=0;y<5;y++) b.Color[0,y]=2;
        m=b.FindMatches(new C(0,2));
        Check(m.creates[new C(0,2)]==4,"five creates rainbow");
        b=Fixture();
        for(int x=1;x<=3;x++) b.Color[x,2]=0;
        for(int y=1;y<=3;y++) b.Color[2,y]=0;
        m=b.FindMatches(new C(2,2));
        Check(m.creates[new C(2,2)]==3,"T creates bomb");
        b=Fixture(); b.Power[3,3]=1; b.Power[6,3]=2;
        m=b.Activate(new C(3,3));
        Check(m.cells.Count==15,"rocket chains into perpendicular rocket");
        b=Fixture(); b.Power[3,3]=3;
        Check(b.Activate(new C(3,3)).cells.Count==9,"bomb clears 3 by 3");
        b=Fixture(); b.Power[0,0]=4; b.Power[1,0]=4;
        Check(b.Combine(new C(0,0),new C(1,0)).cells.Count==64,"double rainbow clears full board");
        b=Fixture(); b.Power[3,3]=3; b.Power[4,3]=3;
        Check(b.Combine(new C(3,3),new C(4,3)).cells.Count==49,"double bomb clears 7 by 7");
        b=Fixture(); b.Power[0,0]=4;
        m=b.Combine(new C(0,0),new C(1,0));
        for(int y=0;y<8;y++) for(int x=0;x<8;x++)
            if(b.Color[x,y]==b.Color[1,0]) Check(m.cells.Contains(new C(x,y)),"rainbow clears target color");
        // Simulate real legal swaps, chain reactions and gravity over varied deterministic boards.
        int swaps=0,cascades=0;
        for(int seed=0;seed<100;seed++)
        {
            b=new B(seed,4);
            Check(b.FindMatches().cells.Count==0,"no opening matches, seed "+seed);
            for(int move=0;move<45;move++)
            {
                C a,c;
                if(!b.FindMove(out a,out c)) b.Shuffle();
                Check(b.FindMove(out a,out c),"playable board");
                if(a.Equals(c)) m=b.Activate(a);
                else
                {
                    b.Swap(a,c);
                    m=(b.Power[a.x,a.y]>0 || b.Power[c.x,c.y]>0)?b.Combine(a,c):b.FindMatches(c);
                }
                Check(m.cells.Count>0,"hint always yields a valid action");
                int guard=0;
                while(m.cells.Count>0)
                {
                    Check(++guard<200,"cascade terminates");
                    b.Expand(m); b.Clear(m,guard); b.Collapse(); m=b.FindMatches(); cascades++;
                }
                Check(b.Score>0,"clearing awards score"); swaps++;
            }
        }
        string report="PASS: "+assertions+" assertions, "+swaps+" legal actions, "+cascades+" cascade steps.\nChecks: propeller + horizontal/vertical rocket/bomb/rainbow, both swap orders and all directions, corner targets, remote footprints, preserved special chains, unique flights, repeat expansion, squares, stable starts, hints, ice, gravity and refill.\n";
        Directory.CreateDirectory("Verification"); File.WriteAllText("Verification/rules.txt",report); Debug.Log(report);
    }
    static void VerifyPropellerCombos()
    {
        var targets=new[]{new C(0,0),new C(7,0),new C(0,7),new C(7,7),new C(6,5)};
        var directions=new[]{new C(1,0),new C(-1,0),new C(0,1),new C(0,-1)};
        foreach(int payload in new[]{1,2,3}) foreach(var direction in directions)
        foreach(var target in targets) for(int reverse=0;reverse<2;reverse++)
        {
            var board=Fixture(); var a=new C(3,3); var b=new C(a.x+direction.x,a.y+direction.y);
            board.Power[a.x,a.y]=5; board.Power[b.x,b.y]=payload; board.Ice[target.x,target.y]=true;
            if(reverse==1) { var temp=a; a=b; b=temp; }
            board.Swap(a,b); var m=board.Combine(a,b);
            Check(m.flights.Count==1 && m.flights[0].payload==payload && m.flights[0].from.Equals(b) && m.flights[0].to.Equals(target),"combo carries the correct payload to distant ice in either swap order");
            var expected=new HashSet<C>{a,b};
            for(int y=0;y<8;y++) for(int x=0;x<8;x++)
                if(Math.Abs(x-b.x)+Math.Abs(y-b.y)<=1 ||
                   (payload==1 && y==target.y) || (payload==2 && x==target.x) ||
                   (payload==3 && Math.Abs(x-target.x)<=1 && Math.Abs(y-target.y)<=1)) expected.Add(new C(x,y));
            Check(m.cells.SetEquals(expected),"combo footprint is launch cross plus remote row/column/3x3; no launch-site payload detonation");
            board.Expand(m); Check(m.flights.Count==1 && m.cells.SetEquals(expected),"carried combo expands only once");
            board.Clear(m,1); Check(board.IceCleared==1 && board.Power[a.x,a.y]==0 && board.Power[b.x,b.y]==0,"combo consumes both originals and breaks target ice");
        }
        foreach(var direction in directions) for(int reverse=0;reverse<2;reverse++)
        {
            HashSet<C> previous=null;
            foreach(int rainbowColor in new[]{0,4})
            {
                var board=Fixture(); var a=new C(3,3); var b=new C(a.x+direction.x,a.y+direction.y);
                board.Power[a.x,a.y]=5; board.Color[a.x,a.y]=2;
                board.Power[b.x,b.y]=4; board.Color[b.x,b.y]=rainbowColor;
                // Make color 1 the unique majority while the spinner stores color 2.
                // The native combo must not select that stored/replaced color.
                for(int y=0;y<8;y++) board.Color[0,y]=1;
                var existing=new C(7,7); board.Color[7,7]=1; board.Power[7,7]=1;
                var seeds=new HashSet<C>();
                var otherColors=new HashSet<C>();
                for(int y=0;y<8;y++) for(int x=0;x<8;x++)
                    if(board.Power[x,y]==0)
                    {
                        if(board.Color[x,y]==1) seeds.Add(new C(x,y));
                        else otherColors.Add(new C(x,y));
                    }
                if(reverse==1) { var temp=a; a=b; b=temp; }
                board.Swap(a,b);
                var propeller=board.Power[a.x,a.y]==5?a:b; seeds.Add(propeller);
                var m=board.Combine(a,b);
                foreach(var seed in seeds) Check(board.Power[seed.x,seed.y]==5 && m.expandedPowers.Contains(seed),"rainbow converts and activates every ordinary tile of majority color, independent of spinner replaced color");
                foreach(var cell in otherColors) Check(board.Power[cell.x,cell.y]==0,"rainbow does not convert minority colors");
                Check(board.Power[existing.x,existing.y]==1,"rainbow does not convert a pre-existing rocket of the selected color");
                Check(m.flights.Select(f=>f.to).Distinct().Count()==m.flights.Count,"rainbow propellers choose distinct remote targets");
                Check(m.flights.All(f=>f.payload==0 && seeds.Contains(f.from)),"rainbow launches only selected-color propellers");
                int count=m.flights.Count; var footprint=new HashSet<C>(m.cells);
                board.Expand(m); Check(m.flights.Count==count && m.cells.SetEquals(footprint),"rainbow combo does not activate twice");
                if(previous!=null) Check(previous.SetEquals(m.cells),"rainbow visual color does not change combined target color");
                previous=new HashSet<C>(m.cells);
            }
        }
        // A carried payload still triggers any special already occupying the remote target.
        {
            var board=Fixture(); board.Power[3,3]=5; board.Power[4,3]=1;
            board.Ice[7,7]=true; board.Power[7,7]=2;
            board.Swap(new C(3,3),new C(4,3)); var m=board.Combine(new C(3,3),new C(4,3));
            for(int i=0;i<8;i++) Check(m.cells.Contains(new C(i,7)) && m.cells.Contains(new C(7,i)),"remote row rocket chains into existing column rocket");
        }
        // SpinnerSpinnerCombo launches three independent propellers from the drop cell.
        {
            var board=Fixture(); board.Power[3,3]=5; board.Power[4,3]=5;
            board.Ice[0,0]=true; board.Ice[7,0]=true; board.Ice[0,7]=true;
            board.Swap(new C(3,3),new C(4,3)); var m=board.Combine(new C(3,3),new C(4,3));
            Check(m.flights.Count==3 && m.flights.All(f=>f.from.Equals(new C(4,3))) && m.flights.Select(f=>f.to).Distinct().Count()==3,
                "propeller plus propeller launches three distinct targets at the drop cell");
            int flights=m.flights.Count; var footprint=new HashSet<C>(m.cells); board.Expand(m);
            Check(m.flights.Count==flights && m.cells.SetEquals(footprint),"propeller plus propeller does not launch twice");
        }
        // Fully covered boards need no extra destination and must not throw.
        {
            var board=EmptyFixture(); board.Color[0,0]=0; board.Power[0,0]=5; board.Color[1,0]=1; board.Power[1,0]=3;
            var m=board.Combine(new C(0,0),new C(1,0));
            Check(m.flights.Count==0 && m.cells.Contains(new C(0,0)) && m.cells.Contains(new C(1,0)),"combo safely consumes originals when no remote tile exists");
        }
    }
    public static void Build()
    {
        VerifyRules();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera").AddComponent<Camera>(); camera.backgroundColor=new Color(.03f,.06f,.14f); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<AudioListener>();
        new GameObject("Crown Garden").AddComponent<CrownGarden.CrownGame>();
        Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene,"Assets/Scenes/CrownGarden.unity");
        PlayerSettings.companyName="Offline Prototypes"; PlayerSettings.productName="Crown Garden";
        PlayerSettings.defaultScreenWidth=1200; PlayerSettings.defaultScreenHeight=860;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=true; PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CrownGarden.unity",true)};
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/CrownGarden.unity"},locationPathName="Build/Windows/CrownGarden.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("BUILD SUCCESS: "+report.summary.totalSize+" bytes");
    }
    [Serializable] sealed class NativeEnvelope { public string source_sha256; public NativeLevel level; }
    [Serializable] sealed class NativeLevel { public string Name; public int Move; public NativeGrid Grid; }
    [Serializable] sealed class NativeGrid { public int Width,Height; public int[] Items; public NativeBoard[] Boards; }
    [Serializable] sealed class NativeBoard { public NativeGoal[] Goals; }
    [Serializable] sealed class NativeGoal { public int Goal,Count; }
    static OriginalLevelDefinition OriginalDefinition()
    { return JsonUtility.FromJson<OriginalLevelDefinition>(File.ReadAllText("Assets/Resources/OriginalLevels/level_1.json")); }
    static void VerifyOriginalBoard()
    {
        var specialFixture=Fixture();
        specialFixture.Color[0,0]=specialFixture.Color[1,0]=specialFixture.Color[2,0]=0; specialFixture.Power[1,0]=5;
        Check(specialFixture.FindMatches().cells.Count==0,"special item does not complete an ordinary color match using its replaced color");
        var definition=OriginalDefinition();
        var native=JsonUtility.FromJson<NativeEnvelope>(File.ReadAllText("Analysis/ReverseEngineering/Levels/1.json"));
        Check(definition.sourceSha256==native.source_sha256 && definition.embeddedName==native.level.Name,"adapter is linked to recovered original level hash/name");
        Check(definition.width==native.level.Grid.Width && definition.height==native.level.Grid.Height && definition.moves==native.level.Move && definition.tiles.SequenceEqual(native.level.Grid.Items),"adapter preserves exact original grid and move count");
        Check(definition.boxGoal==native.level.Grid.Boards.Sum(b=>b.Goals.Where(g=>g.Goal==6).Sum(g=>g.Count)),"adapter preserves original Box goal");
        var board=B.FromOriginal(definition,10201);
        Check(board.Width==9 && board.Height==9 && board.RemainingBoxes==54 && definition.moves==30,"original level 1 dimensions, boxes and moves");
        Check(board.FindMatches().cells.Count==0,"original initial board does not auto-resolve");
        for(int y=0;y<9;y++) for(int x=0;x<9;x++)
        {
            int tile=definition.tiles[x+y*9],viewY=8-y;
            Check(tile==21?board.Boxes[x,viewY]==1 && board.Color[x,viewY]==-1:board.Boxes[x,viewY]==0 && board.Color[x,viewY]==B.ColorFromTiledId(tile),"native x+width*y maps to top-origin rendering without transposing");
        }
        for(int x=0;x<9;x++) Check(board.PredefinedRemaining(x)==11,"original column refill queues retain eleven entries");
        foreach(var size in new[]{new C(3,3),new C(9,9),new C(9,11),new C(7,12)})
        {
            board=new B(42,1,size.x,size.y);
            Check(board.FindMatches().cells.Count==0,"stable variable-sized board");
            var cells=new HashSet<C>();
            for(int y=0;y<size.y;y++) for(int x=0;x<size.x;x++) cells.Add(new C(x,y));
            Check(cells.Count==size.x*size.y,"cell identity is independent of default width");
            for(int y=0;y<size.y;y++) for(int x=0;x<size.x;x++) { board.Color[x,y]=(x+2*y)%5; board.Power[x,y]=0; }
            board.Power[size.x-1,size.y-1]=1;
            Check(board.Activate(new C(size.x-1,size.y-1)).cells.Count==size.x,"row rocket reaches actual board edge");
            board.Power[size.x-1,size.y-1]=2;
            Check(board.Activate(new C(size.x-1,size.y-1)).cells.Count==size.y,"column rocket reaches actual board edge");
        }
        // A T-match touching a layered box from two sides still hits only once.
        board=new B(3,1,9,9);
        for(int y=0;y<9;y++) for(int x=0;x<9;x++) board.Color[x,y]=-1;
        foreach(var c in new[]{new C(2,3),new C(3,3),new C(4,3),new C(3,4),new C(3,5)}) board.Color[c.x,c.y]=0;
        board.Boxes[4,4]=2;
        var match=board.FindMatches(); board.Expand(match); board.Clear(match,1);
        Check(board.Boxes[4,4]==1 && board.BoxesCleared==0,"near match damages one layer once, even with two touching match cells");
        board.Clear(match,1); Check(board.Boxes[4,4]==1,"same explosion cannot damage box twice");
        match=new B.Match(); match.cells.Add(new C(4,4)); board.Expand(match); board.Clear(match,1);
        Check(board.Boxes[4,4]==0 && board.BoxesCleared==1,"direct hit destroys last box layer and counts one goal");
        board=new B(4,1,9,11);
        board.Power[3,3]=3; board.Boxes[5,3]=1; board.Color[5,3]=-1;
        match=board.Activate(new C(3,3)); board.Clear(match,1);
        Check(board.Boxes[5,3]==1,"special blast does not cause near-match damage beyond its own area");
        board=new B(5,1,3,6);
        board.Boxes[1,3]=1; board.Color[1,3]=-1; board.Color[1,0]=-1; board.Color[1,1]=2; board.Color[1,2]=3; board.Color[1,4]=0; board.Color[1,5]=-1;
        var from=board.Collapse();
        Check(board.Boxes[1,3]==1 && board.Color[1,3]==-1 && board.Color[1,5]==0 && from[1,5]==4,"boxes stay fixed and gravity preserves order in each open segment");
        Check(board.Color[1,4]==-1,"no refill is fabricated through a stationary box");
        Check(!board.CanSwap(new C(1,3),new C(1,2)),"box cannot be swapped");
        match=new B.Match(); match.cells.Add(new C(1,3)); board.Clear(match,1); board.Collapse();
        Check(board.Color[1,3]==2 && board.Color[1,4]==3 && board.Color[1,5]==0,"destroying box opens gravity path and preserves existing jewel order");
        board.Active[1,3]=false;
        Check(!board.CanSwap(new C(1,3),new C(1,2)),"inactive cell cannot be swapped");
        board=B.FromOriginal(definition,1);
        var expected=definition.predefined.Where(p=>p.x==0).SelectMany(p=>p.ids).Select(B.ColorFromTiledId).ToArray();
        var queueStart=board.PredefinedRemaining(0);
        // Clear all three initial jewels in one column without affecting boxes.
        for(int y=0;y<3;y++) board.Color[0,y]=-1;
        board.Collapse();
        Check(board.PredefinedRemaining(0)==queueStart-3 && board.Color[0,2]==expected[0] && board.Color[0,1]==expected[1] && board.Color[0,0]==expected[2],"native stored-order refill queue enters lowest open slot first");
        var invalid=OriginalDefinition(); invalid.tiles[0]=9999;
        bool rejected=false; try { B.FromOriginal(invalid,1); } catch(NotSupportedException) { rejected=true; }
        Check(rejected,"unsupported original tile is rejected instead of replaced by a jewel");
        // Resolve valid actions on the actual original board; verify goal progress and stability.
        board=B.FromOriginal(definition,10201);
        int actions=0,steps=0;
        for(int move=0;move<30 && board.RemainingBoxes>0;move++)
        {
            C a,b; if(!board.FindMove(out a,out b)) { if(!board.Shuffle()) break; board.FindMove(out a,out b); }
            Check(a.Equals(b)?board.IsMovable(a):board.CanSwap(a,b),"original-level hint never chooses a box or empty cell");
            B.Match m;
            if(a.Equals(b)) m=board.Activate(a);
            else { board.Swap(a,b); m=board.Power[a.x,a.y]>0 || board.Power[b.x,b.y]>0?board.Combine(a,b):board.FindMatches(b); }
            int guard=0;
            while(m.cells.Count>0)
            {
                Check(++guard<200,"original-level cascade terminates"); board.Expand(m); board.Clear(m,guard); board.Collapse(); m=board.FindMatches(); steps++;
            }
            actions++;
        }
        Check(board.BoxesCleared>0 && board.FindMatches().cells.Count==0,"actual original level resolves actions and breaks source-layout boxes");
        Directory.CreateDirectory("Verification");
        File.WriteAllText("Verification/original-board.txt","PASS: original 9x9 layout / 30 moves / 54 boxes preserved; "+actions+" legal actions, "+steps+" cascade steps, "+board.BoxesCleared+" boxes cleared. Native-directed box hits, fixed blockers, variable dimensions, stored refill order, unsupported tile rejection passed.\nUnverified fidelity: diagonal falling, original RNG and complete obstacle set.\n");
    }
}
