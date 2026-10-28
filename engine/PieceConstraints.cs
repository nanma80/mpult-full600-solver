using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    // byte maps send source-local sticker index to destination-local sticker index.
    static byte[] Identity(int n) {return Enumerable.Range(0,n).Select(i=>(byte)i).ToArray();}
    static byte[] Compose(byte[] after,byte[] before) {var p=new byte[before.Length];for(int i=0;i<p.Length;i++)p[i]=after[before[i]];return p;}
    static byte[] Invert(byte[] p) {var q=new byte[p.Length];for(int i=0;i<p.Length;i++)q[p[i]]=(byte)i;return q;}
    void PieceActions(int[] pieceAt,int[] localAt,int[] sizes,Action<int,int,int,List<int>,int[],byte[][]> visit) {
        var target=Enumerable.Repeat(-1,sizes.Length).ToArray();var maps=sizes.Select(n=>new byte[n]).ToArray();
        foreach(var a in structure.Axes)for(int twist=0;twist<7;twist++)foreach(int layer in new[]{0,2}) {
            var pos=a.Layers[layer];var map=a.Base.Twists[Translation[twist]].Map[layer];var touched=new List<int>();
            for(int i=0;i<pos.Length;i++) {
                int s=pos[i],d=pos[map[i]],p=pieceAt[s];
                if(target[p]<0){target[p]=pieceAt[d];touched.Add(p);}
                else if(target[p]!=pieceAt[d])throw new Exception("Split piece");
                maps[p][localAt[s]]=(byte)localAt[d];
            }
            visit(a.Id,twist,1<<layer,touched,target,maps);
            foreach(int p in touched)target[p]=-1;
        }
    }
    public void ExportPieceConstraints(string directory) {
        var lines=File.ReadAllLines(Path.Combine(directory,"pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=lines.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();
        var orbit=lines.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];
        for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var representatives=new Dictionary<int,int>();for(int p=0;p<pieces.Length;p++)if(!representatives.ContainsKey(orbit[p]))representatives.Add(orbit[p],p);
        var transport=new byte[pieces.Length][];var inverse=new byte[pieces.Length][];
        foreach(int p in representatives.Values){transport[p]=Identity(sizes[p]);inverse[p]=Identity(sizes[p]);}
        for(int p=0;p<pieces.Length;p++)if(sizes[p]==1){transport[p]=Identity(1);inverse[p]=Identity(1);}
        int pass=0;
        while(transport.Any(t=>t==null)) {
            int assigned=0;
            PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
                foreach(int p in touched) {
                    int q=target[p];if(sizes[p]==1)continue;
                    if(transport[p]!=null && transport[q]==null){transport[q]=Compose(maps[p],transport[p]);inverse[q]=Invert(transport[q]);assigned++;}
                    else if(transport[q]!=null && transport[p]==null){transport[p]=Compose(Invert(maps[p]),transport[q]);inverse[p]=Invert(transport[p]);assigned++;}
                }
            });
            Console.WriteLine("Orientation transport pass {0}: assigned {1}",++pass,assigned);
            if(assigned==0 || pass>30)throw new Exception("Transport spanning failed");
        }
        var seeds=new Dictionary<int,HashSet<string>>();foreach(var item in representatives)seeds[item.Key]=new HashSet<string>();
        var nonfixed=representatives.Keys.Where(oid=>orbit.Count(o=>o==oid)>1).OrderBy(o=>o).ToArray();
        var bit=new Dictionary<int,int>();for(int i=0;i<nonfixed.Length;i++)bit[nonfixed[i]]=i;
        var seen=new int[pieces.Length];int stamp=0;long edgeChecks=0;
        using(var w=new StreamWriter(Path.Combine(directory,"generator_parities.tsv"))) {
            w.WriteLine("axis\ttwist\tmask\tparity_bits_decimal");
            PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
                ulong parity=0;stamp++;
                foreach(int p in touched) {
                    if(sizes[p]>1) {
                        var h=Compose(inverse[target[p]],Compose(maps[p],transport[p]));
                        seeds[orbit[p]].Add(Convert.ToBase64String(h));edgeChecks++;
                    }
                    if(seen[p]==stamp)continue;
                    int q=p,length=0;do {seen[q]=stamp;q=target[q];length++;if(q<0)throw new Exception("Incomplete piece cycle");}while(q!=p);
                    if(length%2==0)parity^=1UL<<bit[orbit[p]];
                }
                w.WriteLine("{0}\t{1}\t{2}\t{3}",axis,twist,mask,parity);
            });
        }
        using(var w=new StreamWriter(Path.Combine(directory,"orientation_groups.tsv"))) {
            w.WriteLine("orbit\tstickers_per_piece\tgroup_order\tpermutations_base64");
            foreach(var item in representatives.OrderBy(x=>x.Key)) {
                int oid=item.Key,n=sizes[item.Value];
                var generators=seeds[oid].Select(Convert.FromBase64String).ToArray();
                var closure=new HashSet<string>();var queue=new Queue<byte[]>();var identity=Identity(n);
                closure.Add(Convert.ToBase64String(identity));queue.Enqueue(identity);
                while(queue.Count>0) {
                    var p=queue.Dequeue();foreach(var gen in generators){var q=Compose(gen,p);if(closure.Add(Convert.ToBase64String(q))){queue.Enqueue(q);if(closure.Count>50000)throw new Exception("Orientation group exceeds enumeration limit");}}
                }
                foreach(string p in closure)if(!closure.Contains(Convert.ToBase64String(Invert(Convert.FromBase64String(p)))))throw new Exception("Orientation group inverse closure failed");
                w.WriteLine("{0}\t{1}\t{2}\t{3}",oid,n,closure.Count,String.Join(",",closure.OrderBy(s=>s)));
            }
        }
        using(var w=new BinaryWriter(File.Create(Path.Combine(directory,"transport_base_to_slot.bytes.bin"))))foreach(var t in transport)w.Write(t);
        File.WriteAllText(Path.Combine(directory,"parity_orbit_columns.txt"),String.Join(",",nonfixed));
        File.WriteAllLines(Path.Combine(directory,"constraint_checks.txt"),new[]{"transport_passes="+pass,"orientation_edge_checks="+edgeChecks,"all_transports_assigned=PASS","orientation_group_closure=PASS","orientation_interpretation=stabilizer image; other pieces may move"});
        Console.WriteLine("PASS: transport assigned to all {0} pieces; {1} orientation edge checks; parity vectors for 4200 generators exported",pieces.Length,edgeChecks);
    }
}}
