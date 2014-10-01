

var svgDocument;

var svgRoot;

var originalSVGClone;

var freshOperation = true;

var root = window.location;

var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
var mat = root.toString().match(re)[0];

var Url = mat;

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


function cloneSVG() {

    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var oldLen = svgDocument.documentElement.getElementsByTagName('g').length;
    originalSVGClone = svgDocument.documentElement.cloneNode(true); // clone the whole document and store in originalSVGClone document


}


var doWhat = 0;

var nodeCheckBoxesMap = new Array();

var edgeCheckBoxesMap = new Array();

var visibleNodeColors = [];

var visibleEdgeColors = [];


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

    updateNodeCheckBoxes('All');
    updateEdgeCheckBoxes('All');

    //    for (var i = 0; i < newLen; i++) {
    //        newChildren[i].parentNode.removeChild(newChildren[i]);
    //    }

    //    for (var i = 0; i < oldLen; i++) {
    //        changedRoot.appendChild(oldChildren[i].cloneNode);
    //    }

}


function count() {


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

        else if (newChildren.item(i).getAttribute("display") === null && newChildren.item(i).getAttribute("class") == edge) {


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

        totalNodeCount = 0;
        totalEdgeCount = 0;
        for (key in nodeTypeCount)
            totalNodeCount += nodeTypeCount[key]
        for (key in edgeTypeCount)
            totalEdgeCount += edgeTypeCount[key];

    }

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

function createNodeButtons(string) {
    var nodeColors = string.split(',');

    var column = document.getElementById('nodeButton');

    var l = nodeColors.length;

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

    for (var i = 0; i < l; i++) {

        var items = nodeColors[i].split('~');

        var tr = document.createElement("tr");
        column.appendChild(tr);

        var td = document.createElement("td");
        tr.appendChild(td);

        var checkBox = document.createElement("input");
        checkBox.type = "checkbox";
        checkBox.setAttribute("onclick", "updateNodeCheckBoxes(\"" + items[0] + "\")");
        checkBox.setAttribute("id", items[0] + "NodeCheckBox");
        checkBox.setAttribute("checked", "checked");
        nodeCheckBoxesMap[items[0]] = true;
        checkBox.value = "all";
        td.appendChild(checkBox);


        var button = document.createElement("button");
        button.setAttribute("onclick", "updateNodeCheckBoxes(\"" + items[0] + "\")");
        if (items[0] != "white")
            button.setAttribute("style", "color: White; background-color: " + items[0] + "; border: 0; width: " + buttonWidth + "px;height: " + buttonHeight + "px");
        if (items[0] == "white")
            button.setAttribute("style", "color: Black; background-color: " + items[0] + "; border: 0; width: " + buttonWidth + "px;height: " + buttonHeight + "px");

        button.setAttribute("id", items[0] + "NodeButton");
        button.innerHTML = items[1];
        td.appendChild(button);


        var href = document.createElement("a");
        href.href = "javascript: changeColor(this);";
        href.id = items[0] + "ColorSelector";
        td.appendChild(href);


        var img = document.createElement("img");
        img.src = Url + "Content/icons/edit_icon.png";
        img.width = "24";
        img.height = "24";
        img.alt = "change color";
        img.align = "top";

        href.appendChild(img);

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

    //    var br = document.createElement("br");
    //    column.appendChild(br);
    //    br = document.createElement("br");
    //    column.appendChild(br);


}

function createEdgeButtons(string) {

    var edgeColors = string.split(',');

    var column = document.getElementById('edgeButton');

    var l = edgeColors.length;


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





    for (var i = 0; i < l; i++) {

        var items = edgeColors[i].split('~');

        var tr = document.createElement("tr");
        column.appendChild(tr);

        var td = document.createElement("td");
        tr.appendChild(td);


        var checkBox = document.createElement("input");
        checkBox.type = "checkbox";
        checkBox.setAttribute("onclick", "updateEdgeCheckBoxes(\"" + items[0] + "\")");
        checkBox.setAttribute("id", items[0] + "EdgeCheckBox");
        checkBox.setAttribute("checked", "checked");
        edgeCheckBoxesMap[items[0]] = true;
        checkBox.disabled = false;
        checkBox.value = "all";
        td.appendChild(checkBox);


        var button = document.createElement("button");
        button.setAttribute("onclick", "updateEdgeCheckBoxes(\"" + items[0] + "\")");
        button.setAttribute("style", "color: White; background-color: " + items[0] + "; border: 0; width: " + buttonWidth + "px; height: " + buttonHeight + "px");
        button.setAttribute("width", buttonWidth + "px");
        button.setAttribute("height", buttonHeight + "px");
        button.setAttribute("id", items[0] + "EdgeButton");
        button.innerHTML = items[1];
        td.appendChild(button);


        var href = document.createElement("a");
        href.href = "Javascript: changeColor(this)";
        href.id = items[0] + "ColorSelector";
        td.appendChild(href);

        var img = document.createElement("img");
        img.src = Url + "Content/icons/edit_icon.png";
        img.width = "24";
        img.height = "24";
        img.alt = "change color";
        img.align = "top";
        href.appendChild(img);


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


    for (var i = 1; i < newLen; i++) {

        var color;
        if (newChildren.item(i).getAttribute("class") == "node") {


            try {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("ellipse")[0].getAttribute("fill");
            }

            if (visibleNodeColors.indexOf(color) == -1) {
                newChildren.item(i).setAttribute("display", "none");
            }
            else {
                nodeIDs[nodeIDs.length] = newChildren.item(i).firstChild.firstChild.nodeValue; // get the node id which is to be visible
            }
        }
    }

    for (var i = 1; i < newLen; i++) {
        if (newChildren.item(i).getAttribute("class") == "edge") {

            try {
                color = newChildren.item(i).getElementsByTagName("path")[0].getAttribute("stroke");
            }
            catch (err) {
                color = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");

            }

            var arr = newChildren.item(i).firstChild.firstChild.nodeValue.split("->");

            if (visibleEdgeColors.indexOf(color) == -1)
                newChildren.item(i).setAttribute("display", "none");

            else if (!(nodeIDs.indexOf(arr[0]) != -1 || nodeIDs.indexOf(arr[1]) != -1)) {
                newChildren.item(i).setAttribute("display", "none");
            }

        }

    }


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

function saveJPEG() {
    callConverter("JPEG");
}
function savePNG() {
    callConverter("PNG");
}
function savePDF() {
    callConverter("PDF");
}
function saveSVG() {
    callConverter("SVG");
}

function callConverter(type) {

    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var serializer = new XMLSerializer();
    var str = serializer.serializeToString(changedRoot);

    post_to_url(str, type);
    //    open("data:image/svg+xml," + encodeURIComponent(str));
}

function post_to_url(str, type) {

    var urlAppend = "FormRequest/ConvertTo" + type + "?request=" + str;
    var root = window.location;

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re)[0];

    var Url = mat + urlAppend;
    //alert(Url);

    xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = ProcessRequest;
    xmlHttp.open("GET", Url, true);
    //    xmlHttp.setRequestHeader('Content-Type', 'data:image/svg+xml');
    //    xmlhttp.send(yourdata);   
    xmlHttp.send(str);
}

function ProcessRequest() {
}

function switchView(divID) {


    //    if (divID == "v") {
    //        document.getElementById('filteringMenuV').style.visibility = "collapse";
    //        document.getElementById('filteringMenuH').style.visibility = "visible";
    //        createNodeButtons("h");
    //        
    //    }
    //    else {

    //        document.getElementById('filteringMenuV').style.visibility = "visible";
    //        document.getElementById('filteringMenuH').style.visibility = "collapse";
    //        createNodeButtons("v");
    //    }


}

