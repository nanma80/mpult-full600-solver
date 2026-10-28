using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace _3dedit {
partial class MoveEngine {
    public void ExportPieces(string directory) {
        Directory.CreateDirectory(directory);
        var membership=new List<char>[Count];for(int i=0;i<Count;i++)membership[i]=new List<char>();
        foreach(var a in structure.Axes)foreach(int layer in new[]{0,2})foreach(int p in a.Layers[layer])
            membership[p].Add((char)(1+2*a.Id+(layer==0?0:1)));
        var signatures=new string[Count];for(int i=0;i<Count;i++)signatures[i]=new string(membership[i].ToArray());
        // Cross-check side-of-cut memberships using transformed geometric centroids.
        double minMargin=Double.MaxValue;long planeChecks=0;
        var norm=structure.Axes.Select(a=>PGeom.DotProd(a.Dir,a.Dir)).ToArray();
        foreach(var f in structure.Faces)for(int local=0;local<f.Base.NStickers;local++) {
            var center=PGeom.ApplyMatrix(f.Matrix,f.Base.StickerMesh[local].GetMCtr());
            var geometric=new List<char>();
            foreach(var a in structure.Axes) {
                double v=PGeom.DotProd(center,a.Dir)/norm[a.Id];
                minMargin=Math.Min(minMargin,Math.Min(Math.Abs(v-a.Base.Cut[0]),Math.Abs(v-a.Base.Cut[1])));
                if(v>a.Base.Cut[0])geometric.Add((char)(1+2*a.Id));
                else if(v<a.Base.Cut[1])geometric.Add((char)(2+2*a.Id));
                planeChecks++;
            }
            if(signatures[f.FirstSticker+local]!=new string(geometric.ToArray()))throw new Exception("Geometric cut signature disagreement at "+(f.FirstSticker+local));
        }
        var bySignature=new Dictionary<string,int>();var pieces=new List<List<int>>();var ids=new int[Count];
        for(int p=0;p<Count;p++) {
            int piece;if(!bySignature.TryGetValue(signatures[p],out piece)) {piece=pieces.Count;pieces.Add(new List<int>());bySignature.Add(signatures[p],piece);}
            ids[p]=piece;pieces[piece].Add(p);
        }
        var colors=Solved();
        foreach(var piece in pieces)if(piece.Select(p=>colors[p]).Distinct().Count()!=piece.Count)throw new Exception("Repeated face in chamber");
        var parent=Enumerable.Range(0,pieces.Count).ToArray();var rank=new byte[pieces.Count];
        var destination=Enumerable.Repeat(-1,pieces.Count).ToArray();var count=new int[pieces.Count];long checks=0;
        foreach(var a in structure.Axes)for(int twist=0;twist<7;twist++)foreach(int layer in new[]{0,2}) {
            var pos=a.Layers[layer];var map=a.Base.Twists[Translation[twist]].Map[layer];var touched=new List<int>();
            for(int i=0;i<pos.Length;i++) {
                int source=ids[pos[i]],target=ids[pos[map[i]]];
                if(destination[source]<0){destination[source]=target;touched.Add(source);}
                else if(destination[source]!=target)throw new Exception("Generator splits a candidate piece");
                count[source]++;checks++;
            }
            foreach(int source in touched) {
                int target=destination[source];
                if(count[source]!=pieces[source].Count || pieces[source].Count!=pieces[target].Count)throw new Exception("Not a whole piece bijection");
                int p=Root(parent,source),q=Root(parent,target);
                if(p!=q){if(rank[p]<rank[q])parent[p]=q;else{parent[q]=p;if(rank[p]==rank[q])rank[p]++;}}
                destination[source]=-1;count[source]=0;
            }
        }
        var orbitMap=new Dictionary<int,int>();var orbits=new int[pieces.Count];
        for(int p=0;p<pieces.Count;p++){int root=Root(parent,p),id;if(!orbitMap.TryGetValue(root,out id)){id=orbitMap.Count;orbitMap.Add(root,id);}orbits[p]=id;}
        WriteInts(Path.Combine(directory,"position_piece.int32.bin"),ids);
        WriteInts(Path.Combine(directory,"piece_orbit.int32.bin"),orbits);
        using(var w=new StreamWriter(Path.Combine(directory,"pieces.tsv"))) {
            w.WriteLine("piece\torbit\tpositions\touter_memberships");
            for(int p=0;p<pieces.Count;p++)w.WriteLine("{0}\t{1}\t{2}\t{3}",p,orbits[p],String.Join(",",pieces[p]),String.Join(",",membership[pieces[p][0]].Select(c=>(int)c-1)));
        }
        File.WriteAllLines(Path.Combine(directory,"geometry_checks.txt"),new[]{"pieces="+pieces.Count,"piece_orbits="+orbitMap.Count,"geometric_centroid_axis_checks="+planeChecks,"minimum_cut_margin="+minMargin.ToString("R",CultureInfo.InvariantCulture),"generator_sticker_piece_checks="+checks,"all_checks=PASS"});
        Console.WriteLine("PASS: {0} cut-chamber pieces, {1} piece orbits, {2} geometric axis checks, {3} generator entries preserve whole pieces; min cut margin {4:R}",pieces.Count,orbitMap.Count,planeChecks,checks,minMargin);
    }
}}
