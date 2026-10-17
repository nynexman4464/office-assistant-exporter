using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using FrontierUtopia;

partial class WillActorExporter
{
    class ArtworkInfo {
        public int Vector,Bitmap,Unknown,Composite;
        public string Kind {get{return Unknown>0?"unknown":Vector>0?(Bitmap>0?"mixed":"vector"):Bitmap>0?"bitmap":"unknown";}}
    }
    static ArtworkInfo InspectArtwork(ActEngine e) {
        var info=new ArtworkInfo();byte[] bytes=(byte[])Field(e,"_imagesBytes");uint[] directory=(uint[])Field(e,"_imageDirRaw");
        var seen=new HashSet<int>();
        if(bytes==null || directory==null){info.Unknown++;return info;}
        for(int i=0;i<directory.Length-1;i++) {
            int offset=(int)(directory[i]&0x3fffffff);if(!seen.Add(offset))continue;
            if(offset<0 || (long)offset+8>bytes.Length){info.Unknown++;continue;}
            uint magic=BitConverter.ToUInt32(bytes,offset);
            if(U16(bytes,offset)==20){info.Composite++;continue;}
            // DCIK is the shared palette, not an artwork image.
            if(magic==0x4b494344)continue;
            if(magic==0x4b414e4d || U16(bytes,offset)==0x4d42 || magic==12 || magic==40 || magic==52 || magic==56 || magic==64 || magic==108 || magic==124){info.Bitmap++;continue;}
            int header=magic==0x9ac6cdd7?offset+22:offset;
            if((long)header+18>bytes.Length || U16(bytes,header+2)!=9 || (U16(bytes,header)!=1 && U16(bytes,header)!=2)){info.Unknown++;continue;}
            // Some legacy WMFs have incorrect declared mtSize values. Bound the
            // record walk by the actual ACT resource span, as the renderer does.
            int next=i+1;while(next<directory.Length && (directory[next]&0x3fffffff)==offset)next++;
            long end=next<directory.Length?(long)(directory[next]&0x3fffffff):bytes.Length;int at=header+18;
            bool bitmap=false,vector=false,valid=end<=bytes.Length && end>=at,ended=false;
            while(valid && at+6L<=end) {
                long size=2L*BitConverter.ToUInt32(bytes,at);int code=U16(bytes,at+4);
                if(size<6 || at+size>end){valid=false;break;}
                string type=((EmfPlusRecordType)(code+65536)).ToString();
                if(type.IndexOf("Dib",StringComparison.OrdinalIgnoreCase)>=0 || type.IndexOf("BitBlt",StringComparison.OrdinalIgnoreCase)>=0 || type.IndexOf("StretchBlt",StringComparison.OrdinalIgnoreCase)>=0)bitmap=true;
                if(type.Contains("Polygon") || type.Contains("Polyline") || type.Contains("Ellipse") || type.Contains("Rectangle") || type.Contains("LineTo") || type.Contains("TextOut") || type.Contains("PatBlt") || type.Contains("SetPixel") || type.Contains("FloodFill") || type.EndsWith("Arc") || type.EndsWith("Pie") || type.EndsWith("Chord"))vector=true;
                at+=(int)size;if(code==0){ended=true;break;}
            }
            if(!valid || !ended || (!bitmap && !vector)){info.Unknown++;continue;}
            if(bitmap)info.Bitmap++;if(vector)info.Vector++;
        }
        return info;
    }
    static int ChooseScale(ArtworkInfo artwork,int requested,bool scaleBitmaps) {return scaleBitmaps || artwork.Kind=="vector"?requested:1;}
    static int ApplyArtworkScale(ActEngine e,string output,int requested,bool scaleBitmaps) {
        var artwork=InspectArtwork(e);int effective=ChooseScale(artwork,requested,scaleBitmaps);
        Console.WriteLine("Artwork: "+artwork.Kind+"; requested scale "+requested+"x; effective "+effective+"x"+(effective!=requested?" (native size preserved)":""));
        File.WriteAllText(Path.Combine(output,"artwork.csv"),"kind,vector_resources,bitmap_resources,unknown_resources,composite_records,requested_scale,effective_scale,width,height,scale_bitmaps_override"+Environment.NewLine+
            string.Join(",",artwork.Kind,artwork.Vector,artwork.Bitmap,artwork.Unknown,artwork.Composite,requested,effective,e.OriginalWidth*effective,e.OriginalHeight*effective,CsvBool(scaleBitmaps))+Environment.NewLine);
        return effective;
    }
    static void ArtworkScaleTests() {
        Check(ChooseScale(new ArtworkInfo{Vector=1},4,false)==4,"vector scaling");
        Check(ChooseScale(new ArtworkInfo{Bitmap=1},4,false)==1,"bitmap native size");
        Check(ChooseScale(new ArtworkInfo{Vector=1,Bitmap=1},4,false)==1,"mixed artwork native size");
        Check(ChooseScale(new ArtworkInfo{Unknown=1},4,false)==1,"unknown artwork native size");
        Check(ChooseScale(new ArtworkInfo{Bitmap=1},4,true)==4,"explicit bitmap scaling override");
    }
}
