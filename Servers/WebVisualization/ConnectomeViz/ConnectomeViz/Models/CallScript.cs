#define SKIP

#if !SKIP

using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting;
using System.IO;
using System.Collections;
using System.Web.Mvc;
using System.Collections.Generic;
using Microsoft.Scripting.Runtime;
using System.Diagnostics;


namespace ConnectomeViz.Models
{
    public class CallScript
    {
        public void Execute(string param)
        {
            /*ScriptEngine engine = Python.CreateEngine();
            string cwd = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            Console.WriteLine(cwd);
             string path_file = cwd.Replace("file:\\", "");
             path_file = path_file.Replace("\\bin", "\\Models\\graph_line.py");
             ScriptSource source = engine.CreateScriptSourceFromFile(path_file);

             CompiledCode c = source.Compile();

             ScriptScope scope = engine.CreateScope();
             // string code = "print '2'";
              //ScriptEngine eng2 = Python.CreateEngine();
              //ScriptSource src = eng2.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
              //CompiledCode c = src.Compile();
            c.Execute(scope);
            */
            /*
            ScriptEngine engine = Python.CreateEngine();

             
             string workingdir = HttpContext.Current.Server.MapPath(".");
             string workdir = workingdir.Replace("\\Home\\Manage", "");
            string cwd = workdir.Replace("\\Home\\Manage", "");
         
            string path = cwd.Replace("\\Tracker", "\\Tracker\\Models\\graph_line.py");
            string homepath = cwd.Replace("\\Tracker", "\\Tracker\\Files");
            ScriptSource source = engine.CreateScriptSourceFromFile(path);
            
            CompiledCode c = source.Compile();

           // ScriptScope scope = engine.CreateScope();
            ScriptScope scope = Python.GetSysModule(engine);
            string  args = homepath+","+ param;
            scope.SetVariable("argv", args);
            c.Execute(scope);     */

            string workdir = HttpContext.Current.Server.MapPath("~");

            string path = workdir + "\\Models\\graph_line.py";
            string homepath = workdir + "\\Files";
            ProcessStartInfo startInfo = new ProcessStartInfo("python.exe");
            startInfo.WorkingDirectory = homepath;
            startInfo.Arguments = path + " " + homepath + "," + param + ",0";
            startInfo.UseShellExecute = false;


            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                int i = 0;
                while (process.ExitCode != 0 && i < 3)
                {
                    i++;
                    startInfo.Arguments = path + " " + homepath + "," + param + "," + i.ToString();
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }


            }

        }
        
    }
}
#endif