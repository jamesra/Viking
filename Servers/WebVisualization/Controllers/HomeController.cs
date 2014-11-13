using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Tracker.Models;
using System.Text;
using System.IO;
using PythonScriptEngine;

namespace Tracker.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Index()
        {            
            return View();
        }      

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Content()
        {
            return View();
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Manage(int id)
        {
           

            // Linq to sql statements
            ViewData["Selection"] = "Table";
            var result = Query(id,"ID");
                                                
            return View(result);

        }
        
        [AcceptVerbs(HttpVerbs.Post)]     
        public ActionResult Manage(int cellid, string group1)
        {
         
            // Linq to sql statements
            ViewData["Selection"] = group1;
            var result = Query(cellid,"ID");
            string workdir = Server.MapPath("~");
            string newdir = workdir+"\\Home\\Manage";
            ViewData["Value"] = "false";
                        
            //Make diagram here using VDX
           // var sample = new MakeDiagram();
          //  sample.TestMethod1();
            if(group1=="Table")
                return View(result);
            else if (group1 == "XML")
            {
                Response.ClearHeaders();
                Response.ClearContent();
                Response.Clear();
                Response.AddHeader("content-disposition", "inline;filename=" + cellid+".svg");
                Response.ContentType = "image/svg+xml";
                Response.WriteFile(workdir+"\\Files"+"\\"+cellid+".svg");
                Response.Flush();
                Response.End();
                
            }
           else
            {
                ViewData["filepath"] = workdir + "Files\\demo.svg";
                Response.ClearHeaders();
                Response.ClearContent();
                Response.Clear();
                Response.AddHeader("content-disposition", "attachment;filename=" + cellid + ".svg");
                Response.ContentType = "image/svg+xml";
                Response.WriteFile(workdir + "\\Files" + "\\" + cellid + ".svg");
                Response.Flush();
                Response.End();
            }
         
            return View(result);
           
        }
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Sort(int layout)
        {
            return View("Manage");
        }

        public ActionResult Details(int id)
        {
            var datacont = new DB();
            var result = datacont.Structures.SingleOrDefault(x=>x.ID == id);
      
            return View(result);
        }
        

        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Edit(int id)
        {

            var datacont = new DB();
            var result = datacont.Structures.SingleOrDefault(x => x.ID == id);

            return View(result);

        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Edit(int id, FormCollection formValues)
        {

            var datacont = new DB();
            var result = datacont.Structures.SingleOrDefault(x => x.ID == id);
           /* result.ID = Request.Form["ID"];
            result.TypeID = Request.Form["TypeID"];
            result.Notes = Request.Form["Notes"];
            result.Verified = Request.Form["Verified"];
            result.Confidence = Request.Form["Confidence"];
            result.ParentID = Request.Form["ParentID"];
            result.Created = Request.Form["Created"];
            result.Label = Request.Form["Label"];
            result.Save();*/


            return RedirectToAction("Details",new{ id = result.ID});

        }

        public IOrderedQueryable<Tracker.Models.Inter> Query(long cellid, string group1)
        {

            var result = SQLQuery(cellid); 
            
            ViewData["id"] = cellid;
            string workdir = Server.MapPath("~");
            string newdir = workdir + "Home\\Manage";
            FileInfo f = new FileInfo(workdir + "\\Files\\" + cellid + ".csv");
            StreamWriter w = f.CreateText();

            

            var dataContext = new DB();
            string type_cellid = (from p in dataContext.Structures
                                  where p.ID == cellid
                                  select p.Label).Single();

            // create a CSV file of the results 
            List<long> ids = new List<long>();

            foreach (var items in result)
            {
                /*  if (first == 1)
                  {
                      ids.Add(items.ID);
                      first = 0;
                  }
                
                  foreach (long values in ids)
                  {
                      if (values == items.ID)
                          exists = 1;
                      if (exists = 1)
                         ids.Add(items.ID);
                        
                  }*/

                w.WriteLine(items.MainID + "," + items.ID + "," + items.TypeID + "," + items.ChildTypeID + "," + items.ChildStructID + "," + items.Label + "," + items.Dir+","+items.Name);
            }

            foreach (var items in result)
            {
                var midresult = SQLQuery(items.ID);
                foreach (var midresult_items in midresult)
                {
                    w.WriteLine(midresult_items.MainID + "," + midresult_items.ID + "," + midresult_items.TypeID + "," + midresult_items.ChildTypeID + "," + midresult_items.ChildStructID + "," + midresult_items.Label + "," + midresult_items.Dir+","+midresult_items.Name);
                }
            }
            w.Close();


            CallScript call = new CallScript();
            if (type_cellid != null)
                type_cellid = type_cellid.Replace(" ", "_");
            else
            {
                type_cellid = "---";
            }
            string param = cellid + "," + type_cellid + ViewData["layout"];
            call.Execute(param);

            return result;

        }

        

        public IOrderedQueryable<Tracker.Models.Inter> SQLQuery(long cellid)
        {   
            var dataContext = new DB();
            var first = from a in dataContext.Structures
                        where a.ParentID == cellid
                        select a;

            var one = from k in dataContext.StructureLinks
                      join j in first on k.TargetID equals j.ID
                      select new { HopID = k.SourceID, Dir = "S" };
            Console.WriteLine(one.Count());
            var two = from l in dataContext.StructureLinks
                      join m in first on l.SourceID equals m.ID
                      select new { HopID = l.TargetID, Dir = "T" };

            Console.WriteLine(two.Count());
            var three = ((from a in dataContext.Structures
                          from b in one
                          where (a.ID == b.HopID)
                          select new { a.ParentID, a.ID, a.TypeID, b.Dir }).Union(
                            from a in dataContext.Structures
                            from c in two
                            where a.ID == c.HopID
                            select new { a.ParentID, a.ID, a.TypeID, c.Dir })).Distinct();

            var four = from p in dataContext.Structures
                       join q in three on p.ID equals q.ParentID
                       select new { ID = p.ID, TypeID = p.TypeID, Label = p.Label, RabbitionID = q.ID, ChildTypeID = q.TypeID, q.Dir };

            var five = from p in dataContext.StructureTypes
                       join q in four on p.ID equals q.TypeID
                       select new { ID = q.ID, TypeID = q.TypeID, ChildTypeID = q.ChildTypeID, Label = q.Label, RabbitionID = q.RabbitionID, q.Dir };

            var six = from p in dataContext.StructureTypes
                      join q in five on p.ID equals q.ChildTypeID
                      select new Inter{ MainID = cellid, ID = q.ID, TypeID = q.TypeID, ChildTypeID = q.ChildTypeID, ChildStructID = q.RabbitionID, Label = q.Label, Dir = q.Dir, Name = p.Name, };
            var result = from p in six
                         orderby p.ID
                         select p;
          
            return result;
        }

    }
}
 