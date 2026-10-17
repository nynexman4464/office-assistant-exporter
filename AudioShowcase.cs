using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FrontierUtopia;

partial class WillActorExporter
{
    class SoundCue { public int Id, Pc; public long Ms; }
    class Trace {
        public List<Frame> Frames = new List<Frame>();
        public List<SoundCue> Sounds = new List<SoundCue>();
        public List<int> Path = new List<int>();
        public int NextPc, Cycles, ForcedStep=-1, ForcedFrom=-1, ForcedTo=-1, OutroFrame=-1; public long Ms; public int Loop = -1; public string Outcome = "complete";
    }
    static void Emit(Trace t, ActionInfo a, int pc) {
        t.Path.Add(pc); Op o=a.Ops[pc];
        if(o.Code==0 && o.A!=65535) { int ms=Math.Max(1,o.B); t.Frames.Add(new Frame(o.A,ms)); t.Ms+=ms; }
        // 0xFFFF is not a WAV ID: the installed engine rejects it without
        // interrupting playback. Keep it in the instruction trace as a no-op.
        if(o.Code==2 && o.A!=65535) t.Sounds.Add(new SoundCue{Id=o.A,Pc=pc,Ms=t.Ms});
    }
    static Trace Walk(ActionInfo a,int maxMs,int maxSteps) {
        var t=new Trace(); var seen=new Dictionary<int,int>(); var random=new Random(12345+a.Id);
        int pc=0,steps=0;
        while(pc>=0 && pc<a.Ops.Count) {
            if(a.RandomCycle && steps++>=maxSteps) {t.Outcome="step_cap";break;}
            if(a.HasCycle && !a.RandomCycle && seen.ContainsKey(pc)) {t.Loop=seen[pc];t.Outcome="deterministic_loop";break;}
            if(!seen.ContainsKey(pc))seen[pc]=t.Frames.Count;
            Op o=a.Ops[pc];
            if(o.Code==0 && o.A!=65535 && a.RandomCycle && t.Ms+Math.Max(1,o.B)>maxMs) {t.Outcome="time_cap";break;}
            Emit(t,a,pc); pc=o.Code==1?(random.Next(32767)<o.B?o.A:pc+1):pc+1;
        }
        t.NextPc=pc;return t;
    }
    static List<int> Route(ActionInfo a,int from,int to) {
        var previous=new Dictionary<int,int>();var queue=new Queue<int>();queue.Enqueue(from);previous[from]=-1;
        while(queue.Count>0) {
            int p=queue.Dequeue(); if(p==to) {
                var path=new List<int>(); for(int q=to;q!=-1;q=previous[q])path.Add(q);path.Reverse();return path;
            }
            foreach(int q in Next(a.Ops,p))if(!previous.ContainsKey(q)){previous[q]=p;queue.Enqueue(q);}
        }
        return null;
    }
    static string Edge(int p,int q) {return p+":"+q;}
    // Cover every reachable edge in numeric instruction order. BFS connects the
    // current instruction to the next uncovered edge without inventing transitions.
    // For Will's strongly connected idle graph this produces one continuous walk.
    static Trace Showcase(ActionInfo a,string output) {
        bool[] reachable=Reachable(a.Ops);var done=new HashSet<string>();var t=new Trace(); int current=0;
        var edges=new List<Tuple<int,int>>();var unique=new HashSet<string>();
        for(int p=0;p<a.Ops.Count;p++)if(reachable[p])foreach(int q in Next(a.Ops,p))if(unique.Add(Edge(p,q)))edges.Add(Tuple.Create(p,q));
        edges.Sort(delegate(Tuple<int,int> x,Tuple<int,int> y){int c=x.Item1.CompareTo(y.Item1);return c!=0?c:x.Item2.CompareTo(y.Item2);});
        foreach(var edge in edges) {
            if(done.Contains(Edge(edge.Item1,edge.Item2)))continue;
            var route=Route(a,current,edge.Item1);
            if(route==null)throw new InvalidOperationException("Idle graph requires separate clips; no valid connection to instruction "+edge.Item1);
            for(int i=0;i<route.Count-1;i++) {Emit(t,a,route[i]);done.Add(Edge(route[i],route[i+1]));}
            Emit(t,a,edge.Item1);done.Add(Edge(edge.Item1,edge.Item2)); current=edge.Item2;
            if(t.Path.Count>1000000)throw new InvalidOperationException("Showcase safety limit exceeded");
        }
        // Close at the first recurrent instruction; entry 0 may be intro-only.
        int anchor=a.LoopEntry>=0?a.LoopEntry:0; var close=Route(a,current,anchor);
        bool naturallyEnded=false;
        if(close==null) {
            var finish=Ending(a,current,new bool[a.Ops.Count]);
            if(finish==null)throw new InvalidOperationException("Idle showcase cannot return to its loop anchor or reach an end");
            for(int i=0;i<finish.Count;i++){Emit(t,a,finish[i]);if(i+1<finish.Count)done.Add(Edge(finish[i],finish[i+1]));}
            naturallyEnded=true;
        } else for(int i=0;i<close.Count-1;i++){Emit(t,a,close[i]);done.Add(Edge(close[i],close[i+1]));}
        int frame=0; t.Loop=-1;
        foreach(int pc in t.Path) {if(pc==anchor && t.Loop<0)t.Loop=frame;if(a.Ops[pc].Code==0 && a.Ops[pc].A!=65535)frame++;}
        t.NextPc=anchor;t.Outcome=naturallyEnded?"ordered_showcase_complete":"ordered_showcase";if(naturallyEnded)t.Loop=-1;else if(ViewingCycles>0)Finish(t,a,anchor);
        string folder=Path.Combine(output,Folder(a.Id)+"_showcase");Directory.CreateDirectory(folder);
        var visited=new HashSet<int>(t.Path);
        using(var f=new StreamWriter(Path.Combine(folder,"coverage.csv"))) {
            f.WriteLine("instruction,opcode,operand_a,operand_b,reachable,visited");
            for(int p=0;p<a.Ops.Count;p++) f.WriteLine(string.Join(",",p,a.Ops[p].Code,a.Ops[p].A,a.Ops[p].B,CsvBool(reachable[p]),CsvBool(visited.Contains(p))));
        }
        using(var f=new StreamWriter(Path.Combine(folder,"edges.csv"))) {
            f.WriteLine("from_instruction,to_instruction,covered");foreach(var e in edges)f.WriteLine(e.Item1+","+e.Item2+","+CsvBool(done.Contains(Edge(e.Item1,e.Item2))));
        }
        File.WriteAllText(Path.Combine(folder,"showcase.txt"),"Fixed numeric edge order; shortest valid connecting paths; not original random playback."+Environment.NewLine+"covered_edges="+done.Count+"/"+edges.Count+Environment.NewLine);
        File.WriteAllText(Path.Combine(output,"showcase_summary.csv"),"action_id,action_name,folder,mp4,frames,duration_ms,sound_triggers,covered_edges,total_edges"+Environment.NewLine+"1,actionIdle,"+Folder(a.Id)+"_showcase,"+Folder(a.Id)+"_showcase.mp4,"+t.Frames.Count+","+t.Ms+","+t.Sounds.Count+","+done.Count+","+edges.Count+Environment.NewLine);
        Console.WriteLine("Idle showcase: "+visited.Count+" instructions; "+done.Count+"/"+edges.Count+" edges; "+t.Ms+" ms");return t;
    }
    static void ExtractSounds(ActEngine e,string output) {
        string dir=Path.Combine(output,"sounds");Directory.CreateDirectory(dir);
        byte[] bytes=(byte[])Field(e,"_soundsBytes");int[] offsets=(int[])Field(e,"_soundOffsets");if(bytes==null || offsets==null){File.WriteAllText(Path.Combine(dir,"sounds.csv"),"sound_id,bytes,file"+Environment.NewLine);return;}
        using(var csv=new StreamWriter(Path.Combine(dir,"sounds.csv"))) {
            csv.WriteLine("sound_id,bytes,file");
            for(int i=0;i<offsets.Length-1;i++) {
                int start=offsets[i],length=offsets[i+1]-start;
                if(start<0 || length<=0 || (long)start+length>bytes.Length)throw new InvalidDataException("Invalid sound offset "+i);
                if(length<12 || Encoding.ASCII.GetString(bytes,start,4)!="RIFF" || Encoding.ASCII.GetString(bytes,start+8,4)!="WAVE")throw new InvalidDataException("Sound is not WAV: "+i);
                byte[] wav=new byte[length];Buffer.BlockCopy(bytes,start,wav,0,length);
                string name="sound_"+i.ToString("D3")+".wav";File.WriteAllBytes(Path.Combine(dir,name),wav);csv.WriteLine(i+","+length+","+name);
            }
        }
    }
    static void WriteTrace(Trace t,string dir) {
        PlaybackReport(t,dir);
        using(var f=new StreamWriter(Path.Combine(dir,"sound_events.csv"))) {
            f.WriteLine("event_index,instruction,start_ms,sound_id,file");
            for(int i=0;i<t.Sounds.Count;i++){var s=t.Sounds[i];f.WriteLine(i+","+s.Pc+","+s.Ms+","+s.Id+",../sounds/sound_"+s.Id.ToString("D3")+".wav");}
        }
        using(var f=new StreamWriter(Path.Combine(dir,"instructions.csv"))) {f.WriteLine("step,instruction");for(int i=0;i<t.Path.Count;i++)f.WriteLine(i+","+t.Path[i]);}
    }
    static void Check(bool ok,string message) {if(!ok)throw new Exception("Self-test failed: "+message);}
    static int SelfTests() {
        var finite=new List<Op>();for(int i=0;i<15001;i++)finite.Add(new Op{Code=0,A=1,B=1});
        var t=Walk(Analyze(500,finite),1,10);Check(t.Frames.Count==15001 && t.Outcome=="complete","finite paths must not be capped");
        var cycle=new List<Op>{new Op{Code=0,A=1,B=100},new Op{Code=2,A=3},new Op{Code=0,A=2,B=200},new Op{Code=1,A=1,B=32768}};
        t=Walk(Analyze(500,cycle),1,1);Check(t.Frames.Count==2 && t.Loop==1 && t.Ms==300 && t.Sounds.Count==1 && t.Sounds[0].Ms==100,"intro, cycle and sound alignment");
        var zero=new List<Op>{new Op{Code=1,A=0,B=16384},new Op{Code=1,A=0,B=32768}};
        t=Walk(Analyze(500,zero),1000,100);Check(t.Outcome=="step_cap" && t.Frames.Count==0,"zero-time random loop safety");
        var random=Analyze(1,new List<Op>{new Op{Code=0,A=1,B=100},new Op{Code=1,A=0,B=16384},new Op{Code=1,A=0,B=32768}});
        var x=Walk(random,1000,1000);var y=Walk(random,1000,1000);Check(x.Ms<=1000 && string.Join(",",x.Path)==string.Join(",",y.Path),"random bounds and repeatability");
        var sentinel=Walk(Analyze(777,new List<Op>{new Op{Code=2,A=65535},new Op{Code=0,A=1,B=100}}),1000,100);
        Check(sentinel.Sounds.Count==0 && sentinel.Path.Count==2,"no-sound marker is a traced no-op");
        ViewingTests();ArtworkScaleTests();
        Console.WriteLine("PASS: three cycles and controlled ending; finite paths, deterministic intro/cycle, sound alignment, random bounds/repeatability, zero-time loop safety.");return 0;
    }
}
