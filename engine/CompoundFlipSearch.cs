using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    void BinaryStudyConfig(string directory,out int oid,out HashSet<int> allowed) {
        oid=33;allowed=new HashSet<int>(new[]{29,34});
        string path=Path.Combine(directory,"config.txt");
        if(File.Exists(path)){var c=File.ReadAllLines(path).Where(x=>x.Contains("=")).Select(x=>x.Split(new[]{'='},2)).ToDictionary(x=>x[0],x=>x[1]);oid=int.Parse(c["orbit"]);allowed=new HashSet<int>(c["allowed_collateral"].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));}
        if(allowed.Contains(oid))throw new Exception("Binary target cannot be collateral");
    }
    public void ValidateBinaryPairs(string directory) {
        int oid;HashSet<int> allowed;BinaryStudyConfig(directory,out oid,out allowed);
        string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbits=rows.Select(s=>int.Parse(s[1])).ToArray();var pieceAt=new int[Count];
        for(int p=0;p<pieces.Length;p++)foreach(int pos in pieces[p])pieceAt[pos]=p;
        var report=new List<string>{"case\tchecked_positions\tchecks"};int count=0;
        foreach(var line in File.ReadAllLines(Path.Combine(directory,"label_check_jobs.tsv"))) {
            var f=line.Split('\t');var pair=f[2].Split(',').Select(int.Parse).ToArray();if(pair.Length!=2 || pair[0]==pair[1] || pair.Any(p=>orbits[p]!=oid || pieces[p].Length!=2))throw new Exception("Invalid expected pair");
            var labels=Enumerable.Range(0,Count).ToArray();var data=ReadInts(f[1]);if(data.Length%4!=0)throw new Exception("Bad move word");
            for(int i=0;i<data.Length;i+=4)ApplyInPlace(labels,new[]{data[i],data[i+1],data[i+2],data[i+3]});
            for(int dest=0;dest<Count;dest++){int p=pieceAt[dest];if(orbits[p]==oid){int expected=pair.Contains(p)?(pieces[p][0]==dest?pieces[p][1]:pieces[p][0]):dest;if(labels[dest]!=expected)throw new Exception("Routed flip has incorrect target labelled action");}else if(!allowed.Contains(orbits[p]) && labels[dest]!=dest)throw new Exception("Routed flip has forbidden labelled action");}
            report.Add(f[0]+"\t"+Count+"\tPASS");count++;
        }
        File.WriteAllLines(Path.Combine(directory,"label_pair_results.tsv"),report);Console.WriteLine("PASS: {0} arbitrary paired-flip words checked at all {1} unique sticker positions",count,Count);
    }
    public void SearchCompoundFlips(string directory) {
        Directory.CreateDirectory(directory);int oid;HashSet<int> allowed;BinaryStudyConfig(directory,out oid,out allowed);string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis"),routing=Path.Combine(analysis,"orbit_routing","orbit"+oid);
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var slots=ReadInts(Path.Combine(routing,"orbit_slots.int32.bin"));var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slots.Length;i++)ordinal[slots[i]]=i;
        var metadata=File.ReadAllLines(Path.Combine(routing,"routing_metadata.txt")).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
        var roots=metadata["primitive_ordinal_cycle"].Split(',').Select(int.Parse).ToArray();var primitive=File.ReadAllText(Path.Combine(routing,"primitive_word.txt")).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var route=new CompoundTree[3];
        for(int stage=0;stage<3;stage++){var tree=new CompoundTree{Parent=new int[slots.Length],Cost=new int[slots.Length],TowardsRoot=new int[slots.Length][][]};foreach(var line in File.ReadAllLines(Path.Combine(routing,"routing_stage_"+stage+".tsv")).Skip(1)){var f=line.Split('\t');int p=int.Parse(f[0]);tree.Parent[p]=int.Parse(f[1]);tree.Cost[p]=int.Parse(f[2]);if(f.Length>3 && f[3].Length>0)tree.TowardsRoot[p]=f[3].Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();}route[stage]=tree;}
        var destination=new Dictionary<int,int>[4200];var flip=new Dictionary<int,int>[4200];var generators=new int[4200][];
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);destination[gid]=new Dictionary<int,int>();flip[gid]=new Dictionary<int,int>();generators[gid]=new[]{axis,twist,1,mask};
            foreach(int p in touched)if(orbit[p]==oid){int x=ordinal[p];if(target[p]!=p)destination[gid][x]=ordinal[target[p]];if(maps[p][0]==1)flip[gid][x]=1;}
        });
        Action<int[],int[],int[]> apply=(positions,bits,m)=>{int gid=(m[0]*7+m[1])*2+(m[3]==4?1:0);for(int n=0;n<(m[2]%Order(m[1])+Order(m[1]))%Order(m[1]);n++)for(int i=0;i<positions.Length;i++){int p=positions[i],q;if(flip[gid].ContainsKey(p))bits[i]^=1;if(destination[gid].TryGetValue(p,out q))positions[i]=q;}};
        int depth=1,prefixLimit=32,tested=0,prefixesUsed=0;string configPath=Path.Combine(directory,"config.txt");
        if(File.Exists(configPath)){var cfg=File.ReadAllLines(configPath).Where(x=>x.Contains("=")).Select(x=>x.Split(new[]{'='},2)).ToDictionary(x=>x[0],x=>x[1]);if(cfg.ContainsKey("stabilizer_prefix_depth"))depth=int.Parse(cfg["stabilizer_prefix_depth"]);if(cfg.ContainsKey("prefix_limit"))prefixLimit=int.Parse(cfg["prefix_limit"]);}
        if(depth<1 || depth>2 || prefixLimit<1 || prefixLimit>64)throw new Exception("Invalid bounded stabilizer search config");
        var candidates=new List<Tuple<int,int[][],int>>();var patterns=new Dictionary<int,int>();
        using(var w=new StreamWriter(Path.Combine(directory,"stabilizers.tsv"))){w.WriteLine(depth==1?"generator_id\tstabilizer_turns\tflip_pattern":"prefix_id\tstabilizer_turns\tflip_pattern\tprefix_word");
            Action<int,int[][]> visit=(id,prefix)=>{tested++;var positions=(int[])roots.Clone();var bits=new int[3];var word=new List<int[]>(prefix);foreach(var m in prefix)apply(positions,bits,m);
                for(int stage=0;stage<3;stage++){foreach(var m in TreePath(route[stage],positions[stage])){word.Add(m);apply(positions,bits,m);}for(int j=0;j<=stage;j++)if(positions[j]!=roots[j])throw new Exception("Compound restore failed");}
                int pattern=bits[0]|bits[1]<<1|bits[2]<<2;if(!patterns.ContainsKey(pattern))patterns[pattern]=0;patterns[pattern]++;
                string line=id+"\t"+word.Count+"\t"+pattern;if(depth>1)line+="\t"+String.Join(" ",prefix.Select(m=>String.Join(":",m)));w.WriteLine(line);
                if(pattern!=0 && pattern!=7)candidates.Add(Tuple.Create(id,word.ToArray(),pattern));
            };
            for(int gid=0;gid<4200;gid++)visit(gid,new[]{generators[gid]});
            if(depth==2 && candidates.Count==0){
                var representatives=new List<int>();var seen=new HashSet<string>();
                for(int gid=0;gid<4200;gid++){var positions=(int[])roots.Clone();var bits=new int[3];apply(positions,bits,generators[gid]);if(positions.SequenceEqual(roots) && bits.All(b=>b==0))continue;string key=String.Join(",",positions)+"|"+String.Join(",",bits);if(seen.Add(key))representatives.Add(gid);}
                int count=Math.Min(prefixLimit,representatives.Count);
                for(int i=0;i<count && candidates.Count<32;i++){int g=representatives[count==1?0:i*(representatives.Count-1)/(count-1)];prefixesUsed++;for(int h=0;h<4200 && candidates.Count<32;h++)visit(4200+g*4200+h,new[]{generators[g],generators[h]});}
            }
        }
        var selected=candidates.OrderBy(c=>c.Item2.Length).ThenBy(c=>c.Item1).Take(10).ToArray();var records=new List<string>();
        for(int index=0;index<selected.Length;index++){var h=selected[index].Item2;var word=primitive.Concat(h).Concat(InvertMoves(primitive)).Concat(InvertMoves(h)).ToArray();
            var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in word)ApplyInPlace(labels,m);var flipped=new HashSet<int>();var collateral=new Dictionary<int,HashSet<int>>();int targetChanges=0;
            for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){int p=pieceAt[labels[dest]],q=pieceAt[dest],o=orbit[p];if(o==oid){if(p!=q || localAt[labels[dest]]==localAt[dest])throw new Exception("Target not pure flips");flipped.Add(p);targetChanges++;}else{if(!allowed.Contains(o))throw new Exception("Forbidden collateral");if(!collateral.ContainsKey(o))collateral[o]=new HashSet<int>();collateral[o].Add(p);}}
            if(flipped.Count!=2 || targetChanges!=4)throw new Exception("Expected a paired flip");string name="candidate_"+(index+1).ToString("D2");
            File.WriteAllText(Path.Combine(directory,name+".word.txt"),String.Join(" ",word.Select(m=>String.Join(":",m)))+Environment.NewLine);
            using(var b=new BinaryWriter(File.Create(Path.Combine(directory,name+".source_destination.int32.bin"))))for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){b.Write(labels[dest]);b.Write(dest);}
            records.Add(name+"\t"+selected[index].Item1+"\t"+h.Length+"\t"+word.Length+"\t"+String.Join(",",flipped.OrderBy(p=>p))+"\t"+String.Join(";",collateral.Select(kv=>kv.Key+":"+kv.Value.Count)));
        }
        File.WriteAllLines(Path.Combine(directory,"candidates.tsv"),new[]{(depth==1?"candidate\tgenerator_id\tstabilizer_turns\tliteral_turns\tflipped_piece_ids\tcollateral_piece_counts":"candidate\tprefix_id\tstabilizer_turns\tliteral_turns\tflipped_piece_ids\tcollateral_piece_counts")}.Concat(records));
        File.WriteAllLines(Path.Combine(directory,"summary.txt"),new[]{"orbit="+oid,"stabilizers_tested="+tested,"nonconstant_patterns="+candidates.Count,"full_label_candidates_checked="+selected.Length,"pattern_counts="+String.Join(";",patterns.Select(kv=>kv.Key+":"+kv.Value)),"checks="+(selected.Length>0?"PASS":"NO_CANDIDATE")});
        if(depth>1)File.AppendAllLines(Path.Combine(directory,"summary.txt"),new[]{"prefix_depth="+depth,"two_move_prefixes="+prefixesUsed,"maximum_stabilizers="+(4200+4200*prefixLimit)});
        Console.WriteLine("Compound stabilizers:{0} tested, {1} useful patterns, {2} full-label flip pairs verified; best literal turns {3}",tested,candidates.Count,selected.Length,selected.Length>0?2*(primitive.Length+selected[0].Item2.Length):0);
    }
}}
