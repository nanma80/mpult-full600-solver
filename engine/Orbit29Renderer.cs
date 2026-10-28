using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
namespace _3dedit {
partial class MoveEngine {
    void AppendReduced(List<int[]> moves,int[] input) {
        var m=(int[])input.Clone();m[2]=(m[2]%Order(m[1])+Order(m[1]))%Order(m[1]);if(m[2]==0)return;
        if(moves.Count>0){var last=moves[moves.Count-1];if(last[0]==m[0] && last[1]==m[1] && last[3]==m[3]){last[2]=(last[2]+m[2])%Order(m[1]);if(last[2]==0)moves.RemoveAt(moves.Count-1);return;}}
        moves.Add(m);
    }
    public void RenderOrbit29Batch(string directory) {
        RenderTrivialOrbitBatch(directory,29,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis","isolated_routing_schreier"),new int[0]);
    }
    public void RenderConfiguredOrbitBatch(string directory) {
        var config=File.ReadAllLines(Path.Combine(directory,"renderer_config.txt")).Where(s=>s.Length>0).Select(s=>s.Split(new[]{'='},2)).ToDictionary(s=>s[0],s=>s[1]);
        string mode=config.ContainsKey("orientation_mode")?config["orientation_mode"]:"trivial";
        RenderTrivialOrbitBatch(directory,int.Parse(config["orbit"]),config["routing_directory"],config["allowed_collateral"].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(),mode,config.ContainsKey("primitive_override")?config["primitive_override"]:null);
    }
    void RenderTrivialOrbitBatch(string directory,int targetOrbit,string routing,int[] collateralOrbits,string orientationMode="trivial",string primitiveOverride=null) {
        string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");var collateral=new HashSet<int>(collateralOrbits);
        if(collateral.Contains(targetOrbit))throw new Exception("Target cannot be ignored");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=rows.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];var targetPosition=new bool[Count];
        var positionOrbit=new int[Count];
        for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;positionOrbit[pieces[p][i]]=orbit[p];if(orbit[p]==targetOrbit)targetPosition[pieces[p][i]]=true;}
        var slots=ReadInts(Path.Combine(routing,"orbit_slots.int32.bin"));var ordinal=Enumerable.Repeat(-1,pieces.Length).ToArray();for(int i=0;i<slots.Length;i++)ordinal[slots[i]]=i;
        var metadata=File.ReadAllLines(Path.Combine(routing,"routing_metadata.txt")).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
        if(metadata.ContainsKey("conjugation_validation") && metadata["conjugation_validation"]!="PASS")throw new Exception("Routing catalog validation pending");
        if(metadata["connected_stabilizer_graphs"]!="True" || metadata["piece_orbit"]!=targetOrbit.ToString())throw new Exception("Unsupported routing catalog");
        var group=File.ReadAllLines(Path.Combine(analysis,"pieces","orientation_groups.tsv")).Skip(1).Select(s=>s.Split('\t')).Single(s=>int.Parse(s[0])==targetOrbit);
        bool binary=orientationMode=="binary_positions" || orientationMode=="binary_orientations";
        bool cyclic=orientationMode=="cyclic_positions" || orientationMode=="cyclic_orientations";
        bool nonabelian=orientationMode=="nonabelian_positions";
        bool dihedral=orientationMode=="dihedral_positions" || orientationMode=="dihedral_quotient";
        if(dihedral ? group[2]!="10" || group[1]!="5" : nonabelian ? group[2]!="60" || group[1]!="20" : cyclic ? group[2]!="5" || group[1]!="5" : binary ? group[2]!="2" || group[1]!="2" : orientationMode!="trivial" || group[2]!="1")throw new Exception("Unsupported renderer orientation mode/group");
        if((orientationMode=="binary_orientations" || orientationMode=="cyclic_orientations" || orientationMode=="dihedral_quotient") && primitiveOverride==null)throw new Exception("Paired flip primitive required");
        var roots=metadata["primitive_ordinal_cycle"].Split(',').Select(int.Parse).ToArray();
        var primitive=File.ReadAllText(primitiveOverride??Path.Combine(routing,"primitive_word.txt")).Trim().Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
        var route=new CompoundTree[3];
        for(int stage=0;stage<3;stage++) {
            var tree=new CompoundTree{Parent=new int[slots.Length],Cost=new int[slots.Length],TowardsRoot=new int[slots.Length][][]};
            foreach(string line in File.ReadAllLines(Path.Combine(routing,"routing_stage_"+stage+".tsv")).Skip(1)){
                var f=line.Split('\t');int p=int.Parse(f[0]);tree.Parent[p]=int.Parse(f[1]);tree.Cost[p]=int.Parse(f[2]);
                if(f.Length>3 && f[3].Length>0)tree.TowardsRoot[p]=f[3].Split(' ').Select(s=>s.Split(':').Select(int.Parse).ToArray()).ToArray();
            }
            route[stage]=tree;
        }
        var cache=new Dictionary<int,int>[4200];
        PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
            int gid=(axis*7+twist)*2+(mask==4?1:0);cache[gid]=new Dictionary<int,int>();
            foreach(int p in touched)if(orbit[p]==targetOrbit && target[p]!=p)cache[gid][ordinal[p]]=ordinal[target[p]];
        });
        Func<int,int[],int> image=(p,m)=>{int gid=(m[0]*7+m[1])*2+(m[3]==4?1:0);for(int k=0;k<m[2];k++){int q;if(cache[gid].TryGetValue(p,out q))p=q;}return p;};
        using(var summary=new StreamWriter(Path.Combine(directory,"render_results.tsv"))) {
            summary.WriteLine("case\tcycles\traw_turns\treduced_turns\tsimulation_seconds\torbit_wrong_colors\tother_changed_colors\tallowed_collateral_changed_colors\torbit_wrong_identities\torientation_mode");
            foreach(string line in File.ReadAllLines(Path.Combine(directory,"jobs.tsv"))) {
                var fields=line.Split('\t');string name=fields[0];var initial=ReadInts(fields[1]);if(initial.Length!=Count)throw new Exception("Wrong color count");
                var triples=File.ReadAllLines(fields[2]).Where(s=>s.Length>0).Select(s=>s.Split('\t').Select(int.Parse).ToArray()).ToArray();
                var moves=new List<int[]>();long rawCount=0;
                foreach(var entry in triples) {
                    int repetitions=entry.Length==4 && cyclic?entry[3]:1;var triple=entry.Take(3).ToArray();
                    if((entry.Length!=3 && !(cyclic && entry.Length==4)) || repetitions<1 || repetitions>4)throw new Exception("Invalid repetition count");
                    if(triple.Length!=3 || triple.Distinct().Count()!=3 || triple.Any(p=>p<0 || p>=slots.Length))throw new Exception("Invalid triple");
                    var current=(int[])triple.Clone();var setup=new List<int[]>();
                    for(int stage=0;stage<3;stage++) {
                        foreach(var m in TreePath(route[stage],current[stage])){setup.Add(m);for(int j=0;j<3;j++)current[j]=image(current[j],m);}
                        // A Schreier connector restores anchors as a whole word; its elementary prefixes may move them.
                        for(int j=0;j<=stage;j++)if(current[j]!=roots[j])throw new Exception("Setup stage fails to restore a frozen slot");
                    }
                    if(!current.SequenceEqual(roots))throw new Exception("Setup fails");
                    rawCount+=2*setup.Count+repetitions*primitive.Length;
                    foreach(var m in setup)AppendReduced(moves,m);for(int repeat=0;repeat<repetitions;repeat++)foreach(var m in primitive)AppendReduced(moves,m);foreach(var m in InvertMoves(setup.ToArray()))AppendReduced(moves,m);
                }
                var state=(int[])initial.Clone();var watch=Stopwatch.StartNew();foreach(var m in moves)ApplyInPlace(state,m);watch.Stop();
                int wrong=0,other=0,allowed=0;for(int p=0;p<Count;p++){if(targetPosition[p]){if(state[p]!=p/433)wrong++;}else if(state[p]!=initial[p]){if(collateral.Contains(positionOrbit[p]))allowed++;else other++;}}
                int wrongIdentities=0;foreach(int piece in slots){var actualColors=pieces[piece].Select(p=>state[p]).OrderBy(c=>c);var solvedColors=pieces[piece].Select(p=>p/433).OrderBy(c=>c);if(!actualColors.SequenceEqual(solvedColors))wrongIdentities++;}
                if(wrongIdentities!=0 || (orientationMode!="binary_positions" && orientationMode!="cyclic_positions" && orientationMode!="nonabelian_positions" && !dihedral && wrong!=0) || other!=0)throw new Exception("Expanded word fails target checks: "+name+" colors="+wrong+" identities="+wrongIdentities+" other="+other);
                WriteInts(Path.Combine(directory,name+".final.int32.bin"),state);
                using(var w=new BinaryWriter(File.Create(Path.Combine(directory,name+".moves.int32.bin"))))foreach(var m in moves)foreach(int x in m)w.Write(x);
                summary.WriteLine("{0}\t{1}\t{2}\t{3}\t{4:F6}\t{5}\t{6}\t{7}\t{8}\t{9}",name,triples.Length,rawCount,moves.Count,watch.Elapsed.TotalSeconds,wrong,other,allowed,wrongIdentities,orientationMode);summary.Flush();
                Console.WriteLine("PASS orbit {0}, {1}, {6}: {2} corrections, {3} turns, {4:F3}s; forbidden changes=0, allowed collateral colors={5}, residual wrong target colors={7}",targetOrbit,name,triples.Length,moves.Count,watch.Elapsed.TotalSeconds,allowed,orientationMode,wrong);
            }
        }
    }
}}
