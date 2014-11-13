var text_data = 'SVG';
var svgdoc = "";
var nodes = new Array();
var edges = new Array();
var nodeIDs = new Array();
var edgeIDs = new Array();
var elements = [];
var nodesLength;
var elementsLength;
var edgesLength;
var blurValue = 0.05;
var opaqueValue = 1.0;
var originalCloneRoot;
var SVGRoot;

var isHidden = 0;

var removeNodes = [];
var removeEdges;

xmlns = "http://www.w3.org/2000/svg"
var svgns = 'http://www.w3.org/2000/svg';
var xlinkns = 'http://www.w3.org/1999/xlink';

try {
    // function to create letters and animated elements on loading svg
    function init(evt) {

        SVGDocument = evt.target.ownerDocument;
        SVGRoot = SVGDocument.documentElement;
        originalCloneRoot = SVGRoot.cloneNode(true);

        initialize();
        top.cloneSVG(evt);
    };

    function initialize() {
        elements = SVGRoot.getElementsByTagName('g');
        elementsLength = elements.length;
        for (var i = 1; i < elementsLength; i++) {
            var node = elements.item(i);

            node.setAttribute('visibility', "visible");

            if (node.getAttribute("class") == "node") {

                node.setAttribute('onclick', "blur(evt)");
                node.setAttribute("onmouseover", "blur(evt)");
                node.setAttribute('onmouseout', "dark(evt)");
                nodes[node.getAttribute('id')] = node;
                nodeIDs[node.firstChild.firstChild.nodeValue] = node.getAttribute('id');
                //nodes.item(i).setAttribute('onmousedown', "top.copyToClipBoard(this.getAttribute('href'))");
            }
            else if (node.getAttribute("class") == "edge") {
                edges[node.getAttribute('id')] = node;

                edgeIDs[node.firstChild.firstChild.nodeValue] = node.getAttribute('id');
            }

        }

        nodesLength = nodes.length;

        edgesLength = edges.length;



        //	edges =  SVGDocument.getElementsByTagName('g');

    }

    function blur(evt) {

        var nodeID = evt.target.parentNode.parentNode.getAttribute('id');
        var ID = evt.target.parentNode.parentNode.firstChild.firstChild.nodeValue;

        for (var nodeKey in nodes) {
            if (nodeKey == nodeID)
                continue;
            else
                nodes[nodeKey].setAttribute("visibility", "hidden");
            //	        removeNodes.push(nodeKey);
        }


        var connected = new Array(); // contains NodeIds of all the connected cells

        for (var edgeKey in edgeIDs) {
            arr = edgeKey.split("->");
            if ((arr[0] == ID)) {
                edges[edgeIDs[edgeKey]].setAttribute("visibility", "visible");
                removeNodes.splice(removeNodes.indexOf(nodeIDs[arr[1]]), 1);
                connected.push(arr[1]);
            }
            else if ((arr[1] == ID)) {
                edges[edgeIDs[edgeKey]].setAttribute("visibility", "visible");
                removeNodes.splice(removeNodes.indexOf(nodeIDs[arr[0]]), 1);
                connected.push(arr[0]);
            }
            else {
                //                edges[edgeIDs[edgeKey]].setAttribute("opacity", blurValue);
                //                edges[edgeIDs[edgeKey]].parentNode.removeChild(edges[edgeIDs[edgeKey]]);
                edges[edgeIDs[edgeKey]].setAttribute("visibility", "hidden");
            }


        }


        //        var delLen = removeNodes.length;
        //        for (var i = 0; i < delLen; i++) {

        //            //            nodes[removeNodes[i]].parentNode.removeChild(nodes[removeNodes[i]]);
        //            nodes[removeNodes[i]].setAttribute("visibility", "hidden");
        //        }

        var len = connected.length;
        for (var i = 0; i < len; i++) {
            nodes[nodeIDs[connected[i]]].setAttribute("visibility", "visible");
        }

    }

    function dark(evt) {

        for (var nodeKey in nodes) {
            nodes[nodeKey].setAttribute("visibility", "visible");
        }

        for (var edgeKey in edges) {
            edges[edgeKey].removeAttribute("visibility", "visible");
        }

    }




}
catch (err) {
    alert(err.description);

}