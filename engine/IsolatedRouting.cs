using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    struct RouteEdge {internal int Destination,Axis,Twist,Mask;}
    public void TestIsolatedRouting(string directory) {
        Directory.CreateDirectory(directory);
        string analysis=Path.GetDirectoryName(Path.GetFullPath(directory));
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(s=>int.Parse(s[1])).ToArray();
        var sizes=pieces.Select(p=>p.Length).ToArray();var pieceAt=new int[Count];var localAt=new int[Count];
        for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var best=File.ReadAllLines(Path.Combine(analysis,"final_word_depth","branch_1","best_words.tsv"))[1].Split('\t');
        var primitive=best[1].Split(' ').Select(t=>t.Split(':').Select(int.Parse).ToArray()).ToArray();
        var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in primitive)labels=Apply(labels,move);
        var pmap=new Dictionary<int,int>();for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest)pmap[pieceAt[labels[dest]]]=pieceAt[dest];
        if(pmap.Count!=3)throw new Exception("Primitive is not a three-piece cycle");
        int a=pmap.Keys.Min(),b=pmap[a],c=pmap[b];if(pmap[c]!=a)throw new Exception("Not a 3-cycle");int oid=orbit[a];
        var slot=Enumerable.Range(0,pieces.Length).Where(p=>orbit[p]==oid).ToArray();
        var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slot.Length;i++)ordinal[slot[i]]=i;
        var roots=new[]{ordinal[a],ordinal[b],ordinal[c]};
        var graphs=new List<RouteEdge>[3][];for(int stage=0;stage<3;stage++)graphs[stage]=Enumerable.Range(0,slot.Length).Select(i=>new List<RouteEdge>()).ToArray();
        var allowed=new int[3];
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            bool fixedA=target[a]<0 || target[a]==a,fixedB=target[b]<0 || target[b]==b;
            for(int stage=0;stage<3;stage++) {
                if(stage>=1 && !fixedA || stage>=2 && !fixedB)continue;allowed[stage]++;
                if(stage>=1 && target[a]>=0 && !maps[a].SequenceEqual(Identity(sizes[a])))throw new Exception("Frozen piece orientation moves");
                if(stage>=2 && target[b]>=0 && !maps[b].SequenceEqual(Identity(sizes[b])))throw new Exception("Frozen piece orientation moves");
                foreach(int p in touched)if(orbit[p]==oid && target[p]!=p)graphs[stage][ordinal[p]].Add(new RouteEdge{Destination=ordinal[target[p]],Axis=axis,Twist=twist,Mask=mask});
            }
        });
        var parent=new int[3][];var routeMove=new int[3][][];var depth=new int[3][];bool connected=true;
        for(int stage=0;stage<3;stage++) {
            parent[stage]=Enumerable.Repeat(-1,slot.Length).ToArray();depth[stage]=new int[slot.Length];routeMove[stage]=new int[slot.Length][];
            int root=roots[stage];parent[stage][root]=root;var queue=new Queue<int>();queue.Enqueue(root);
            while(queue.Count>0){int p=queue.Dequeue();foreach(var edge in graphs[stage][p])if(parent[stage][edge.Destination]<0){int q=edge.Destination;parent[stage][q]=p;depth[stage][q]=depth[stage][p]+1;routeMove[stage][q]=new[]{edge.Axis,edge.Twist,Order(edge.Twist)-1,edge.Mask};queue.Enqueue(q);}}
            int reached=parent[stage].Count(p=>p>=0);if(reached!=slot.Length-stage)connected=false;
            Console.WriteLine("Routing stage {0}: {1} allowed generators; root component {2}/{3}; max path {4}",stage,allowed[stage],reached,slot.Length-stage,depth[stage].Max());
            using(var w=new BinaryWriter(File.Create(Path.Combine(directory,"routing_stage_"+stage+".bin"))))for(int i=0;i<slot.Length;i++){w.Write(parent[stage][i]);w.Write(depth[stage][i]);var move=routeMove[stage][i]??new int[4];foreach(int x in move)w.Write(x);}
        }
        WriteInts(Path.Combine(directory,"orbit_slots.int32.bin"),slot);
        File.WriteAllText(Path.Combine(directory,"primitive_word.txt"),best[1]+Environment.NewLine);
        File.WriteAllLines(Path.Combine(directory,"routing_metadata.txt"),new[]{"piece_orbit="+oid,"slots="+slot.Length,"primitive_piece_cycle="+a+","+b+","+c,"primitive_ordinal_cycle="+String.Join(",",roots),"connected_stabilizer_graphs="+connected});
        if(!connected){Console.WriteLine("Routing feasibility failed; no arbitrary-triple claim");return;}
        var random=new Random(293600);var costs=new List<int>();
        using(var w=new StreamWriter(Path.Combine(directory,"random_triple_tests.tsv"))) {
            w.WriteLine("triple_piece_ids\tsetup_turns\ttotal_turns\tchecks");
            for(int test=0;test<20;test++) {
                var current=new List<int>();while(current.Count<3){int p=slot[random.Next(slot.Length)];if(!current.Contains(p))current.Add(p);}var original=current.ToArray();var setup=new List<int[]>();
                for(int stage=0;stage<3;stage++) {
                    int position=ordinal[current[stage]];
                    while(position!=roots[stage]) {
                        var move=routeMove[stage][position];setup.Add(move);
                        var perm=Permutation(move[0],move[1],move[2],move[3]);
                        for(int j=0;j<3;j++)current[j]=pieceAt[perm[pieces[current[j]][0]]];
                        position=ordinal[current[stage]];
                        for(int j=0;j<stage;j++)if(current[j]!=slot[roots[j]])throw new Exception("Setup moved a frozen piece");
                    }
                }
                var reverse=setup.AsEnumerable().Reverse().Select(m=>new[]{m[0],m[1],(Order(m[1])-m[2])%Order(m[1]),m[3]}).ToArray();
                var word=setup.Concat(primitive).Concat(reverse).ToArray();labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)labels=Apply(labels,move);
                var actual=new Dictionary<int,int>();for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest)actual[pieceAt[labels[dest]]]=pieceAt[dest];
                if(actual.Count!=3 || actual[original[0]]!=original[1] || actual[original[1]]!=original[2] || actual[original[2]]!=original[0])throw new Exception("Conjugate did not realize target triple");
                costs.Add(word.Length);w.WriteLine("{0}\t{1}\t{2}\tPASS",String.Join(",",original),setup.Count,word.Length);
            }
        }
        Console.WriteLine("PASS: isolated cycle routed to 20 random ordered triples; full-label checks passed; literal turns {0}..{1}",costs.Min(),costs.Max());
    }
}}
