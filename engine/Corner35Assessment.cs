using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void AssessCorner35(string directory) {
        Directory.CreateDirectory(directory);string analysis=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis");
        var rows=File.ReadAllLines(Path.Combine(analysis,"pieces","pieces.tsv")).Skip(1).Select(x=>x.Split('\t')).ToArray();
        var pieces=rows.Select(x=>x[2].Split(',').Select(int.Parse).ToArray()).ToArray();var orbit=rows.Select(x=>int.Parse(x[1])).ToArray();var sizes=pieces.Select(x=>x.Length).ToArray();
        var at=new int[Count];var local=new int[Count];for(int p=0;p<pieces.Length;p++)for(int i=0;i<sizes[p];i++){at[pieces[p][i]]=p;local[pieces[p][i]]=i;}
        var word=File.ReadAllText(Path.Combine(analysis,"primitives","orbit35","primitive_word.txt")).Trim().Split(' ').Select(x=>x.Split(':').Select(int.Parse).ToArray()).ToArray();
        var labels=Enumerable.Range(0,Count).ToArray();foreach(var move in word)ApplyInPlace(labels,move);
        var changed=Enumerable.Range(0,pieces.Length).Where(p=>orbit[p]==35 && pieces[p].Any(pos=>labels[pos]!=pos)).ToArray();
        if(changed.Length!=1)throw new Exception("Expected single corner action");int root=changed[0],degree=sizes[root];
        Func<int[],byte[]> extract=state=>{var map=new byte[degree];foreach(int dest in pieces[root]){int source=state[dest];if(at[source]!=root)throw new Exception("Corner moved");map[local[source]]=(byte)local[dest];}return map;};
        var rotation=extract(labels);int singleOrder=Closure(new[]{rotation},degree,60).Count;
        int[][] conjugator=null;byte[] conjugated=null;int stabilizers=0;int bestClosure=singleOrder;
        PieceActions(at,local,sizes,(axis,twist,mask,touched,target,maps)=>{
            if(!touched.Contains(root) || target[root]!=root || maps[root].SequenceEqual(Identity(degree)))return;
            stabilizers++;var k=maps[root];var candidate=Compose(k,Compose(rotation,Invert(k)));int order=Closure(new[]{rotation,candidate},degree,60).Count;
            if(order>bestClosure){bestClosure=order;conjugator=new[]{new[]{axis,twist,1,mask}};conjugated=candidate;}
        });
        var lines=new List<string>{"orbit=35","root_piece="+root,"single_rotation_closure="+singleOrder,"nontrivial_elementary_root_stabilizers="+stabilizers,"best_two_rotation_closure="+bestClosure,"corner_position_solver_evaluated=False"};
        if(conjugator!=null){
            var second=InvertMoves(conjugator).Concat(word).Concat(conjugator).ToArray();var check=Enumerable.Range(0,Count).ToArray();foreach(var move in second)ApplyInPlace(check,move);
            if(!extract(check).SequenceEqual(conjugated))throw new Exception("Conjugate orientation mismatch");
            int changedCornerPositions=0;var collateral=new HashSet<int>();for(int pos=0;pos<Count;pos++)if(check[pos]!=pos){int p=at[pos];if(orbit[p]==35){if(p!=root)throw new Exception("Another corner changed");changedCornerPositions++;}else{if(orbit[p]<24 || orbit[p]>34)throw new Exception("Unexpected collateral");collateral.Add(orbit[p]);}}
            File.WriteAllText(Path.Combine(directory,"second_rotation.word.txt"),String.Join(" ",second.Select(m=>String.Join(":",m)))+Environment.NewLine);
            File.WriteAllText(Path.Combine(directory,"conjugator.word.txt"),String.Join(" ",conjugator.Select(m=>String.Join(":",m)))+Environment.NewLine);
            lines.Add("full_label_conjugate_check=PASS");lines.Add("conjugate_changed_corner_stickers="+changedCornerPositions);lines.Add("conjugate_collateral="+String.Join(",",collateral.OrderBy(x=>x)));
        }
        File.WriteAllLines(Path.Combine(directory,"summary.txt"),lines);Console.WriteLine(String.Join(Environment.NewLine,lines));
    }
}}
