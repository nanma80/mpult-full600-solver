using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    class WordCandidate {
        internal int Axis,Twist,Mask,StickerCount,PieceCount,OrbitCount,MovedPieceCount;
        internal int EffectivePieceCount,EffectiveOrbitCount;
        internal int[] Sources,Targets;
        internal string Detail;
    }
    public void SearchCommutators(string directory,bool nested=false,string anchorFile=null,int[] ignoredOrbits=null,int movingOrbit=-1) {
        Directory.CreateDirectory(directory);
        var catalogParent=Directory.GetParent(Path.GetFullPath(directory));
        while(catalogParent!=null && !File.Exists(Path.Combine(catalogParent.FullName,"pieces","pieces.tsv")))catalogParent=catalogParent.Parent;
        if(catalogParent==null)throw new Exception("No piece catalog found in analysis parent directories");
        var pieceFolder=Path.Combine(catalogParent.FullName,"pieces");
        var rows=File.ReadAllLines(Path.Combine(pieceFolder,"pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieceOrbit=rows.Select(s=>int.Parse(s[1])).ToArray();var pieceAt=new int[Count];
        var ignored=new HashSet<int>(ignoredOrbits??new int[0]);
        for(int p=0;p<rows.Length;p++)foreach(int pos in rows[p][2].Split(',').Select(int.Parse))pieceAt[pos]=p;
        var anchorWord=new List<int[]>{new[]{62,6,2,1}};
        if(nested) {
            string previous=Path.Combine(Path.GetDirectoryName(directory),"generator_search","best_words.tsv");
            anchorWord=File.ReadAllLines(previous)[1].Split('\t')[1].Split(' ').Select(token=>token.Split(':').Select(int.Parse).ToArray()).ToList();
        }
        if(anchorFile!=null)anchorWord=File.ReadAllText(anchorFile).Trim().Split(' ').Select(token=>token.Split(':').Select(int.Parse).ToArray()).ToList();
        var inverseWord=anchorWord.AsEnumerable().Reverse().Select(m=>new[]{m[0],m[1],(Order(m[1])-m[2]%Order(m[1]))%Order(m[1]),m[3]}).ToArray();
        var a=Enumerable.Range(0,Count).ToArray();
        foreach(var move in anchorWord){var p=Permutation(move[0],move[1],move[2],move[3]);for(int i=0;i<Count;i++)a[i]=p[a[i]];}
        var ai=new int[Count];for(int i=0;i<Count;i++)ai[a[i]]=i;
        var anchor=Enumerable.Range(0,Count).Where(p=>a[p]!=p).ToArray();
        var b=Enumerable.Range(0,Count).ToArray();var bi=(int[])b.Clone();var stamps=new int[Count];int stamp=0;
        var best=new List<WordCandidate>();int identity=0,candidates=0,singleOrbit=0,effectiveNonidentity=0,effectiveSingleOrbit=0;
        using(var w=new StreamWriter(Path.Combine(directory,"scan.tsv"))) {
            w.WriteLine("axis\ttwist\tmask\tmoved_stickers\taffected_pieces\tmoved_piece_slots\taffected_piece_orbits\torbit_affected_moved_inplace\teffective_pieces\teffective_orbits");
            foreach(var axis in structure.Axes)for(int twist=0;twist<7;twist++)foreach(int layer in new[]{0,2}) {
                candidates++;stamp++;var pos=axis.Layers[layer];var map=axis.Base.Twists[Translation[twist]].Map[layer];
                for(int i=0;i<pos.Length;i++){b[pos[i]]=pos[map[i]];bi[pos[map[i]]]=pos[i];}
                var sources=new List<int>();var targets=new List<int>();var affected=new HashSet<int>();var moved=new HashSet<int>();
                foreach(var support in new[]{anchor,pos})foreach(int p in support) {
                    if(stamps[p]==stamp)continue;stamps[p]=stamp;
                    int q=bi[ai[b[a[p]]]];
                    if(p==q)continue;
                    sources.Add(p);targets.Add(q);affected.Add(pieceAt[p]);if(pieceAt[p]!=pieceAt[q])moved.Add(pieceAt[p]);
                }
                var counts=new SortedDictionary<int,int[]>();
                foreach(int p in affected){int oid=pieceOrbit[p];if(!counts.ContainsKey(oid))counts.Add(oid,new int[3]);counts[oid][0]++;counts[oid][moved.Contains(p)?1:2]++;}
                string detail=String.Join(";",counts.Select(kv=>kv.Key+":"+String.Join(",",kv.Value)));
                int effectivePieces=affected.Count(p=>!ignored.Contains(pieceOrbit[p])),effectiveOrbits=counts.Keys.Count(oid=>!ignored.Contains(oid));
                int requiredMoved=movingOrbit<0?0:moved.Count(p=>pieceOrbit[p]==movingOrbit);
                if(movingOrbit>=0 && ignored.Contains(movingOrbit)){effectivePieces+=requiredMoved;if(requiredMoved>0)effectiveOrbits++;}
                w.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}",axis.Id,twist,1<<layer,sources.Count,affected.Count,moved.Count,counts.Count,detail,effectivePieces,effectiveOrbits);
                if(sources.Count==0)identity++;
                else {
                    if(counts.Count==1)singleOrbit++;
                    if(effectivePieces>0){effectiveNonidentity++;if(effectiveOrbits==1)effectiveSingleOrbit++;}
                    if(effectivePieces>0 && (movingOrbit<0 || requiredMoved>0) && (best.Count<25 || effectivePieces<=best[best.Count-1].EffectivePieceCount)) {
                        best.Add(new WordCandidate{Axis=axis.Id,Twist=twist,Mask=1<<layer,StickerCount=sources.Count,PieceCount=affected.Count,OrbitCount=counts.Count,MovedPieceCount=moved.Count,Sources=sources.ToArray(),Targets=targets.ToArray(),Detail=detail,EffectivePieceCount=effectivePieces,EffectiveOrbitCount=effectiveOrbits});
                        best=best.OrderBy(c=>c.EffectivePieceCount).ThenBy(c=>c.EffectiveOrbitCount).ThenBy(c=>c.StickerCount).Take(25).ToList();
                    }
                }
                foreach(int p in pos){b[p]=p;bi[p]=p;}
            }
        }
        using(var w=new StreamWriter(Path.Combine(directory,"best_words.tsv"))) {
            w.WriteLine("rank\tchronological_word\taffected_pieces\tmoved_stickers\taffected_piece_orbits\torbit_detail\teffective_pieces\teffective_orbits");
            for(int i=0;i<best.Count;i++) {
                var c=best[i];int inverseAngle=Order(c.Twist)-1;
                var word=anchorWord.Concat(new[]{new[]{c.Axis,c.Twist,1,c.Mask}}).Concat(inverseWord).Concat(new[]{new[]{c.Axis,c.Twist,inverseAngle,c.Mask}}).ToArray();
                w.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}",i+1,String.Join(" ",word.Select(m=>String.Join(":",m))),c.PieceCount,c.StickerCount,c.OrbitCount,c.Detail,c.EffectivePieceCount,c.EffectiveOrbitCount);
                // Cross-check sparse composition against the independent Apply path on unique labels.
                var perm=Enumerable.Range(0,Count).ToArray();for(int j=0;j<c.Sources.Length;j++)perm[c.Sources[j]]=c.Targets[j];
                if(perm.Distinct().Count()!=Count)throw new Exception("Candidate not bijective");
                var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)labels=Apply(labels,move);
                for(int p=0;p<Count;p++)if(labels[perm[p]]!=p)throw new Exception("Word composition mismatch");
                var destination=new Dictionary<int,int>();
                for(int j=0;j<c.Sources.Length;j++){int source=pieceAt[c.Sources[j]],target=pieceAt[c.Targets[j]];if(destination.ContainsKey(source) && destination[source]!=target)throw new Exception("Word splits a piece");destination[source]=target;}
                using(var bin=new BinaryWriter(File.Create(Path.Combine(directory,"candidate_"+(i+1).ToString("D2")+".source_destination.int32.bin"))))for(int j=0;j<c.Sources.Length;j++){bin.Write(c.Sources[j]);bin.Write(c.Targets[j]);}
            }
        }
        File.WriteAllLines(Path.Combine(directory,"checks.txt"),new[]{"candidates="+candidates,"identities="+identity,"nonidentities="+(candidates-identity),"single_piece_orbit_words="+singleOrbit,"best_support_pieces="+(best.Count>0?best[0].PieceCount:0),"ignored_orbits="+String.Join(",",ignored),"effective_nonidentities="+effectiveNonidentity,"effective_single_orbit_words="+effectiveSingleOrbit,"best_effective_support="+(best.Count>0?best[0].EffectivePieceCount:0),"top_candidates_direct_label_checks=PASS","top_candidates_bijection_checks=PASS"});
        Console.WriteLine("PASS: {0} words scanned, {1} nonidentities, {2} single-orbit words, best effective support {3}; {4} single-orbit words outside ignored set; top retained words checked on distinct labels",candidates,candidates-identity,singleOrbit,best.Count>0?best[0].EffectivePieceCount:0,effectiveSingleOrbit);
    }
}}
