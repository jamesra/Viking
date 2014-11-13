<%@ Control Language="C#" ClassName="EndpointSelectorControl" CodeBehind="EndpointSelectorControl.ascx.cs"
    Inherits="ConnectomeViz.Views.Shared.EndpointSelectorControl" %>
<script type="text/javascript" language="javascript">

    function GetServerListElement() {
        return document.getElementById('<%=serverList.ClientID%>')
    }

    function GetVolumeListElement() {
        return document.getElementById('<%=volumeList.ClientID%>')
    }

     function SelectedServer() {
         return document.getElementById('<%=serverList.ClientID%>').value;
     }

     function SelectedVolume() { 
         return document.getElementById('<%=volumeList.ClientID%>').value;
     }
      
     function updateVolumeList() {

         serverName = SelectedServer(); 

         var urlAppend = "FormRequest/GetVolumes?server=";
         var root = window.location

         var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
         var mat = root.toString().match(re)[0];

         var Url = mat + urlAppend + serverName;
         //alert(Url); 

         xmlHttp = new XMLHttpRequest();
         xmlHttp.onreadystatechange = ProcessVolumeRequest;
         xmlHttp.open("GET", Url, true);

         xmlHttp.send(null);

         var list = document.getElementById("<%=volumeList.ClientID%>");
         if (list == null)
             return;

         ClearList(list);
         ListAddItem(list, "Finding Volumes...");
         list.disabled = true; 
     } 

     //After lab information is received, populate lab and datasource
     function ProcessVolumeRequest() {

         if (xmlHttp.readyState == 4 && xmlHttp.status == 200) {
          
             if (xmlHttp.responseText == "Not found") {
                 alert("Sorry couldn't connect to server, Please try again after some time");
             }
             else {
                 var info = eval("(" + xmlHttp.responseText + ")");

                 var listVolumes = document.getElementById("<%=volumeList.ClientID%>");
                 removeOptionSelected("<%=volumeList.ClientID%>");
                 // No parsing necessary with JSON!
                 //                 alert(info);
                 for (var i in info) {
                     ListAddItem(listVolumes, info[i]);
                 }

                 listVolumes.disabled = false; 
             } 
         }
     }; 

</script> 
<div>
    Select Server&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; :&nbsp; &nbsp;<asp:DropDownList
        ID="serverList" runat="server" Height="23px" title="Select a Server">
    </asp:DropDownList>
    &nbsp; &amp;&nbsp; Volume :
    <asp:DropDownList ID="volumeList" onchange="updateList()" runat="server"
        Height="23px">
    </asp:DropDownList>
</div>
<style type="text/css">
    #flashGraph
    {
        font-weight: 700;
    }
</style>
