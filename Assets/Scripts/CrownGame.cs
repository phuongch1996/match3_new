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
        int recoveredArtCount;
        readonly Dictionary<string,AudioClip> recoveredAudio=new Dictionary<string,AudioClip>();
        Font font;
        AudioSource audioSource;
        AudioClip[] notes;
        int level,unlocked,stars,built,moves,combo,targetRed,targetBlue;
        int targetBoxes;
        bool originalMode;
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
            unlocked=Mathf.Max(1,PlayerPrefs.GetInt("cg.level",1)); level=unlocked;
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
            originalMode=false; targetBoxes=0;
            level=unlocked; board=new MatchBoard(seed++ + level*193,level);
            moves=level==1?26:30; targetRed=level==1?22:24+Mathf.Min(level,10); targetBlue=level==1?22:24+Mathf.Min(level,10);
            won=lost=busy=hammer=false; hammers=3; home=false; selected=null; combo=0; idleAt=Time.time;
            sparks.Clear(); Notify(level==1?"Match 4 in a square to create a propeller!":"Break the ice and collect red & blue jewels.",4);
        }
        void StartOriginal()
        {
            try
            {
                var source=Resources.Load<TextAsset>("OriginalLevels/level_1");
                if(source==null) throw new InvalidOperationException("Recovered level file is missing.");
                originalDefinition=JsonUtility.FromJson<OriginalLevelDefinition>(source.text);
                var restored=MatchBoard.FromOriginal(originalDefinition,10201);
                board=restored; originalMode=true; level=1; targetBoxes=originalDefinition.boxGoal;
                moves=originalDefinition.moves; targetRed=targetBlue=0;
                won=lost=busy=hammer=false; hammers=3; home=false; selected=null; dragStart=null;
                combo=0; idleAt=Time.time; sparks.Clear();
                Notify("Original level 1: break all "+targetBoxes+" boxes in "+moves+" moves.",4);
            }
            catch(Exception error) { home=true; Debug.LogException(error); Notify("Original level could not load. See the game log.",5); }
        }
        void RetryCurrent() { if(originalMode) StartOriginal(); else StartLevel(); }
        bool GoalsDone { get { return originalMode?targetBoxes>0 && board.BoxesCleared>=targetBoxes:board.Collected[0]>=targetRed && board.Collected[1]>=targetBlue && board.RemainingIce==0; } }
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
                if(!verificationRun && !originalMode) { stars+=award; unlocked++; Save(); }
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
            Text(new Rect(811,382,275,57),unlocked==1?"Collect red and blue jewels\nto earn your first stars.":"Collect jewels and clear the ice.\nA new challenge awaits.",18,pale,TextAnchor.MiddleCenter);
            if(Button(new Rect(812,469,275,65),"PLAY",new Color(.25f,.69f,.37f),27)) StartLevel();
            if(Button(new Rect(812,540,275,43),"ORIGINAL LEVEL 1",new Color(.29f,.37f,.59f),17)) StartOriginal();
            Text(new Rect(805,591,285,35),"STARS  "+stars,23,gold,TextAnchor.MiddleCenter,true);
            string[] tasks={"Plant the royal trees","Add the flower beds","Restore the fountain","Garden complete"};
            Text(new Rect(805,631,285,30),tasks[Mathf.Min(built,3)],16,pale,TextAnchor.MiddleCenter);
            if(Button(new Rect(812,672,275,40),built>=3?"RESTORED":"RESTORE  /  1 STAR",new Color(.29f,.37f,.59f),17,stars>0 && built<3))
            { stars--; built++; Save(); Sound(6); Notify("A little more magic in your kingdom."); }
            Text(new Rect(83,757,800,32),"LOCAL SAVE     •     NO ACCOUNT     •     NO INTERNET REQUIRED",13,new Color(.61f,.74f,.84f));
            if(Button(new Rect(978,65,140,43),muted?"SOUND OFF":"SOUND ON",new Color(.13f,.24f,.39f),14)) { muted=!muted; Save(); }
            Text(new Rect(80,805,1040,25),"Offline reconstruction • Recovered item art and audio • Original level 1 available.",12,new Color(.53f,.67f,.77f));
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
            Text(new Rect(239,91,550,25),originalMode?"ORIGINAL LEVEL 1   /   "+board.Width+" x "+board.Height:"LEVEL "+level+"   /   THE JEWEL COURT",14,gold,TextAnchor.MiddleLeft,true);
            if(Button(new Rect(929,64,145,43),muted?"SOUND OFF":"SOUND ON",new Color(.12f,.23f,.38f),14)) { muted=!muted; Save(); }
            Box(new Rect(91,151,292,610),navy);
            Text(new Rect(115,176,244,26),"MOVES LEFT",14,pale,TextAnchor.MiddleCenter,true);
            Text(new Rect(115,209,244,81),moves.ToString(),65,moves<=5?colors[0]:Color.white,TextAnchor.MiddleCenter,true);
            Text(new Rect(115,315,244,30),"COLLECT TO WIN",15,gold,TextAnchor.MiddleCenter,true);
            if(originalMode)
            {
                Box(new Rect(113,363,248,72),new Color(.10f,.18f,.34f));
                DrawBox(new Rect(118,370,60,60),1);
                Text(new Rect(183,370,165,54),Mathf.Min(board.BoxesCleared,targetBoxes)+" / "+targetBoxes,24,Color.white,TextAnchor.MiddleLeft,true);
                Text(new Rect(116,452,242,54),"Match beside boxes\nto break them.",17,pale,TextAnchor.MiddleCenter);
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
                else if(board.Walls[x,y]>0) DrawWall(rect,board.Walls[x,y],board.WallGroup[x,y]);
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
        void DrawBox(Rect rect,int layers)
        {
            if(boxArt!=null) GUI.DrawTexture(rect,boxArt,ScaleMode.ScaleToFit,true);
            else Box(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),new Color(.70f,.39f,.12f));
            if(layers>1) Text(rect,layers.ToString(),18,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawWall(Rect rect,int layers,int group)
        {
            int palette=((group%3)+3)%3;
            Color stone=palette==1?new Color(.42f,.47f,.56f):palette==2?new Color(.30f,.36f,.45f):new Color(.51f,.40f,.32f);
            Box(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),stone);
            for(int i=1;i<4;i++) Solid(new Rect(rect.x+7,rect.y+rect.height*i/4f,rect.width-14,2),new Color(.12f,.15f,.21f,.6f));
            if(layers>1) Text(rect,layers.ToString(),18,Color.white,TextAnchor.MiddleCenter,true);
        }
        void DrawResult()
        {
            Solid(new Rect(0,0,W,H),new Color(.01f,.03f,.10f,.78f));
            Box(new Rect(365,193,470,461),new Color(.13f,.23f,.40f));
            Text(new Rect(395,224,410,60),won?"Splendid!":"One more try?",43,Color.white,TextAnchor.MiddleCenter,true);
            int earned=1+(moves>=5?1:0)+(moves>=12?1:0);
            for(int i=0;i<3;i++) { GUI.color=won && i<earned?gold:new Color(.30f,.37f,.47f); GUI.DrawTexture(new Rect(460+i*99,303,82,82),star); } GUI.color=Color.white;
            Text(new Rect(400,404,400,60),won?(originalMode?"Original level 1 complete!":"Level complete. Your garden awaits!"):"Collect every target before\nyou run out of moves.",20,pale,TextAnchor.MiddleCenter);
            Text(new Rect(400,470,400,32),"SCORE  "+board.Score.ToString("N0"),22,gold,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(420,529,360,59),won?"CONTINUE":"TRY AGAIN",new Color(.25f,.69f,.37f),23)) { if(won && originalMode) { home=true; won=lost=false; } else RetryCurrent(); }
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
