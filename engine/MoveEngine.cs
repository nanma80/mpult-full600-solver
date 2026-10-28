using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
// Geometry is supplied by the recovered source; all public moves use v1.55 indices.
partial class MoveEngine {
    readonly PuzzleStructure structure;
    static readonly int[] Translation={0,2,6,1,4,3,5};
    public int Count { get { return structure.NStickers; } }
    public MoveEngine(string baseline) {
        var lines=File.ReadAllLines(baseline); int end=Array.IndexOf(lines,"EndPuzzle");
        structure=PuzzleStructure.Create("full-600-cell",lines.Skip(2).Take(end-2).ToArray());
        if(Count!=259800 || structure.Faces.Length!=600)throw new Exception("Unexpected geometry");
    }
    public int Order(int twist) { return structure.BaseAxes[0].Twists[Translation[twist]].Order; }
    public int[] Solved() { var colors=new short[Count];structure.SetColors(colors);return colors.Select(c=>(int)c).ToArray(); }
    // Destination of every source position. Unlike short[] colors, int[] supports unique labels.
    public int[] Permutation(int axis,int twist,int angle,int mask) {
        if(axis<0 || axis>=structure.Axes.Length || twist<0 || twist>=7 || (mask!=1 && mask!=4 && mask!=5))throw new ArgumentException("Invalid move");
        var a=structure.Axes[axis]; var t=a.Base.Twists[Translation[twist]];
        int turns=(angle%t.Order+t.Order)%t.Order;
        var perm=Enumerable.Range(0,Count).ToArray();
        for(int layer=0;layer<a.Layers.Length;layer++) if((mask&(1<<layer))!=0) {
            int[] positions=a.Layers[layer],map=t.Map[layer];
            for(int i=0;i<positions.Length;i++) {
                int j=i;for(int n=0;n<turns;n++)j=map[j];
                perm[positions[i]]=positions[j];
            }
        }
        return perm;
    }
    public int[] Apply(int[] state,int[] move) {
        if(state.Length!=Count)throw new ArgumentException("Wrong state length");
        if(move.Length!=4 || move[0]<0 || move[0]>=structure.Axes.Length || move[1]<0 || move[1]>=7 || (move[3]!=1 && move[3]!=4 && move[3]!=5))throw new ArgumentException("Invalid move");
        var a=structure.Axes[move[0]];var t=a.Base.Twists[Translation[move[1]]];
        int turns=(move[2]%t.Order+t.Order)%t.Order;
        int[] next=(int[])state.Clone();
        for(int layer=0;layer<a.Layers.Length;layer++)if((move[3]&(1<<layer))!=0) {
            int[] positions=a.Layers[layer],map=t.Map[layer];
            for(int i=0;i<positions.Length;i++) {int j=i;for(int k=0;k<turns;k++)j=map[j];next[positions[j]]=state[positions[i]];}
        }
        return next;
    }
    int[] layerScratch;
    public void ApplyInPlace(int[] state,int[] move) {
        if(state.Length!=Count || move.Length!=4 || move[0]<0 || move[0]>=structure.Axes.Length || move[1]<0 || move[1]>=7 || (move[3]!=1 && move[3]!=4 && move[3]!=5))throw new ArgumentException("Invalid state or move");
        var a=structure.Axes[move[0]];var t=a.Base.Twists[Translation[move[1]]];int turns=(move[2]%t.Order+t.Order)%t.Order;
        if(turns==0)return;
        for(int layer=0;layer<a.Layers.Length;layer++)if((move[3]&(1<<layer))!=0) {
            int[] pos=a.Layers[layer],map=t.Map[layer];
            if(layerScratch==null || layerScratch.Length<pos.Length)layerScratch=new int[pos.Length];
            for(int i=0;i<pos.Length;i++)layerScratch[i]=state[pos[i]];
            for(int i=0;i<pos.Length;i++){int j=i;for(int k=0;k<turns;k++)j=map[j];state[pos[j]]=layerScratch[i];}
        }
    }
    public void Validate() {
        long checkedEntries=0;
        foreach(var a in structure.Axes) {
            var occupied=new HashSet<int>();
            for(int layer=0;layer<a.Layers.Length;layer++) {
                int[] pos=a.Layers[layer];
                if(pos==null)continue;
                foreach(int p in pos)if(p<0 || p>=Count || !occupied.Add(p))throw new Exception("Invalid axis layers");
                foreach(var t in a.Base.Twists) {
                    int[] map=t.Map[layer],inv=t.InvMap[layer];
                    if(map==null)continue;
                    if(map.Length!=pos.Length || inv.Length!=pos.Length)throw new Exception("Map length");
                    for(int i=0;i<map.Length;i++) {
                        int j=map[i];if(j<0 || j>=map.Length || inv[j]!=i)throw new Exception("Not inverse bijections");
                        j=i;for(int k=0;k<t.Order;k++)j=map[j];
                        if(j!=i)throw new Exception("Wrong rotation order");checkedEntries++;
                    }
                }
            }
        }
        var labels=Enumerable.Range(0,Count).ToArray();var state=labels;var inplace=(int[])labels.Clone();
        var random=new Random(600155);var moves=new List<int[]>();
        for(int i=0;i<100;i++) {var m=new[]{random.Next(300),random.Next(7),random.Next(2)==0?1:-1,new[]{1,4,5}[random.Next(3)]};moves.Add(m);state=Apply(state,m);ApplyInPlace(inplace,m);if(!state.SequenceEqual(inplace))throw new Exception("In-place and copying engines disagree");}
        for(int i=moves.Count-1;i>=0;i--) {var m=(int[])moves[i].Clone();m[2]=-m[2];state=Apply(state,m);}
        if(!state.SequenceEqual(labels))throw new Exception("Labelled inverse sequence failed");
        Console.WriteLine("PASS: {0} map entries checked for bijection/inverse/order; 100-move labelled round trip",checkedEntries);
    }
    static void WriteInts(string path,int[] data) {using(var w=new BinaryWriter(File.Create(path)))foreach(int i in data)w.Write(i);}
    static int Root(int[] parent,int x) {
        while(parent[x]!=x) {parent[x]=parent[parent[x]];x=parent[x];}return x;
    }
    public void ExportOrbits(string directory) {
        Directory.CreateDirectory(directory);
        var parent=Enumerable.Range(0,Count).ToArray();var rank=new byte[Count];
        long edges=0;int generators=0;
        foreach(var a in structure.Axes)for(int twist=0;twist<7;twist++)foreach(int layer in new[]{0,2}) {
            var positions=a.Layers[layer];var map=a.Base.Twists[Translation[twist]].Map[layer];generators++;
            for(int i=0;i<positions.Length;i++) {
                int p=Root(parent,positions[i]),q=Root(parent,positions[map[i]]);edges++;
                if(p==q)continue;
                if(rank[p]<rank[q])parent[p]=q;
                else {parent[q]=p;if(rank[p]==rank[q])rank[p]++;}
            }
        }
        var ids=new int[Count];var canonical=new Dictionary<int,int>();
        for(int i=0;i<Count;i++) {int r=Root(parent,i),id;if(!canonical.TryGetValue(r,out id)){id=canonical.Count;canonical.Add(r,id);}ids[i]=id;}
        foreach(var a in structure.Axes)for(int twist=0;twist<7;twist++)foreach(int layer in new[]{0,2}) {
            var pos=a.Layers[layer];var map=a.Base.Twists[Translation[twist]].Map[layer];
            for(int i=0;i<pos.Length;i++)if(ids[pos[i]]!=ids[pos[map[i]]])throw new Exception("Generator crosses orbit");
        }
        WriteInts(Path.Combine(directory,"position_orbit.int32.bin"),ids);
        File.WriteAllLines(Path.Combine(directory,"model_summary.txt"),new[]{"positions="+Count,"orbits="+canonical.Count,"generators="+generators,"generator_entries="+edges,"generator_orbit_check=PASS","masks=1,4; axis=0..299; twist=0..6; angle=1"});
        Console.WriteLine("PASS: {0} positions, {1} orbits, {2} generators, {3} generator entries; orbit preservation checked",Count,canonical.Count,generators,edges);
    }
    static void Main(string[] args) {
        if(args.Length!=3)throw new ArgumentException("MoveEngine baseline.log jobs.tsv output-directory");
        var engine=new MoveEngine(args[0]);engine.Validate();Directory.CreateDirectory(args[2]);
        if(args[1]=="--self-check") {Console.WriteLine("PASS: full600-cell engine ready");return;}
        if(args[1]=="--orbits") {engine.ExportOrbits(args[2]);return;}
        if(args[1]=="--pieces") {engine.ExportPieces(args[2]);return;}
        if(args[1]=="--constraints") {engine.ExportPieceConstraints(args[2]);return;}
        if(args[1]=="--orientation-invariants") {engine.ExportOrientationInvariants(args[2]);return;}
        if(args[1]=="--search-commutators") {engine.SearchCommutators(args[2]);return;}
        if(args[1]=="--search-nested") {engine.SearchCommutators(args[2],true);return;}
        if(args[1]=="--search-anchor") {engine.SearchCommutators(args[2],false,Path.Combine(args[2],"anchor_word.txt"));return;}
        if(args[1]=="--search-dirty29") {engine.SearchCommutators(args[2],false,Path.Combine(args[2],"anchor_word.txt"),new[]{29});return;}
        if(args[1]=="--verify-macro-package") {engine.VerifyMacroPackage(args[2]);return;}
        if(args[1]=="--validate-dihedral4-words") {engine.ValidateDihedral4Words(args[2]);return;}
        if(args[1]=="--prepare-dihedral4") {engine.PrepareDihedral4(args[2]);return;}
        if(args[1]=="--render-dihedral4-kernel") {engine.RenderDihedral4Kernel(args[2]);return;}
        if(args[1]=="--prepare-corner35-orientations") {engine.PrepareCorner35Orientations(args[2]);return;}
        if(args[1]=="--render-corner35-orientations") {engine.RenderCorner35Orientations(args[2]);return;}
        if(args[1]=="--assess-corner35") {engine.AssessCorner35(args[2]);return;}
        if(args[1]=="--search-cyclic-orientations") {engine.SearchCyclicOrientations(args[2]);return;}
        if(args[1]=="--search-position-target") {engine.SearchCommutators(args[2],false,Path.Combine(args[2],"anchor_word.txt"),File.ReadAllText(Path.Combine(args[2],"ignored_orbits.txt")).Trim().Split(',').Select(int.Parse).ToArray(),int.Parse(File.ReadAllText(Path.Combine(args[2],"moving_orbit.txt")).Trim()));return;}
        if(args[1]=="--search-repair-later") {engine.SearchCommutators(args[2],false,Path.Combine(args[2],"anchor_word.txt"),File.ReadAllText(Path.Combine(args[2],"ignored_orbits.txt")).Trim().Split(',').Select(int.Parse).ToArray());return;}
        if(args[1]=="--routing-test") {engine.TestIsolatedRouting(args[2]);return;}
        if(args[1]=="--routing-schreier") {engine.TestSchreierRouting(args[2]);return;}
        if(args[1]=="--render-orbit29") {engine.RenderOrbit29Batch(args[2]);return;}
        if(args[1]=="--render-trivial-orbit") {engine.RenderConfiguredOrbitBatch(args[2]);return;}
        if(args[1]=="--prepare-orbit-routing") {engine.PrepareOrbitRouting(args[2]);return;}
        if(args[1]=="--search-paired-flips") {engine.SearchPairedFlips(args[2]);return;}
        if(args[1]=="--search-compound-flips") {engine.SearchCompoundFlips(args[2]);return;}
        if(args[1]=="--validate-binary-pairs") {engine.ValidateBinaryPairs(args[2]);return;}
        if(args[1]=="--export-piece-locations") {engine.ExportPieceLocations(args[2]);return;}
        if(args[1]=="--export-reflection") {engine.ExportReflection(args[2]);return;}
        foreach(string line in File.ReadAllLines(args[1])) {
            if(String.IsNullOrWhiteSpace(line))continue;
            string[] fields=line.Split('\t');
            int[] state=engine.Solved();
            if(fields[1]!="-") { using(var r=new BinaryReader(File.OpenRead(fields[1]))) {state=new int[engine.Count];for(int i=0;i<state.Length;i++)state[i]=r.ReadInt32();} }
            foreach(string token in fields[2].Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))state=engine.Apply(state,token.Split(':').Select(int.Parse).ToArray());
            WriteInts(Path.Combine(args[2],fields[0]+".state.bin"),state);
            Console.WriteLine("Generated state: {0}",fields[0]);
        }
        // A small, explicit permutation export for independent tooling: destination[source], int32 LE.
        WriteInts(Path.Combine(args[2],"top_face.destination.bin"),engine.Permutation(62,6,2,1));
    }
}}

