using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FrontierUtopia;

partial class WillActorExporter
{
    static void AssembleIdle(ActEngine actor,ActionInfo a,string output,int scale,bool force) {
        string manifest=Path.Combine(output,"idle_variants.csv");if(!File.Exists(manifest))return;
        var variants=new List<string>();var key=new StringBuilder("assembled-idle-v1;rest=250;scale="+scale+";");
        foreach(string row in File.ReadAllLines(manifest)) {
            string[] c=row.Split(',');if(c[0]=="variant" || c.Length<6 || int.Parse(c[3],CultureInfo.InvariantCulture)==0)continue;
            variants.Add(c[1]);string source=Path.Combine(output,c[1]);
            key.Append(c[1]).Append(File.ReadAllText(Path.Combine(source,"export.complete"))).Append(File.ReadAllText(Path.Combine(source,"frames.csv"))).Append(File.ReadAllText(Path.Combine(source,"sound_events.csv")));
        }
        if(variants.Count==0)return;
        string fingerprint;using(var hash=SHA256.Create())fingerprint=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key.ToString())));
        string folder=Folder(a.Id)+"_showcase",dir=Path.Combine(output,folder),marker=Path.Combine(dir,"export.complete");
        if(!force && File.Exists(marker) && File.ReadAllText(marker)==fingerprint){Console.WriteLine("Assembled idle: reused complete output");return;}
        Directory.CreateDirectory(dir);if(File.Exists(marker))File.Delete(marker);
        int rest=actor.ImageShow;
        using(var bmp=new Bitmap(actor.OriginalWidth*scale,actor.OriginalHeight*scale,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bmp)) {
            g.Clear(BackdropColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.CompositingQuality=CompositingQuality.HighQuality;
            DrawBackdrop(g,bmp.Width,bmp.Height);actor.DrawPoseInto(g,rest,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(dir,"rest.png"),ImageFormat.Png);
        }
        long time=0;int frame=0,eventIndex=0,segment=0;
        using(var frames=new StreamWriter(Path.Combine(dir,"frames.csv")))
        using(var sounds=new StreamWriter(Path.Combine(dir,"sound_events.csv")))
        using(var joins=new StreamWriter(Path.Combine(dir,"segments.csv")))
        using(var ff=new StreamWriter(Path.Combine(dir,"animation.ffconcat"),false,new UTF8Encoding(false))) {
            frames.WriteLine("frame_index,pose_id,start_ms,duration_ms,file,source_variant");sounds.WriteLine("event_index,instruction,start_ms,sound_id,file,source_variant");
            joins.WriteLine("segment,variant,start_frame,start_ms,body_frames,body_duration_ms,rest_frame,rest_duration_ms,rest_pose");ff.WriteLine("ffconcat version 1.0");
            foreach(string variant in variants) {
                string source=Path.Combine(output,variant);long start=time;int startFrame=frame;
                foreach(string row in File.ReadAllLines(Path.Combine(source,"sound_events.csv"))) {
                    string[] c=row.Split(',');if(c[0]=="event_index" || c.Length<5)continue;
                    sounds.WriteLine(string.Join(",",eventIndex++,c[1],start+long.Parse(c[2],CultureInfo.InvariantCulture),c[3],c[4],variant));
                }
                foreach(string row in File.ReadAllLines(Path.Combine(source,"frames.csv"))) {
                    string[] c=row.Split(',');if(c[0]=="frame_index" || c.Length<5)continue;
                    int ms=int.Parse(c[3],CultureInfo.InvariantCulture);string file="../"+variant+"/"+c[4];
                    frames.WriteLine(string.Join(",",frame++,c[1],time,ms,file,variant));
                    ff.WriteLine("file "+Quote(file));ff.WriteLine("option framerate 1000");ff.WriteLine("duration "+(ms/1000.0).ToString("0.000",CultureInfo.InvariantCulture));time+=ms;
                }
                long body=time-start;int bodyFrames=frame-startFrame;
                // The original engine appends ImageShow when an action finishes.
                // Hold it briefly before starting the next independent path.
                joins.WriteLine(string.Join(",",++segment,variant,startFrame,start,bodyFrames,body,frame,250,rest));
                frames.WriteLine(string.Join(",",frame++,rest,time,250,"rest.png","rest"));
                ff.WriteLine("file 'rest.png'");ff.WriteLine("option framerate 1000");ff.WriteLine("duration 0.250");time+=250;
            }
            ff.WriteLine("file 'rest.png'");ff.WriteLine("option framerate 1000");
        }
        File.WriteAllText(Path.Combine(dir,"loop.txt"),"classification=assembled_idle"+Environment.NewLine+"loop_start_frame=-1"+Environment.NewLine+"frames="+frame+Environment.NewLine+"duration_ms="+time+Environment.NewLine);
        File.WriteAllText(Path.Combine(dir,"assembly.txt"),"Independent valid idle variants played in fixed order, each followed by the engine's normal ImageShow rest pose for 250 ms. Segment boundaries are action restarts, not bytecode jumps."+Environment.NewLine);
        File.WriteAllText(Path.Combine(dir,"background.txt"),BackdropDescription+Environment.NewLine);
        int total=0,covered=0;foreach(string row in File.ReadAllLines(Path.Combine(output,"idle_variant_coverage.csv"))){var c=row.Split(',');if(c[0]=="from_instruction")continue;total++;if(c[2]=="yes")covered++;}
        File.WriteAllText(Path.Combine(output,"showcase_summary.csv"),"action_id,action_name,folder,mp4,frames,duration_ms,sound_triggers,covered_edges,total_edges,assembly_segments"+Environment.NewLine+
            string.Join(",",a.Id,ActionName(a.Id),folder,folder+".mp4",frame,time,eventIndex,covered,total,variants.Count)+Environment.NewLine);
        File.WriteAllText(marker,fingerprint);Console.WriteLine("Assembled idle: "+variants.Count+" variants, "+time+" ms, one showcase clip");
    }
}
