var text_data = 'SVG';
var svgdoc = "";
var nodes = [];
var edges = [];
var nodesLength;
xmlns = "http://www.w3.org/2000/svg"
var svgns = 'http://www.w3.org/2000/svg';
var xlinkns = 'http://www.w3.org/1999/xlink';


var nodeColorCount = new Array();
var nodeColorTable = new Array(); 

var clicked = 0;

try {
    // function to create letters and animated elements on loading svg
    function init(evt) {

        SVGDocument = evt.target.ownerDocument;
        SVGRoot = SVGDocument.documentElement;
        nodes = SVGDocument.getElementsByTagName('g');
        edges = SVGDocument.getElementsByTagName('g');
        nodesLength = nodes.length;

        top.processGraph(); 
    }
   
    function click(evt) {

        if (clicked == 0) {
            clicked = 1;           
        }
        else
            clicked = 0;

        blur(evt);

        if(clicked == 1)
        top.updateMessage("Cell ID:" + evt.target.parentNode.parentNode.getAttribute('id') + " is pinned(only showing its connections), click on any visible node to unpin");
    }


    function blur(evt) {
        for (var i = 1; i < nodesLength; i++) {
            if (nodes.item(i).getAttribute('id') == evt.target.parentNode.parentNode.getAttribute('id'))
                continue;
            else
                nodes.item(i).setAttribute("visibility", "hidden");
        }
        nodeid = evt.target.parentNode.parentNode.firstChild.firstChild.nodeValue;


        var connected = new Array();
        for (var i = 1; i < nodesLength; i++) {

            if (edges.item(i).getAttribute("class") == "edge") {
                arr = edges.item(i).firstChild.firstChild.nodeValue.split("->");
                if ((arr[0] == nodeid)) {
                    edges.item(i).setAttribute("visibility", "visible");
                    connected.push(arr[1]);


                }
                else {
                    if ((arr[1] == nodeid)) {
                        edges.item(i).setAttribute("visibility", "visible");
                        connected.push(arr[0]);
                    }
                    else
                        edges.item(i).setAttribute("visibility", "hidden");

                }
            }

        }
        for (var i = 1; i < nodesLength; i++) {
            var connectedLength = connected.length;
            if (nodes.item(i).getAttribute("class") == "node") {
                for (var j = 0; j < connectedLength; j++) {
                    if (connected[j] == (nodes.item(i).firstChild.firstChild.nodeValue))
                        nodes.item(i).setAttribute("visibility", "visible");
                }
            }
        }


        top.updateMessage("Showing connections for Cell ID:" + nodeid);
    }

    function dark(evt) {

        if (clicked == 0) {

            for (var i = 1; i < nodes.length; i++) {
                nodes.item(i).setAttribute("visibility", "visible");
            }

            top.updateMessage(top.globalMessage);
        }

    }


}
catch (err) {
    alert(err.description);

}