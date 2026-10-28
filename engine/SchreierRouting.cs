using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    struct CompoundEdge {internal int Destination;internal int[][] Word;}
    class CompoundTree {internal int[] Parent,Cost;internal int[][][] TowardsRoot;}
    int[][] InvertMoves(int[][] word) {return word.Reverse().Select(m=>new[]{m[0],m[1],(Order(m[1])-m[2]%Order(m[1]))%Order(m[1]),m[3]}).ToArray();}
    CompoundTree MakeTree(List<CompoundEdge>[] graph,int root) {
        var r=new CompoundTree{Parent=Enumerable.Repeat(-1,graph.Length).ToArray(),Cost=new int[graph.Length],TowardsRoot=new int[graph.Length][][]};
        r.Parent[root]=root;var queue=new Queue<int>();queue.Enqueue(root);
        while(queue.Count>0){int p=queue.Dequeue();foreach(var e in graph[p])if(r.Parent[e.Destination]<0){int q=e.Destination;r.Parent[q]=p;r.Cost[q]=r.Cost[p]+e.Word.Length;r.TowardsRoot[q]=InvertMoves(e.Word);queue.Enqueue(q);}}
        return r;
    }
    static int[][] TreePath(CompoundTree tree,int x) {
        var result=new List<int[]>();if(tree.Parent[x]<0)throw new Exception("Unreachable tree point");
        while(tree.Parent[x]!=x){result.AddRange(tree.TowardsRoot[x]);x=tree.Parent[x];}return result.ToArray();
    }
    static int[] ReadInts(string path) {using(var r=new BinaryReader(File.OpenRead(path))){var result=new int[r.BaseStream.Length/4];for(int i=0;i<result.Length;i++)result[i]=r.ReadInt32();return result;}}
    public void TestSchreierRouting(string directory) {
        Directory.CreateDirectory(directory);string analysis=Path.GetDirectoryName(Path.GetFullPath(directory)),old=Path.Combine(analysis,"isolated_routing");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var slot=ReadInts(Path.Combine(old,"orbit_slots.int32.bin"));var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slot.Length;i++)ordinal[slot[i]]=i;
        var metadata=File.ReadAllLines(Path.Combine(old,"routing_metadata.txt")).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
        var roots=metadata["primitive_ordinal_cycle"].Split(',').Select(int.Parse).ToArray();int oid=int.Parse(metadata["piece_orbit"]),a=slot[roots[0]],b=slot[roots[1]];
        var groupRow=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_groups.tsv")).Skip(1).Select(s=>s.Split('\t')).Single(s=>int.Parse(s[0])==oid);
        if(groupRow[2]!="1")throw new Exception("This routing representation requires a trivial local orientation image");
        var primitive=File.ReadAllText(Path.Combine(old,"primitive_word.txt")).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var route=new CompoundTree[3];
        for(int stage=0;stage<2;stage++) {
            var tree=new CompoundTree{Parent=new int[slot.Length],Cost=new int[slot.Length],TowardsRoot=new int[slot.Length][][]};
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(old,"routing_stage_"+stage+".bin"))))for(int i=0;i<slot.Length;i++){tree.Parent[i]=r.ReadInt32();tree.Cost[i]=r.ReadInt32();var move=new int[4];for(int j=0;j<4;j++)move[j]=r.ReadInt32();if(tree.Parent[i]>=0 && tree.Parent[i]!=i)tree.TowardsRoot[i]=new[]{move};}
            route[stage]=tree;
        }
        var graph=Enumerable.Range(0,slot.Length).Select(i=>new List<CompoundEdge>()).ToArray();var cache=new Dictionary<int,int>[4200];var elementary=new int[4200][][];var aFixers=new List<int>();
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);var word=new[]{new[]{axis,twist,1,mask}};elementary[gid]=word;cache[gid]=new Dictionary<int,int>();
            foreach(int p in touched)if(orbit[p]==oid && target[p]!=p)cache[gid][ordinal[p]]=ordinal[target[p]];
            bool fa=target[a]<0 || target[a]==a,fb=target[b]<0 || target[b]==b;if(fa)aFixers.Add(gid);
            if(fa && fb)foreach(var edge in cache[gid])graph[edge.Key].Add(new CompoundEdge{Destination=edge.Value,Word=word});
        });
        Func<int,int[][],int> image=(p,word)=>{foreach(var m in word){int gid=(m[0]*7+m[1])*2+(m[3]==4?1:0),turns=(m[2]%Order(m[1])+Order(m[1]))%Order(m[1]);for(int k=0;k<turns;k++){int q;if(cache[gid].TryGetValue(p,out q))p=q;}}return p;};
        route[2]=MakeTree(graph,roots[2]);var compounds=new List<int[][]>();long tested=0;
        var missing=Enumerable.Range(0,slot.Length).Where(x=>route[2].Parent[x]<0 && x!=roots[0] && x!=roots[1]).ToList();
        var transports=new List<int>(missing);var random=new Random(2994);while(transports.Count<100){int x=random.Next(slot.Length);if(x!=roots[0] && !transports.Contains(x))transports.Add(x);}
        foreach(int x in transports) {
            var tx=InvertMoves(TreePath(route[1],x));
            foreach(int gid in aFixers) {
                if(missing.Count==0)break;tested++;int gx=image(x,elementary[gid]);var word=tx.Concat(elementary[gid]).Concat(TreePath(route[1],gx)).ToArray();
                if(!missing.Any(p=>route[2].Parent[image(p,word)]>=0))continue;
                if(image(roots[0],word)!=roots[0] || image(roots[1],word)!=roots[1])throw new Exception("Schreier connector moves anchor");
                var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)labels=Apply(labels,move);foreach(int p in pieces[a].Concat(pieces[b]))if(labels[p]!=p)throw new Exception("Connector rotates anchor piece");
                for(int p=0;p<slot.Length;p++){int q=image(p,word);if(p!=q)graph[p].Add(new CompoundEdge{Destination=q,Word=word});}
                compounds.Add(word);route[2]=MakeTree(graph,roots[2]);missing=Enumerable.Range(0,slot.Length).Where(p=>route[2].Parent[p]<0 && p!=roots[0] && p!=roots[1]).ToList();
                Console.WriteLine("Added {0}-turn Schreier connector; component {1}/{2}",word.Length,route[2].Parent.Count(p=>p>=0),slot.Length-2);
            }
            if(missing.Count==0)break;
        }
        for(int stage=0;stage<3;stage++)using(var w=new StreamWriter(Path.Combine(directory,"routing_stage_"+stage+".tsv"))) {
            w.WriteLine("slot_ordinal\tparent_ordinal\tliteral_cost_to_root\tword_to_parent");
            for(int p=0;p<slot.Length;p++){var word=route[stage].TowardsRoot[p];if(word!=null && image(p,word)!=route[stage].Parent[p])throw new Exception("Invalid predecessor path");w.WriteLine("{0}\t{1}\t{2}\t{3}",p,route[stage].Parent[p],route[stage].Cost[p],word==null?"":String.Join(" ",word.Select(m=>String.Join(":",m))));}
        }
        WriteInts(Path.Combine(directory,"orbit_slots.int32.bin"),slot);File.WriteAllText(Path.Combine(directory,"primitive_word.txt"),String.Join(" ",primitive.Select(m=>String.Join(":",m))));
        File.WriteAllLines(Path.Combine(directory,"schreier_connectors.txt"),compounds.Select(word=>String.Join(" ",word.Select(m=>String.Join(":",m)))));
        File.WriteAllLines(Path.Combine(directory,"routing_metadata.txt"),new[]{"piece_orbit="+oid,"slots="+slot.Length,"primitive_piece_cycle="+metadata["primitive_piece_cycle"],"primitive_ordinal_cycle="+String.Join(",",roots),"connected_stabilizer_graphs="+(missing.Count==0),"schreier_candidates_tested="+tested,"connectors="+compounds.Count});
        if(missing.Count>0){Console.WriteLine("Bound reached with {0} stranded slots; routing remains incomplete",missing.Count);return;}
        var rng=new Random(293600);var costs=new List<int>();
        var oldThird=ReadInts(Path.Combine(old,"routing_stage_2.bin"));
        var stranded=Enumerable.Range(0,slot.Length).Where(p=>oldThird[6*p]<0 && p!=roots[0] && p!=roots[1]).ToArray();
        using(var w=new StreamWriter(Path.Combine(directory,"random_triple_tests.tsv"))) {
            w.WriteLine("triple_piece_ids\tsetup_turns\ttotal_turns\tchecks");
            for(int test=0;test<20+stranded.Length;test++) {
                var current=new List<int>();
                if(test>=20)current.AddRange(new[]{slot[roots[0]],slot[roots[1]],slot[stranded[test-20]]});
                else while(current.Count<3){int p=slot[rng.Next(slot.Length)];if(!current.Contains(p))current.Add(p);}
                var original=current.ToArray();var setup=new List<int[]>();
                for(int stage=0;stage<3;stage++) {
                    foreach(var move in TreePath(route[stage],ordinal[current[stage]])){setup.Add(move);var perm=Permutation(move[0],move[1],move[2],move[3]);for(int j=0;j<3;j++)current[j]=pieceAt[perm[pieces[current[j]][0]]];}
                    for(int j=0;j<=stage;j++)if(current[j]!=slot[roots[j]])throw new Exception("Setup stage fails to restore frozen slots");
                }
                if(!current.SequenceEqual(roots.Select(p=>slot[p])))throw new Exception("Setup misses target triple");
                var full=setup.Concat(primitive).Concat(InvertMoves(setup.ToArray())).ToArray();var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in full)labels=Apply(labels,move);
                var actual=new Dictionary<int,int>();for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest)actual[pieceAt[labels[dest]]]=pieceAt[dest];
                if(actual.Count!=3 || actual[original[0]]!=original[1] || actual[original[1]]!=original[2] || actual[original[2]]!=original[0])throw new Exception("Incorrect isolated conjugate");
                costs.Add(full.Length);w.WriteLine("{0}\t{1}\t{2}\tPASS",String.Join(",",original),setup.Count,full.Length);
            }
        }
        Console.WriteLine("PASS: all ordered triples routable; 20 random plus {3} stranded-slot full-label conjugates checked; cost {0}..{1} turns; Schreier candidates tested {2}",costs.Min(),costs.Max(),tested,stranded.Length);
    }
}}
