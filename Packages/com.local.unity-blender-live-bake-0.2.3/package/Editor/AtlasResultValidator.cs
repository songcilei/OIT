using System;
using System.Collections.Generic;
using UnityEngine;
namespace LiveBakeBridge.Editor
{
    public static class AtlasResultValidator
    {
        public static void ValidateCapacity(int count,int size,int margin) {
            if(count<1)throw new InvalidOperationException("No instances to pack.");
            int columns=(int)Math.Ceiling(Math.Sqrt(count)),rows=(count+columns-1)/columns;
            if(Math.Min(size/columns,size/rows)-2*(margin+2)<16)throw new InvalidOperationException("Atlas too small for this instance count and padding. Increase Atlas resolution or reduce synced instances/padding.");
        }
        public static Vector4 ScaleOffset(BakeResult result,BakeItem item) {
            if(!result.atlas)return new Vector4(1,1,0,0);
            var st=item.scaleOffset;return new Vector4(st[0],st[1],st[2],st[3]);
        }
        public static void Validate(BakeResult result) {
            if(!result.atlas)return; // Older per-instance results have no atlas/ST fields.
            if(result.items==null||result.items.Length==0)throw new InvalidOperationException("Empty atlas result.");
            if(result.atlasSize<64||result.atlasSize>4096||(result.atlasSize&(result.atlasSize-1))!=0)throw new InvalidOperationException("Invalid atlas resolution.");
            var first=result.items[0];var rectangles=new List<Vector4>();
            foreach(var item in result.items) {
                if(string.IsNullOrEmpty(item.image)||item.image!=first.image||string.IsNullOrEmpty(item.sha256)||item.sha256!=first.sha256)throw new InvalidOperationException("Single-atlas result must reference exactly one image/checksum.");
                var st=item.scaleOffset;
                if(st==null||st.Length!=4)throw new InvalidOperationException("Missing atlas Tiling/Offset.");
                foreach(float x in st)if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidOperationException("Invalid atlas Tiling/Offset.");
                if(st[0]<=0||st[1]<=0||st[2]<0||st[3]<0||st[0]+st[2]>1.000001f||st[1]+st[3]>1.000001f)throw new InvalidOperationException("Atlas rectangle out of range.");
                var rect=new Vector4(st[2],st[3],st[2]+st[0],st[3]+st[1]);
                foreach(var other in rectangles)if(Math.Min(rect.z,other.z)-Math.Max(rect.x,other.x)>1e-7f&&Math.Min(rect.w,other.w)-Math.Max(rect.y,other.y)>1e-7f)throw new InvalidOperationException("Overlapping atlas instance rectangles.");
                rectangles.Add(rect);
            }
        }
    }
}
