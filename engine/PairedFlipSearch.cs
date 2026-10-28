using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void SearchPairedFlips(string directory) {
        Directory.CreateDirectory(directory);string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var config=File.ReadAllLines(Path.Combine(directory,"config.txt")).Where(s=>s.Length>0).Select(s=>s.Split(new[]{'='},2)).ToDictionary(s=>s[0],s=>s[1]);
        int oid=int.Parse(config["orbit"]);var allowed=new HashSet<int>(config["allowed_collateral"].Split(',').Select(int.Parse));
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbits=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var primitive=File.ReadAllText(config["primitive_word_file"]).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in primitive)ApplyInPlace(labels,m);
        var action=new Dictionary<int,int>();for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){int p=pieceAt[labels[dest]],q=pieceAt[dest];if(orbits[p]==oid)action[p]=q;else if(!allowed.Contains(orbits[p]))throw new Exception("Primitive has forbidden support");}
        if(action.Count!=3 || action.Any(kv=>kv.Key==kv.Value) || action.Keys.Any(p=>sizes[p]!=2))throw new Exception("Need a three-cycle of two-sticker pieces");
        var roots=action.Keys.OrderBy(p=>p).ToArray();int fixers=0,inspected=0;var candidates=new List<int[]>();
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            inspected++;if(roots.Any(p=>target[p]>=0 && target[p]!=p))return;fixers++;
            int pattern=0;for(int i=0;i<3;i++)if(target[roots[i]]>=0 && maps[roots[i]][0]==1)pattern|=1<<i;
            if(pattern!=0 && pattern!=7)candidates.Add(new[]{axis,twist,1,mask});
        });
        var records=new List<string>();int checkedCount=0;
        foreach(var g in candidates.Take(10)) {
            var word=primitive.Concat(new[]{g}).Concat(InvertMoves(primitive)).Concat(InvertMoves(new[]{g})).ToArray();
            labels=Enumerable.Range(0,Count).ToArray();foreach(var m in word)ApplyInPlace(labels,m);
            var flipped=new HashSet<int>();var collateral=new Dictionary<int,HashSet<int>>();int targetChanges=0;
            for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){int p=pieceAt[labels[dest]],q=pieceAt[dest],o=orbits[p];if(o==oid){if(p!=q || pieces[p].Length!=2 || localAt[labels[dest]]==localAt[dest])throw new Exception("Not a pure target flip");flipped.Add(p);targetChanges++;}else{if(!allowed.Contains(o))throw new Exception("Forbidden collateral action");if(!collateral.ContainsKey(o))collateral[o]=new HashSet<int>();collateral[o].Add(p);}}
            if(flipped.Count!=2 || targetChanges!=4)throw new Exception("Expected exactly a flip pair");
            string name="candidate_"+(++checkedCount).ToString("D2");File.WriteAllText(Path.Combine(directory,name+".word.txt"),String.Join(" ",word.Select(m=>String.Join(":",m)))+Environment.NewLine);
            using(var w=new BinaryWriter(File.Create(Path.Combine(directory,name+".source_destination.int32.bin"))))for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){w.Write(labels[dest]);w.Write(dest);}
            records.Add(name+"\t"+String.Join(":",g)+"\t"+word.Length+"\t"+String.Join(",",flipped.OrderBy(p=>p))+"\t"+String.Join(";",collateral.Select(kv=>kv.Key+":"+kv.Value.Count)));
        }
        File.WriteAllLines(Path.Combine(directory,"candidates.tsv"),new[]{"candidate\telementary_stabilizer\tliteral_turns\tflipped_piece_ids\tcollateral_piece_counts"}.Concat(records));
        File.WriteAllLines(Path.Combine(directory,"summary.txt"),new[]{"orbit="+oid,"elementary_generators_inspected="+inspected,"primitive_support_slot_fixers="+fixers,"nonconstant_flip_patterns="+candidates.Count,"full_label_candidates_checked="+checkedCount,"checks="+(checkedCount>0?"PASS":"NO_CANDIDATE")});
        Console.WriteLine("Orbit {0}: inspected {1}, slot fixers {2}, nonconstant patterns {3}, full-label flip pairs checked {4}",oid,inspected,fixers,candidates.Count,checkedCount);
    }
}}
