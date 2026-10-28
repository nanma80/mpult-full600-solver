using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void PrepareOrbitRouting(string directory) {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"routing_metadata.txt"),"conjugation_validation=PENDING\nconnected_stabilizer_graphs=False\n");string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var config=File.ReadAllLines(Path.Combine(directory,"config.txt")).Where(s=>s.Length>0).Select(s=>s.Split(new[]{'='},2)).ToDictionary(s=>s[0],s=>s[1]);
        int maxTargeted=config.ContainsKey("max_targeted_cases")?int.Parse(config["max_targeted_cases"]):64;if(maxTargeted<1 || maxTargeted>7200)throw new Exception("Invalid targeted validation bound");
        int oid=int.Parse(config["orbit"]);var collateral=new HashSet<int>(config["allowed_collateral"].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
        bool binary=config.ContainsKey("orientation_mode") && config["orientation_mode"]=="binary";
        if(collateral.Contains(oid))throw new Exception("Target cannot be ignored");
        var group=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_groups.tsv")).Skip(1).Select(s=>s.Split('\t')).Single(s=>int.Parse(s[0])==oid);
        bool cyclic=config.ContainsKey("orientation_mode") && config["orientation_mode"]=="cyclic";
        bool dihedral=config.ContainsKey("orientation_mode") && config["orientation_mode"]=="dihedral10";
        bool nonabelian=config.ContainsKey("orientation_mode") && config["orientation_mode"]=="nonabelian60";
        if(dihedral ? group[2]!="10" || group[1]!="5" : nonabelian ? group[2]!="60" || group[1]!="20" : cyclic ? group[2]!="5" || group[1]!="5" : binary ? group[2]!="2" || group[1]!="2" : group[2]!="1")throw new Exception("Unsupported orientation mode/group");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbits=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var primitive=File.ReadAllText(config["primitive_word_file"]).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var labels=Enumerable.Range(0,Count).ToArray();foreach(var m in primitive)ApplyInPlace(labels,m);
        var action=new Dictionary<int,int>();var observedCollateral=new HashSet<int>();int changedTarget=0;
        for(int dest=0;dest<Count;dest++)if(labels[dest]!=dest){int p=pieceAt[labels[dest]],q=pieceAt[dest],o=orbits[p];if(o==oid){if(action.ContainsKey(p) && action[p]!=q)throw new Exception("Primitive splits target piece");action[p]=q;changedTarget++;}else{if(!collateral.Contains(o))throw new Exception("Primitive has forbidden action");observedCollateral.Add(o);}}
        if(action.Count!=3 || changedTarget!=3*int.Parse(group[1]))throw new Exception("Primitive must move exactly three target pieces");
        int a=action.Keys.Min(),b=action[a],c=action[b];if(a==b || b==c || c==a || action[c]!=a)throw new Exception("Primitive target is not a three-cycle");
        var slots=Enumerable.Range(0,pieces.Length).Where(p=>orbits[p]==oid).ToArray();var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slots.Length;i++)ordinal[slots[i]]=i;
        var roots=new[]{ordinal[a],ordinal[b],ordinal[c]};
        var graph=new List<CompoundEdge>[3][];var pool=new List<int[][]>[3];for(int stage=0;stage<3;stage++){graph[stage]=Enumerable.Range(0,slots.Length).Select(i=>new List<CompoundEdge>()).ToArray();pool[stage]=new List<int[][]>();}
        var cache=new Dictionary<int,int>[4200];
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);cache[gid]=new Dictionary<int,int>();var word=new[]{new[]{axis,twist,1,mask}};
            foreach(int p in touched)if(orbits[p]==oid && target[p]!=p)cache[gid][ordinal[p]]=ordinal[target[p]];
            bool fa=target[a]<0 || target[a]==a,fb=target[b]<0 || target[b]==b;
            for(int stage=0;stage<3;stage++){
                if(stage>0 && !fa || stage>1 && !fb)continue;pool[stage].Add(word);
                foreach(var edge in cache[gid])graph[stage][edge.Key].Add(new CompoundEdge{Destination=edge.Value,Word=word});
            }
        });
        Func<int,int[][],int> image=(p,word)=>{foreach(var m in word){int gid=(m[0]*7+m[1])*2+(m[3]==4?1:0),turns=(m[2]%Order(m[1])+Order(m[1]))%Order(m[1]);for(int k=0;k<turns;k++){int q;if(cache[gid].TryGetValue(p,out q))p=q;}}return p;};
        var route=new CompoundTree[3];var originallyStranded=new List<int>[3];var connectors=new List<string>();var candidateCounts=new long[3];
        for(int stage=0;stage<3;stage++) {
            route[stage]=MakeTree(graph[stage],roots[stage]);var frozen=new HashSet<int>(roots.Take(stage));
            Func<List<int>> missing=()=>Enumerable.Range(0,slots.Length).Where(p=>route[stage].Parent[p]<0 && !frozen.Contains(p)).ToList();
            originallyStranded[stage]=missing();Console.WriteLine("Orbit {0}, stage {1}: elementary component {2}/{3}",oid,stage,route[stage].Parent.Count(p=>p>=0),slots.Length-stage);
            if(stage==0 && originallyStranded[stage].Count>0)throw new Exception("Orbit graph is not transitive");
            if(stage>0 && originallyStranded[stage].Count>0) {
                var choices=new List<int>(originallyStranded[stage].Take(100));var rng=new Random(1000*oid+stage);
                int desired=Math.Min(100,slots.Length-(stage-1));while(choices.Count<desired){int p=rng.Next(slots.Length);if(!roots.Take(stage-1).Contains(p) && !choices.Contains(p))choices.Add(p);}
                foreach(int x in choices) {
                    var tx=InvertMoves(TreePath(route[stage-1],x));
                    foreach(var g in pool[stage-1]) {
                        var absent=missing();if(absent.Count==0)break;candidateCounts[stage]++;
                        int gx=image(x,g);var word=tx.Concat(g).Concat(TreePath(route[stage-1],gx)).ToArray();
                        if(!absent.Any(p=>route[stage].Parent[image(p,word)]>=0))continue;
                        foreach(int r in frozen)if(image(r,word)!=r)throw new Exception("Connector moves a frozen target slot");
                        var test=Enumerable.Range(0,Count).ToArray();foreach(var m in word)ApplyInPlace(test,m);
                        foreach(int r in frozen)foreach(int p in pieces[slots[r]])if((binary || cyclic || nonabelian || dihedral) ? pieceAt[test[p]]!=pieceAt[p] : test[p]!=p)throw new Exception("Connector fails frozen anchor condition");
                        for(int p=0;p<slots.Length;p++){int q=image(p,word);if(p!=q)graph[stage][p].Add(new CompoundEdge{Destination=q,Word=word});}
                        pool[stage].Add(word);connectors.Add(stage+"\t"+String.Join(" ",word.Select(m=>String.Join(":",m))));route[stage]=MakeTree(graph[stage],roots[stage]);
                        Console.WriteLine("Added stage-{0} connector ({1} turns), component {2}/{3}",stage,word.Length,route[stage].Parent.Count(p=>p>=0),slots.Length-stage);
                    }
                    if(missing().Count==0)break;
                }
                if(missing().Count>0)throw new Exception("Bounded Schreier routing incomplete at stage "+stage);
            }
            using(var w=new StreamWriter(Path.Combine(directory,"routing_stage_"+stage+".tsv"))){w.WriteLine("slot_ordinal\tparent_ordinal\tliteral_cost_to_root\tword_to_parent");for(int p=0;p<slots.Length;p++){var word=route[stage].TowardsRoot[p];if(word!=null && image(p,word)!=route[stage].Parent[p])throw new Exception("Invalid predecessor");w.WriteLine("{0}\t{1}\t{2}\t{3}",p,route[stage].Parent[p],route[stage].Cost[p],word==null?"":String.Join(" ",word.Select(m=>String.Join(":",m))));}}
        }
        WriteInts(Path.Combine(directory,"orbit_slots.int32.bin"),slots);File.WriteAllText(Path.Combine(directory,"primitive_word.txt"),String.Join(" ",primitive.Select(m=>String.Join(":",m))));File.WriteAllLines(Path.Combine(directory,"schreier_connectors.txt"),connectors);
        File.WriteAllLines(Path.Combine(directory,"routing_metadata.txt"),new[]{"piece_orbit="+oid,"slots="+slots.Length,"primitive_piece_cycle="+a+","+b+","+c,"primitive_ordinal_cycle="+String.Join(",",roots),"connected_stabilizer_graphs=True","conjugation_validation=PENDING","orientation_mode="+(dihedral?"dihedral10":nonabelian?"nonabelian60":cyclic?"cyclic":binary?"binary":"trivial"),"allowed_collateral="+String.Join(",",collateral),"observed_primitive_collateral="+String.Join(",",observedCollateral),"candidate_counts="+String.Join(",",candidateCounts),"max_stage_turns="+String.Join(",",route.Select(r=>r.Cost.Max()))});
        var triples=new List<int[]>();var random=new Random(oid*10000+155);
        for(int i=0;i<20;i++){var t=new List<int>();while(t.Count<3){int p=random.Next(slots.Length);if(!t.Contains(p))t.Add(p);}triples.Add(t.ToArray());}
        for(int stage=1;stage<3;stage++) {
            if(originallyStranded[stage].Count>maxTargeted)throw new Exception("More than "+maxTargeted+" targeted cases; refine test bound before claiming completion");
            foreach(int missing in originallyStranded[stage]){var t=(int[])roots.Clone();t[stage]=missing;if(t.Distinct().Count()!=3)t[(stage+1)%3]=Enumerable.Range(0,slots.Length).First(p=>!t.Contains(p));triples.Add(t);}
        }
        using(var w=new StreamWriter(Path.Combine(directory,"conjugation_tests.tsv"))) {
            w.WriteLine("triple_ordinals\tsetup_turns\ttotal_turns\tforbidden_changes\tchecks");
            foreach(var original in triples){var current=(int[])original.Clone();var setup=new List<int[]>();for(int stage=0;stage<3;stage++){foreach(var m in TreePath(route[stage],current[stage])){setup.Add(m);for(int j=0;j<3;j++)current[j]=image(current[j],new[]{m});}for(int j=0;j<=stage;j++)if(current[j]!=roots[j])throw new Exception("Setup fails");}
                var word=setup.Concat(primitive).Concat(InvertMoves(setup.ToArray())).ToArray();var test=Enumerable.Range(0,Count).ToArray();foreach(var m in word)ApplyInPlace(test,m);
                var actual=new Dictionary<int,int>();int forbidden=0;for(int dest=0;dest<Count;dest++)if(test[dest]!=dest){int source=test[dest],o=orbits[pieceAt[source]];if(o==oid)actual[ordinal[pieceAt[source]]]=ordinal[pieceAt[dest]];else if(!collateral.Contains(o))forbidden++;}
                if(forbidden!=0 || actual.Count!=3 || actual[original[0]]!=original[1] || actual[original[1]]!=original[2] || actual[original[2]]!=original[0])throw new Exception("Conjugate has wrong action");
                w.WriteLine("{0}\t{1}\t{2}\t{3}\tPASS",String.Join(",",original),setup.Count,word.Length,forbidden);
            }
        }
        string certificate=Path.Combine(directory,"routing_metadata.txt");File.WriteAllText(certificate,File.ReadAllText(certificate).Replace("conjugation_validation=PENDING","conjugation_validation=PASS"));
        Console.WriteLine("PASS orbit {0}: complete routing; {1} random/targeted full-label tests; max stage costs {2}",oid,triples.Count,String.Join(",",route.Select(r=>r.Cost.Max())));
    }
}}
