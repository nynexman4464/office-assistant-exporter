using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using FrontierUtopia;

partial class WillActorExporter
{
    static readonly Dictionary<int,string> Names = new Dictionary<int,string> {
        {1, "idle"},
        {2, "greetingShow"},
        {3, "goodbyeHide"},
        {4, "beginSpeaking"},
        {5, "clickedOn"},
        {6, "characterSuccessMajor"},
        {7, "characterSuccessMinor"},
        {8, "characterFailsMajor"},
        {9, "characterFailsMinor"},
        {10, "acknowledgingACancel"},
        {11, "getAttentionMajor"},
        {12, "getAttentionMinor"},
        {13, "searching"},
        {14, "found"},
        {15, "errorFoundComputer"},
        {16, "errorFoundHuman"},
        {17, "humanSuccess"},
        {18, "printing"},
        {19, "gestureToCharactersRight"},
        {20, "areYouSure"},
        {21, "birthdayCelebration"},
        {22, "writingNotingSomething"},
        {23, "workingAtSomething"},
        {24, "thinking"},
        {25, "sendingMail"},
        {26, "listensToComputer"},
        {31, "disappear"},
        {32, "appear"},
        {100, "getArtsy"},
        {101, "processing"},
        {102, "congratulate"},
        {103, "checkingSomething"},
        {104, "lookDown"},
        {105, "lookDownRight"},
        {106, "lookDownLeft"},
        {107, "lookLeft"},
        {108, "lookRight"},
        {109, "lookUp"},
        {110, "lookUpLeft"},
        {111, "lookUpRight"},
        {112, "save"},
        {113, "gestureDown"},
        {114, "gestureLeft"},
        {115, "gestureUp"},
        {116, "emptyTrash"},
    };
    static string ActionName(int id) {
        string name;
        if(!Names.TryGetValue(id,out name))return "unknown";
        return name;
    }
    static string Folder(int id) { return id.ToString("D3")+"_"+ActionName(id); }
    static string Kind(ActionInfo a) { return !a.HasCycle?"finite":a.RandomCycle?(a.MayExit?"random_possible_loop":"random_no_exit"):"deterministic_loop"; }
    class Op { public int Code, A, B; }
    class ActionInfo { public int Id, Count, Branches, LoopEntry = -1, LoopPoseIndex = -1; public bool HasCycle, MayExit, RandomCycle; public List<Op> Ops; }
    class Frame { public int Pose, Ms; public Frame(int p, int d) { Pose=p; Ms=d; } }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(obj); }
    static int NumField(object obj, string name) { return Convert.ToInt32(Field(obj,name)); }
    static string CsvBool(bool x) { return x ? "yes" : "no"; }
    static string Quote(string path) { return "'" + path.Replace("'", "'\\''") + "'"; }
    static int U16(byte[] b,int i) { return b[i] | b[i+1]<<8; }
    static List<Op> ReadOps(ActEngine engine, int action)
    {
        // Read the parsed ACT directory from the original Frontier Actor engine.
        // This is intentionally isolated: private fields may change in future DLL releases.
        object dictionary = Field(engine,"_actionInfos");
        object info = ((IDictionary)dictionary)[action];
        if (info == null || NumField(info,"Defs") < 1) return new List<Op>();
        int index=NumField(info,"FirstPtrIndex");
        object ptr = ((IList)Field(engine,"_actionPtrs"))[index];
        int offset=NumField(ptr,"Offset"), size=NumField(ptr,"Size");
        byte[] bytes=(byte[])Field(engine,"_animsBytes");
        if (offset<0 || size<4 || offset+size>bytes.Length) return new List<Op>();
        int declared=U16(bytes,offset+2);
        int count=Math.Min(declared,(size-4)/6);
        var ops=new List<Op>();
        for(int i=0;i<count;i++) { int at=offset+4+i*6; ops.Add(new Op{Code=U16(bytes,at),A=U16(bytes,at+2),B=U16(bytes,at+4)}); }
        return ops;
    }
    static IEnumerable<int> Next(List<Op> ops,int pc)
    {
        Op o=ops[pc];
        if(o.Code==1) {
            if(o.B>0 && o.A>=0 && o.A<ops.Count) yield return o.A;
            if(o.B<32767 && pc+1<ops.Count) yield return pc+1;
        } else if(pc+1<ops.Count) yield return pc+1;
    }
    static bool[] Reachable(List<Op> ops)
    {
        bool[] seen=new bool[ops.Count];
        var todo=new Stack<int>(); if(ops.Count>0) todo.Push(0);
        while(todo.Count>0){ int p=todo.Pop(); if(p<0||p>=ops.Count||seen[p])continue; seen[p]=true; foreach(int q in Next(ops,p))todo.Push(q); }
        return seen;
    }
    static ActionInfo Analyze(int id,List<Op> ops)
    {
        var info=new ActionInfo{Id=id,Ops=ops,Count=ops.Count};
        bool[] reachable=Reachable(ops);
        int[] color=new int[ops.Count];
        // DFS back edges detect reachable cycles (also handles random branches).
        for(int i=0;i<ops.Count;i++) if(reachable[i]) {
            if(ops[i].Code==1 && ops[i].B>0 && ops[i].B<32767)info.Branches++;
        }
        for(int i=0;i<ops.Count;i++) if(reachable[i] && color[i]==0) Detect(i,ops,reachable,color,info);
        // Is a finite end reachable from entry? Out-of-range PC means terminal.
        for(int i=0;i<ops.Count;i++) if(reachable[i]) {
            Op o=ops[i];
            if(o.Code==1) { if(o.B>0 && (o.A<0||o.A>=ops.Count))info.MayExit=true; if(o.B<32767 && i+1>=ops.Count)info.MayExit=true; }
            else if(i+1>=ops.Count)info.MayExit=true;
        }
        info.RandomCycle=info.HasCycle && info.Branches>0;
        return info;
    }
    static void Detect(int start,List<Op> ops,bool[] reachable,int[] color,ActionInfo info)
    {
        // Iterative DFS avoids stack overflows for large animations.
        var stack=new Stack<Tuple<int,IEnumerator<int>>>();
        color[start]=1; stack.Push(Tuple.Create(start,Next(ops,start).GetEnumerator()));
        while(stack.Count>0) {
            var top=stack.Peek();
            if(!top.Item2.MoveNext()) { top.Item2.Dispose(); color[top.Item1]=2; stack.Pop(); continue; }
            int q=top.Item2.Current;
            if(q<0||q>=ops.Count||!reachable[q])continue;
            if(color[q]==1) { info.HasCycle=true; if(info.LoopEntry<0)info.LoopEntry=q; }
            if(color[q]==0) {color[q]=1; stack.Push(Tuple.Create(q,Next(ops,q).GetEnumerator()));}
        }
    }
    static List<Frame> Traverse(ActionInfo info,int maxMs,int maxSteps,out int cycleFrame,out string outcome)
    {
        Trace trace=Viewing(info,maxMs,maxSteps);cycleFrame=trace.Loop;outcome=trace.Outcome;return trace.Frames;
    }
    static void Render(ActEngine actor,ActionInfo a,string output,int scale,int maxMs,int maxSteps,bool force,Trace supplied=null,string suffix="")
    {
        string dir=Path.Combine(output,Folder(a.Id)+suffix);
        string complete=Path.Combine(dir,"export.complete");
        string signature="auto-scale-v10;bg="+BackdropKey+";cycles="+ViewingCycles+";scale="+scale+";maxMs="+maxMs+";maxSteps="+maxSteps;
        if(!force&&File.Exists(complete)&&File.ReadAllText(complete)==signature) {Console.WriteLine("Action "+a.Id+": skipped (already complete)");return;}
        Directory.CreateDirectory(dir);
        if(File.Exists(complete))File.Delete(complete);
        int loopFrame;string result;
        Trace trace=supplied??Viewing(a,maxMs,maxSteps);
        loopFrame=trace.Loop;result=trace.Outcome;List<Frame> frames=trace.Frames;WriteTrace(trace,dir);WriteOps(a,dir);File.WriteAllText(Path.Combine(dir,"background.txt"),BackdropDescription+Environment.NewLine);
        long current=0;
        var durations=new List<int>();var filenames=new List<string>();
        using(var csv=new StreamWriter(Path.Combine(dir,"frames.csv"),false,new UTF8Encoding(false))) {
            csv.WriteLine("frame_index,pose_id,start_ms,duration_ms,file");
            for(int i=0;i<frames.Count;i++) {
                Frame f=frames[i];string name="frame_"+i.ToString("D4")+".png";
                string path=Path.Combine(dir,name);
                using(var bmp=new Bitmap(actor.OriginalWidth*scale,actor.OriginalHeight*scale,PixelFormat.Format32bppArgb))
                using(var g=Graphics.FromImage(bmp)) {
                    g.Clear(BackdropColor);g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality=CompositingQuality.HighQuality;
                    DrawBackdrop(g,bmp.Width,bmp.Height);
                    actor.DrawPoseInto(g,f.Pose,new Rectangle(0,0,bmp.Width,bmp.Height));
                    bmp.Save(path,ImageFormat.Png);
                }
                csv.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4}",i,f.Pose,current,f.Ms,name));
                filenames.Add(name);durations.Add(f.Ms);current+=f.Ms;
            }
        }
        if(filenames.Count>0)using(var ff=new StreamWriter(Path.Combine(dir,"animation.ffconcat"),false,new UTF8Encoding(false))) {
            ff.WriteLine("ffconcat version 1.0");
            for(int i=0;i<filenames.Count;i++) {ff.WriteLine("file "+Quote(filenames[i]));ff.WriteLine("option framerate 1000");ff.WriteLine("duration "+(durations[i]/1000.0).ToString("0.000",CultureInfo.InvariantCulture));}
            ff.WriteLine("file "+Quote(filenames[filenames.Count-1]));ff.WriteLine("option framerate 1000");
        }
        File.WriteAllText(Path.Combine(dir,"loop.txt"),"classification="+result+Environment.NewLine+"loop_start_frame="+loopFrame+Environment.NewLine+"frames="+frames.Count+Environment.NewLine+"duration_ms="+current+Environment.NewLine);
        File.WriteAllText(complete,signature);
        Console.WriteLine("Action "+a.Id+": "+frames.Count+" frames, "+current+" ms ["+result+(loopFrame>=0?", loop frame "+loopFrame:"")+"]");
    }
    static int Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--self-test")return SelfTests();
        if(args.Length<1) {Console.WriteLine("Usage: WillActorExporter.exe <ACT> [output=WillOutput] [scale=4] [analyze|export] [maxLoopSeconds=10] [force=true|false]");return 2;}
        string act=Path.GetFullPath(args[0]); if(!File.Exists(act)){Console.Error.WriteLine("ACT not found: "+act);return 2;}
        string output=Path.GetFullPath(args.Length>1?args[1]:"WillOutput");
        int scale=4;if(args.Length>2&&(!int.TryParse(args[2],out scale)||scale<1||scale>12))return 2;
        string mode=args.Length>3?args[3].ToLowerInvariant():"analyze";
        int seconds=10; if(args.Length>4&&(!int.TryParse(args[4],out seconds)||seconds<1||seconds>600))return 2;
        bool force=args.Length>5&&args[5].Equals("true",StringComparison.OrdinalIgnoreCase);
        if(args.Length>7 && (!int.TryParse(args[7],out ViewingCycles) || ViewingCycles<0 || ViewingCycles>10))return 2;
        try{InitBackdrop(args.Length>8?args[8]:"Transparent",args.Length>9 && args[9]!="-"?args[9]:"");}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 2;}
        Directory.CreateDirectory(output);
        using(var engine=new ActEngine()) {
            engine.Load(act);engine.AutoIdleEnabled=false;engine.MouthOverlayEnabled=false;
            scale=ApplyArtworkScale(engine,output,scale,args.Length>10 && args[10].Equals("true",StringComparison.OrdinalIgnoreCase));
            int[] ids=engine.GetActions();
            Console.WriteLine("Character: "+engine.CharacterName+", "+ids.Length+" actions, "+(engine.OriginalWidth*scale)+"x"+(engine.OriginalHeight*scale));
            var infos=new List<ActionInfo>();
            foreach(int id in ids) infos.Add(Analyze(id,ReadOps(engine,id)));
            using(var f=new StreamWriter(Path.Combine(output,"analysis.csv"),false,new UTF8Encoding(false))) {
                f.WriteLine("action_id,action_name,instructions,random_branches,has_cycle,can_reach_end,loop_instruction,classification");
                foreach(var a in infos) {
                    string kind=Kind(a);
                    f.WriteLine(string.Join(",",a.Id,ActionName(a.Id),a.Count,a.Branches,CsvBool(a.HasCycle),CsvBool(a.MayExit),a.LoopEntry,kind));
                    Console.WriteLine("Action "+a.Id+": "+kind+"; "+a.Count+" instructions; "+a.Branches+" random branches"+(a.HasCycle?"; cycle at instruction "+a.LoopEntry:""));
                }
            }
            Console.WriteLine("Analysis saved to "+Path.Combine(output,"analysis.csv"));
            using(var f=new StreamWriter(Path.Combine(output,"actions.csv"),false,new UTF8Encoding(false))) {
                f.WriteLine("action_id,action_name,classification,folder,webp,mp4,frames,duration_ms,loop_start_frame,outcome");
                foreach(var a in infos) {
                    int loop; string outcome; var frames=Traverse(a,seconds*1000,1000000,out loop,out outcome);
                    long duration=0; foreach(var frame in frames)duration+=frame.Ms;
                    f.WriteLine(string.Join(",",a.Id,ActionName(a.Id),Kind(a),Folder(a.Id),Folder(a.Id)+".webp",Folder(a.Id)+".mp4",frames.Count,duration,loop,outcome));
                }
            }
            if(mode=="analyze") {Console.WriteLine("Analysis only — no frames rendered. Run with -Mode export to render.");return 0;}
            if(mode!="export") {Console.Error.WriteLine("Unknown mode: "+mode);return 2;}
            ExtractSounds(engine,output);
            // Only random cyclic walks need bounds. Finite paths and deterministic cycles are exact.
            foreach(var a in infos) Render(engine,a,output,scale,seconds*1000,1000000,force);
            if(args.Length>6 && args[6]=="showcase") { foreach(var a in infos)if(a.Id==1){try{Render(engine,a,output,scale,seconds*1000,1000000,force,Showcase(a,output),"_showcase");}catch(InvalidOperationException ex){Console.WriteLine("Showcase unavailable: "+ex.Message);File.WriteAllText(Path.Combine(output,"showcase-build.txt"),"Source graph needs separate action paths; the exporter assembles them into one idle showcase. "+ex.Message);RenderVariants(engine,a,output,scale,seconds*1000,1000000,force);AssembleIdle(engine,a,output,scale,force);}} }
        }
        return 0;
    }
}
