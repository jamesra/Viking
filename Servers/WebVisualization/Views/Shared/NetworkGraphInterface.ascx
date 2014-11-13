<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="NetworkGraphInterface.ascx.cs"
    Inherits="ConnectomeViz.Views.Shared.NetworkGraphInterface" %>
<%@ Register TagPrefix="uc" TagName="EndpointSelectorControl" Src="~/Views/Shared/EndpointSelectorControl.ascx" %>

<script language="javascript" type="text/javascript">
 
</script>
<div>
    <label style="font-size: medium">
        <uc:EndpointSelectorControl ID="EndpointSelector" runat="server" />
        <br />
        Enter Cell ID&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;&nbsp;
    <input type="text" id="cellID" name="cellID" autocomplete="off"
        title="Enter a Cell ID" />
    &nbsp; (or) Choose from List :
    <asp:DropDownList ID="structureList" clientIDMode="Static" onchange="update_button1()"
        runat="server">
    </asp:DropDownList>
    <a href="#networkGrid" id="networkButton" name="modal" style="display: none">
        <img border="0" id="arrow" src="" alt="explore" height="24" width="24" align="absmiddle" /></a>
    <br />
    <br />
    Choose Hops&nbsp;&nbsp;&nbsp; :&nbsp;&nbsp;
    <select name="hops">
        <option value="1">1</option>
        <option value="2">2</option>
        <option value="3" selected="selected">3</option>
        <option value="12">Max Possible</option>
    </select>&nbsp;&nbsp;<span class="Apple-style-span" style="border-collapse: separate;
        color: rgb(0, 0, 0); font-family: 'Times New Roman'; font-style: normal; font-variant: normal;
        font-weight: normal; letter-spacing: normal; line-height: normal; orphans: 2;
        text-align: -webkit-auto; text-indent: 0px; text-transform: none; white-space: normal;
        widows: 2; word-spacing: 0px; -webkit-border-horizontal-spacing: 0px; -webkit-border-vertical-spacing: 0px;
        -webkit-text-decorations-in-effect: none; -webkit-text-size-adjust: auto; -webkit-text-stroke-width: 0px;
        font-size: medium;"><span class="style1" style="color: rgb(51, 51, 51); font-family: Verdana, Arial, sans-serif;
            line-height: 14px;"> → </span></span>&nbsp;&nbsp;&nbsp;&nbsp;
        
        <img id="progress" style="visibility: hidden;" height="25" width="25" src="" align="middle"
            alt="Loading..." />
        <br />
        <br />        
        <input name="freshQuery" id="freshQuery" type="checkbox" value="latest" />&nbsp;&nbsp;<strong>Refresh graph</strong>&nbsp;&nbsp;&nbsp;    
    <br />
    <input name="reduceEdges" id="reduceEdges"
        type="checkbox" value="reduce" checked="checked" />&nbsp;&nbsp;<strong>Reduce edges</strong><br />
    <input name="showExtraHop" id="showExtraHop"
        type="checkbox" value="showExtraHop"/>&nbsp;&nbsp;<strong>Show connections past last hop</strong><br />
    <input name="pinNodes" id="pinNodes"
        type="checkbox" value="nodepos" />&nbsp;&nbsp;<strong>Anatomic Node Positions</strong>
        <br />
    <br />

    <strong>Output type</strong>
    <ul style="list-style: none;">
        <li><input type="radio" name="OutputType" value="dot" id="dotGraph" />&nbsp;&nbsp;<strong>Dot File</strong></li> 
        <li><input type="radio" name="OutputType" value="generate" id="svgGraph" checked="checked" />&nbsp;&nbsp;<strong>Interactive Graph </strong></li>
        <li><input type="radio" name="OutputType" value="flash" id="flashGraph" />&nbsp;&nbsp;<strong>Flash Graph</strong></li>
     </ul> 
</div>
<style type="text/css">
    .style1
    {
        font-size: large;
    }
    #flashGraph
    {
        font-weight: 700;
    }
</style>
<script language="javascript" type="text/javascript">

    InitStructureList();
    InitToolTips();
    
    function InitStructureList() {

        var serverList = GetServerListElement();
        var volumeList = GetVolumeListElement();

        serverList.addEventListener("onchange", updateStructures(), false);
        volumeList.addEventListener("onchange", updateStructures(), false); 

    }

    function InitToolTips()
    {
       
    }

    function OnSubmit()
    {
         var animatorIconPath = "../Content/ajax-loader.gif"; 
         progressElement = document.getElementById("progress");
         progressElement.src = animatorIconPath;

         progressElement.style.visibility = "visible";
         var msg = document.getElementById("message");
         msg.innerHTML = " <b>Generating Graph...</b>";
    }

    function OnSubmitGetDOT()
    {
         var animatorIconPath = "../Content/ajax-loader.gif"; 
         progressElement = document.getElementById("progress");
         progressElement.src = animatorIconPath;

         progressElement.style.visibility = "visible";
         var msg = document.getElementById("message");
         msg.innerHTML = " <b>Generating Graph...</b>"; 
    }

    function updateStructures() {

        serverName = SelectedServer();
        volumeName = SelectedVolume();

        var structureList = document.getElementById('<%=structureList.ClientID%>');
        ClearList(structureList);
        ListAddItem(structureList, "...Fetching Cell IDs...");

        var urlAppend = "FormRequest/GetTopStructures?request=";
        var root = window.location

        var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
        var mat = root.toString().match(re)[0];

        var typeOfCall = 1;
        var Url = mat + urlAppend + serverName + "," + volumeName + "," + typeOfCall;
        
        document.getElementById('networkButton').style.display = "none";

        xmlHttp = new XMLHttpRequest();
        xmlHttp.onreadystatechange = ProcessStructuresRequest;
        xmlHttp.open("GET", Url, true);

        xmlHttp.send(null);
    };

    
// update IDs list after callback
function ProcessStructuresRequest() {

    if (xmlHttp.readyState == 4 && xmlHttp.status == 200) {


        if (xmlHttp.responseText == "Not found") {
            alert("Sorry couldn't connect to server, try again after some time");
        }
        else {
            var info = eval("(" + xmlHttp.responseText + ")");
            // No parsing necessary with JSON!
            var updatedData = [];
            networkIDMap = new Array();
            actual = [];

            count = info.length - 1;

            var structureList = document.getElementById('<%=structureList.ClientID%>');
            ClearList(structureList);

            var ctr = 0;
            for (var i in info) {  

                if (i == 0) { 
                    ListAddItem(structureList,  SelectedVolume() + " - IDs Ordered by Structure Type", 0);  
                }

                else {
                    var arr = info[i].split('~');
                    var idNum = String(arr[1]);
                    var idType = String(arr[0]);
                    var cnt = String(arr[2]);

                    networkIDMap[arr[1].replace(" ", "")] = arr[0];
                    networkIDSort[arr[1].replace(" ", "")] = arr[2];
                     
                    updatedData[ctr] = idNum + " " + idType + "<br/>> Connections = " + cnt;

                    actual[ctr] = idNum;
                    ctr++;

                    //elOptNew.text = idType + " " + idNum + "  > Connections = " + cnt; 

                    ListAddItem(structureList,  idType + " " + idNum + "  > Connections = " + cnt, idNum); 
                }
            }

            SetupAutocomplete(structureList, document.getElementById('cellID'));
        }
    }

var autoCompleteSetup = [];

function SetupAutocomplete(listElement, textElement) 
{
    var PoundTextID = "#" + textElement.id;
    if($.inArray(PoundTextID, autoCompleteSetup))
    {
        $(PoundTextID).unautocomplete();
    }
    else
    {
        autoCompleteSetup[autoCompleteSetup.length] = PoundTextID; 
    }


    $(PoundTextID).autocomplete(updatedData, { minChars: 1,

        formatItem: function (dat) { return dat + ""; },

        formatMatch: function (inp) {
            var res = String(inp);
            var arr_res = res.split(' ');

            return arr_res[0];
        },

        formatResult: function (inp) {
            var res = String(inp);
            var arr_res = res.split(' ');

            return arr_res[0];
        },

        autoFill: true,
        matchContains: false,
        matchSubset: true,
        mustMatch: false,
        selectOnly: 1
    });

    document.getElementById("cellID").value = "";
    document.getElementById("cellID").disabled = false;
    document.getElementById("cellID").focus();

    enableGrid(); 
};

</script>
