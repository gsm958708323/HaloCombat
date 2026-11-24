using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Combat.Config
{
    internal static class ProjectFileTransaction
    {
        public static T Locked<T>(string path,Func<T> action)
        {
            using(var mutex=new Mutex(false,"HaloCombat.Project."+SkillJsonCodec.Fingerprint(Path.GetFullPath(path).ToUpperInvariant())))
            {
                bool entered=false;
                try
                {
                    try{entered=mutex.WaitOne(TimeSpan.FromSeconds(10));}catch(AbandonedMutexException){entered=true;}
                    if(!entered)throw new IOException("Project is busy: "+path);
                    return action();
                }
                finally{if(entered)mutex.ReleaseMutex();}
            }
        }
        public static void Commit(IReadOnlyDictionary<string,string> files,IEnumerable<string> obsolete,
            IEnumerable<string> requiredDeletes=null)
        {
            var originals=new Dictionary<string,byte[]>();var changed=new List<string>();
            try
            {
                foreach(var file in files)
                {
                    byte[] bytes=Encoding.UTF8.GetBytes(file.Value);
                    var old=File.Exists(file.Key)?File.ReadAllBytes(file.Key):null;
                    if(old!=null&&Same(old,bytes))continue;
                    originals.Add(file.Key,old);
                    // A failed atomic replacement leaves its original intact. Roll back only completed writes.
                    Atomic(file.Key,bytes);changed.Add(file.Key);
                }
                // Published slots remain live until removed, so their deletions are part of the commit.
                if(requiredDeletes!=null)foreach(string path in requiredDeletes)
                {
                    if(files.ContainsKey(path))throw new ArgumentException("Cannot write and delete the same path: "+path);
                    originals.Add(path,File.ReadAllBytes(path));
                    File.Delete(path);
                    changed.Add(path);
                }
            }
            catch(Exception error)
            {
                var rollbackErrors=new List<Exception>();
                for(int i=changed.Count-1;i>=0;i--)
                {
                    var path=changed[i];
                    try
                    {
                        if(originals[path]==null)File.Delete(path);
                        else Atomic(path,originals[path]);
                    }
                    catch(Exception rollbackError) when(rollbackError is IOException||rollbackError is UnauthorizedAccessException)
                    {rollbackErrors.Add(rollbackError);}
                }
                if(rollbackErrors.Count>0)
                {
                    rollbackErrors.Insert(0,error);
                    throw new IOException("Project commit failed and some files could not be restored.",new AggregateException(rollbackErrors));
                }
                throw;
            }
            // The new index is committed. Only remove paths owned by its previous index.
            foreach(string path in obsolete)if(!files.ContainsKey(path)&&File.Exists(path))
            {
                // A locked orphan is harmless after commit; do not report a successful save as failed.
                try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
        static bool Same(byte[] left,byte[] right)
        {if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
        static void Atomic(string path,byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
