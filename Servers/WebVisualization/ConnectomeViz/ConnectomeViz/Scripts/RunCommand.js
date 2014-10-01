

var svgDocument;

var svgRoot;

var originalSVGClone;

var freshOperation = true;

var root = window.location;

var Url = root.toString();

var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
var mat = root.toString().match(re);

if (mat != null)
    Url = mat[0]; 


var jsonGraphStats = {};

function getJsonGraphStats() {
    return jsonGraphStats;
}



function updateGrid() {
    //    jQuery("#jsonGraphStats").jqGrid(
    //         { url: 'Javascript: getJsonGraphStats();', datatype: "json",
    //             colNames: ['Cell1', 'Cell2', 'ConnectionType'],
    //             colModel: [{ name: 'id', index: 'id', width: 55 },
    //          { name: 'Cell1', index: 'Cell1', width: 90 },
    //           { name: 'Cell2', index: 'Cell2', width: 100 },
    //           { name: 'ConnectionType', index: 'ConnectionType', width: 80, align: "right"}],
    //             rowNum: 10, rowList: [10, 20, 30],
    //             pager: '#jsonGraphStatsList', sortname: 'id',
    //             viewrecords: true, sortorder: "desc", caption: "JSON Example"
    //         });
    //    jQuery("#jsonGraphStats").jqGrid('navGrid', '#jsonGraphStatsList', { edit: false, add: false, del: false });
    //    

}


/*
function runcheck() {

    svgDocument = document.getElementById("SVGGraph").contentDocument;
    svgRoot = svgDocument.documentElement;
    svgRoot.removeAttribute('width');
    svgRoot.removeAttribute('height');
    svgRoot.removeAttribute('viewBox');
    //    svgRoot.setAttribute('width','100%');
    //    svgRoot.setAttribute('height', '100%');

    svgRoot.setAttribute("svgns", "http://www.w3.org/2000/svg");
    //    svgRoot.setAttribute("onload", "init(evt)");

    var topElement = svgDocument.getElementById('graph1');
    topElement.setAttribute('id', 'viewport');
}
*/


function cloneSVG() {

    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var oldLen = svgDocument.documentElement.getElementsByTagName('g').length;
    originalSVGClone = svgDocument.documentElement.cloneNode(true); // clone the whole document and store in originalSVGClone document   

}

function processGraph() {

    freshOperation = true;
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var nodes = changedRoot.getElementsByTagName('g');
    var nodesLength = nodes.length;

    requestedCell = changedRoot.getElementsByTagName('title')[0].firstChild.nodeValue;

    //        SVGDocument = evt.target.ownerDocument;
    //        SVGRoot = SVGDocument.documentElement;
    //        nodes = SVGDocument.getElementsByTagName('g');
    //        nodesLength = nodes.length;

    //   
      
    for (var i = 1; i < nodesLength; i++) {

        nodes.item(i).setAttribute('visibility', "visible");

        if (nodes.item(i).getAttribute("class") == "node") {

            var color;
            try {
                color = nodes.item(i).getElementsByTagName("polygon")[0].getAttribute("fill")
            }
            catch (err) {
                color = nodes.item(i).getElementsByTagName("ellipse")[0].getAttribute("fill")
            }

            var node = nodes.item(i);
            if (node == null)
                continue;

            var aElements = node.getElementsByTagName('a');
            if (aElements == null || aElements == undefined)
                continue;

            if (aElements.length == 0)
                continue;

            var title = aElements[0].getAttribute("xlink:title");

            var type = title.toString().split(',')[1];

            color = color.replace("notdefined", "");
            color = color.replace(" ", "");
            color = color.replace("\n", "");

            if (nodeColorTable[color] != undefined)
                originalNodeColors[color] = nodeColorTable[color];
            else
                originalNodeColors[color] = type + "appended";

            if (nodeTypeCount[color] === undefined) {
                nodeTypeCount[color] = 1;
            }
            else {
                nodeTypeCount[color] = nodeTypeCount[color] + 1;
            }

            nodes.item(i).setAttribute('onclick', "click(evt)");
            nodes.item(i).setAttribute('onmouseover', "blur(evt)");
            nodes.item(i).setAttribute('onmouseout', "dark(evt)");

            var idTemp = nodes.item(i).getElementsByTagName('a')[0].getAttribute('xlink:title').split(',');
            nodeIDMap[idTemp[0].replace(" ", "")] = idTemp[1];
            nodeNameMap[nodes.item(i).getAttribute("id")] = idTemp[0].replace(" ", "");

            //nodes.item(i).setAttribute('onmousedown', "top.copyToClipBoard(this.getAttribute('href'))");
        }
        else //edge
        {

            var color;
            try {
                color = nodes.item(i).getElementsByTagName("path")[0].getAttribute("stroke")
            }
            catch (err) {
                color = nodes.item(i).getElementsByTagName("polygon")[0].getAttribute("fill")
            }

            var type = nodes.item(i).getAttribute("id");

            color = color.replace("notdefined", "");
            color = color.replace(" ", "");
            color = color.replace("\n", "");

            if (edgeColorTable[color] != undefined)
                originalEdgeColors[color] = edgeColorTable[color];
            else
                originalEdgeColors[color] = type + "appended";

            if (edgeTypeCount[color] === undefined)
                edgeTypeCount[color] = 1;
            else
                edgeTypeCount[color] = edgeTypeCount[color] + 1;

            var idTemp = nodes.item(i).firstChild.firstChild.nodeValue;
            edgeIDMap[idTemp] = edgeColorTable[color];
            edgeNameMap[nodes.item(i).getAttribute("id")] = idTemp;
        }



    }




    //        ans = ans.substring(0, ans.length - 1);
    //       top.createNodeButtons(ans);
    //        

    //        color = "";
    //        ans = "";
    //        for (type in originalEdgeColors) {
    //               ans += type + "~" + edgeColorTable[type] + ",";
    //        }
    //        ans = ans.substring(0, ans.length - 1);
    //        top.createEdgeButtons(ans);

    if (selectedMap["Network"]) {
        backUp();

        createNodeButtons();

        createEdgeButtons();

        cloneSVG();

        createAdjacencyList();

        updateMessage(globalMessage);
    }

}


function createAdjacencyList() {

    adjacencyList = new Array();

    for (key in edgeIDMap) {

        var arr = key.split("->");
        var id1 = arr[0].replace(" ", "");
        var id2 = arr[1].replace(" ", "");

        if (adjacencyList[id1] === undefined) {
            adjacencyList[id1] = new Array();
            adjacencyList[id1].push(id2);
        }
        else {
            if (adjacencyList[id1].indexOf(id2) == -1)
                adjacencyList[id1].push(id2);
        }

        if (adjacencyList[id2] === undefined) {
            adjacencyList[id2] = new Array();
            adjacencyList[id2].push(id1);
        }
        else {
            if (adjacencyList[id2].indexOf(id1) == -1)
                adjacencyList[id2].push(id1);
        }


    }


    for (key in nodeIDMap) {
        if (adjacencyList[key] === undefined) {
            adjacencyList[key] = new Array();
        }

        //requested cell is hop 0

        var hopCount = 0
        hopNodeMap = new Array();

        var penultimateArray = new Array();
        penultimateArray.push(requestedCell);

        var ultimateArray = new Array();

        while (penultimateArray.length > 0) {

            ultimateArray = new Array();

            for (i in penultimateArray) {
                hopNodeMap[penultimateArray[i]] = hopCount;
                var tempArray = adjacencyList[penultimateArray[i]];
                for (j in tempArray) {
                    if (ultimateArray.indexOf(tempArray[j]) == -1 && hopNodeMap[tempArray[j]] === undefined && penultimateArray.indexOf(tempArray[j]) == -1)
                        ultimateArray.push(tempArray[j]);
                }
            }

            penultimateArray = new Array();
            for (val in ultimateArray) {
                penultimateArray.push(ultimateArray[val]);
            }

            hopCount++;

        } //while

        maxHop = --hopCount;
        minHop = 0;


        hopEdgeMap = new Array();
        for (key in edgeIDMap) {
            var vals = key.split("->");
            if (hopNodeMap[vals[0]] > hopNodeMap[vals[1]])
                hopEdgeMap[key] = hopNodeMap[vals[0]];
            else
                hopEdgeMap[key] = hopNodeMap[vals[1]];
        }






    } //for

} //func


var visibleNodeColors = [];

var visibleEdgeColors = [];



function Count() {


    freshOperation = true;
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    for (var i = 1; i < newLen; i++) {

        if (newChildren.item(i).getAttribute("display") === null && newChildren.item(i).getAttribute("class") == "node") {
            try {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("ellipse")[0].getAttribute("fill");
            }

            if (nodeTypeCount[color] == undefined)
                nodeTypeCount[color] = 1;
            else
                nodeTypeCount[color] = nodeTypeCount[color] + 1;
        }

        else if (newChildren.item(i).getAttribute("display") === null && newChildren.item(i).getAttribute("class") == "edge") {


            try {
                color = newChildren.item(i).getElementsByTagName("path")[0].getAttribute("stroke");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");

            }

            if (edgeTypeCount[color] == undefined)
                edgeTypeCount[color] = 1;
            else
                edgeTypeCount[color] = edgeTypeCount[color] + 1;
        }

    }

    totalNodeCount = 0;
    totalEdgeCount = 0;
    for (key in nodeTypeCount)
        totalNodeCount += nodeTypeCount[key]
    for (key in edgeTypeCount)
        totalEdgeCount += edgeTypeCount[key];

};

function inverseSVG() {

    for (key in nodeCheckBoxesMap) {
        nodeCheckBoxesMap[key] = !nodeCheckBoxesMap[key];
        if (nodeCheckBoxesMap[key])
            document.getElementById(key + "NodeCheckBox").checked = true;
        else
            document.getElementById(key + "NodeCheckBox").checked = false;
    }

    for (key in edgeCheckBoxesMap) {
        edgeCheckBoxesMap[key] = !edgeCheckBoxesMap[key];

        if (edgeCheckBoxesMap[key])
            document.getElementById(key + "EdgeCheckBox").checked = true;
        else
            document.getElementById(key + "EdgeCheckBox").checked = false;

    }

    updateVisibleColors();
    hideNodes();
}

function resetSVG() {

    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    var oldMain = changedRoot.getElementsByTagName('g')[0];
    oldMain.parentNode.removeChild(oldMain);

    changedRoot.appendChild(originalSVGClone.getElementsByTagName('g')[0].cloneNode(true));

    restore();
    refreshButtonColors();
    updateNodeCheckBoxes('All');
    updateEdgeCheckBoxes('All');




    //    for (var i = 0; i < newLen; i++) {
    //        newChildren[i].parentNode.removeChild(newChildren[i]);
    //    }

    //    for (var i = 0; i < oldLen; i++) {
    //        changedRoot.appendChild(oldChildren[i].cloneNode);
    //    }

}

function refreshButtonColors() {
    var node = document.getElementById('nodeButton');
    while (node.hasChildNodes())
        node.removeChild(node.lastChild);

    var node = document.getElementById('edgeButton');
    while (node.hasChildNodes())
        node.removeChild(node.lastChild);

    createNodeButtons();
    createEdgeButtons();
    updateMessage(globalMessage);
}

function updateNodeCheckBoxes(string) {



    if (string == "All") // all nodes
    {

        for (key in nodeCheckBoxesMap) {
            nodeCheckBoxesMap[key] = true;
            document.getElementById(key + "NodeCheckBox").checked = true;
        }


    }

    else if (string == "None") {

        for (key in nodeCheckBoxesMap) {
            nodeCheckBoxesMap[key] = false;
            document.getElementById(key + "NodeCheckBox").checked = false;
        }

    }
    else {


        nodeCheckBoxesMap[string] = !nodeCheckBoxesMap[string];

        if (nodeCheckBoxesMap[string])
            document.getElementById(string + "NodeCheckBox").checked = true;
        else
            document.getElementById(string + "NodeCheckBox").checked = false;





    }

    updateVisibleColors();
    hideNodes();

}

//function checkBox(string)
//{
//    var obj = document.getElementById(string);

//    if(obj.checked)
//        obj.checked = false;
//    else
//        obj.checked = true;
//}

function updateEdgeCheckBoxes(string) {



    if (string == "All") // all edges
    {

        for (key in edgeCheckBoxesMap) {
            edgeCheckBoxesMap[key] = true;
            document.getElementById(key + "EdgeCheckBox").checked = true;
        }

    }
    else if (string == "None") {

        for (key in edgeCheckBoxesMap) {
            edgeCheckBoxesMap[key] = false;
            document.getElementById(key + "EdgeCheckBox").checked = false;
        }

    }
    else {

        edgeCheckBoxesMap[string] = !edgeCheckBoxesMap[string];
        if (edgeCheckBoxesMap[string])
            document.getElementById(string + "EdgeCheckBox").checked = true;
        else
            document.getElementById(string + "EdgeCheckBox").checked = false;

    }

    updateVisibleColors();
    hideNodes();


}

function updateVisibleColors() {

    visibleNodeColors = [];

    visibleEdgeColors = [];

    for (key in nodeCheckBoxesMap) {
        if (nodeCheckBoxesMap[key])
            visibleNodeColors[visibleNodeColors.length] = nodeColorLookUp[key];
    }

    for (key in edgeCheckBoxesMap) {
        if (edgeCheckBoxesMap[key])
            visibleEdgeColors[visibleEdgeColors.length] = edgeColorLookUp[key];
    }
}


var buttonWidth = 250;

var buttonHeight = 23;


var selectButtonWidth = 100;



function createNodeButtons() {


    var column = document.getElementById('nodeButton');

    var columnCount = 0;

    var override = 0;



    var tr = document.createElement("tr");
    column.appendChild(tr);

    var td = document.createElement("td");
    tr.appendChild(td);

    var button = document.createElement("button");
    button.setAttribute("onclick", "updateNodeCheckBoxes('None')");
    button.setAttribute("style", "color: White; background-color: Black; border: 0; padding: 2 ; margin: 4;height: " + buttonHeight + "px\"");

    button.setAttribute("id", "NoneNodeButton");

    button.innerHTML = "Clear All";
    td.appendChild(button);


    for (key in originalNodeColors) {



        var tr = document.createElement("tr");
        column.appendChild(tr);

        var td = document.createElement("td");
        tr.appendChild(td);

        var checkBox = document.createElement("input");
        checkBox.type = "checkbox";
        checkBox.setAttribute("onclick", "updateNodeCheckBoxes(\"" + key + "\")");
        checkBox.setAttribute("id", key + "NodeCheckBox");
        checkBox.setAttribute("checked", "checked");
        nodeCheckBoxesMap[key] = true;
        checkBox.value = "all";
        td.appendChild(checkBox);

        var button = document.createElement("button");
        button.setAttribute("onclick", "updateNodeCheckBoxes(\"" + key + "\")");
        if (key != "white")
            button.setAttribute("style", "color: White; background-color: " + key + "; border: 0; width: " + buttonWidth + "px;height: " + buttonHeight + "px");
        if (key == "white")
            button.setAttribute("style", "color: Black; background-color: " + key + "; border: 0; width: " + buttonWidth + "px;height: " + buttonHeight + "px");

        button.setAttribute("id", key + "NodeButton");
        button.innerHTML = originalNodeColors[key];
        td.appendChild(button);


        //        var href = document.createElement("a");
        //        href.href = "javascript: changeColor(this);";
        //        href.id = items[0] + "ColorSelector";
        //        td.appendChild(href);

        //       
        //        var img = document.createElement("img");
        //        img.src = Url + "Content/icons/edit_icon.png";
        //        img.width = "24";
        //        img.height = "24";
        //        img.alt = "change color";          
        //        img.align = "top";

        //        href.appendChild(img);

        var text = document.createTextNode(" ");
        td.appendChild(text);

        var span = document.createElement("span");

        var tempId = "";
        if (key.indexOf('#') != -1)
            tempId = key.substring(1, key.length) + "SpanNode";
        else
            tempId = key + "SpanNode";

        span.setAttribute("style", "display:inline; align:middle; width: 10px; height: 10px;");
        span.id = tempId;
        td.appendChild(span);

        //        var br = document.createElement("br");
        //        column.appendChild(br);
        //        br = document.createElement("br");
        //        column.appendChild(br);

        //        var str = "<button style =\"color: White ; background-color: " + items[0] + "; border: 0; width: 16\" onclick=\"hideNodes(\""+items[0]+"\","+ doWhat +")\">" + items[1] + "</button> ";
        //        column.innerHTML += str;
        columnCount++;
    }


    //    var br = document.createElement("br");
    //    column.appendChild(br);


    var tr = document.createElement("tr");
    column.appendChild(tr);

    var td = document.createElement("td");
    tr.appendChild(td);

    var button = document.createElement("button");
    button.setAttribute("onclick", "updateNodeCheckBoxes('All')");
    button.setAttribute("style", "color: White; background-color: Black; border: 0; padding: 2 ; margin: 4; height: " + buttonHeight + "px\"");
    button.setAttribute("width", buttonWidth.toString);
    button.setAttribute("id", "AllNodeButton");


    button.innerHTML = "Select All";
    td.appendChild(button);

    updateColorPanels("Node");

    //    var br = document.createElement("br");
    //    column.appendChild(br);
    //    br = document.createElement("br");
    //    column.appendChild(br);

}



function createEdgeButtons() {

    var column = document.getElementById('edgeButton');


    var columnCount = 0;

    var override = 0;

    var tr = document.createElement("tr");
    column.appendChild(tr);

    var td = document.createElement("td");
    tr.appendChild(td);

    var button = document.createElement("button");
    button.setAttribute("onclick", "updateEdgeCheckBoxes('None')");
    button.setAttribute("style", "color: White; background-color: Black; border: 0; padding: 2 ; margin: 4;height: " + buttonHeight + "px\"");
    button.setAttribute("width", buttonWidth.toString);
    button.setAttribute("id", "EdgeButton");


    button.innerHTML = "Clear All";
    td.appendChild(button);





    for (key in originalEdgeColors) {

        var tr = document.createElement("tr");
        column.appendChild(tr);

        var td = document.createElement("td");
        tr.appendChild(td);


        var checkBox = document.createElement("input");
        checkBox.type = "checkbox";
        checkBox.setAttribute("onclick", "updateEdgeCheckBoxes(\"" + key + "\")");
        checkBox.setAttribute("id", key + "EdgeCheckBox");
        checkBox.setAttribute("checked", "checked");
        edgeCheckBoxesMap[key] = true;
        checkBox.disabled = false;
        checkBox.value = "all";
        td.appendChild(checkBox);


        var button = document.createElement("button");
        button.setAttribute("onclick", "updateEdgeCheckBoxes(\"" + key + "\")");
        button.setAttribute("style", "color: White; background-color: " + key + "; border: 0; width: " + buttonWidth + "px; height: " + buttonHeight + "px");
        button.setAttribute("width", buttonWidth + "px");
        button.setAttribute("height", buttonHeight + "px");
        button.setAttribute("id", key + "EdgeButton");
        button.innerHTML = originalEdgeColors[key];
        td.appendChild(button);


        //        var href = document.createElement("a");
        //        href.href = "Javascript: changeColor(this)";
        //        href.id = items[0] + "ColorSelector";
        //        td.appendChild(href);

        //        var img = document.createElement("img");
        //        img.src = Url + "Content/icons/edit_icon.png";
        //        img.width = "24";
        //        img.height = "24";
        //        img.alt = "change color";
        //        img.align = "top";        
        //        href.appendChild(img);

        var text = document.createTextNode(" ");
        td.appendChild(text);


        var tempId = "";
        if (key.indexOf('#') != -1)
            tempId = key.substring(1, key.length) + "SpanEdge";
        else
            tempId = key + "SpanNode";

        var span = document.createElement('span');
        span.setAttribute("style", "display:inline; vertical-align:bottom; width: 10px; height: 10px;");
        span.id = tempId;
        td.appendChild(span);

        //      

        //        var str = "<button style =\"color: White ; background-color: " + items[0] + "; border: 0; width: 16\" onclick=\"hideNodes(\""+items[0]+"\","+ doWhat +")\">" + items[1] + "</button> ";
        //        column.innerHTML += str;
        columnCount++;



    }

    var tr = document.createElement("tr");
    column.appendChild(tr);

    var td = document.createElement("td");
    tr.appendChild(td);


    var button = document.createElement("button");
    button.setAttribute("onclick", "updateEdgeCheckBoxes('All')");
    button.setAttribute("style", "color: White; background-color: Black; border: 0; padding: 2 ; margin: 4 ;height: " + buttonHeight + "px\"");
    button.setAttribute("width", buttonWidth.toString);
    button.setAttribute("id", "AllEdgeButton");


    button.innerHTML = "Select All";
    td.appendChild(button);


    // everything is ready, now create color panels
    updateColorPanels("Edge");
}

function updateMessage(string) {

    document.getElementById("messages").innerHTML = string;
}

function hideNodes() {

    showNodes(true);
    updateVisibleColors();
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    var nodeIDs = [];
    var filteredColors = [];
    jsonGraphStats = {};

    for (var i = 1; i < newLen; i++) {

        var color;
        if (newChildren.item(i).getAttribute("class") == "node") {

            var it = hopNodeMap[nodeNameMap[newChildren.item(i).getAttribute("id")]];

            try {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("ellipse")[0].getAttribute("fill");
            }

            if (visibleNodeColors.indexOf(color) == -1 || !(it <= maxHop && it >= minHop)) {
                newChildren.item(i).setAttribute("display", "none");
            }
            else {
                nodeIDs[nodeIDs.length] = newChildren.item(i).firstChild.firstChild.nodeValue; // get the node id which is to be visible
            }
        }
    }

    for (var i = 1; i < newLen; i++) {


        if (newChildren.item(i).getAttribute("class") == "edge") {

            var it = hopEdgeMap[edgeNameMap[newChildren.item(i).getAttribute("id")]];

            try {
                color = newChildren.item(i).getElementsByTagName("path")[0].getAttribute("stroke");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");

            }

            var arr = newChildren.item(i).firstChild.firstChild.nodeValue.split("->");


            var sections = newChildren.item(i).getElementsByTagName("a")[0];

            if (sections != undefined) {
                var sectionIDs = sections.getAttribute("xlink:href");
                sectionIDs = sectionIDs.replace("#", "");
                var involvedSections = sectionIDs.split(',');
            }
            else {

                var faultyEdge = newChildren.item(i).firstChild.firstChild.nodeValue;
            }


            var sectionMatchHide = false;

            for (var j = 0; j < involvedSections.length; j++) {
                if (!(parseInt(involvedSections[j]) >= minSection && parseInt(involvedSections[j]) <= maxSection)) {
                    sectionMatchHide = true;
                    nodeIDs[nodeIDs.length] = newChildren.item(i).firstChild.firstChild.nodeValue;
                }
            }


            if (sectionMatchHide)
                newChildren.item(i).setAttribute("display", "none");


            if (visibleEdgeColors.indexOf(color) == -1 || !(it <= maxHop && it >= minHop))
                newChildren.item(i).setAttribute("display", "none");

            else if (!(nodeIDs.indexOf(arr[0]) != -1 || nodeIDs.indexOf(arr[1]) != -1)) {
                newChildren.item(i).setAttribute("display", "none");
            }
            //               else // this edge needs to be displayed
            //               {
            //                   jsonGraphStats.push({Cell1: arr[0],Cell2: arr[1], ConnectionType:edgeColorTable[color]}); // create active visible connections graph
            //               }


        }

    }

    //          updateGrid();

    displayConnectedCellsString();

}




function showNodes(show) {

    freshOperation = true;
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    for (var i = 1; i < newLen; i++) {

        if (show) {
            try {
                newChildren.item(i).removeAttribute("display");
            }
            catch (err)
        { }
        }
        else {
            try {
                newChildren.item(i).setAttribute("display", "none");
            }
            catch (err)
        { }
        }

    }
}

function showEdges(show) {

    freshOperation = true;
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    for (var i = 1; i < newLen; i++) {

        if (show) {
            try {
                newChildren.item(i).removeAttribute("display");
            }
            catch (err)
        { }
        }
        else {
            try {
                if (newChildren.item(i).getAttribute("class") == "edge")
                    newChildren.item(i).setAttribtue("display", "none");
            }
            catch (err)
        { }
        }

    }
}


function hideEdges() {

    hideNodes();
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    var nodeIDs = [];
    var filteredColors = [];

    for (key in edgeCheckBoxesMap) {
        if (edgeCheckBoxesMap[key]) // colors to be shown
            filteredColors[filteredColors.length] = key;
    }



    for (var i = 1; i < newLen; i++) {

        var color;
        if (newChildren.item(i).getAttribute("class") == "edge" && newChildren.item(i).getAttribute("display") === null) {


            try {
                color = newChildren.item(i).getElementsByTagName("path")[0].getAttribute("stroke");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");

            }

            // if the color is not in array, hide it
            if (filteredColors.indexOf(color) == -1) {
                newChildren.item(i).setAttribute("display", "none");
            }




        }

    }


}

function getConnectedNodes() {


    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    var connectedNodeIDs = new Array();
    var connectedNodeIDsResult = new Array();


    for (var i = 1; i < newLen; i++) {

        var color;
        if (newChildren.item(i).getAttribute("class") == "node" && newChildren.item(i).getAttribute("display") === null) {

            connectedNodeIDs[newChildren.item(i).firstChild.firstChild.nodeValue] = true;

        } //if

        else {
            connectedNodeIDs[newChildren.item(i).firstChild.firstChild.nodeValue] = false;

        }

    } // for


    for (var i = 1; i < newLen; i++) {

        var color;
        if (newChildren.item(i).getAttribute("class") == "edge" && newChildren.item(i).getAttribute("display") === null) {

            var arr = newChildren.item(i).firstChild.firstChild.nodeValue.split("->");

            if (connectedNodeIDs[arr[0]] && connectedNodeIDs[arr[1]]) {

                connectedNodeIDsResult[arr[0]] = true;
                connectedNodeIDsResult[arr[1]] = true;
            }

        }
    }


    return connectedNodeIDsResult;

}

Object.size = function (obj) {
    var size = 0, key;
    for (key in obj) {
        if (obj.hasOwnProperty(key)) size++;
    }
    return size;
};


function sortObj(arr) {
    // Setup Arrays
    var sortedKeys = new Array();
    var sortedObj = {};

    // Separate keys and sort them
    for (var i in arr) {
        sortedKeys.push(parseInt(i.toString()));
    }
    sortedKeys.sort();

    // Reconstruct sorted obj based on keys
    for (var i in sortedKeys) {
        sortedObj[sortedKeys[i].toString()] = arr[sortedKeys[i].toString()];
    }
    return sortedObj;
}


function displayConnectedCellsString() {
    var nodes = getConnectedNodes();

    var nodes = sortObj(nodes);

    var sortedKeys = new Array();
    for (var i in nodes) {
        sortedKeys.push(parseInt(i.toString()));
    }
    sortedKeys = sortedKeys.sort();


    var answer = "";
    for (var index in sortedKeys) {

        answer += sortedKeys[index] + " ";
    }

    var length = Object.size(nodes);

    answer = answer.substring(0, answer.length - 1);

    document.getElementById("vikingPlotMessage").innerHTML = length + " Cells connected by edges:"

    document.getElementById("vikingPlotNumbers").value = answer;
}


function getPath() {

    var root = window.location;

    var Url = root.toString(); 

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re);

    if (mat != null)
        Url = mat[0]; 

    return Url;
}

function saveJPG() {
    var Url = getPath();
    document.getElementById("saveJPGLink").style.visibility = "visible";
    document.getElementById("saveJPGReady").src = Url + "Content/ajax-loader.gif";
    document.getElementById("saveJPGLink").href = "";

    callConverter("JPG");
}
function savePNG() {
    var Url = getPath();
    document.getElementById("savePNGLink").style.visibility = "visible";
    document.getElementById("savePNGReady").src = Url + "Content/ajax-loader.gif";
    document.getElementById("savePNGLink").href = "";
    callConverter("PNG");
}

function savePDF() {
    var Url = getPath();
    document.getElementById("savePDFLink").style.visibility = "visible";
    document.getElementById("savePDFReady").src = Url + "Content/ajax-loader.gif";
    document.getElementById("savePDFLink").href = "";
    callConverter("PDF");
}

function saveDOT() {
    var Url = getPath();
    document.getElementById("saveDOTLink").style.visibility = "visible";
    document.getElementById("saveDOTReady").src = Url + "Content/ajax-loader.gif";
    document.getElementById("saveDOTLink").href = "";
    callConverter("DOT");
}

function saveSVG() {
    var Url = getPath();
    document.getElementById("saveSVGLink").style.visibility = "visible";
    document.getElementById("saveSVGReady").src = Url + "Content/ajax-loader.gif";
    document.getElementById("saveSVGLink").href = "";
    callConverter("SVG");
}

function callConverter(type) {

    var mat = getPath();
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var serializer = new XMLSerializer();
    var str = serializer.serializeToString(changedRoot);

    post_to_url(str, type);
    //    open("data:image/svg+xml," + encodeURIComponent(str));
}



function post_to_url(str, type) {

    var Url = getPath();

    var urlAppend = "FormRequest/ConvertTo" + type;

    Url = Url + urlAppend;


    //alert(Url
    var sendData = new Array();
    sendData["svgData"] = str;
    var values = { values: sendData };
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = function () {

        if (xmlHttp.readyState == 4 && xmlHttp.status == 200) {

            var path = xmlHttp.responseText;
            var re = new RegExp("\.(...)$", 'im');
            var type = path.toString().match(re)[1];
            type = type.toUpperCase();
            document.getElementById("save" + type + "Ready").src = Url + "Content/icons/arrow_b_d.png";
            document.getElementById("save" + type + "Link").href = xmlHttp.responseText;
            document.getElementById("save" + type + "Link").style.visibility = "visible";
            var win = open(xmlHttp.responseText, "Opening graph...");
            win.focus();
        }
    };
    xmlHttp.open("POST", Url, true);
    //    xmlhttp.send(yourdata);   
    xmlHttp.send(str);


    //    $.ajax({
    //        type: "POST",
    //        url: Url,
    //        data: values,
    //        success: function (data) {
    //            alert(data.Result);
    //        },
    //        dataType: "json",
    //        traditional: true
    //    });
};



