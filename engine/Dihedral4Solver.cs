using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
namespace _3dedit {
partial class MoveEngine {
    public void PrepareDihedral4(string directory) {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"metadata.txt"),"checks=PENDING\n");
        var c=LoadCornerModel(4);string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var seedWord=CornerWord(Path.Combine(analysis,"primitives","orbit4_rotation_short","primitive_word.txt"));
        var seedLabels=Enumerable.Range(0,Count).ToArray();foreach(var m in seedWord)ApplyInPlace(seedLabels,m);var seedMap=CornerExtract(c,c.Root,seedLabels);
        var library=new Dictionary<string,CornerElement>();var map=Identity(5);var word=new List<int[]>();
        for(int k=0;k<5;k++){string key=Convert.ToBase64String(map);if(library.ContainsKey(key))throw new Exception("Kernel has order below5");var element=new CornerElement{Map=map,Word=word.ToArray()};CheckCornerLabelWord(c,c.Root,map,element.Word);library[key]=element;map=Compose(seedMap,map);word.AddRange(seedWord);}
        if(!map.SequenceEqual(Identity(5)))throw new Exception("Kernel order differs from5");
        var bytes=File.ReadAllBytes(Path.Combine(analysis,"pieces","transport_base_to_slot.bytes.bin"));int offset=c.Sizes.Take(c.Slots[c.Root]).Sum();var t=new byte[5];Buffer.BlockCopy(bytes,offset,t,0,5);
        var character=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_characters.tsv")).Skip(1).Select(x=>x.Split('\t')).Single(x=>x[0]=="4");
        var kernel=new HashSet<string>(character[5].Split(',').Where(x=>x.Split(':')[1]=="0").Select(x=>Convert.FromBase64String(x.Split(':')[0])).Select(p=>Convert.ToBase64String(Compose(t,Compose(p,Invert(t))))));if(!kernel.SetEquals(library.Keys))throw new Exception("Kernel library differs from derived subgroup");
        using(var w=new StreamWriter(Path.Combine(directory,"orientation_library.tsv"))){w.WriteLine("permutation_base64\tturns\tword");foreach(var kv in library)w.WriteLine("{0}\t{1}\t{2}",kv.Key,kv.Value.Word.Length,CornerWordText(kv.Value.Word));}
        var routes=CornerRoutes(c);
        using(var w=new StreamWriter(Path.Combine(directory,"label_transport_checks.tsv"))){w.WriteLine("slot\tsetup_turns\tchecks");for(int p=0;p<c.Slots.Length;p++){
            var setup=TreePath(routes,p);int current=p;var h=Identity(5);foreach(var m in setup)CornerAdvance(c,ref current,ref h,m);if(current!=c.Root)throw new Exception("Transport misses root");
            var expected=Compose(Invert(h),Compose(seedMap,h));CheckCornerLabelWord(c,p,expected,setup.Concat(seedWord).Concat(InvertMoves(setup)).ToArray());w.WriteLine("{0}\t{1}\tPASS",p,setup.Length);
        }}
        // Move the paired-reflection witness onto the first two position-routing roots.
        string routing=Path.Combine(analysis,"orbit_routing","orbit4");var meta=File.ReadAllLines(Path.Combine(routing,"routing_metadata.txt")).Select(x=>x.Split('=')).ToDictionary(x=>x[0],x=>x[1]);if(meta["conjugation_validation"]!="PASS")throw new Exception("Routing pending");var roots=meta["primitive_ordinal_cycle"].Split(',').Select(int.Parse).ToArray();
        var trees=new CompoundTree[3];for(int stage=0;stage<3;stage++){var tree=new CompoundTree{Parent=new int[c.Slots.Length],Cost=new int[c.Slots.Length],TowardsRoot=new int[c.Slots.Length][][]};foreach(string line in File.ReadAllLines(Path.Combine(routing,"routing_stage_"+stage+".tsv")).Skip(1)){var f=line.Split('\t');int p=int.Parse(f[0]);tree.Parent[p]=int.Parse(f[1]);tree.Cost[p]=int.Parse(f[2]);tree.TowardsRoot[p]=f.Length<4 || f[3].Length==0?new int[0][]:f[3].Split(' ').Select(x=>x.Split(':').Select(int.Parse).ToArray()).ToArray();}trees[stage]=tree;}
        var pairWord=CornerWord(Path.Combine(analysis,"core_power_filters","power_15.word.txt"));var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in pairWord)ApplyInPlace(labels,m);var pair=Enumerable.Range(0,c.Slots.Length).Where(p=>c.Pieces[c.Slots[p]].Any(x=>labels[x]!=x)).ToArray();if(pair.Length!=2)throw new Exception("Not a two-piece witness");
        var currentTriple=new[]{pair[0],pair[1],Enumerable.Range(0,c.Slots.Length).First(p=>!pair.Contains(p))};var setupPair=new List<int[]>();for(int stage=0;stage<3;stage++){foreach(var m in TreePath(trees[stage],currentTriple[stage])){setupPair.Add(m);for(int j=0;j<3;j++){int slot=currentTriple[j];var local=Identity(5);CornerAdvance(c,ref slot,ref local,m);currentTriple[j]=slot;}}if(currentTriple[stage]!=roots[stage])throw new Exception("Pair setup fails");}
        var aligned=InvertMoves(setupPair.ToArray()).Concat(pairWord).Concat(setupPair).ToArray();labels=Enumerable.Range(0,Count).ToArray();foreach(var m in aligned)ApplyInPlace(labels,m);
        var chars=character[5].Split(',').ToDictionary(x=>x.Split(':')[0],x=>int.Parse(x.Split(':')[1]));var transportOffset=new int[c.Pieces.Length];for(int p=1;p<c.Pieces.Length;p++)transportOffset[p]=transportOffset[p-1]+c.Sizes[p-1];
        for(int p=0;p<c.Slots.Length;p++){var actual=CornerExtract(c,p,labels);var baseMap=new byte[5];Buffer.BlockCopy(bytes,transportOffset[c.Slots[p]],baseMap,0,5);int bit=chars[Convert.ToBase64String(Compose(Invert(baseMap),Compose(actual,baseMap)))];if(bit!=((p==roots[0] || p==roots[1])?1:0) || (p!=roots[0] && p!=roots[1] && !actual.SequenceEqual(Identity(5))))throw new Exception("Aligned pair has wrong quotient/support");}
        for(int pos=0;pos<Count;pos++)if(labels[pos]!=pos && c.Orbits[c.At[pos]]!=4 && !c.Allowed.Contains(c.Orbits[c.At[pos]]))throw new Exception("Pair forbidden collateral");
        File.WriteAllText(Path.Combine(directory,"paired_reflection.word.txt"),CornerWordText(aligned));
        File.WriteAllLines(Path.Combine(directory,"metadata.txt"),new[]{"orbit=4","root_piece="+c.Slots[c.Root],"root_ordinal="+c.Root,"group_order=5","pair_root_indices=0,1","pair_turns="+aligned.Length,"root_full_label_checks=5","slot_full_label_checks=720","max_setup_turns="+routes.Cost.Max(),"allowed_collateral="+String.Join(",",c.Allowed.OrderBy(x=>x)),"checks=PASS"});
        Console.WriteLine("PASS orbit4:5 root/720 transported kernel label checks and aligned quotient pair; pair turns {0}",aligned.Length);
    }
    public void RenderDihedral4Kernel(string directory) {
        var c=LoadCornerModel(4);string libraryDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis","dihedral4_kernel");
        if(!File.ReadAllLines(Path.Combine(libraryDir,"metadata.txt")).Contains("checks=PASS"))throw new Exception("Kernel validation pending");
        var library=File.ReadAllLines(Path.Combine(libraryDir,"orientation_library.tsv")).Skip(1).Select(x=>x.Split('\t')).ToDictionary(x=>x[0],x=>x.Length<3 || x[2].Length==0?new int[0][]:x[2].Split(' ').Select(m=>m.Split(':').Select(int.Parse).ToArray()).ToArray());if(library.Count!=5)throw new Exception("Incomplete orientation library");
        var routes=CornerRoutes(c);var allowed=c.Allowed;
        using(var report=new StreamWriter(Path.Combine(directory,"render_results.tsv"))){report.WriteLine("case\tcycles\traw_turns\treduced_turns\tsimulation_seconds\torbit_wrong_colors\tother_changed_colors\tallowed_collateral_changed_colors\torbit_wrong_identities\torientation_mode");
        foreach(string line in File.ReadAllLines(Path.Combine(directory,"jobs.tsv"))){var fields=line.Split('\t');string name=fields[0];var initial=ReadInts(fields[1]);if(initial.Length!=Count)throw new Exception("Invalid state length");var state=(int[])initial.Clone();var moves=new List<int[]>();int corrections=0;long rawTurns=0;var watch=Stopwatch.StartNew();
            for(int slot=0;slot<c.Slots.Length;slot++){
                int piece=c.Slots[slot];var destinations=c.Pieces[piece].Select((pos,i)=>new{Color=pos/433,Index=i}).ToDictionary(x=>x.Color,x=>x.Index);var correction=new byte[5];
                for(int i=0;i<5;i++){int index;if(!destinations.TryGetValue(state[c.Pieces[piece][i]],out index))throw new Exception("Orbit4 identities must be solved before orientations");correction[i]=(byte)index;}
                if(correction.SequenceEqual(Identity(5)))continue;if(correction.Distinct().Count()!=5)throw new Exception("Repeated orbit4 colors");
                var setup=TreePath(routes,slot);int current=slot;var h=Identity(5);foreach(var move in setup)CornerAdvance(c,ref current,ref h,move);if(current!=c.Root)throw new Exception("Bad setup");
                string key=Convert.ToBase64String(Compose(h,Compose(correction,Invert(h))));int[][] rootWord;if(!library.TryGetValue(key,out rootWord))throw new Exception("Orbit4 orientation outside allowed five-state kernel group");
                var word=setup.Concat(rootWord).Concat(InvertMoves(setup)).ToArray();rawTurns+=word.Length;foreach(var move in word){AppendReduced(moves,move);ApplyInPlace(state,move);}corrections++;
            }
            watch.Stop();int wrong=0,other=0,collateral=0;for(int pos=0;pos<Count;pos++){int o=c.Orbits[c.At[pos]];if(o==4){if(state[pos]!=pos/433)wrong++;}else if(state[pos]!=initial[pos]){if(allowed.Contains(o))collateral++;else other++;}}
            if(wrong!=0 || other!=0)throw new Exception("Orbit4 orientation solver final check fails");
            // Replay the reduced emitted word independently of correction generation.
            var replay=(int[])initial.Clone();foreach(var move in moves)ApplyInPlace(replay,move);if(!replay.SequenceEqual(state))throw new Exception("Reduced word replay differs");
            WriteInts(Path.Combine(directory,name+".final.int32.bin"),state);using(var w=new BinaryWriter(File.Create(Path.Combine(directory,name+".moves.int32.bin"))))foreach(var move in moves)foreach(int value in move)w.Write(value);
            report.WriteLine("{0}\t{1}\t{2}\t{3}\t{4:F6}\t0\t0\t{5}\t0\tdihedral4_kernel",name,corrections,rawTurns,moves.Count,watch.Elapsed.TotalSeconds,collateral);report.Flush();Console.WriteLine("PASS orbit4 kernel {0}: {1} pieces, {2} turns; forbidden changes0",name,corrections,moves.Count);
        }}
    }
    public void ValidateDihedral4Words(string directory) {
        var c=LoadCornerModel(4);using(var report=new StreamWriter(Path.Combine(directory,"label_results.tsv"))){report.WriteLine("case\tchecks");
        foreach(string line in File.ReadAllLines(Path.Combine(directory,"label_jobs.tsv"))){var f=line.Split('\t');var flat=ReadInts(f[1]);if(flat.Length%4!=0)throw new Exception("Invalid word");var expected=ReadInts(f[2]);if(expected.Length!=Count)throw new Exception("Invalid expected labels");var state=Enumerable.Range(0,Count).ToArray();for(int i=0;i<flat.Length;i+=4)ApplyInPlace(state,new[]{flat[i],flat[i+1],flat[i+2],flat[i+3]});for(int pos=0;pos<Count;pos++){int o=c.Orbits[c.At[pos]];if(o==4 ? state[pos]!=expected[pos] : !c.Allowed.Contains(o) && state[pos]!=pos)throw new Exception("Full-label correction differs: "+f[0]+" pos="+pos);}report.WriteLine(f[0]+"\tPASS");report.Flush();Console.WriteLine("PASS orbit4 full-label correction "+f[0]);}
        }
    }

}}
