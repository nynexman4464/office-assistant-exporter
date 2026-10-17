using System;
using System.Collections.Generic;
using System.IO;
using FrontierUtopia;

partial class WillActorExporter
{
    // A graph with one-way forks cannot be covered by a continuous tour.
    // Export independent entry-to-ending paths instead of inventing connections.
    static void RenderVariants(ActEngine engine,ActionInfo a,string output,int scale,int maxMs,int maxSteps,bool force) {
        var covered=new HashSet<string>();var edges=new List<Tuple<int,int>>();var unique=new HashSet<string>();bool[] reachable=Reachable(a.Ops);
        for(int p=0;p<a.Ops.Count;p++)if(reachable[p])foreach(int q in Next(a.Ops,p))if(unique.Add(Edge(p,q)))edges.Add(Tuple.Create(p,q));
        edges.Sort(delegate(Tuple<int,int> x,Tuple<int,int> y){int c=x.Item1.CompareTo(y.Item1);return c==0?x.Item2.CompareTo(y.Item2):c;});
        int variant=0;
        using(var f=new StreamWriter(Path.Combine(output,"idle_variants.csv"))) {
            f.WriteLine("variant,folder,mp4,frames,duration_ms,outcome");
            foreach(var edge in edges) {
                if(covered.Contains(Edge(edge.Item1,edge.Item2)))continue;
                var prefix=Route(a,0,edge.Item1);if(prefix==null)throw new InvalidDataException("Unreachable variant edge");
                var t=new Trace();foreach(int p in prefix)Emit(t,a,p);
                int current=edge.Item2;var tail=Ending(a,current,new bool[a.Ops.Count]);
                if(tail!=null) {foreach(int p in tail)Emit(t,a,p);t.Outcome="idle_variant_complete";}
                else {
                    var seen=new HashSet<int>();int budget=a.Ops.Count+1;
                    while(current>=0 && current<a.Ops.Count && seen.Add(current) && budget-->0) {
                        Emit(t,a,current);int next=a.Ops.Count;foreach(int q in Next(a.Ops,current)){next=q;break;}current=next;
                    }
                    t.Outcome="idle_variant_bounded";if(current>=0 && current<a.Ops.Count)Finish(t,a,current);
                }
                for(int i=0;i<t.Path.Count-1;i++) {
                    int p=t.Path[i],q=t.Path[i+1];foreach(int next in Next(a.Ops,p))if(next==q && unique.Contains(Edge(p,q)))covered.Add(Edge(p,q));
                }
                string suffix="_variant_"+(++variant).ToString("D3");Render(engine,a,output,scale,maxMs,maxSteps,force,t,suffix);
                string folder=Folder(a.Id)+suffix;f.WriteLine(variant+","+folder+","+folder+".mp4,"+t.Frames.Count+","+t.Ms+","+t.Outcome);
            }
        }
        using(var f=new StreamWriter(Path.Combine(output,"idle_variant_coverage.csv"))) {
            f.WriteLine("from_instruction,to_instruction,covered");foreach(var e in edges)f.WriteLine(e.Item1+","+e.Item2+","+CsvBool(covered.Contains(Edge(e.Item1,e.Item2))));
        }
        if(covered.Count!=edges.Count)throw new InvalidDataException("Idle variant coverage incomplete");
        Console.WriteLine("Idle variants: "+variant+" separate valid clips; "+covered.Count+"/"+edges.Count+" edges");
    }
}
