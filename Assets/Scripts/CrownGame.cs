using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using C = CrownGarden.MatchBoard.Cell;

namespace CrownGarden
{
    public sealed class CrownGame : MonoBehaviour
    {
        const float W=1200,H=860;
        float S { get { return board==null?73:Mathf.Min(584f/board.Width,584f/board.Height); } }
        float BX { get { return 446+(board==null?0:(584-board.Width*S)/2); } }
        float BY { get { return 169+(board==null?0:(584-board.Height*S)/2); } }
        readonly Color navy=new Color(.055f,.09f,.22f), gold=new Color(1,.79f,.35f), pale=new Color(.84f,.91f,1);
        readonly Color[] colors={new Color(.98f,.24f,.32f),new Color(.17f,.68f,1),new Color(.35f,.86f,.40f),new Color(1,.75f,.16f),new Color(.75f,.35f,.98f)};
        MatchBoard board;
        Texture2D circle,gradient,star;
        Texture2D boxArt;
        Texture2D[] gems;
        Texture2D[] recoveredSpecial;
        readonly List<Texture2D> obstacleArt=new List<Texture2D>();
        int recoveredArtCount;
        readonly Dictionary<string,AudioClip> recoveredAudio=new Dictionary<string,AudioClip>();
        Font font;
        AudioSource audioSource;
        AudioClip[] notes;
        int level,unlocked,stars,built,moves,combo,targetRed,targetBlue;
        string levelEntry="1";
        int targetBoxes;
        bool originalMode,obstacleMode;
        bool recoveredLevelMode;
        int[] recoveredGoalTargets;
        bool recoveredRequiresObstacleClear;
        int obstacleLevelIndex=-1;
        static readonly string[] obstacleLevels={"level_obstacle_lab","level_shape_lab","level_columns","level_walls","level_crates","level_all","level_layers","level_lantern","level_puzzle","level_gift","level_flowers","level_chalice","level_dragon","level_birdhouse","level_gnome","level_safe","level_mask","level_turtle","level_snowman","level_steambomb","level_knight","level_turtlehouse","level_clock","level_native_remaining_lab","level_enum_future_lab"};
        bool verificationRun;
        OriginalLevelDefinition originalDefinition;
        int seed=3021;
        bool home=true,busy,won,lost,muted,hammer,homePainted,levelPainted;
        int hammers=3;
        float anim,toastUntil,hintUntil,idleAt;
        string toast="";
        C? selected,dragStart;
        Vector2 pointerStart;
        C swapA,swapB,hintA,hintB;
        bool swapping,clearing,falling,flying;
        MatchBoard.Match clearMatch;
        int[,] fallFrom;
        readonly List<Spark> sparks=new List<Spark>();
        sealed class Spark { public Vector2 p,v; public Color c; public float born,life,size; }

        void Awake()
        {
            Application.targetFrameRate=60;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            circle=MakeTexture(64,(x,y)=>new Color(1,1,1,Mathf.Clamp01((.96f-Mathf.Sqrt(x*x+y*y))*32)));
            star=MakeTexture(128,(x,y)=> { float angle=Mathf.Atan2(x,-y),r=Mathf.Sqrt(x*x+y*y); float edge=.67f+.22f*Mathf.Cos(angle*5); return new Color(1,1,1,Mathf.Clamp01((edge-r)*50)); });
            gradient=new Texture2D(1,256,TextureFormat.RGBA32,false);
            for(int y=0;y<256;y++) gradient.SetPixel(0,y,Color.Lerp(new Color(.045f,.09f,.20f),new Color(.13f,.27f,.44f),y/255f));
            gradient.Apply();
            gems=new Texture2D[5];
            for(int i=0;i<5;i++) { int type=i; gems[i]=MakeTexture(128,(x,y)=>GemPixel(type,x,y)); }
            string[] recoveredColors={"red","blue","green","yellow","pink"};
            for(int i=0;i<5;i++)
            {
                var texture=Resources.Load<Texture2D>("RecoveredArt/match_"+recoveredColors[i]);
                if(texture!=null) { gems[i]=texture; recoveredArtCount++; }
            }
            recoveredSpecial=new Texture2D[6];
            string[] specialNames={"","rocket_horizontal","rocket_vertical","dynamite","electroball","spinner"};
            for(int i=1;i<specialNames.Length;i++)
            {
                recoveredSpecial[i]=Resources.Load<Texture2D>("RecoveredArt/"+specialNames[i]);
                if(recoveredSpecial[i]!=null) recoveredArtCount++;
            }
            boxArt=Resources.Load<Texture2D>("RecoveredArt/box");
            obstacleArt.AddRange(Resources.LoadAll<Texture2D>("RecoveredArt/Obstacles"));
            audioSource=gameObject.AddComponent<AudioSource>();
            string[] soundNames={"match_explode_1","match_explode_2","match_explode_3","match_explode_4","match_explode_5","match_explode_6","spinner_take_off","spinner_hit","rocket_explode","dynamite_explode","electro_ball_explode","swipe_whoosh"};
            foreach(string name in soundNames)
            {
                var clip=Resources.Load<AudioClip>("RecoveredAudio/"+name);
                if(clip!=null) recoveredAudio[name]=clip;
            }
            notes=new AudioClip[8];
            for(int n=0;n<8;n++)
            {
                float frequency=392*Mathf.Pow(2,n/12f); int length=6600;
                float[] data=new float[length];
                for(int i=0;i<length;i++) { float t=i/22050f; data[i]=(.17f*Mathf.Sin(2*Mathf.PI*frequency*t)+.05f*Mathf.Sin(4*Mathf.PI*frequency*t))*Mathf.Exp(-t*17)*Mathf.Min(t*150,1); }
                notes[n]=AudioClip.Create("chime"+n,length,1,22050,false); notes[n].SetData(data,0);
            }
            unlocked=Mathf.Clamp(PlayerPrefs.GetInt("cg.level",1),1,2300); level=unlocked; levelEntry=unlocked.ToString();
            stars=PlayerPrefs.GetInt("cg.stars",0); built=PlayerPrefs.GetInt("cg.built",0);
            muted=PlayerPrefs.GetInt("cg.muted",0)==1;
            var args=Environment.GetCommandLineArgs();
            verificationRun=Array.Exists(args,a=>a=="--smoke" || a=="--combo-smoke" || a=="--original-smoke");
            for(int i=0;i<args.Length;i++) if(args[i]=="--smoke" && i+1<args.Length) StartCoroutine(Smoke(args[i+1]));
            for(int i=0;i<args.Length;i++) if(args[i]=="--combo-smoke" && i+1<args.Length) StartCoroutine(ComboSmoke(args[i+1]));
            for(int i=0;i<args.Length;i++) if(args[i]=="--original-smoke" && i+1<args.Length) StartCoroutine(OriginalSmoke(args[i+1]));
        }
        static float Rounded(float x,float y,float w,float h,float r)
        { Vector2 q=new Vector2(Mathf.Abs(x)-w+r,Mathf.Abs(y)-h+r); return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-r; }
        static Texture2D MakeTexture(int n,Func<float,float,Color> pixel)
        {
            Texture2D t=new Texture2D(n,n,TextureFormat.RGBA32,false); Color[] data=new Color[n*n];
            for(int y=0;y<n;y++) for(int x=0;x<n;x++) data[y*n+x]=pixel((x+.5f)/n*2-1,1-(y+.5f)/n*2);
            t.SetPixels(data); t.Apply(); t.filterMode=FilterMode.Bilinear; return t;
        }
        Color GemPixel(int type,float x,float y)
        {
            float d;
            if(type==0) d=Rounded(x,y,.73f,.73f,.25f);
            else if(type==1) d=(Mathf.Abs(x)*.95f+Mathf.Abs(y)*.76f-.70f)*.8f;
            else if(type==2) d=Rounded(x,y,.66f,.77f,.40f);
            else if(type==3) { float a=Mathf.Atan2(x,-y); d=new Vector2(x,y).magnitude-(.63f+.19f*Mathf.Cos(5*a)); }
            else { float a=Mathf.Atan2(x,y); d=new Vector2(x,y).magnitude-(.67f+.09f*Mathf.Cos(6*a)); }
            float alpha=Mathf.Clamp01(-d*50);
            Color c=colors[type]*Mathf.Lerp(.62f,1.14f,Mathf.Clamp01(.48f-y*.52f)); c.a=alpha;
            if(d>-.085f) c=Color.Lerp(c,new Color(.05f,.07f,.16f,alpha),.38f);
            if(d<-.09f && d>-.14f && y<.12f) c=Color.Lerp(c,new Color(1,1,1,alpha),.35f);
            float shine=Mathf.Exp(-((x+.24f)*(x+.24f)*20+(y+.37f)*(y+.37f)*95));
            c=Color.Lerp(c,new Color(1,1,1,alpha),shine*.58f); c.a=alpha;
            return c;
        }
        void Save()
        {
            if(verificationRun) return;
            PlayerPrefs.SetInt("cg.level",unlocked); PlayerPrefs.SetInt("cg.stars",stars);
            PlayerPrefs.SetInt("cg.built",built); PlayerPrefs.SetInt("cg.muted",muted?1:0); PlayerPrefs.Save();
        }
        void StartLevel()
        {
            originalMode=false; recoveredLevelMode=false; obstacleMode=false; targetBoxes=0;
            level=Mathf.Clamp(unlocked,1,2300);
            var recovered=Resources.Load<TextAsset>("RecoveredLevels/level_"+level.ToString("D4"));
            if(recovered!=null) { StartRecoveredLevel(level,recovered); return; }
            board=new MatchBoard(seed++ + level*193,level);
            moves=level==1?26:30; targetRed=level==1?22:24+Mathf.Min(level,10); targetBlue=level==1?22:24+Mathf.Min(level,10);
            won=lost=busy=hammer=false; hammers=3; home=false; selected=null; combo=0; idleAt=Time.time;
            sparks.Clear(); Notify(level==1?"Match 4 in a square to create a propeller!":"Break the ice and collect red & blue jewels.",4);
        }
        void StartRecoveredLevel(int index,TextAsset source=null)
        {
            try
            {
                if(source==null) source=Resources.Load<TextAsset>("RecoveredLevels/level_"+index.ToString("D4"));
                if(source==null) throw new InvalidOperationException("Recovered level file is missing: "+index);
                originalDefinition=JsonUtility.FromJson<OriginalLevelDefinition>(source.text);
                board=MatchBoard.FromOriginal(originalDefinition,3021+index*193);
                originalMode=true; recoveredLevelMode=true; obstacleMode=false; obstacleLevelIndex=-1; level=index;
                targetBoxes=originalDefinition.boxGoal; recoveredGoalTargets=originalDefinition.goalTargets ?? new int[5];
                recoveredRequiresObstacleClear=originalDefinition.requireObstacleClear;
                moves=originalDefinition.moves; targetRed=targetBlue=0;
                won=lost=busy=hammer=false; hammers=3; home=false; selected=null; dragStart=null; combo=0; idleAt=Time.time; sparks.Clear();
                Notify("Recovered level "+index+"  /  "+originalDefinition.embeddedName,4);
            }
            catch(Exception error) { home=true; Debug.LogException(error); Notify("Recovered level could not load. See the game log.",5); }
        }
        void OpenEnteredLevel()
        {
            int requested;
            if(!int.TryParse(levelEntry,out requested)) { Notify("Enter a level number from 1 to 2300.",3); return; }
            requested=Mathf.Clamp(requested,1,2300); levelEntry=requested.ToString(); unlocked=requested; Save();
            StartRecoveredLevel(requested);
        }
        void StartOriginal()
        {
            try
            {
                var source=Resources.Load<TextAsset>("OriginalLevels/level_1");
                if(source==null) throw new InvalidOperationException("Recovered level file is missing.");
                originalDefinition=JsonUtility.FromJson<OriginalLevelDefinition>(source.text);
                var restored=MatchBoard.FromOriginal(originalDefinition,10201);
                board=restored; originalMode=true; recoveredLevelMode=false; obstacleMode=false; level=1; targetBoxes=originalDefinition.boxGoal;
                moves=originalDefinition.moves; targetRed=targetBlue=0;
                won=lost=busy=hammer=false; hammers=3; home=false; selected=null; dragStart=null;
                combo=0; idleAt=Time.time; sparks.Clear();
                Notify("Original level 1: break all "+targetBoxes+" boxes in "+moves+" moves.",4);
            }
            catch(Exception error) { home=true; Debug.LogException(error); Notify("Original level could not load. See the game log.",5); }
        }
        void StartObstacle(int index)
        {
            try
            {
                index=Mathf.Clamp(index,0,obstacleLevels.Length-1);
                var source=Resources.Load<TextAsset>("ObstacleLevels/"+obstacleLevels[index]);
                if(source==null) throw new InvalidOperationException("Obstacle level file is missing: "+obstacleLevels[index]);
                originalDefinition=JsonUtility.FromJson<OriginalLevelDefinition>(source.text);
                board=MatchBoard.FromOriginal(originalDefinition,22000+index*97);
                originalMode=true; recoveredLevelMode=false; obstacleMode=true; obstacleLevelIndex=index; level=100+index; targetBoxes=originalDefinition.boxGoal;
                moves=originalDefinition.moves; targetRed=targetBlue=0;
                won=lost=busy=hammer=false; hammers=3; home=false; selected=null; dragStart=null; combo=0; idleAt=Time.time; sparks.Clear();
                Notify("Obstacle level "+(index+1)+": break the box and test every static obstacle.",4);
            }
            catch(Exception error) { home=true; Debug.LogException(error); Notify("Obstacle level could not load. See the game log.",5); }
        }
        void RetryCurrent() { if(obstacleMode) StartObstacle(obstacleLevelIndex); else if(recoveredLevelMode) StartRecoveredLevel(level); else if(originalMode) StartOriginal(); else StartLevel(); }
        bool GoalsDone
        {
            get
            {
                if(!originalMode) return board.Collected[0]>=targetRed && board.Collected[1]>=targetBlue && board.RemainingIce==0;
                bool boxesDone=targetBoxes<=0 || board.BoxesCleared>=targetBoxes;
                if(recoveredLevelMode)
                {
                    for(int i=0;i<5;i++) if(recoveredGoalTargets!=null && i<recoveredGoalTargets.Length && board.Collected[i]<recoveredGoalTargets[i]) return false;
                    return boxesDone && (!recoveredRequiresObstacleClear || board.RemainingObstacleCellsTotal==0);
                }
                return boxesDone && (!obstacleMode || board.RemainingNativeObstaclesTotal==0);
            }
        }
        void Sound(int n) { if(!muted) audioSource.PlayOneShot(notes[Mathf.Clamp(n,0,7)]); }
        void PlayRecovered(string name,int fallback)
        {
            if(muted) return;
            AudioClip clip;
            if(recoveredAudio.TryGetValue(name,out clip)) audioSource.PlayOneShot(clip,.65f);
            else Sound(fallback);
        }
        void Notify(string s,float seconds=2.4f) { toast=s; toastUntil=Time.time+seconds; }
        IEnumerator Tween(float duration,Action<float> f)
        {
            float t=0; while(t<duration) { t+=Time.deltaTime; f(Mathf.SmoothStep(0,1,t/duration)); yield return null; } f(1);
        }
        IEnumerator SwapMove(C a,C b)
        {
            if(!board.CanSwap(a,b)) { selected=null; Notify("Boxes stay in place. Match jewels beside them."); yield break; }
            busy=true; selected=null; swapA=a; swapB=b; swapping=true;
            PlayRecovered("swipe_whoosh",0);
            yield return Tween(.16f,t=>anim=t);
            swapping=false; board.Swap(a,b);
            MatchBoard.Match m;
            if(board.Power[a.x,a.y]>0 || board.Power[b.x,b.y]>0) m=board.Combine(a,b);
            else m=board.FindMatches(b);
            if(m.cells.Count==0)
            {
                swapA=a; swapB=b; swapping=true;
                yield return Tween(.16f,t=>anim=t); swapping=false; board.Swap(a,b);
                Notify("Make a line of 3 or more jewels."); busy=false; yield break;
            }
            moves--; yield return Resolve(m);
        }
        IEnumerator UsePower(C c)
        { busy=true; selected=null; moves--; yield return Resolve(board.Activate(c)); }
        IEnumerator UseHammer(C c)
        {
            busy=true; hammer=false; selected=null; hammers--;
            var m=new MatchBoard.Match(); m.cells.Add(c); board.Expand(m);
            yield return Resolve(m);
        }
        IEnumerator Resolve(MatchBoard.Match m)
        {
            combo=0;
            while(m.cells.Count>0)
            {
                combo++; board.Expand(m); clearMatch=m;
                if(m.flights.Count>0)
                {
                    flying=true; PlayRecovered("spinner_take_off",3);
                    yield return Tween(.55f,t=>anim=t);
                    flying=false;
                }
                clearing=true;
                PlayRecovered("match_explode_"+Mathf.Clamp(combo,1,6),combo-1);
                var sounds=new HashSet<string>();
                foreach(var cell in m.cells)
                {
                    int power=board.Power[cell.x,cell.y];
                    string sound=(power==1 || power==2)?"rocket_explode":power==3?"dynamite_explode":power==4?"electro_ball_explode":power==5?"spinner_hit":null;
                    if(sound!=null && sounds.Add(sound)) PlayRecovered(sound,combo-1);
                }
                foreach(var c in m.cells) Burst(c,board.Color[c.x,c.y]>=0?colors[board.Color[c.x,c.y]]:gold);
                if(combo>1) Notify(combo==2?"Lovely cascade!":"ROYAL COMBO  x"+combo,1.3f);
                yield return Tween(.22f,t=>anim=t);
                board.Clear(m,combo); clearing=false; clearMatch=null;
                fallFrom=board.Collapse(); falling=true;
                yield return Tween(.30f,t=>anim=t); falling=false;
                m=board.FindMatches();
            }
            if(GoalsDone)
            {
                won=true; int award=1+(moves>=5?1:0)+(moves>=12?1:0);
                if(!verificationRun && (!originalMode || recoveredLevelMode)) { stars+=award; unlocked=Mathf.Min(2300,unlocked+1); levelEntry=unlocked.ToString(); Save(); }
                else if(!verificationRun && originalMode && PlayerPrefs.GetInt("cg.original1Complete",0)==0) { stars+=award; PlayerPrefs.SetInt("cg.original1Complete",1); Save(); }
                Sound(7);
                for(int i=0;i<20;i++) Burst(new C(UnityEngine.Random.Range(0,board.Width),UnityEngine.Random.Range(0,board.Height)),gold);
            }
            else if(moves<=0) lost=true;
            else { C a,b; if(!board.FindMove(out a,out b)) { bool ready=board.Shuffle(); Notify(ready?"Fresh jewels! The board was reshuffled.":"No match available. Use a hammer or retry."); } }
            busy=false; idleAt=Time.time;
        }
        void Burst(C c,Color color)
        {
            for(int i=0;i<8;i++)
            { float a=UnityEngine.Random.value*Mathf.PI*2; sparks.Add(new Spark { p=new Vector2(BX+(c.x+.5f)*S,BY+(c.y+.5f)*S),v=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*UnityEngine.Random.Range(45,155),c=color,born=Time.time,life=UnityEngine.Random.Range(.4f,.75f),size=UnityEngine.Random.Range(4,9) }); }
        }
        void Update()
        {
            sparks.RemoveAll(s=>Time.time-s.born>s.life);
            if(Input.GetKeyDown(KeyCode.Escape) && !busy) { if(home) Application.Quit(); else { home=true; won=lost=false; } }
            if(home || busy || won || lost) return;
            if(Input.GetKeyDown(KeyCode.H)) Hint();
            if(Time.time-idleAt>7) { Hint(false); idleAt=Time.time; }
        }
        void Hint(bool sound=true)
        { if(board.FindMove(out hintA,out hintB)) { hintUntil=Time.time+3; if(sound) Sound(2); } }
        Vector2 MouseLogical(Vector2 p)
        { float scale=Mathf.Min(Screen.width/W,Screen.height/H); return (p-new Vector2((Screen.width-W*scale)/2,(Screen.height-H*scale)/2))/scale; }
        void HandleInput()
        {
            if(home || busy || won || lost) return;
            Event e=Event.current;
            Vector2 p=MouseLogical(e.mousePosition);
            int x=Mathf.FloorToInt((p.x-BX)/S),y=Mathf.FloorToInt((p.y-BY)/S);
            if(e.type==EventType.MouseDown && e.button==0 && board.IsCell(x,y))
            { dragStart=new C(x,y); pointerStart=p; e.Use(); }
            if(e.type==EventType.MouseUp && e.button==0 && dragStart.HasValue)
            {
                C start=dragStart.Value; dragStart=null; idleAt=Time.time; hintUntil=0;
                if((p-pointerStart).magnitude>18)
                {
                    Vector2 d=p-pointerStart;
                    C end=Mathf.Abs(d.x)>Mathf.Abs(d.y)?new C(start.x+(d.x>0?1:-1),start.y):new C(start.x,start.y+(d.y>0?1:-1));
                    if(board.IsCell(end.x,end.y)) StartCoroutine(SwapMove(start,end));
                }
                else if(board.IsCell(x,y))
                {
                    C c=new C(x,y);
                    if(hammer) StartCoroutine(UseHammer(c));
                    else if(!board.IsMovable(c)) { selected=null; Notify("Boxes stay in place. Match jewels beside them."); }
                    else if(selected.HasValue && MatchBoard.Adjacent(selected.Value,c)) StartCoroutine(SwapMove(selected.Value,c));
                    else if(board.Power[x,y]>0) StartCoroutine(UsePower(c));
                    else selected=c;
                }
                e.Use();
            }
        }
        void OnGUI()
        {
            HandleInput();
            float scale=Mathf.Min(Screen.width/W,Screen.height/H);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-W*scale)/2,(Screen.height-H*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            GUI.DrawTexture(new Rect(0,0,W,H),gradient);
            // Atmospheric motes and rolling hills.
            for(int i=0;i<30;i++) { float x=(i*193.7f)%W,y=80+(i*67.9f)%630; Disc(x,y,2+(i%3),new Color(1,.91f,.64f,.12f+.08f*Mathf.Sin(Time.time+i))); }
            GUI.color=new Color(.055f,.16f,.24f); GUI.DrawTexture(new Rect(-190,715,1100,450),circle);
            GUI.color=new Color(.06f,.18f,.26f); GUI.DrawTexture(new Rect(580,745,900,400),circle); GUI.color=Color.white;
            if(home) DrawHome(); else DrawLevel();
            if(flying && clearMatch!=null)
                foreach(var flight in clearMatch.flights)
                {
                    Vector2 from=new Vector2(BX+(flight.from.x+.5f)*S,BY+(flight.from.y+.5f)*S);
                    Vector2 to=new Vector2(BX+(flight.to.x+.5f)*S,BY+(flight.to.y+.5f)*S);
                    Vector2 position=Vector2.Lerp(from,to,anim)+new Vector2(0,-75*Mathf.Sin(anim*Mathf.PI));
                    Disc(to.x,to.y,30,new Color(1,.84f,.35f,.22f));
                    for(int i=1;i<=4;i++)
                    {
                        float t=Mathf.Max(0,anim-i*.035f);
                        Vector2 trail=Vector2.Lerp(from,to,t)+new Vector2(0,-75*Mathf.Sin(t*Mathf.PI));
                        Disc(trail.x,trail.y,7-i,gold);
                    }
                    DrawGem(new Rect(position.x-28,position.y-28,56,56),board.Color[flight.from.x,flight.from.y],5);
                    if(flight.payload>0) DrawGem(new Rect(position.x-10,position.y+6,38,38),3,flight.payload);
                }
            if(Time.time<toastUntil && !won && !lost)
            {
                float toastY=home?790:118;
                Box(new Rect(250,toastY,800,34),new Color(.03f,.07f,.17f,.92f));
                Text(new Rect(265,toastY+2,770,30),toast,16,pale,TextAnchor.MiddleCenter);
            }
            foreach(var spark in sparks)
            {
                float t=Time.time-spark.born; Vector2 pos=spark.p+spark.v*t+new Vector2(0,120*t*t);
                Color c=spark.c; c.a=1-t/spark.life; Disc(pos.x,pos.y,spark.size*(1-t/spark.life),c);
            }
            GUI.matrix=Matrix4x4.identity;
        }
        void DrawHome()
        {
            if(Event.current.type==EventType.Repaint) homePainted=true;
            Text(new Rect(85,50,700,24),"CROWN GARDEN  /  OFFLINE EDITION",16,gold,TextAnchor.MiddleLeft,true);
            Text(new Rect(80,91,800,68),"A kingdom to make your own.",42,Color.white,TextAnchor.MiddleLeft,true);
            Text(new Rect(85,164,800,40),"Match jewels. Earn stars. Bring the royal garden to life.",19,pale);
            Box(new Rect(80,239,665,492),new Color(.06f,.14f,.28f));
            Box(new Rect(93,252,639,466),new Color(.16f,.32f,.39f));
            Disc(600,320,39,new Color(1,.85f,.50f,.95f));
            for(int i=0;i<4;i++) Cloud(150+i*145,290+(i%2)*50,.65f);
            DrawCastle(225,360,1.10f);
            Box(new Rect(107,649,610,54),new Color(.08f,.23f,.24f));
            Text(new Rect(122,653,580,43),built>=3?"THE ROYAL GARDEN  /  RESTORED":"THE ROYAL GARDEN  /  "+Mathf.Min(built,3)+" OF 3 RESTORED",16,gold,TextAnchor.MiddleCenter,true);
            if(built>=1) for(int i=0;i<6;i++) { Disc(143+i*103,620,19,new Color(.18f,.40f,.24f)); Disc(143+i*103,597,24,new Color(.21f,.55f,.31f)); }
            if(built>=2) for(int i=0;i<12;i++) { Disc(130+i*50,640,6,colors[i%5]); Disc(130+i*50,631,4,gold); }
            if(built>=3) { Disc(413,639,33,new Color(.40f,.79f,.95f)); Disc(413,630,23,new Color(.66f,.93f,1)); Box(new Rect(409,580,8,57),pale); }
            Box(new Rect(779,239,341,492),navy);
            Text(new Rect(805,265,285,26),"YOUR NEXT ADVENTURE",15,gold,TextAnchor.MiddleCenter,true);
            Text(new Rect(805,309,285,64),"Level "+unlocked,43,Color.white,TextAnchor.MiddleCenter,true);
            Text(new Rect(811,382,275,57),unlocked==1?"Recovered level 1\nfrom the APK is ready.":"Recovered level "+Mathf.Min(unlocked,2300)+" of 2300\nis ready to play.",18,pale,TextAnchor.MiddleCenter);
            Text(new Rect(805,435,90,28),"LEVEL #",14,gold,TextAnchor.MiddleLeft,true);
            GUIStyle levelFieldStyle=new GUIStyle(GUI.skin.textField); levelFieldStyle.fontSize=18; levelFieldStyle.alignment=TextAnchor.MiddleCenter;
            levelEntry=GUI.TextField(new Rect(894,433,80,35),levelEntry,5,levelFieldStyle);
            if(Button(new Rect(982,433,103,35),"OPEN",new Color(.22f,.48f,.58f),15)) OpenEnteredLevel();
            if(Button(new Rect(812,469,275,65),"PLAY",new Color(.25f,.69f,.37f),27)) StartLevel();
            if(Button(new Rect(812,540,275,43),"ORIGINAL LEVEL 1",new Color(.29f,.37f,.59f),17)) StartOriginal();
            Text(new Rect(805,585,285,28),"STARS  "+stars+"  •  TEST LEVELS",16,gold,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(812,615,275,39),"OBSTACLE LEVEL  "+(Mathf.Max(0,obstacleLevelIndex)+1),new Color(.22f,.48f,.58f),15))
            { int chosen=obstacleLevelIndex<0?0:(obstacleLevelIndex+1)%obstacleLevels.Length; StartObstacle(chosen); }
            string[] tasks={"Plant the royal trees","Add the flower beds","Restore the fountain","Garden complete"};
            Text(new Rect(805,658,285,25),tasks[Mathf.Min(built,3)],14,pale,TextAnchor.MiddleCenter);
            if(Button(new Rect(812,688,275,39),built>=3?"RESTORED":"RESTORE  /  1 STAR",new Color(.29f,.37f,.59f),16,stars>0 && built<3))
            { stars--; built++; Save(); Sound(6); Notify("A little more magic in your kingdom."); }
            Text(new Rect(83,757,800,32),"LOCAL SAVE     •     NO ACCOUNT     •     NO INTERNET REQUIRED",13,new Color(.61f,.74f,.84f));
            if(Button(new Rect(978,65,140,43),muted?"SOUND OFF":"SOUND ON",new Color(.13f,.24f,.39f),14)) { muted=!muted; Save(); }
            Text(new Rect(80,805,1040,25),"Offline reconstruction • 2,300 recovered levels • Original level data and art.",12,new Color(.53f,.67f,.77f));
        }
        void Cloud(float x,float y,float s)
        { Color c=new Color(.70f,.85f,.90f,.17f); Disc(x,y,32*s,c); Disc(x+28*s,y-14*s,40*s,c); Disc(x+62*s,y,29*s,c); }
        void DrawCastle(float x,float y,float scale)
        {
            // Architectural shapes are authored for this prototype, with no external art dependency.
            GUI.BeginGroup(new Rect(x,y,370*scale,270*scale));
            Matrix4x4 previous=GUI.matrix;
            GUI.matrix=previous*Matrix4x4.Scale(new Vector3(scale,scale,1));
            Color stone=new Color(.84f,.78f,.62f),light=new Color(.98f,.91f,.73f),roof=new Color(.21f,.39f,.67f);
            Box(new Rect(17,219,328,19),new Color(.05f,.17f,.20f));
            Box(new Rect(64,120,232,111),stone); Box(new Rect(121,69,118,162),light);
            Box(new Rect(40,80,62,151),light); Box(new Rect(264,80,62,151),light);
            for(int i=0;i<3;i++) { Box(new Rect(38+i*22,68,16,25),stone); Box(new Rect(262+i*22,68,16,25),stone); }
            // Tall roofs rendered as rows for sharp, clean silhouettes.
            for(int y0=0;y0<65;y0++)
            { float width=(y0+1)*1.2f; Solid(new Rect(180-width/2,7+y0,width,1.5f),roof); }
            Solid(new Rect(179,0,3,20),gold); Solid(new Rect(182,1,25,12),new Color(.95f,.33f,.36f));
            Box(new Rect(154,167,53,65),new Color(.23f,.19f,.23f));
            Box(new Rect(160,174,41,58),new Color(.41f,.28f,.27f));
            Solid(new Rect(179,179,3,53),gold);
            for(int i=0;i<5;i++)
            { float wx=55+i*54; Box(new Rect(wx,135,19,30),roof); Box(new Rect(wx+5,139,4,18),new Color(.62f,.84f,.93f)); }
            Box(new Rect(163,90,34,47),roof); Solid(new Rect(178,94,4,39),gold);
            Solid(new Rect(149,149,63,4),stone); Solid(new Rect(35,193,299,5),stone);
            GUI.matrix=previous; GUI.EndGroup();
        }
        void DrawLevel()
        {
            if(Event.current.type==EventType.Repaint) levelPainted=true;
            if(Button(new Rect(90,62,118,43),"<  GARDEN",new Color(.12f,.23f,.38f),14,!busy)) { home=true; won=lost=false; }
            Text(new Rect(237,49,550,40),"CROWN GARDEN",28,Color.white,TextAnchor.MiddleLeft,true);
            string levelTitle=obstacleMode?"OBSTACLE LEVEL "+(obstacleLevelIndex+1):recoveredLevelMode?"RECOVERED LEVEL "+level:originalMode?"ORIGINAL LEVEL 1":"LEVEL "+level;
            Text(new Rect(239,91,550,25),levelTitle+"   /   "+board.Width+" x "+board.Height,14,gold,TextAnchor.MiddleLeft,true);
            if(Button(new Rect(929,64,145,43),muted?"SOUND OFF":"SOUND ON",new Color(.12f,.23f,.38f),14)) { muted=!muted; Save(); }
            Box(new Rect(91,151,292,610),navy);
            Text(new Rect(115,176,244,26),"MOVES LEFT",14,pale,TextAnchor.MiddleCenter,true);
            Text(new Rect(115,209,244,81),moves.ToString(),65,moves<=5?colors[0]:Color.white,TextAnchor.MiddleCenter,true);
            Text(new Rect(115,315,244,30),"COLLECT TO WIN",15,gold,TextAnchor.MiddleCenter,true);
            if(originalMode)
            {
                Box(new Rect(113,363,248,72),new Color(.10f,.18f,.34f));
                DrawBox(new Rect(118,370,60,60),1);
                if(obstacleMode)
                {
                    Text(new Rect(183,370,165,27),"BOX  "+Mathf.Min(board.BoxesCleared,targetBoxes)+" / "+targetBoxes,18,Color.white,TextAnchor.MiddleLeft,true);
                    Text(new Rect(183,398,165,27),"ITEM  "+board.RemainingNativeObstaclesTotal,18,Color.white,TextAnchor.MiddleLeft,true);
                    Text(new Rect(116,452,242,54),"Match beside obstacles\nto clear their layers.",17,pale,TextAnchor.MiddleCenter);
                }
                else
                {
                    Text(new Rect(183,370,165,27),Mathf.Min(board.BoxesCleared,targetBoxes)+" / "+targetBoxes,24,Color.white,TextAnchor.MiddleLeft,true);
                    if(recoveredLevelMode)
                        Text(new Rect(183,398,165,27),"OBSTACLE  "+board.RemainingObstacleCellsTotal,16,board.RemainingObstacleCellsTotal==0?colors[2]:pale,TextAnchor.MiddleLeft,true);
                    Text(new Rect(116,452,242,54),recoveredLevelMode?"Match beside obstacles\nto clear them.":"Match beside boxes\nto break them.",17,pale,TextAnchor.MiddleCenter);
                }
                Text(new Rect(116,510,242,45),"Recovered level layout",13,gold,TextAnchor.MiddleCenter);
            }
            else { GoalRow(0,363,targetRed,board.Collected[0]); GoalRow(1,433,targetBlue,board.Collected[1]); }
            if(!originalMode && level>1)
            {
                Box(new Rect(113,507,248,58),new Color(.10f,.18f,.34f));
                Box(new Rect(128,518,35,35),new Color(.40f,.81f,.94f));
                Text(new Rect(180,516,160,35),board.RemainingIce==0?"ICE CLEARED":board.RemainingIce+" ICE LEFT",17,board.RemainingIce==0?colors[2]:pale,TextAnchor.MiddleLeft,true);
            }
            Text(new Rect(115,595,244,26),"SCORE",13,pale,TextAnchor.MiddleCenter,true);
            Text(new Rect(115,628,244,40),board.Score.ToString("N0"),29,gold,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(117,696,239,42),"RETRY LEVEL",new Color(.17f,.27f,.44f),14,!busy)) RetryCurrent();
            Box(new Rect(BX-19,BY-19,S*board.Width+38,S*board.Height+38),new Color(.55f,.51f,.36f));
            Box(new Rect(BX-14,BY-14,S*board.Width+28,S*board.Height+28),new Color(.035f,.075f,.17f));
            for(int y=0;y<board.Height;y++) for(int x=0;x<board.Width;x++)
            {
                if(!board.Active[x,y]) continue;
                Rect r=new Rect(BX+x*S+2,BY+y*S+2,S-4,S-4);
                Box(r,(x+y)%2==0?new Color(.10f,.18f,.32f):new Color(.12f,.21f,.36f));
                if(board.Ice[x,y])
                {
                    Box(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.36f,.80f,1,.48f));
                    Solid(new Rect(r.x+10,r.y+8,30,2),new Color(.8f,1,1,.7f));
                }
                if(board.Jelly[x,y]) Box(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),new Color(.95f,.35f,.75f,.22f));
                if(board.Ivy[x,y]>0)
                {
                    Solid(new Rect(r.x+3,r.y+3,4,r.height-6),new Color(.25f,.78f,.30f,.72f));
                    Solid(new Rect(r.x+3,r.y+r.height-7,r.width-6,4),new Color(.25f,.78f,.30f,.72f));
                }
                if(board.Curtains[x,y]>0) Box(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),new Color(.62f,.28f,.82f,.25f));
                if(board.Chains[x,y]>0)
                {
                    Solid(new Rect(r.x+5,r.y+5,r.width-10,3),new Color(.70f,.76f,.84f,.85f));
                    Solid(new Rect(r.x+5,r.y+r.height-8,r.width-10,3),new Color(.70f,.76f,.84f,.85f));
                    Solid(new Rect(r.x+5,r.y+5,3,r.height-10),new Color(.70f,.76f,.84f,.85f));
                }
                if(board.Sand[x,y]>0) Box(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),new Color(.78f,.62f,.28f,.28f));
                if(board.CellPuzzles[x,y]>0) Box(new Rect(r.x+8,r.y+8,r.width-16,r.height-16),new Color(.25f,.55f,.90f,.25f));
                if(board.Borders[x,y]>0)
                {
                    Solid(new Rect(r.x+1,r.y+1,r.width-2,2),new Color(.95f,.75f,.25f,.75f));
                    Solid(new Rect(r.x+1,r.y+r.height-3,r.width-2,2),new Color(.95f,.75f,.25f,.75f));
                }
                C c=new C(x,y);
                if((selected.HasValue && selected.Value.Equals(c)) || (Time.time<hintUntil && (hintA.Equals(c) || hintB.Equals(c))))
                    Box(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),new Color(1,.86f,.35f,.45f+.2f*Mathf.Sin(Time.time*8)));
            }
            for(int y=0;y<board.Height;y++) for(int x=0;x<board.Width;x++)
            {
                if(!board.Active[x,y]) continue;
                C c=new C(x,y); Vector2 p=new Vector2(BX+x*S,BY+y*S); float size=S;
                if(swapping)
                { if(c.Equals(swapA)) p+=new Vector2(swapB.x-swapA.x,swapB.y-swapA.y)*S*anim;
                  if(c.Equals(swapB)) p+=new Vector2(swapA.x-swapB.x,swapA.y-swapB.y)*S*anim; }
                if(falling) p.y=BY+Mathf.Lerp(fallFrom[x,y],y,anim)*S;
                if(clearing && clearMatch.cells.Contains(c)) size=S*Mathf.Max(.05f,1-anim);
                var rect=new Rect(p.x+(S-size)/2,p.y+(S-size)/2,size,size);
                if(board.Boxes[x,y]>0) DrawBox(rect,board.Boxes[x,y]);
                else if(board.MetalCrates[x,y]>0) DrawMetalCrate(rect,board.MetalCrates[x,y]);
                else if(board.Vases[x,y]>0) DrawVase(rect);
                else if(board.FlowerPots[x,y]>0) DrawFlowerPot(rect,board.FlowerPots[x,y]);
                else if(board.Logs[x,y]>0) DrawLog(rect,board.Logs[x,y]);
                else if(board.Lanterns[x,y]>0) DrawLantern(rect,board.Lanterns[x,y]);
                else if(board.PuzzleBoxes[x,y]>0) DrawPuzzleBox(rect,board.PuzzleBoxes[x,y]);
                else if(board.GiftBoxes[x,y]>0) DrawGiftBox(rect,board.GiftBoxes[x,y]);
                else if(board.Flowers[x,y]>0) DrawFlowers(rect,board.Flowers[x,y]);
                else if(board.ChaliceBoxes[x,y]>0) DrawChaliceBox(rect,board.ChaliceBoxes[x,y]);
                else if(board.DragonBoxes[x,y]>0) DrawDragonBox(rect,board.DragonBoxes[x,y]);
                else if(board.Birdhouses[x,y]>0) DrawBirdhouse(rect,board.Birdhouses[x,y]);
                else if(board.Gnomes[x,y]>0) DrawGnome(rect,board.Gnomes[x,y]);
                else if(board.Safes[x,y]>0) DrawSafe(rect,board.Safes[x,y]);
                else if(board.Masks[x,y]>0) DrawMask(rect,board.Masks[x,y]);
                else if(board.Turtles[x,y]>0) DrawTurtle(rect,board.Turtles[x,y]);
                else if(board.Snowmen[x,y]>0) DrawSnowman(rect,board.Snowmen[x,y]);
                else if(board.SteamBombs[x,y]>0) DrawSteamBomb(rect,board.SteamBombs[x,y]);
                else if(board.Knights[x,y]>0) DrawKnight(rect,board.Knights[x,y]);
                else if(board.TurtleHouses[x,y]>0) DrawTurtleHouse(rect,board.TurtleHouses[x,y]);
                else if(board.Clocks[x,y]>0) DrawClock(rect,board.Clocks[x,y]);
                else if(board.NativeObstacleAt(x,y)>0) DrawNativeObstacle(rect,board.NativeObstacleKindAt(x,y),board.NativeObstacleAt(x,y),board.NativeObstacleVariant[x,y]);
                else if(board.Walls[x,y]>0) DrawWall(rect,board.Walls[x,y],board.WallGroup[x,y]);
                else if(board.Columns[x,y]>0) DrawColumn(rect,board.ColumnDirection[x,y],board.Columns[x,y],false);
                else if(board.MetalColumns[x,y]>0) DrawColumn(rect,board.MetalColumnDirection[x,y],board.MetalColumns[x,y],true);
                else DrawGem(rect,board.Color[x,y],board.Power[x,y]);
            }
            if(Time.time>=toastUntil) Text(new Rect(BX,114,584,31),busy?"A LITTLE MAGIC IN MOTION...":hammer?"CHOOSE A JEWEL TO BREAK":"SWAP JEWELS  •  MATCH 3 OR MORE",13,pale,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(446,779,170,46),"HINT  [H]",new Color(.21f,.33f,.52f),15,!busy && !won && !lost)) Hint();
            if(Button(new Rect(630,779,218,46),hammer?"CANCEL HAMMER":"HAMMER  x"+hammers,hammer?new Color(.69f,.43f,.15f):new Color(.21f,.33f,.52f),15,!busy && !won && !lost && hammers>0))
            { hammer=!hammer; if(hammer) Notify("Choose any jewel. Hammer does not cost a move."); }
            Text(new Rect(857,777,190,53),"2x2 = propeller\n4 in line = rocket\n5 in line = rainbow",12,pale,TextAnchor.MiddleCenter);
            if(won || lost) DrawResult();
        }
        void GoalRow(int color,float y,int target,int collected)
        {
            Box(new Rect(113,y,248,58),new Color(.10f,.18f,.34f));
            DrawGem(new Rect(119,y+1,56,56),color,0);
            Text(new Rect(185,y+6,160,43),collected>=target?"COMPLETE":Mathf.Min(collected,target)+" / "+target,20,collected>=target?colors[2]:Color.white,TextAnchor.MiddleLeft,true);
            Solid(new Rect(185,y+48,143,3),new Color(.18f,.28f,.43f));
            Solid(new Rect(185,y+48,143*Mathf.Clamp01((float)collected/target),3),colors[color]);
        }
        void DrawGem(Rect r,int color,int power)
        {
            if(color<0) return;
            if(power>0 && power<recoveredSpecial.Length && recoveredSpecial[power]!=null)
            {
                GUI.DrawTexture(r,recoveredSpecial[power],ScaleMode.ScaleToFit,true);
                return;
            }
            if(power==5)
            {
                Disc(r.center.x,r.center.y,r.width*.31f,navy);
                for(int blade=0;blade<4;blade++)
                {
                    float angle=blade*Mathf.PI*.5f+Time.time*1.7f;
                    Disc(r.center.x+Mathf.Cos(angle)*r.width*.17f,r.center.y+Mathf.Sin(angle)*r.height*.17f,r.width*.115f,colors[color]);
                }
                Disc(r.center.x,r.center.y,r.width*.08f,gold);
                return;
            }
            if(power==4)
            {
                Disc(r.center.x,r.center.y,r.width*.34f,new Color(.96f,.89f,.69f));
                Disc(r.center.x,r.center.y,r.width*.29f,new Color(.19f,.16f,.32f));
                for(int i=0;i<5;i++) { float a=i*Mathf.PI*2/5+Time.time*.65f; Disc(r.center.x+Mathf.Cos(a)*r.width*.18f,r.center.y+Mathf.Sin(a)*r.width*.18f,r.width*.09f,colors[i]); }
                return;
            }
            GUI.color=new Color(0,0,0,.25f); GUI.DrawTexture(new Rect(r.x+1,r.y+4,r.width,r.height),gems[color]); GUI.color=Color.white;
            GUI.DrawTexture(r,gems[color]);
            if(power==1 || power==2)
            {
                if(power==1) { Box(new Rect(r.x+r.width*.15f,r.y+r.height*.41f,r.width*.70f,r.height*.18f),Color.white); Box(new Rect(r.x+r.width*.44f,r.y+r.height*.37f,r.width*.10f,r.height*.26f),gold); }
                else { Box(new Rect(r.x+r.width*.41f,r.y+r.height*.15f,r.width*.18f,r.height*.70f),Color.white); Box(new Rect(r.x+r.width*.37f,r.y+r.height*.44f,r.width*.26f,r.height*.10f),gold); }
            }
            if(power==3)
            { Disc(r.center.x,r.center.y,r.width*.25f,navy); Disc(r.center.x-r.width*.08f,r.center.y-r.height*.08f,r.width*.07f,pale); Solid(new Rect(r.center.x,r.y+r.height*.18f,r.width*.06f,r.height*.14f),gold); }
        }
        Texture2D FindObstacleArt(string token)
        {
            for(int i=0;i<obstacleArt.Count;i++)
                if(string.Equals(obstacleArt[i].name,token,StringComparison.OrdinalIgnoreCase)) return obstacleArt[i];
            for(int i=0;i<obstacleArt.Count;i++)
            {
                int separator=obstacleArt[i].name.IndexOf('_');
                if(separator>0 && string.Equals(obstacleArt[i].name.Substring(separator+1),token,StringComparison.OrdinalIgnoreCase)) return obstacleArt[i];
            }
            for(int i=0;i<obstacleArt.Count;i++)
                if(obstacleArt[i].name.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0) return obstacleArt[i];
            return null;
        }
        bool DrawRecoveredObstacle(Rect rect,string token,int layers,bool showLayers=true)
        {
            var art=FindObstacleArt(token);
            if(art==null) return false;
            GUI.DrawTexture(rect,art,ScaleMode.ScaleToFit,true);
            if(showLayers && layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
            return true;
        }
        void DrawBox(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Items-BoxItem-box",layers)) return;
            if(boxArt!=null) GUI.DrawTexture(rect,boxArt,ScaleMode.ScaleToFit,true);
            else Box(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),new Color(.70f,.39f,.12f));
            if(layers>1) Text(rect,layers.ToString(),18,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawMetalCrate(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"metal_crate",layers)) return;
            Box(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),new Color(.28f,.36f,.44f));
            Box(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-16),new Color(.52f,.60f,.66f));
            Solid(new Rect(rect.x+rect.width*.43f,rect.y+6,rect.width*.14f,rect.height-12),new Color(.27f,.34f,.40f));
            Solid(new Rect(rect.x+6,rect.y+rect.height*.43f,rect.width-12,rect.height*.14f),new Color(.27f,.34f,.40f));
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawVase(Rect rect)
        {
            if(DrawRecoveredObstacle(rect,"Items-Vase-vase",1,false)) return;
            Disc(rect.center.x,rect.y+rect.height*.34f,rect.width*.22f,new Color(.88f,.58f,.25f));
            Box(new Rect(rect.x+rect.width*.28f,rect.y+rect.height*.30f,rect.width*.44f,rect.height*.42f),new Color(.78f,.42f,.16f));
            Box(new Rect(rect.x+rect.width*.20f,rect.y+rect.height*.68f,rect.width*.60f,rect.height*.12f),new Color(.98f,.72f,.34f));
        }
        void DrawFlowerPot(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Flower_vase_01",layers)) return;
            Color clay=new Color(.78f,.38f,.16f), rim=new Color(.98f,.63f,.25f), leaf=new Color(.25f,.65f,.26f);
            Disc(rect.center.x-rect.width*.10f,rect.y+rect.height*.24f,rect.width*.13f,leaf);
            Disc(rect.center.x+rect.width*.10f,rect.y+rect.height*.20f,rect.width*.13f,leaf);
            Box(new Rect(rect.x+rect.width*.22f,rect.y+rect.height*.34f,rect.width*.56f,rect.height*.42f),clay);
            Box(new Rect(rect.x+rect.width*.14f,rect.y+rect.height*.28f,rect.width*.72f,rect.height*.14f),rim);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawLog(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Parts-log_full",layers)) return;
            Color bark=new Color(.42f,.22f,.10f), cut=new Color(.82f,.57f,.30f), ring=new Color(.55f,.32f,.15f);
            Box(new Rect(rect.x+rect.width*.12f,rect.y+rect.height*.24f,rect.width*.76f,rect.height*.52f),bark);
            Disc(rect.center.x,rect.y+rect.height*.50f,rect.width*.24f,cut);
            Disc(rect.center.x,rect.y+rect.height*.50f,rect.width*.16f,ring);
            Disc(rect.center.x,rect.y+rect.height*.50f,rect.width*.08f,cut);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawLantern(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,layers==1?"Items-Lantern-Lantern_On":"Items-Lantern-Lantern_Off",layers)) return;
            Color metal=new Color(.31f,.38f,.48f), glass=layers==1?new Color(1f,.78f,.28f):new Color(.55f,.68f,.78f);
            Box(new Rect(rect.x+rect.width*.30f,rect.y+rect.height*.18f,rect.width*.40f,rect.height*.64f),metal);
            Box(new Rect(rect.x+rect.width*.38f,rect.y+rect.height*.30f,rect.width*.24f,rect.height*.38f),glass);
            Solid(new Rect(rect.x+rect.width*.24f,rect.y+rect.height*.14f,rect.width*.52f,3),gold);
            Solid(new Rect(rect.x+rect.width*.24f,rect.y+rect.height*.78f,rect.width*.52f,3),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawPuzzleBox(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Puzzle_board_01",layers)) return;
            Color wood=new Color(.48f,.25f,.12f), edge=new Color(.92f,.65f,.28f), piece=new Color(.22f,.52f,.86f);
            Box(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),wood);
            Box(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-16),new Color(.12f,.20f,.31f));
            Box(new Rect(rect.x+rect.width*.44f,rect.y+rect.height*.18f,rect.width*.12f,rect.height*.64f),piece);
            Box(new Rect(rect.x+rect.width*.18f,rect.y+rect.height*.44f,rect.width*.64f,rect.height*.12f),piece);
            Solid(new Rect(rect.x+7,rect.y+7,rect.width-14,2),edge);
            Solid(new Rect(rect.x+7,rect.y+rect.height-9,rect.width-14,2),edge);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawGiftBox(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"gift_box_"+Mathf.Clamp(layers,1,6),layers,false)) return;
            Color ribbon=layers<=1?new Color(.95f,.86f,.30f):new Color(.78f,.32f,.72f);
            Box(new Rect(rect.x+4,rect.y+6,rect.width-8,rect.height-10),new Color(.85f,.20f,.18f));
            Box(new Rect(rect.x+rect.width*.43f,rect.y+5,rect.width*.14f,rect.height-8),ribbon);
            Box(new Rect(rect.x+5,rect.y+rect.height*.42f,rect.width-10,rect.height*.14f),ribbon);
            Disc(rect.center.x,rect.y+rect.height*.24f,rect.width*.13f,ribbon);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawFlowers(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Flowers_Big",layers)) return;
            Color stem=new Color(.22f,.56f,.25f), petal=layers<=1?new Color(1f,.62f,.85f):new Color(.78f,.42f,.88f);
            Solid(new Rect(rect.center.x-2,rect.y+rect.height*.38f,4,rect.height*.40f),stem);
            Disc(rect.center.x-rect.width*.13f,rect.y+rect.height*.62f,rect.width*.13f,stem);
            Disc(rect.center.x+rect.width*.13f,rect.y+rect.height*.62f,rect.width*.13f,stem);
            Disc(rect.center.x,rect.y+rect.height*.29f,rect.width*.15f,petal);
            Disc(rect.center.x-rect.width*.15f,rect.y+rect.height*.40f,rect.width*.13f,petal);
            Disc(rect.center.x+rect.width*.15f,rect.y+rect.height*.40f,rect.width*.13f,petal);
            Disc(rect.center.x,rect.y+rect.height*.42f,rect.width*.08f,gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawChaliceBox(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Items-ChaliceBox-box_01",layers)) return;
            Color wood=new Color(.35f,.20f,.12f), metal=new Color(.86f,.66f,.25f), glass=new Color(.45f,.72f,.95f,.85f);
            Box(new Rect(rect.x+5,rect.y+7,rect.width-10,rect.height-12),wood);
            Solid(new Rect(rect.x+8,rect.y+10,rect.width-16,3),metal);
            Solid(new Rect(rect.x+8,rect.y+rect.height-13,rect.width-16,3),metal);
            Disc(rect.center.x,rect.y+rect.height*.48f,rect.width*.19f,glass);
            Box(new Rect(rect.x+rect.width*.43f,rect.y+rect.height*.24f,rect.width*.14f,rect.height*.48f),metal);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawDragonBox(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Parts-dragon_door",layers)) return;
            Color shell=new Color(.23f,.28f,.38f), gold=new Color(.92f,.61f,.24f), egg=layers>=4?new Color(.96f,.55f,.28f):new Color(.72f,.76f,.86f);
            Box(new Rect(rect.x+4,rect.y+5,rect.width-8,rect.height-10),shell);
            Box(new Rect(rect.x+9,rect.y+10,rect.width-18,rect.height-20),new Color(.10f,.14f,.22f));
            Disc(rect.center.x,rect.y+rect.height*.52f,rect.width*.20f,egg);
            Solid(new Rect(rect.x+rect.width*.20f,rect.y+rect.height*.20f,rect.width*.60f,3),gold);
            Solid(new Rect(rect.x+rect.width*.20f,rect.y+rect.height*.78f,rect.width*.60f,3),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawBirdhouse(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Items-BirdhouseItem-bird_goal",layers)) return;
            Color wood=new Color(.55f,.30f,.14f), roof=new Color(.84f,.22f,.18f), hole=new Color(.10f,.12f,.18f);
            Box(new Rect(rect.x+rect.width*.22f,rect.y+rect.height*.30f,rect.width*.56f,rect.height*.52f),wood);
            Solid(new Rect(rect.x+rect.width*.16f,rect.y+rect.height*.22f,rect.width*.68f,5),roof);
            Disc(rect.center.x,rect.y+rect.height*.52f,rect.width*.13f,hole);
            Solid(new Rect(rect.center.x-2,rect.y+rect.height*.78f,4,rect.height*.13f),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawGnome(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Gnome_big",layers)) return;
            Color hat=layers>=3?new Color(.85f,.22f,.26f):new Color(.30f,.50f,.86f), body=new Color(.68f,.40f,.20f);
            Disc(rect.center.x,rect.y+rect.height*.60f,rect.width*.22f,body);
            Solid(new Rect(rect.x+rect.width*.33f,rect.y+rect.height*.25f,rect.width*.34f,rect.height*.30f),hat);
            Disc(rect.center.x,rect.y+rect.height*.43f,rect.width*.12f,new Color(1f,.78f,.58f));
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawSafe(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Parts-safe01",layers)) return;
            Color steel=layers==1?new Color(.60f,.67f,.74f):new Color(.34f,.42f,.52f);
            Box(new Rect(rect.x+5,rect.y+8,rect.width-10,rect.height-14),steel);
            Disc(rect.center.x,rect.center.y,rect.width*.16f,gold);
            Solid(new Rect(rect.center.x-2,rect.y+rect.height*.24f,4,rect.height*.52f),navy);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawMask(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"mask-first_layer_fill",layers)) return;
            Color sand=layers==1?new Color(.94f,.72f,.35f):new Color(.64f,.46f,.27f);
            Disc(rect.center.x,rect.center.y,rect.width*.30f,sand);
            Box(new Rect(rect.x+rect.width*.28f,rect.y+rect.height*.40f,rect.width*.16f,rect.height*.10f),navy);
            Box(new Rect(rect.x+rect.width*.56f,rect.y+rect.height*.40f,rect.width*.16f,rect.height*.10f),navy);
            Solid(new Rect(rect.x+rect.width*.36f,rect.y+rect.height*.64f,rect.width*.28f,3),new Color(.34f,.20f,.12f));
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawTurtle(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Items-TurtleItem-turtle",layers)) return;
            Color shell=layers==1?new Color(.30f,.70f,.42f):new Color(.25f,.45f,.30f);
            Disc(rect.center.x,rect.center.y,rect.width*.28f,shell);
            Solid(new Rect(rect.x+rect.width*.12f,rect.center.y-2,rect.width*.76f,4),new Color(.12f,.28f,.18f));
            Disc(rect.x+rect.width*.78f,rect.center.y,rect.width*.08f,new Color(.38f,.76f,.48f));
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawSnowman(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Snowman_big",layers)) return;
            Color ice=layers>=5?new Color(.65f,.82f,.94f):new Color(.88f,.95f,1f);
            Disc(rect.center.x,rect.y+rect.height*.58f,rect.width*.24f,ice);
            Disc(rect.center.x,rect.y+rect.height*.34f,rect.width*.17f,ice);
            Solid(new Rect(rect.x+rect.width*.33f,rect.y+rect.height*.20f,rect.width*.34f,4),new Color(.16f,.15f,.20f));
            Disc(rect.center.x,rect.y+rect.height*.35f,rect.width*.04f,new Color(.94f,.45f,.18f));
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawSteamBomb(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Barrel_bomb",layers)) return;
            Color metal=layers>=8?new Color(.33f,.42f,.52f):new Color(.55f,.64f,.72f);
            Box(new Rect(rect.x+7,rect.y+9,rect.width-14,rect.height-16),metal);
            Disc(rect.center.x,rect.center.y,rect.width*.15f,new Color(.95f,.42f,.18f));
            Solid(new Rect(rect.x+rect.width*.22f,rect.y+rect.height*.20f,rect.width*.56f,3),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawKnight(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"knight_goal",layers)) return;
            Color armor=layers>=5?new Color(.38f,.46f,.62f):new Color(.62f,.72f,.86f);
            Disc(rect.center.x,rect.y+rect.height*.53f,rect.width*.22f,armor);
            Box(new Rect(rect.x+rect.width*.32f,rect.y+rect.height*.23f,rect.width*.36f,rect.height*.28f),armor);
            Solid(new Rect(rect.x+rect.width*.22f,rect.y+rect.height*.31f,rect.width*.56f,3),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawTurtleHouse(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"Parts-turtlehouse_icon_cropped",layers)) return;
            Color wood=layers>=5?new Color(.42f,.26f,.15f):new Color(.72f,.45f,.22f), roof=new Color(.30f,.50f,.64f);
            Box(new Rect(rect.x+5,rect.y+12,rect.width-10,rect.height-16),wood);
            Solid(new Rect(rect.x+rect.width*.15f,rect.y+rect.height*.25f,rect.width*.70f,5),roof);
            Box(new Rect(rect.x+rect.width*.40f,rect.y+rect.height*.53f,rect.width*.20f,rect.height*.30f),navy);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawClock(Rect rect,int layers)
        {
            if(DrawRecoveredObstacle(rect,"clock_icon",layers)) return;
            Color face=layers>=8?new Color(.45f,.50f,.60f):new Color(.82f,.86f,.92f);
            Disc(rect.center.x,rect.center.y,rect.width*.28f,face);
            Disc(rect.center.x,rect.center.y,rect.width*.22f,new Color(.18f,.23f,.32f));
            Solid(new Rect(rect.center.x-2,rect.y+rect.height*.28f,4,rect.height*.24f),gold);
            Solid(new Rect(rect.center.x,rect.center.y-2,rect.width*.20f,4),gold);
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawNativeObstacle(Rect rect,int kind,int layers,int variant)
        {
            MatchBoard.NativeObstacleKind obstacle=(MatchBoard.NativeObstacleKind)kind;
            string token=null;
            switch(obstacle)
            {
                case MatchBoard.NativeObstacleKind.Armory: token="Armory_big"; break;
                case MatchBoard.NativeObstacleKind.Barrel: token="Barrel"; break;
                case MatchBoard.NativeObstacleKind.Cannon: token="atlas_CannonItemAtlas_008_body"; break;
                case MatchBoard.NativeObstacleKind.Lily: token="lily_goal"; break;
                case MatchBoard.NativeObstacleKind.Lyre: token="lyre"; break;
                case MatchBoard.NativeObstacleKind.JellyMaker: token="JellyFountain_big"; break;
                case MatchBoard.NativeObstacleKind.Lightbulb: token="atlas_LightBulbSpriteAtlas_005_Potion_tube_goal_icon_01"; break;
                case MatchBoard.NativeObstacleKind.MagicCards: token="magic_cards_goal_icon"; break;
                case MatchBoard.NativeObstacleKind.MagicGlobe: token="MagicGlobe_big"; break;
                case MatchBoard.NativeObstacleKind.MagicOrb: token="MagicOrb_big"; break;
                case MatchBoard.NativeObstacleKind.Magnet: token=variant==95?"magnet_down":"magnet_goal_icon"; break;
                case MatchBoard.NativeObstacleKind.Starfish: token="starfish"; break;
                case MatchBoard.NativeObstacleKind.Tablet: token=layers>=2?"tablet_column_1x4_01":"Tablet_goal_icon_01"; break;
                case MatchBoard.NativeObstacleKind.Target: token="target_goal"; break;
                case MatchBoard.NativeObstacleKind.Gem: token="Parts-dragon_gem"; break;
                case MatchBoard.NativeObstacleKind.DragonEgg: token="dragon_goal_icon"; break;
                case MatchBoard.NativeObstacleKind.DragonSmallBoxHorizontal:
                    token=variant==10002?"dragon_box_yatay1_2":"dragon_box_yatay_2"; break;
                case MatchBoard.NativeObstacleKind.DragonSmallBoxVertical:
                    token=variant==10000?"dragon_box_dikey_2":"dragon_box_dikey1_2"; break;
                case MatchBoard.NativeObstacleKind.FireDragonEgg: token="atlas_FireDragonEggSpritesAtlas_004_Dragon_Egg_001"; break;
                case MatchBoard.NativeObstacleKind.Volcano: token="atlas_VolcanoSpritesAtlas_022_Body"; break;
                case MatchBoard.NativeObstacleKind.ArsenalMissile: token="atlas_AttackArsenalSpriteAtlas_013_firework_01"; break;
            }
            if(token!=null && DrawRecoveredObstacle(rect,token,layers,false)) return;
            Color baseColor;
            switch(obstacle)
            {
                case MatchBoard.NativeObstacleKind.Armory: baseColor=new Color(.47f,.28f,.16f); break;
                case MatchBoard.NativeObstacleKind.Barrel: baseColor=new Color(.62f,.34f,.14f); break;
                case MatchBoard.NativeObstacleKind.Cannon: baseColor=new Color(.28f,.34f,.43f); break;
                case MatchBoard.NativeObstacleKind.Gem: baseColor=new Color(.35f,.85f,1f); break;
                case MatchBoard.NativeObstacleKind.JellyMaker: baseColor=new Color(.85f,.34f,.70f); break;
                case MatchBoard.NativeObstacleKind.Lightbulb: baseColor=new Color(1f,.82f,.24f); break;
                case MatchBoard.NativeObstacleKind.Lily: baseColor=new Color(.35f,.72f,.38f); break;
                case MatchBoard.NativeObstacleKind.Lyre: baseColor=new Color(.93f,.67f,.22f); break;
                case MatchBoard.NativeObstacleKind.MagicCards: baseColor=new Color(.46f,.28f,.78f); break;
                case MatchBoard.NativeObstacleKind.MagicGlobe: baseColor=new Color(.24f,.68f,.92f); break;
                case MatchBoard.NativeObstacleKind.MagicOrb: baseColor=new Color(.65f,.28f,.94f); break;
                case MatchBoard.NativeObstacleKind.Magnet: baseColor=new Color(.86f,.24f,.28f); break;
                case MatchBoard.NativeObstacleKind.Starfish: baseColor=new Color(1f,.48f,.25f); break;
                case MatchBoard.NativeObstacleKind.Tablet: baseColor=new Color(.27f,.57f,.76f); break;
                case MatchBoard.NativeObstacleKind.Target: baseColor=new Color(.95f,.38f,.28f); break;
                case MatchBoard.NativeObstacleKind.ArsenalMissile: baseColor=new Color(.52f,.60f,.72f); break;
                case MatchBoard.NativeObstacleKind.DragonEgg: baseColor=new Color(.96f,.55f,.28f); break;
                case MatchBoard.NativeObstacleKind.DragonSmallBoxHorizontal: baseColor=new Color(.48f,.30f,.18f); break;
                case MatchBoard.NativeObstacleKind.DragonSmallBoxVertical: baseColor=new Color(.56f,.36f,.20f); break;
                case MatchBoard.NativeObstacleKind.FireDragonEgg: baseColor=new Color(1f,.24f,.12f); break;
                default: baseColor=new Color(.75f,.22f,.16f); break;
            }
            Box(new Rect(rect.x+4,rect.y+5,rect.width-8,rect.height-10),baseColor);
            if(obstacle==MatchBoard.NativeObstacleKind.Cannon)
            {
                Disc(rect.center.x,rect.center.y,rect.width*.20f,navy);
                Box(new Rect(rect.x+rect.width*.18f,rect.y+rect.height*.42f,rect.width*.64f,rect.height*.16f),new Color(.62f,.70f,.78f));
            }
            else if(obstacle==MatchBoard.NativeObstacleKind.Lightbulb || obstacle==MatchBoard.NativeObstacleKind.Gem)
                Disc(rect.center.x,rect.center.y,rect.width*.28f,baseColor);
            else if(obstacle==MatchBoard.NativeObstacleKind.Starfish)
            {
                for(int i=0;i<5;i++) { float a=i*Mathf.PI*2/5; Disc(rect.center.x+Mathf.Cos(a)*rect.width*.18f,rect.center.y+Mathf.Sin(a)*rect.height*.18f,rect.width*.13f,baseColor); }
                Disc(rect.center.x,rect.center.y,rect.width*.10f,gold);
            }
            else if(obstacle==MatchBoard.NativeObstacleKind.Target)
            {
                Disc(rect.center.x,rect.center.y,rect.width*.30f,Color.white); Disc(rect.center.x,rect.center.y,rect.width*.20f,baseColor); Disc(rect.center.x,rect.center.y,rect.width*.09f,Color.white);
            }
            else
            {
                Solid(new Rect(rect.x+rect.width*.18f,rect.y+rect.height*.25f,rect.width*.64f,3),gold);
                Solid(new Rect(rect.x+rect.width*.18f,rect.y+rect.height*.72f,rect.width*.64f,3),new Color(.10f,.15f,.25f,.45f));
            }
            if(layers>1) Text(rect,layers.ToString(),15,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawWall(Rect rect,int layers,int group)
        {
            if(DrawRecoveredObstacle(rect,"WallBase-fill",layers)) return;
            int palette=((group%3)+3)%3;
            Color stone=palette==1?new Color(.42f,.47f,.56f):palette==2?new Color(.30f,.36f,.45f):new Color(.51f,.40f,.32f);
            Box(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),stone);
            for(int i=1;i<4;i++) Solid(new Rect(rect.x+7,rect.y+rect.height*i/4f,rect.width-14,2),new Color(.12f,.15f,.21f,.6f));
            if(layers>1) Text(rect,layers.ToString(),18,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawColumn(Rect rect,int direction,int layers,bool metal)
        {
            if(DrawRecoveredObstacle(rect,metal?"tablet_column_1x4_01":direction==3?"Items-Column-Column_Up":direction==4?"Items-Column-Column_Down":direction==1?"Items-Column-Column_Horizontal":"Items-Column-Column_Horizontal",layers)) return;
            Color body=metal?new Color(.38f,.45f,.54f):new Color(.56f,.35f,.16f);
            Color edge=metal?new Color(.70f,.80f,.88f):new Color(.88f,.62f,.25f);
            Box(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),new Color(.03f,.07f,.13f,.72f));
            bool horizontal=direction==1 || direction==2;
            Rect shaft=horizontal
                ?new Rect(rect.x+2,rect.y+rect.height*.36f,rect.width-4,rect.height*.28f)
                :new Rect(rect.x+rect.width*.36f,rect.y+2,rect.width*.28f,rect.height-4);
            Box(shaft,body);
            if(horizontal)
            {
                Solid(new Rect(shaft.x,shaft.y+2,shaft.width,2),edge);
                Solid(new Rect(direction==1?rect.x+rect.width*.10f:rect.x+rect.width*.72f,rect.y+rect.height*.22f,rect.width*.18f,rect.height*.56f),edge);
            }
            else
            {
                Solid(new Rect(shaft.x+2,shaft.y,2,shaft.height),edge);
                Solid(new Rect(rect.x+rect.width*.22f,direction==3?rect.y+rect.height*.10f:rect.y+rect.height*.72f,rect.width*.56f,rect.height*.18f),edge);
            }
            if(layers>1) Text(rect,layers.ToString(),16,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawResult()
        {
            Solid(new Rect(0,0,W,H),new Color(.01f,.03f,.10f,.78f));
            Box(new Rect(365,193,470,461),new Color(.13f,.23f,.40f));
            Text(new Rect(395,224,410,60),won?"Splendid!":"One more try?",43,Color.white,TextAnchor.MiddleCenter,true);
            int earned=1+(moves>=5?1:0)+(moves>=12?1:0);
            for(int i=0;i<3;i++) { GUI.color=won && i<earned?gold:new Color(.30f,.37f,.47f); GUI.DrawTexture(new Rect(460+i*99,303,82,82),star); } GUI.color=Color.white;
            Text(new Rect(400,404,400,60),won?(recoveredLevelMode?"Level "+level+" complete!":originalMode?"Original level 1 complete!":"Level complete. Your garden awaits!"):"Collect every target before\nyou run out of moves.",20,pale,TextAnchor.MiddleCenter);
            Text(new Rect(400,470,400,32),"SCORE  "+board.Score.ToString("N0"),22,gold,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(420,529,360,59),won?"CONTINUE":"TRY AGAIN",new Color(.25f,.69f,.37f),23)) { if(won && (originalMode || recoveredLevelMode)) { home=true; won=lost=false; } else RetryCurrent(); }
            if(Button(new Rect(455,602,290,31),"BACK TO GARDEN",new Color(.17f,.28f,.45f),14)) { home=true; won=lost=false; }
        }
        void Box(Rect r,Color c) { GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,Mathf.Min(12,Mathf.Min(r.width,r.height)*.2f)); }
        void Solid(Rect r,Color c) { GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white; }
        void Disc(float x,float y,float radius,Color c) { GUI.color=c; GUI.DrawTexture(new Rect(x-radius,y-radius,radius*2,radius*2),circle); GUI.color=Color.white; }
        void Text(Rect r,string s,int size,Color c,TextAnchor alignment=TextAnchor.MiddleLeft,bool bold=false)
        { GUI.Label(r,s,new GUIStyle { font=font,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=alignment,normal={textColor=c},wordWrap=true }); }
        bool Button(Rect r,string label,Color color,int size,bool enabled=true)
        {
            bool over=r.Contains(MouseLogical(Event.current.mousePosition));
            Box(new Rect(r.x,r.y+4,r.width,r.height),new Color(.01f,.04f,.10f,.5f));
            Box(r,enabled?(over?Color.Lerp(color,Color.white,.12f):color):Color.Lerp(color,navy,.6f));
            Text(r,label,size,enabled?Color.white:new Color(.48f,.59f,.68f),TextAnchor.MiddleCenter,true);
            return enabled && GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        IEnumerator Smoke(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"garden.png"));
            yield return new WaitForSeconds(.5f); StartLevel();
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"gameplay.png"));
            yield return new WaitForSeconds(.5f);
            // Exercise actual animation/input action pathways through valid hints.
            int actions=0;
            for(int i=0;i<8 && !won && !lost;i++)
            {
                C a,b; board.FindMove(out a,out b);
                if(a.Equals(b)) yield return UsePower(a); else yield return SwapMove(a,b);
                actions++;
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"after-moves.png"));
            bool stable=board.FindMatches().cells.Count==0,passed=homePainted && levelPainted && stable && recoveredArtCount==10 && recoveredAudio.Count==12 && (actions==8 || won);
            File.WriteAllText(Path.Combine(folder,"runtime-check.txt"),(passed?"PASS":"FAIL")+": homeRepaint="+homePainted+", gameplayRepaint="+levelPainted+", recoveredArt="+recoveredArtCount+"/10, recoveredAudio="+recoveredAudio.Count+"/12, legal actions="+actions+", score="+board.Score+", moves="+moves+", stable="+stable);
            yield return new WaitForSeconds(1); Application.Quit(passed?0:2);
        }
        int comboFlightScreens;
        IEnumerator CaptureFlight(string path)
        {
            float deadline=Time.time+5;
            while(!flying && Time.time<deadline) yield return null;
            if(flying)
            {
                yield return new WaitForSeconds(.24f);
                ScreenCapture.CaptureScreenshot(path); comboFlightScreens++;
            }
        }
        IEnumerator ComboSmoke(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1);
            bool passed=true;
            for(int partner=1;partner<=4;partner++)
            {
                StartLevel(); targetRed=targetBlue=999;
                board=new MatchBoard(123+partner,1); moves=30;
                board.Power[3,3]=5; board.Power[4,3]=partner;
                board.Color[3,3]=1; board.Color[4,3]=2; board.Ice[6,6]=true;
                Notify("PROPELLER + "+(partner==1?"HORIZONTAL ROCKET":partner==2?"VERTICAL ROCKET":partner==3?"BOMB":"RAINBOW"),10);
                yield return new WaitForSeconds(.3f);
                StartCoroutine(CaptureFlight(Path.Combine(folder,"combo-"+partner+"-flight.png")));
                yield return SwapMove(new C(3,3),new C(4,3));
                passed &= moves==29 && board.FindMatches().cells.Count==0 && !board.Ice[6,6];
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"combo-"+partner+"-after.png"));
                yield return new WaitForSeconds(.3f);
            }
            passed &= comboFlightScreens==4 && recoveredArtCount==10 && recoveredAudio.Count==12;
            File.WriteAllText(Path.Combine(folder,"combo-runtime.txt"),(passed?"PASS":"FAIL")+": four partner combinations rendered and resolved; one move each, ice target cleared, stable boards, flight captures="+comboFlightScreens);
            yield return new WaitForSeconds(.5f); Application.Quit(passed?0:2);
        }
        IEnumerator OriginalSmoke(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1);
            StartOriginal();
            bool passed=originalMode && board.Width==9 && board.Height==9 && board.RemainingBoxes==54 && moves==30 && boxArt!=null;
            yield return new WaitForSeconds(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"original-level-1-start.png"));
            int actions=0;
            for(int i=0;i<12 && !won && !lost;i++)
            {
                C a,b;
                if(!board.FindMove(out a,out b)) { if(!board.Shuffle()) { passed=false; break; } board.FindMove(out a,out b); }
                passed &= a.Equals(b)?board.IsMovable(a):board.CanSwap(a,b);
                if(a.Equals(b)) yield return UsePower(a); else yield return SwapMove(a,b);
                actions++;
            }
            passed &= board.BoxesCleared>0 && board.FindMatches().cells.Count==0 && moves==30-actions && recoveredArtCount==10 && recoveredAudio.Count==12;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"original-level-1-after.png"));
            File.WriteAllText(Path.Combine(folder,"original-runtime.txt"),(passed?"PASS":"FAIL")+": original 9x9 / 30 moves / 54 boxes loaded; actions="+actions+", boxesCleared="+board.BoxesCleared+", boxesLeft="+board.RemainingBoxes+", moves="+moves+", stable="+(board.FindMatches().cells.Count==0)+", won="+won+", boxArt="+(boxArt!=null));
            yield return new WaitForSeconds(1); Application.Quit(passed?0:2);
        }
    }
}




