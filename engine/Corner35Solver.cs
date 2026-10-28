using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
namespace _3dedit {
partial class MoveEngine {
    class CornerModel {
        internal int[][] Pieces,Destination;
        internal int[] Orbits,At,Local,Sizes,Slots,Ordinal;
        internal byte[][][] Maps;
        internal int Root,Orbit=35,Degree=20;
        internal HashSet<int> Allowed=new HashSet<int>(Enumerable.Range(24,11));
    }
    class CornerElement { internal byte[] Map; internal int[][] Word; }
    static int[][] CornerWord(string path) {return File.ReadAllText(path).Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split(':').Select(int.Parse).ToArray()).ToArray();}
    static string CornerWordText(int[][] word) {return String.Join(" ",word.Select(m=>String.Join(":",m)));}
    CornerModel LoadCornerModel(int targetOrbit=35) {
        string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(x=>x.Split('\t')).ToArray();
        var c=new CornerModel();c.Orbit=targetOrbit;c.Degree=targetOrbit==35?20:5;if(targetOrbit==4)c.Allowed=new HashSet<int>(Enumerable.Range(2,34).Where(o=>o!=4));c.Pieces=rows.Select(x=>x[2].Split(',').Select(int.Parse).ToArray()).ToArray();c.Orbits=rows.Select(x=>int.Parse(x[1])).ToArray();c.Sizes=c.Pieces.Select(x=>x.Length).ToArray();c.At=new int[Count];c.Local=new int[Count];
        for(int p=0;p<c.Pieces.Length;p++)for(int i=0;i<c.Sizes[p];i++){c.At[c.Pieces[p][i]]=p;c.Local[c.Pieces[p][i]]=i;}
        c.Slots=Enumerable.Range(0,c.Pieces.Length).Where(p=>c.Orbits[p]==targetOrbit).ToArray();if(c.Slots.Length!=(targetOrbit==35?120:720) || c.Slots.Any(p=>c.Sizes[p]!=c.Degree))throw new Exception("Unexpected corners");
        c.Ordinal=Enumerable.Repeat(-1,c.Pieces.Length).ToArray();for(int i=0;i<c.Slots.Length;i++)c.Ordinal[c.Slots[i]]=i;
        if(targetOrbit==35){var assessment=File.ReadAllLines(Path.Combine(analysis,"corner35_assessment","summary.txt")).Select(x=>x.Split('=')).ToDictionary(x=>x[0],x=>x[1]);c.Root=c.Ordinal[int.Parse(assessment["root_piece"])];}
        else {var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in CornerWord(Path.Combine(analysis,"primitives","orbit4_rotation_short","primitive_word.txt")))ApplyInPlace(labels,m);var changed=c.Slots.Where(p=>c.Pieces[p].Any(x=>labels[x]!=x)).ToArray();if(changed.Length!=1)throw new Exception("Kernel root witness not single piece");c.Root=c.Ordinal[changed[0]];}
        c.Destination=new int[4200][];c.Maps=new byte[4200][][];
        PieceActions(c.At,c.Local,c.Sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);c.Destination[gid]=Enumerable.Range(0,c.Slots.Length).ToArray();c.Maps[gid]=new byte[c.Slots.Length][];
            foreach(int p in touched)if(c.Orbits[p]==targetOrbit){int x=c.Ordinal[p];c.Destination[gid][x]=c.Ordinal[target[p]];c.Maps[gid][x]=(byte[])maps[p].Clone();}
        });return c;
    }
    void CornerAdvance(CornerModel c,ref int slot,ref byte[] map,int[] move) {
        int gid=(move[0]*7+move[1])*2+(move[3]==4?1:0),turns=(move[2]%Order(move[1])+Order(move[1]))%Order(move[1]);
        for(int n=0;n<turns;n++){var local=c.Maps[gid][slot];if(local!=null)map=Compose(local,map);slot=c.Destination[gid][slot];}
    }
    byte[] CornerExtract(CornerModel c,int slot,int[] labels) {
        int piece=c.Slots[slot];var map=new byte[c.Degree];foreach(int dest in c.Pieces[piece]){int source=labels[dest];if(c.At[source]!=piece)throw new Exception("Corner changes position");map[c.Local[source]]=(byte)c.Local[dest];}return map;
    }
    void CheckCornerLabelWord(CornerModel c,int slot,byte[] expected,int[][] word) {
        var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)ApplyInPlace(labels,move);
        if(!CornerExtract(c,slot,labels).SequenceEqual(expected))throw new Exception("Corner label orientation differs");
        for(int pos=0;pos<Count;pos++)if(labels[pos]!=pos){int p=c.At[pos];if(c.Orbits[p]==c.Orbit){if(p!=c.Slots[slot])throw new Exception("Other corner changed");}else if(!c.Allowed.Contains(c.Orbits[p]))throw new Exception("Forbidden corner collateral");}
    }
    CompoundTree CornerRoutes(CornerModel c) {
        var tree=new CompoundTree{Parent=Enumerable.Repeat(-1,c.Slots.Length).ToArray(),Cost=new int[c.Slots.Length],TowardsRoot=new int[c.Slots.Length][][]};tree.Parent[c.Root]=c.Root;tree.TowardsRoot[c.Root]=new int[0][];var queue=new Queue<int>();queue.Enqueue(c.Root);
        while(queue.Count>0){int p=queue.Dequeue();for(int gid=0;gid<4200;gid++){int q=c.Destination[gid][p];if(tree.Parent[q]>=0)continue;int axis=gid/14,twist=(gid/2)%7,mask=gid%2==0?1:4;tree.Parent[q]=p;tree.Cost[q]=tree.Cost[p]+1;tree.TowardsRoot[q]=new[]{new[]{axis,twist,Order(twist)-1,mask}};queue.Enqueue(q);}}
        if(tree.Parent.Any(p=>p<0))throw new Exception("Corner orientation transport incomplete");return tree;
    }
    public void PrepareCorner35Orientations(string directory) {
        Directory.CreateDirectory(directory);var c=LoadCornerModel();string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var seeds=new List<CornerElement>();foreach(string path in new[]{Path.Combine(analysis,"primitives","orbit35","primitive_word.txt"),Path.Combine(analysis,"corner35_assessment","second_rotation.word.txt")}){
            var word=CornerWord(path);var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)ApplyInPlace(labels,move);var map=CornerExtract(c,c.Root,labels);
            seeds.Add(new CornerElement{Map=map,Word=word});seeds.Add(new CornerElement{Map=Invert(map),Word=InvertMoves(word)});
        }
        var id=new CornerElement{Map=Identity(20),Word=new int[0][]};var library=new Dictionary<string,CornerElement>{{Convert.ToBase64String(id.Map),id}};var queue=new Queue<CornerElement>();queue.Enqueue(id);
        while(queue.Count>0){var current=queue.Dequeue();foreach(var seed in seeds.OrderBy(e=>e.Word.Length)){var map=Compose(seed.Map,current.Map);string key=Convert.ToBase64String(map);if(library.ContainsKey(key))continue;var next=new CornerElement{Map=map,Word=current.Word.Concat(seed.Word).ToArray()};library[key]=next;queue.Enqueue(next);if(library.Count>60)throw new Exception("Corner group exceeds60");}}
        if(library.Count!=60)throw new Exception("Corner orientation library incomplete");
        var bytes=File.ReadAllBytes(Path.Combine(analysis,"pieces","transport_base_to_slot.bytes.bin"));int offset=c.Sizes.Take(c.Slots[c.Root]).Sum();var transport=new byte[20];Buffer.BlockCopy(bytes,offset,transport,0,20);
        var group=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_groups.tsv")).Skip(1).Select(x=>x.Split('\t')).Single(x=>x[0]=="35");
        var expected=new HashSet<string>(group[3].Split(',').Select(Convert.FromBase64String).Select(p=>Convert.ToBase64String(Compose(transport,Compose(p,Invert(transport))))));
        if(!expected.SetEquals(library.Keys))throw new Exception("Library differs from validated local group");
        using(var w=new StreamWriter(Path.Combine(directory,"orientation_library.tsv"))){w.WriteLine("permutation_base64\tturns\tword");foreach(var kv in library){CheckCornerLabelWord(c,c.Root,kv.Value.Map,kv.Value.Word);w.WriteLine("{0}\t{1}\t{2}",kv.Key,kv.Value.Word.Length,CornerWordText(kv.Value.Word));}}
        var routes=CornerRoutes(c);WriteInts(Path.Combine(directory,"orbit_slots.int32.bin"),c.Slots);
        using(var w=new StreamWriter(Path.Combine(directory,"orientation_routes.tsv"))){w.WriteLine("slot\tparent\tcost\tword_to_parent");for(int p=0;p<120;p++)w.WriteLine("{0}\t{1}\t{2}\t{3}",p,routes.Parent[p],routes.Cost[p],CornerWordText(routes.TowardsRoot[p]));}
        using(var w=new StreamWriter(Path.Combine(directory,"label_transport_checks.tsv"))){w.WriteLine("slot\tsetup_turns\tchecks");for(int p=0;p<120;p++){
            var setup=TreePath(routes,p);int current=p;byte[] h=Identity(20);foreach(var move in setup)CornerAdvance(c,ref current,ref h,move);if(current!=c.Root)throw new Exception("Transport misses root");
            var seed=seeds[0];var expectedMap=Compose(Invert(h),Compose(seed.Map,h));var word=setup.Concat(seed.Word).Concat(InvertMoves(setup)).ToArray();CheckCornerLabelWord(c,p,expectedMap,word);w.WriteLine("{0}\t{1}\tPASS",p,setup.Length);
        }}
        File.WriteAllLines(Path.Combine(directory,"metadata.txt"),new[]{"orbit=35","root_piece="+c.Slots[c.Root],"root_ordinal="+c.Root,"group_order=60","root_full_label_checks=60","slot_full_label_checks=120","max_setup_turns="+routes.Cost.Max(),"max_library_turns="+library.Values.Max(e=>e.Word.Length),"allowed_collateral=24,25,26,27,28,29,30,31,32,33,34","checks=PASS"});
        Console.WriteLine("PASS corner35 orientations: complete60-state library;60 root and120 transported full-label checks; max setup {0}, max library {1}",routes.Cost.Max(),library.Values.Max(e=>e.Word.Length));
    }
    public void RenderCorner35Orientations(string directory) {
        var c=LoadCornerModel();string libraryDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis","corner35_orientations");
        var library=File.ReadAllLines(Path.Combine(libraryDir,"orientation_library.tsv")).Skip(1).Select(x=>x.Split('\t')).ToDictionary(x=>x[0],x=>x.Length<3 || x[2].Length==0?new int[0][]:x[2].Split(' ').Select(m=>m.Split(':').Select(int.Parse).ToArray()).ToArray());if(library.Count!=60)throw new Exception("Incomplete orientation library");
        var routes=CornerRoutes(c);var allowed=new HashSet<int>(Enumerable.Range(24,11));
        using(var report=new StreamWriter(Path.Combine(directory,"render_results.tsv"))){report.WriteLine("case\tcycles\traw_turns\treduced_turns\tsimulation_seconds\torbit_wrong_colors\tother_changed_colors\tallowed_collateral_changed_colors\torbit_wrong_identities\torientation_mode");
        foreach(string line in File.ReadAllLines(Path.Combine(directory,"jobs.tsv"))){var fields=line.Split('\t');string name=fields[0];var initial=ReadInts(fields[1]);if(initial.Length!=Count)throw new Exception("Invalid state length");var state=(int[])initial.Clone();var moves=new List<int[]>();int corrections=0;long rawTurns=0;var watch=Stopwatch.StartNew();
            for(int slot=0;slot<120;slot++){
                int piece=c.Slots[slot];var destinations=c.Pieces[piece].Select((pos,i)=>new{Color=pos/433,Index=i}).ToDictionary(x=>x.Color,x=>x.Index);var correction=new byte[20];
                for(int i=0;i<20;i++){int index;if(!destinations.TryGetValue(state[c.Pieces[piece][i]],out index))throw new Exception("Corner identities must be solved before orientations");correction[i]=(byte)index;}
                if(correction.SequenceEqual(Identity(20)))continue;if(correction.Distinct().Count()!=20)throw new Exception("Repeated corner colors");
                var setup=TreePath(routes,slot);int current=slot;var h=Identity(20);foreach(var move in setup)CornerAdvance(c,ref current,ref h,move);if(current!=c.Root)throw new Exception("Bad setup");
                string key=Convert.ToBase64String(Compose(h,Compose(correction,Invert(h))));int[][] rootWord;if(!library.TryGetValue(key,out rootWord))throw new Exception("Corner orientation outside allowed60-state group");
                var word=setup.Concat(rootWord).Concat(InvertMoves(setup)).ToArray();rawTurns+=word.Length;foreach(var move in word){AppendReduced(moves,move);ApplyInPlace(state,move);}corrections++;
            }
            watch.Stop();int wrong=0,other=0,collateral=0;for(int pos=0;pos<Count;pos++){int o=c.Orbits[c.At[pos]];if(o==35){if(state[pos]!=pos/433)wrong++;}else if(state[pos]!=initial[pos]){if(allowed.Contains(o))collateral++;else other++;}}
            if(wrong!=0 || other!=0)throw new Exception("Corner orientation solver final check fails");
            // Replay the reduced emitted word independently of correction generation.
            var replay=(int[])initial.Clone();foreach(var move in moves)ApplyInPlace(replay,move);if(!replay.SequenceEqual(state))throw new Exception("Reduced word replay differs");
            WriteInts(Path.Combine(directory,name+".final.int32.bin"),state);using(var w=new BinaryWriter(File.Create(Path.Combine(directory,name+".moves.int32.bin"))))foreach(var move in moves)foreach(int value in move)w.Write(value);
            report.WriteLine("{0}\t{1}\t{2}\t{3}\t{4:F6}\t0\t0\t{5}\t0\tcorner35_orientations",name,corrections,rawTurns,moves.Count,watch.Elapsed.TotalSeconds,collateral);report.Flush();Console.WriteLine("PASS corner35 orientations {0}: {1} corners, {2} turns; forbidden changes0",name,corrections,moves.Count);
        }}
    }
}}
