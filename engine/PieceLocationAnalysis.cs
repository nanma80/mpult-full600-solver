using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace _3dedit {
partial class MoveEngine {
    public void ExportPieceLocations(string directory) {
        Directory.CreateDirectory(directory);var vertices=new List<double[]>();var cells=new HashSet<int>[600];
        for(int f=0;f<600;f++){
            var face=structure.Faces[f];var mesh=face.Base.FaceMesh;if(mesh.NV!=4 || face.FirstSticker!=433*f)throw new Exception("Unexpected tetrahedral cell");cells[f]=new HashSet<int>();
            for(int i=0;i<4;i++){var p=new double[4];Array.Copy(mesh.Coords,i*4,p,0,4);p=PGeom.ApplyMatrix(face.Matrix,p);int index=vertices.FindIndex(q=>PGeom.VertEqual(p,q));if(index<0){index=vertices.Count;vertices.Add(p);}cells[f].Add(index);}
        }
        if(vertices.Count!=120)throw new Exception("Unexpected600-cell vertex count");
        string catalog=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"analysis","pieces","pieces.tsv");int checkedPieces=0;
        using(var w=new StreamWriter(Path.Combine(directory,"piece_locations.tsv"))){w.WriteLine("piece\torbit\tstickers\tcommon_vertices\tedge_t_min\tedge_t_max");
        foreach(var row in File.ReadAllLines(catalog).Skip(1).Select(x=>x.Split('\t'))){int oid=int.Parse(row[1]);if(oid<1 || oid>35)continue;var positions=row[2].Split(',').Select(int.Parse).ToArray();if(positions.Length==1)continue;
            var common=new HashSet<int>(cells[positions[0]/433]);foreach(int pos in positions.Skip(1))common.IntersectWith(cells[pos/433]);
            int expected=positions.Length==2?3:positions.Length==5?2:positions.Length==20?1:-1;if(common.Count!=expected)throw new Exception("Unexpected original simplex incidence");
            string loText="",hiText="";
            if(common.Count==2){var ends=common.OrderBy(x=>x).ToArray();var a=vertices[ends[0]];var b=vertices[ends[1]];var outer=new HashSet<int>(row[3].Split(',').Select(int.Parse));double lo=0,hi=1;
                foreach(var axis in structure.Axes){double norm=PGeom.DotProd(axis.Dir,axis.Dir),x=PGeom.DotProd(a,axis.Dir)/norm,y=PGeom.DotProd(b,axis.Dir)/norm,slope=y-x;double lower=Double.NegativeInfinity,upper=Double.PositiveInfinity;
                    if(outer.Contains(2*axis.Id))lower=axis.Base.Cut[0];else if(outer.Contains(2*axis.Id+1))upper=axis.Base.Cut[1];else{lower=axis.Base.Cut[1];upper=axis.Base.Cut[0];}
                    if(Math.Abs(slope)<1e-10){if(x<lower-1e-8 || x>upper+1e-8)throw new Exception("Edge misses piece chamber");continue;}
                    double t0=(lower-x)/slope,t1=(upper-x)/slope;lo=Math.Max(lo,Math.Min(t0,t1));hi=Math.Min(hi,Math.Max(t0,t1));
                }
                if(hi<=lo+1e-8)throw new Exception("No positive edge interval");loText=lo.ToString("G15",CultureInfo.InvariantCulture);hiText=hi.ToString("G15",CultureInfo.InvariantCulture);
            }
            w.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",row[0],oid,positions.Length,String.Join(",",common.OrderBy(x=>x)),loText,hiText);checkedPieces++;
        }}
        File.WriteAllLines(Path.Combine(directory,"checks.txt"),new[]{"original_vertices=120","cells=600","all_cells_tetrahedral=True","checked_multi_sticker_pieces="+checkedPieces,"simplex_incidence_checks=PASS","positive_edge_interval_checks=PASS"});
        Console.WriteLine("PASS original-simplex incidence for {0} multi-sticker pieces; edge intervals verified",checkedPieces);
    }
}}
