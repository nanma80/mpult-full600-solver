using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    static HashSet<string> Closure(byte[][] generators,int degree,int limit) {
        var result=new HashSet<string>();var queue=new Queue<byte[]>();var id=Identity(degree);
        queue.Enqueue(id);result.Add(Convert.ToBase64String(id));
        while(queue.Count>0){var p=queue.Dequeue();foreach(var g in generators){var q=Compose(g,p);if(result.Add(Convert.ToBase64String(q))){queue.Enqueue(q);if(result.Count>limit)throw new Exception("Group closure limit");}}}
        return result;
    }
    public void ExportOrientationInvariants(string directory) {
        var lines=File.ReadAllLines(Path.Combine(directory,"pieces.tsv")).Skip(1).Select(s=>s.Split('\t')).ToArray();
        var pieces=lines.Select(s=>s[2].Split(',').Select(int.Parse).ToArray()).ToArray();
        var orbit=lines.Select(s=>int.Parse(s[1])).ToArray();var sizes=pieces.Select(p=>p.Length).ToArray();
        var pieceAt=new int[Count];var localAt=new int[Count];
        for(int p=0;p<pieces.Length;p++)for(int i=0;i<pieces[p].Length;i++){pieceAt[pieces[p][i]]=p;localAt[pieces[p][i]]=i;}
        var allBytes=File.ReadAllBytes(Path.Combine(directory,"transport_base_to_slot.bytes.bin"));int offset=0;
        var transport=new byte[pieces.Length][];var inverse=new byte[pieces.Length][];
        for(int p=0;p<pieces.Length;p++){var t=new byte[sizes[p]];Buffer.BlockCopy(allBytes,offset,t,0,t.Length);offset+=t.Length;transport[p]=t;inverse[p]=Invert(t);}
        var characters=new Dictionary<int,Dictionary<string,int>>();var modulus=new Dictionary<int,int>();
        using(var w=new StreamWriter(Path.Combine(directory,"orientation_characters.tsv"))) {
            w.WriteLine("orbit\tdegree\tgroup_order\tderived_order\tquotient_order\tcharacter_base64_residue");
            foreach(var row in File.ReadAllLines(Path.Combine(directory,"orientation_groups.tsv")).Skip(1).Select(s=>s.Split('\t'))) {
                int oid=int.Parse(row[0]),n=int.Parse(row[1]);var group=row[3].Split(',').Select(Convert.FromBase64String).ToArray();
                var commutators=new HashSet<string>();
                foreach(var g in group)foreach(var h in group)commutators.Add(Convert.ToBase64String(Compose(g,Compose(h,Compose(Invert(g),Invert(h))))));
                var derived=Closure(commutators.Select(Convert.FromBase64String).ToArray(),n,group.Length);
                int q=group.Length/derived.Count;
                var character=new Dictionary<string,int>();
                if(q!=1) {
                    if(q!=2 && q!=5)throw new Exception("Unexpected non-scalar quotient");
                    var generator=group.First(p=>!derived.Contains(Convert.ToBase64String(p)));var power=Identity(n);
                    for(int exponent=0;exponent<q;exponent++) {
                        foreach(string d in derived)character.Add(Convert.ToBase64String(Compose(Convert.FromBase64String(d),power)),exponent);
                        power=Compose(generator,power);
                    }
                    if(character.Count!=group.Length)throw new Exception("Incomplete quotient character");
                    foreach(var g in group)foreach(var h in group)if(character[Convert.ToBase64String(Compose(g,h))]!=(character[Convert.ToBase64String(g)]+character[Convert.ToBase64String(h)])%q)throw new Exception("Character not homomorphism");
                    characters[oid]=character;modulus[oid]=q;
                }
                w.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",oid,n,group.Length,derived.Count,q,String.Join(",",character.Select(kv=>kv.Key+":"+kv.Value)));
            }
        }
        var columns=characters.Keys.OrderBy(oid=>oid).ToArray();
        using(var w=new StreamWriter(Path.Combine(directory,"generator_orientation_sums.tsv"))) {
            w.WriteLine("axis\ttwist\tmask\t"+String.Join("\t",columns.Select(oid=>"orbit_"+oid+"_mod_"+modulus[oid])));
            PieceActions(pieceAt,localAt,sizes,(axis,twist,mask,touched,target,maps)=>{
                var sum=new Dictionary<int,int>();foreach(int oid in columns)sum[oid]=0;
                foreach(int p in touched) {
                    int oid=orbit[p];Dictionary<string,int> character;
                    if(!characters.TryGetValue(oid,out character))continue;
                    var h=Compose(inverse[target[p]],Compose(maps[p],transport[p]));
                    sum[oid]=(sum[oid]+character[Convert.ToBase64String(h)])%modulus[oid];
                }
                w.WriteLine("{0}\t{1}\t{2}\t{3}",axis,twist,mask,String.Join("\t",columns.Select(oid=>sum[oid])));
            });
        }
        Console.WriteLine("PASS: derived subgroups computed and quotient homomorphisms checked; {0} prime-field orientation coordinates exported for all 4200 generators",columns.Length);
    }
}}
