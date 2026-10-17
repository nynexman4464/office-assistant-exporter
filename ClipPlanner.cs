using System;
using System.Collections.Generic;
using System.IO;

partial class WillActorExporter
{
    static int ViewingCycles=3;
    static void WriteOps(ActionInfo a,string dir) {
        using(var f=new StreamWriter(Path.Combine(dir,"opcodes.csv"))) {
            f.WriteLine("instruction,opcode,operand_a,operand_b");for(int p=0;p<a.Ops.Count;p++)f.WriteLine(p+","+a.Ops[p].Code+","+a.Ops[p].A+","+a.Ops[p].B);
        }
    }
    static List<int> Ending(ActionInfo a,int from,bool[] normal) {
        if(from<0 || from>=a.Ops.Count || normal[from])return null;
        var queue=new Queue<int>();var prev=new Dictionary<int,int>();queue.Enqueue(from);prev[from]=-1;
        while(queue.Count>0) {
            int p=queue.Dequeue();Op o=a.Ops[p];
            bool terminal=o.Code==1 ? ((o.B>0 && o.A>=a.Ops.Count) || (o.B<32767 && p+1>=a.Ops.Count)) : p+1>=a.Ops.Count;
            if(terminal) {var path=new List<int>();for(int q=p;q!=-1;q=prev[q])path.Add(q);path.Reverse();return path;}
            foreach(int q in Next(a.Ops,p))if(!normal[q] && !prev.ContainsKey(q)){prev[q]=p;queue.Enqueue(q);}
        }
        return null;
    }
    static bool Finish(Trace t,ActionInfo a,int current) {
        bool[] normal=Reachable(a.Ops);List<int> prefix=null,tail=null;int source=-1,target=-1;
        // Prefer explicit zero-threshold exits; allow a loop latch fallthrough
        // only when it has a separate finite tail. Never select arbitrary poses.
        for(int mode=0;mode<2 && prefix==null;mode++)for(int p=0;p<a.Ops.Count;p++) {
            Op o=a.Ops[p];if(o.Code!=1 || !normal[p])continue;
            // Prefer explicit zero-threshold exit targets. Otherwise suppress a
            // loop back-jump only when its fallthrough is a separate finite tail.
            if(mode==0 && o.B!=0)continue;
            if(mode==1 && (o.B<32767 || o.A>p))continue;
            int destination=mode==0?o.A:p+1;
            var candidate=Ending(a,destination,normal);if(candidate==null)continue;
            var connect=Route(a,current,p);if(connect==null)continue;
            if(prefix==null || connect.Count<prefix.Count) {prefix=connect;tail=candidate;source=p;target=destination;}
        }
        if(prefix==null)return false;
        for(int i=0;i<prefix.Count-1;i++)Emit(t,a,prefix[i]);
        Emit(t,a,source);t.ForcedStep=t.Path.Count-1;t.ForcedFrom=source;t.ForcedTo=target;t.OutroFrame=t.Frames.Count;
        foreach(int p in tail)Emit(t,a,p);
        t.Outcome="controlled_ending";return true;
    }
    static Trace Viewing(ActionInfo a,int maxMs,int maxSteps) {
        Trace t=Walk(a,maxMs,maxSteps);List<int> cycle=null;
        if(ViewingCycles==0 || t.Outcome=="complete")return t;
        // Random cycles may have an ordinary reachable ending. A time budget
        // should end sampling, not freeze the actor midway through a transition.
        if(a.RandomCycle && a.MayExit) {
            var natural=Ending(a,t.NextPc,new bool[a.Ops.Count]);
            if(natural!=null) {
                t.OutroFrame=t.Frames.Count;
                foreach(int p in natural)Emit(t,a,p);
                t.Outcome="natural_ending_after_sample";return t;
            }
        }
        if(!a.RandomCycle && t.Outcome=="deterministic_loop") {
            int start=t.Path.IndexOf(t.NextPc);if(start<0)throw new InvalidDataException("Cannot identify cycle start");
            cycle=t.Path.GetRange(start,t.Path.Count-start);
            var intro=t.Path.GetRange(0,start);int loop=t.Loop,entry=t.NextPc;
            t=new Trace{Loop=loop,NextPc=entry,Cycles=ViewingCycles,Outcome="deterministic_loop"};
            foreach(int p in intro)Emit(t,a,p);
            // The final pass takes the exit checkpoint rather than jumping back.
            for(int i=0;i<ViewingCycles-1;i++)foreach(int p in cycle)Emit(t,a,p);
        }
        if(!Finish(t,a,t.NextPc)) {if(cycle!=null)foreach(int p in cycle)Emit(t,a,p);t.Outcome+="_no_ending_found";}
        return t;
    }
    static void PlaybackReport(Trace t,string dir) {
        File.WriteAllText(Path.Combine(dir,"playback.csv"),"outcome,requested_cycle_passes,loop_start_frame,outro_start_frame,forced_step,forced_from_instruction,forced_to_instruction"+Environment.NewLine+
            string.Join(",",t.Outcome,t.Cycles,t.Loop,t.OutroFrame,t.ForcedStep,t.ForcedFrom,t.ForcedTo)+Environment.NewLine);
    }
    static void ViewingTests() {
        int saved=ViewingCycles;ViewingCycles=3;
        var a=Analyze(999,new List<Op>{new Op{Code=0,A=1,B=100},new Op{Code=0,A=2,B=200},new Op{Code=1,A=4,B=0},new Op{Code=1,A=1,B=32768},new Op{Code=2,A=5},new Op{Code=0,A=3,B=300}});
        var t=Viewing(a,1,1);Check(t.Cycles==3 && t.Outcome=="controlled_ending" && t.ForcedFrom==2 && t.ForcedTo==4,"three cycles and explicit ending");
        Check(t.Ms==1000 && t.Frames.Count==5 && t.Sounds.Count==1 && t.Sounds[0].Ms==700,"ending timing and final cycle exit checkpoint");
        ViewingCycles=0;var raw=Viewing(a,1,1);Check(raw.Ms==300 && raw.Outcome=="deterministic_loop","raw mode retains one exact cycle");
        ViewingCycles=3;
        var latch=Analyze(998,new List<Op>{new Op{Code=0,A=1,B=100},new Op{Code=1,A=0,B=32768},new Op{Code=0,A=2,B=200}});
        var ended=Viewing(latch,1,1);Check(ended.ForcedFrom==1 && ended.ForcedTo==2 && ended.Ms==500,"loop latch fallthrough into finite ending");
        var probabilistic=Analyze(997,new List<Op>{new Op{Code=0,A=1,B=100},new Op{Code=1,A=0,B=16384},new Op{Code=0,A=2,B=200}});
        var returned=Viewing(probabilistic,1,100);Check(returned.Outcome=="natural_ending_after_sample" && returned.Frames.Count==2 && returned.Frames[1].Pose==2 && returned.ForcedStep==-1,"random sample follows legal reachable return to neutral");
        ViewingCycles=saved;
    }
}
