using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;

partial class WillActorExporter
{
    static Color BackdropColor=Color.Transparent;
    static Image BackdropImage;
    static string BackdropKey="transparent",BackdropDescription="color=Transparent";
    static void InitBackdrop(string color,string image) {
        if(color.StartsWith("#",StringComparison.Ordinal)) {
            if(color.Length!=7)throw new ArgumentException("Background hex color must be #RRGGBB.");
            int rgb;if(!int.TryParse(color.Substring(1),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out rgb))throw new ArgumentException("Invalid background hex color.");
            BackdropColor=Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255);
        } else {
            Color named=Color.FromName(color);
            if(!named.IsKnownColor)throw new ArgumentException("Background must be Transparent, a known color name, or #RRGGBB.");
            BackdropColor=named;
        }
        BackdropKey=BackdropColor.ToArgb().ToString("X8");BackdropDescription="color="+color;
        if(!string.IsNullOrEmpty(image)) {
            string full=Path.GetFullPath(image);if(!File.Exists(full))throw new FileNotFoundException("Background image not found",full);
            byte[] data=File.ReadAllBytes(full);
            using(var sha=SHA256.Create())BackdropKey+=":"+BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");
            using(var stream=new MemoryStream(data))using(var decoded=Image.FromStream(stream))BackdropImage=new Bitmap(decoded);
            BackdropDescription+=Environment.NewLine+"image="+Path.GetFileName(full)+Environment.NewLine+"fit=cover (centered crop; aspect ratio preserved)";
        }
    }
    static void DrawBackdrop(Graphics g,int width,int height) {
        if(BackdropImage==null)return;
        double ratio=Math.Max((double)width/BackdropImage.Width,(double)height/BackdropImage.Height);
        float sw=(float)(width/ratio),sh=(float)(height/ratio);
        using(var attributes=new System.Drawing.Imaging.ImageAttributes()) {
            attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
            g.DrawImage(BackdropImage,new Rectangle(0,0,width,height),
                (BackdropImage.Width-sw)/2,(BackdropImage.Height-sh)/2,sw,sh,GraphicsUnit.Pixel,attributes);
        }
    }
}
