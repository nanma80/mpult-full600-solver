using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void ExportReflection(string directory) {
        Directory.CreateDirectory(directory);string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var reflection=PGeom.MatrixIdentity(4);reflection[0,0]=-1;
        var axisMap=new int[structure.Axes.Length];var reverseAxis=new bool[axisMap.Length];
        for(int a=0;a<axisMap.Length;a++){
            int signed=structure.FindAxis(structure.Axes[a].Dir,reflection);axisMap[a]=Math.Abs(signed)-1;reverseAxis[a]=signed<0;
            bool rev;if(!PGeom.AxisEqual(PGeom.ApplyMatrix(reflection,structure.Axes[a].Dir),structure.Axes[axisMap[a]].Dir,out rev) || rev!=reverseAxis[a])throw new Exception("Reflection does not preserve axes");
        }
        var faceMap=new int[structure.Faces.Length];
        for(int f=0;f<faceMap.Length;f++){
            var pole=PGeom.ApplyMatrix(reflection,structure.Faces[f].Pole);int found=-1;
            for(int q=0;q<faceMap.Length;q++)if(PGeom.VertEqual(pole,structure.Faces[q].Pole)){if(found>=0)throw new Exception("Ambiguous reflected face");found=q;}
            if(found<0)throw new Exception("Coordinate reflection is not a face symmetry");faceMap[f]=found;
        }
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbits=rows.Select(s=>int.Parse(s[1])).ToArray();
        var signatures=rows.Select(s=>s[3].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()).ToArray();
        var lookup=new Dictionary<string,int>();for(int p=0;p<pieces.Length;p++)lookup.Add(String.Join(",",signatures[p].OrderBy(x=>x)),p);
        var pieceMap=new int[pieces.Length];var positions=new int[Count];
        for(int p=0;p<pieces.Length;p++){
            var key=signatures[p].Select(x=>2*axisMap[x/2]+((x%2)^(reverseAxis[x/2]?1:0))).OrderBy(x=>x);
            int q;if(!lookup.TryGetValue(String.Join(",",key),out q))throw new Exception("Reflected chamber missing");pieceMap[p]=q;
            foreach(int source in pieces[p]){
                int reflectedCell=faceMap[source/433];var possible=pieces[q].Where(dest=>dest/433==reflectedCell).ToArray();
                if(possible.Length!=1)throw new Exception("Reflected sticker incidence ambiguous");positions[source]=possible[0];
            }
        }
        if(positions.Distinct().Count()!=Count || Enumerable.Range(0,Count).Any(p=>positions[positions[p]]!=p))throw new Exception("Reflection not a sticker involution");
        if(Enumerable.Range(0,pieces.Length).Any(p=>pieceMap[pieceMap[p]]!=p))throw new Exception("Reflection not a piece involution");
        var orbitMap=new SortedDictionary<int,int>();for(int p=0;p<pieces.Length;p++){int oid=orbits[p],target=orbits[pieceMap[p]];if(orbitMap.ContainsKey(oid) && orbitMap[oid]!=target)throw new Exception("Reflection splits piece orbit");orbitMap[oid]=target;}
        var stickerOrbit=ReadInts(Path.Combine(analysis,"sticker_orbits","position_orbit.int32.bin"));var stickerOrbitMap=new SortedDictionary<int,int>();
        for(int p=0;p<Count;p++){int oid=stickerOrbit[p],target=stickerOrbit[positions[p]];if(stickerOrbitMap.ContainsKey(oid) && stickerOrbitMap[oid]!=target)throw new Exception("Reflection splits sticker orbit");stickerOrbitMap[oid]=target;}
        long checkedEntries=0;var inverseTranslation=new int[7];for(int t=0;t<7;t++)inverseTranslation[Translation[t]]=t;var index=new int[Count];
        using(var w=new StreamWriter(Path.Combine(directory,"move_map.tsv"))){w.WriteLine("axis\ttwist\tmirrored_axis\tmirrored_twist\tangle_sign\treverse_mask");
            for(int a=0;a<axisMap.Length;a++){
                int b=axisMap[a];var sourceAxis=structure.Axes[a];var destinationAxis=structure.Axes[b];
                for(int t=0;t<7;t++){
                    bool rev;int targetSourceTwist=destinationAxis.FindTwist(sourceAxis.Twists[Translation[t]],reflection,out rev),targetTwist=inverseTranslation[targetSourceTwist];
                    if(Order(t)!=Order(targetTwist))throw new Exception("Reflection changes rotation order");
                    w.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",a,t,b,targetTwist,rev?-1:1,reverseAxis[a]?1:0);
                    foreach(int layer in new[]{0,2}){
                        int mirroredLayer=reverseAxis[a]?2-layer:layer;var source=sourceAxis.Layers[layer];var dest=destinationAxis.Layers[mirroredLayer];
                        if(source.Length!=dest.Length)throw new Exception("Reflection changes layer size");for(int i=0;i<dest.Length;i++)index[dest[i]]=i;
                        var map=sourceAxis.Base.Twists[Translation[t]].Map[layer];var targetMap=rev?destinationAxis.Base.Twists[targetSourceTwist].InvMap[mirroredLayer]:destinationAxis.Base.Twists[targetSourceTwist].Map[mirroredLayer];
                        for(int i=0;i<source.Length;i++){
                            int r=positions[source[i]],local=index[r];if(dest[local]!=r)throw new Exception("Reflection changes layer support");
                            if(positions[source[map[i]]]!=dest[targetMap[local]])throw new Exception("Reflected move fails exact conjugacy");checkedEntries++;
                        }
                    }
                }
            }
        }
        WriteInts(Path.Combine(directory,"position_reflection.int32.bin"),positions);WriteInts(Path.Combine(directory,"piece_reflection.int32.bin"),pieceMap);WriteInts(Path.Combine(directory,"cell_reflection.int32.bin"),faceMap);
        File.WriteAllLines(Path.Combine(directory,"piece_orbit_map.tsv"),new[]{"orbit\tmirrored_orbit"}.Concat(orbitMap.Select(kv=>kv.Key+"\t"+kv.Value)));
        File.WriteAllLines(Path.Combine(directory,"sticker_orbit_map.tsv"),new[]{"orbit\tmirrored_orbit"}.Concat(stickerOrbitMap.Select(kv=>kv.Key+"\t"+kv.Value)));
        File.WriteAllLines(Path.Combine(directory,"checks.txt"),new[]{"reflection=diag(-1,1,1,1)","determinant=-1","positions="+Count,"position_bijection_involution=PASS","generators_checked=4200","generator_entries_checked="+checkedEntries,"exact_conjugacy=PASS","interpretation=reflection-related legal-move orbits; intrinsic shape chirality not asserted"});
        Console.WriteLine("PASS reflection: {0} positions; exact conjugacy on {1} generator entries",Count,checkedEntries);
        Console.WriteLine("Movable orbit mapping: "+String.Join("; ",orbitMap.Where(kv=>kv.Key>=1 && kv.Key<=35).Select(kv=>kv.Key+"->"+kv.Value)));
    }
}}
