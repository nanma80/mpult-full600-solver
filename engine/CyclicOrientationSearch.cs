using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void SearchCyclicOrientations(string directory) {
        Directory.CreateDirectory(directory);int oid=26;string configFile=Path.Combine(directory,"config.txt");if(File.Exists(configFile)){var config=File.ReadAllLines(configFile).Where(x=>x.Contains("=")).Select(x=>x.Split(new[]{'='},2)).ToDictionary(x=>x[0],x=>x[1]);oid=int.Parse(config["orbit"]);}
        string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis"),routing=Path.Combine(analysis,"orbit_routing","orbit"+oid);
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var at=new int[Count];var local=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<sizes[p];i++){at[pieces[p][i]]=p;local[pieces[p][i]]=i;}
        var raw=File.ReadAllBytes(Path.Combine(analysis,"pieces","transport_base_to_slot.bytes.bin"));int offset=0;
        var transport=new byte[pieces.Length][];var inverse=new byte[pieces.Length][];
        for(int p=0;p<pieces.Length;p++){transport[p]=new byte[sizes[p]];Buffer.BlockCopy(raw,offset,transport[p],0,sizes[p]);offset+=sizes[p];inverse[p]=Invert(transport[p]);}
        var chrRow=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_characters.tsv")).Skip(1).Select(s=>s.Split('\t')).Single(s=>int.Parse(s[0])==oid);
        if(chrRow[2]!="5" || chrRow[3]!="1" || chrRow[4]!="5")throw new Exception("Requires faithful cyclic5 character");
        var character=chrRow[5].Split(',').Select(s=>s.Split(':')).ToDictionary(s=>s[0],s=>int.Parse(s[1]));
        var meta=File.ReadAllLines(Path.Combine(routing,"routing_metadata.txt")).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
        var allowed=new HashSet<int>(meta["allowed_collateral"].Split(',').Select(int.Parse));
        var slots=ReadInts(Path.Combine(routing,"orbit_slots.int32.bin"));var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slots.Length;i++)ordinal[slots[i]]=i;
        var roots=meta["primitive_ordinal_cycle"].Split(',').Select(int.Parse).ToArray();
        var primitive=File.ReadAllText(Path.Combine(routing,"primitive_word.txt")).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var route=new CompoundTree[3];for(int stage=0;stage<3;stage++){var tree=new CompoundTree{Parent=new int[slots.Length],Cost=new int[slots.Length],TowardsRoot=new int[slots.Length][][]};foreach(string line in File.ReadAllLines(Path.Combine(routing,"routing_stage_"+stage+".tsv")).Skip(1)){var f=line.Split('\t');int p=int.Parse(f[0]);tree.Parent[p]=int.Parse(f[1]);tree.Cost[p]=int.Parse(f[2]);if(f.Length>3 && f[3].Length>0)tree.TowardsRoot[p]=f[3].Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();}route[stage]=tree;}
        var destination=new Dictionary<int,int>[4200];var delta=new Dictionary<int,int>[4200];var generators=new int[4200][];
        PieceActions(at,local,sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);destination[gid]=new Dictionary<int,int>();delta[gid]=new Dictionary<int,int>();generators[gid]=new[]{axis,twist,1,mask};int sum=0;
            foreach(int p in touched)if(orbit[p]==oid){int q=target[p],x=ordinal[p];if(q!=p)destination[gid][x]=ordinal[q];int d=character[Convert.ToBase64String(Compose(inverse[q],Compose(maps[p],transport[p])))];if(d!=0)delta[gid][x]=d;sum=(sum+d)%5;}
            if(sum!=0)throw new Exception("Generator violates orientation sum");
        });
        Action<int[],int[],int[]> apply=(positions,bits,m)=>{int gid=(m[0]*7+m[1])*2+(m[3]==4?1:0);for(int n=0;n<(m[2]%Order(m[1])+Order(m[1]))%Order(m[1]);n++)for(int i=0;i<positions.Length;i++){int p=positions[i],d,q;if(delta[gid].TryGetValue(p,out d))bits[i]=(bits[i]+d)%5;if(destination[gid].TryGetValue(p,out q))positions[i]=q;}};
        var candidates=new List<Tuple<int,int[][],string>>();var patterns=new Dictionary<string,int>();
        using(var w=new StreamWriter(Path.Combine(directory,"stabilizers.tsv"))){w.WriteLine("generator_id\tturns\tpattern");for(int gid=0;gid<4200;gid++){
            var positions=(int[])roots.Clone();var bits=new int[3];var word=new List<int[]>{generators[gid]};apply(positions,bits,generators[gid]);
            for(int stage=0;stage<3;stage++){foreach(var m in TreePath(route[stage],positions[stage])){word.Add(m);apply(positions,bits,m);}for(int j=0;j<=stage;j++)if(positions[j]!=roots[j])throw new Exception("Restore failed");}
            string pattern=String.Join(",",bits);if(!patterns.ContainsKey(pattern))patterns[pattern]=0;patterns[pattern]++;w.WriteLine("{0}\t{1}\t{2}",gid,word.Count,pattern);
            if(bits.Distinct().Count()==2)candidates.Add(Tuple.Create(gid,word.ToArray(),pattern));
        }}
        var selected=candidates.OrderBy(c=>c.Item2.Length).ThenBy(c=>c.Item1).Take(10).ToArray();var records=new List<string>();
        for(int index=0;index<selected.Length;index++){
            var h=selected[index].Item2;var word=primitive.Concat(h).Concat(InvertMoves(primitive)).Concat(InvertMoves(h)).ToArray();
            var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in word)ApplyInPlace(labels,m);
            var changed=new Dictionary<int,int>();var collateral=new HashSet<int>();
            for(int p=0;p<pieces.Length;p++){
                if(pieces[p].All(pos=>labels[pos]==pos))continue;
                if(orbit[p]!=oid){if(!allowed.Contains(orbit[p]))throw new Exception("Forbidden collateral");collateral.Add(orbit[p]);continue;}
                var map=new byte[sizes[p]];foreach(int dest in pieces[p]){int source=labels[dest];if(at[source]!=p)throw new Exception("Orientation primitive moves piece");map[local[source]]=(byte)local[dest];}
                int d=character[Convert.ToBase64String(Compose(inverse[p],Compose(map,transport[p])))];if(d==0)throw new Exception("Nonfaithful character");changed[p]=d;
            }
            if(changed.Count!=2 || changed.Values.Sum()%5!=0)throw new Exception("Not opposite paired twists");
            string name="candidate_"+(index+1).ToString("D2");File.WriteAllText(Path.Combine(directory,name+".word.txt"),String.Join(" ",word.Select(m=>String.Join(":",m)))+Environment.NewLine);
            records.Add(name+"\t"+selected[index].Item1+"\t"+word.Length+"\t"+String.Join(",",changed.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+":"+kv.Value))+"\t"+String.Join(",",collateral.OrderBy(o=>o)));
        }
        File.WriteAllLines(Path.Combine(directory,"candidates.tsv"),new[]{"candidate\tgenerator_id\tturns\tpiece_residues\tcollateral"}.Concat(records));
        File.WriteAllLines(Path.Combine(directory,"summary.txt"),new[]{"orbit="+oid,"stabilizers_tested=4200","all_generator_sums_zero=True","useful_patterns="+candidates.Count,"full_label_candidates="+selected.Length,"pattern_counts="+String.Join(";",patterns.Select(kv=>kv.Key+":"+kv.Value)),"checks="+(selected.Length>0?"PASS":"NO_CANDIDATE")});
        Console.WriteLine("Cyclic5 search:4200 stabilizers, {0} useful patterns, {1} full-label opposite pairs",candidates.Count,selected.Length);
    }
}}
