<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage"  %>


<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
        string vPath = HttpContext.Current.Request.ApplicationPath;
        if (vPath == "/")
            vPath = "";
        string webPath = vHost + vPath + "/VikingPlot/Plot/";
       
        form1.Disabled = true;

        foreach (String t in ConnectomeViz.Models.State.labsDictionary.Keys.ToArray<String>())
        {
            labName.Items.Insert(labName.Items.Count, new ListItem(t, t));
        }

        labName.SelectedIndex = 0;


        ConnectomeViz.Models.State.selectedLab = labName.SelectedValue.ToString();

        foreach (string str in ConnectomeViz.Models.State.labsDictionary[ConnectomeViz.Models.State.selectedLab])
        {
            dataSource.Items.Insert(dataSource.Items.Count, new ListItem(str, str));
        }

        dataSource.SelectedIndex = 0;


        string val = ConnectomeViz.Models.State.selectedService;

        if (!String.IsNullOrEmpty(val))
        {
            List<string> keys = ConnectomeViz.Models.State.serviceDictionary.Keys.ToList<string>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].ToString().Equals(val))
                    dataSource.SelectedIndex = i;
            }
        }
    }    
</script>

<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
   3D Viz
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server"> 
    <% string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
   string vPath = HttpContext.Current.Request.ApplicationPath;
   if (vPath == "/")
       vPath = "";
   string webPath = vHost + vPath; %>    
<script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/TabControl.js"></script>


 <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/3DVizScript.js"></script>
 <script type="text/javascript" src="<%=webPath%>/Scripts/o3d/o3d-webgl/base.js"></script>
<script type="text/javascript" src="<%=webPath%>/Scripts/o3d/o3djs/base.js"></script>
<script type="text/javascript" src="<%=webPath%>/Scripts/o3d/3DLoader.js"></script>

 <script type="text/javascript" language="javascript">
     window.onload = function () {

         typeOfCall = 0;

         var animatorIconPath = "<%=webPath%>/Content/ajax-loader.gif";

         document.getElementById("arrow").src = "<%=webPath%>/Content/icons/arrow_b_r.png";

         $("#arrow")
        .mouseover(function () {
            var src = "<%=webPath%>/Content/icons/arrow_g_r.png";
            $(this).attr("src", src);
        })
        .mouseout(function () {
            var src = "<%=webPath%>/Content/icons/arrow_b_r.png";
            $(this).attr("src", src);
        });


         document.getElementById("progress").src = animatorIconPath;

         structureClientID = "<%=structureID.ClientID%>";

         dataSourceClientID = "<%=dataSource.ClientID%>";

         labNameClientID = "<%=labName.ClientID%>";

         RunAfterLoad();


     }

 </script>
 
  <div>

    <form method="post" action="<%=webPath%>/FormRequest/ExportToExcel">
    <input type="hidden" name="gridData" id="gridData" value="" />
</form>
  
    <form id="form1" runat="server" onsubmit="animate(); return false;">
        <label style="font-size: medium">
        <br />
        &nbsp; Select Lab&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; :&nbsp; &nbsp;<asp:DropDownList 
            id="labName" clientIDMode="Static" runat="server" 
           onchange="updateLab()" Height="23px" title="Select a Lab">
        </asp:DropDownList>&nbsp; &amp;&nbsp; Volume :
        <asp:DropDownList id="dataSource"  clientIDMode="Static" onchange="updateList()"
         runat="server" Height="23px"></asp:DropDownList>&nbsp; then&nbsp;&nbsp;&nbsp;
              <br />
        &nbsp;<br />
        &nbsp; Enter Cell ID(s)&nbsp; :</label>&nbsp;&nbsp;&nbsp; <input type="text" id="cellID" name="cellID" 
         onkeydown="update_button()" onkeyup="update_button()" onfocus="update_button()" onmousemove="update_button()" 
         onchange="update_button()" onkeypress="update_button()" onmouseout="update_button()" autocomplete="off" title="Enter a Cell ID"/>
       &nbsp;<label style="font-size:medium"></label>
        <label style="font-size: medium">
        (or) 
        Choose from List : <asp:DropDownList id="structureID" clientIDMode="Static" onchange= "update_button1()" runat="server">
        </asp:DropDownList>  <a href="#structureGrid" id="structureButton" name="modal" style="display:none">
                    <img  border="0" id="arrow" src="" alt="explore" height="24" width="24" src="" align="absmiddle"/></a>
        <br />
        <br />
        </label>
        </form>
        &nbsp;&nbsp;
       
        <label style="font-size:medium"> Choose Hops&nbsp;&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;
        <select name="hops" id="hops">
        <option value="0">No Hops</option>
        <option value="1">1</option>
        <option value="2">2</option>
        </select>
         <strong><span class="Apple-style-span" 
            style="border-collapse: separate; color: rgb(0, 0, 0); font-family: 'Times New Roman'; font-style: normal; font-variant: normal; font-weight: normal; letter-spacing: normal; line-height: normal; orphans: 2; text-align: -webkit-auto; text-indent: 0px; text-transform: none; white-space: normal; widows: 2; word-spacing: 0px; -webkit-border-horizontal-spacing: 0px; -webkit-border-vertical-spacing: 0px; -webkit-text-decorations-in-effect: none; -webkit-text-size-adjust: auto; -webkit-text-stroke-width: 0px; font-size: medium; "><span 
            class="style1" 
            style="color: rgb(51, 51, 51); font-family: Verdana, Arial, sans-serif; line-height: 14px;"> 
          → </span></span></strong>
       <input name="freshQuery" id="freshQuery" type="checkbox" value="latest" /><strong> Update 3D Model(s) on Server</strong>&nbsp;&nbsp;&nbsp;       
       <input type="submit" id="submitButton" value="Go" onclick="doload()"/>&nbsp;
       <label id="message" style="font-size:small"></label>      
      <img id="progress" style="visibility:hidden;" height="25" width="25" src="" align="absmiddle" alt="Loading..."/>     <br /><br /> 
       

</div>

<div id="modelMessage" style="color:Green; font-size:medium" align="center"></div><br /><br />

<div align="center">
 <table id="legend" class="info">
 <tr><th colspan="4">Query Information</th></tr>
 <tr>
    
     <td align="center"><b>3D Plot of Cell Number(s):<a href="#" id="3dId"></a> </b></td> 
     <td align="center"><b>Lab Server: <a href="#" id="3dLab"> </a></b></td>
     <td align="center"><b>Database: <a href="#" id="3dDb"> </a></b></td>
     <td align="center"><b>Collada Download Link: <a href="" id="3dLink"> </a></b></td>
 </tr>
 </table>
 </div>
 <br /><br />

 

<%--
<h1>
3D Viz
</h1>

<p>
<a href="#" id="colladaLocation">Download Collada File</a>
<input type="text" name="fileurl" id="fileurl" size="120">
<input type="button" id="load" onclick="doload();" value="Render 3D">
<img id="3dprogress" style="visibility:hidden;" height="25" width="25" src="<%=webPath%>/Content/ajax-loader.gif" align="absmiddle" alt="Loading..."/>
<div id="Div1" style="color:Green;"></div>
</p>
--%>

<div align="center" id="o3d" style="width: 100%; height: 100%;"></div>



</asp:Content>

<asp:Content ID="Content1" runat="server" contentplaceholderid="HeadContent">
    <style type="text/css">
        #form1
        {
            height: 104px;
        }
        .style1
        {
            font-size: large;
        }
        </style>
</asp:Content>


