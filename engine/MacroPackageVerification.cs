using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace _3dedit {
partial class MoveEngine {
    public void VerifyMacroPackage(string directory) {
        var native=new CMacroFile(Path.Combine(directory,"joint_solution.dat"));
        if(!native.CheckSize("full-600-cell") || native.FileName==null)throw new Exception("Reference macro reader rejected file");
        var lines=File.ReadAllLines(Path.Combine(directory,"macro_expected.tsv"));var names=native.GetNamesList();
        if(names.Length!=lines.Length)throw new Exception("Macro entry count mismatch");
        var reports=new List<string>{"name\tturns\tchecks"};long total=0;CMacro first=null;
        foreach(var line in lines){
            var f=line.Split('\t');var macro=native.GetMacro(f[0]);if(macro==null)throw new Exception("Missing macro");
            if(first==null)first=macro;
            if(macro.NStickers!=first.NStickers || !macro.Stickers.SequenceEqual(first.Stickers) || !macro.Point.SequenceEqual(first.Point))throw new Exception("Inconsistent anchors");
            int count=0;
            foreach(string path in f[1].Split(';'))using(var r=new BinaryReader(File.OpenRead(path))){
                if(r.BaseStream.Length%16!=0)throw new Exception("Invalid int32 move file");
                while(r.BaseStream.Position<r.BaseStream.Length){long axis=r.ReadInt32(),twist=r.ReadInt32(),angle=r.ReadInt32(),mask=r.ReadInt32();long code=axis|(twist<<16)|(angle<<32)|(mask<<48);
                    if(count>=macro.LMacro || macro.Code[count++]!=code)throw new Exception("Native macro code differs from simulated moves");
                }
            }
            if(count!=macro.LMacro)throw new Exception("Native macro length mismatch");
            total+=count;reports.Add(macro.Name+"\t"+count+"\tPASS");
        }
        File.WriteAllLines(Path.Combine(directory,"native_macro_checks.tsv"),reports);Console.WriteLine("PASS reference macro reader: {0} entries, {1} codes, exact simulated words and consistent anchors",names.Length,total);
    }
}}
