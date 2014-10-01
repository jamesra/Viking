var root = window.location

var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');

var mat = root.toString().match(re);

var Url = root.toString();
if (mat != null)
    Url = mat[0]; 


var minHop;
var maxHop;

var minSection;
var maxSection;

var adjacencyList;
var hopNodeMap;
var hopEdgeMap;

var requestedCell;

var sampleFirst = true;

//alert(Url);


//store edge and node types count   
var nodeTypeCount = new Array();
var edgeTypeCount = new Array();

var totalNodeCount = 0;
var totalEdgeCount = 0;


//store colors and type information
var nodeColorTable = new Array();
var edgeColorTable = new Array();

var nodeIDMap = new Array();
var edgeIDMap = new Array();

var nodeNameMap = new Array();
var edgeNameMap = new Array();

var networkIDMap = new Array();
var networkIDSort = new Array();
var structureIDMap = new Array();
var structureIDSort = new Array();


nodeColorTable["#ee0000"] = "AC";   // Flag Red
nodeColorTable["#cdcd00"] = "Aii";  // Golden
nodeColorTable["blue"] = "BC | OFF BC | CBa"; //blue
nodeColorTable["#008b00"] = "CBab"; //Dark Green
nodeColorTable["#00cd00"] = "GAC"; // Green
nodeColorTable["cadetblue"] = "GBC | CBb"; //cadetblue
nodeColorTable["saddlebrown"] = "GC"; //brown
nodeColorTable["white"] = "Ghost Nodes";
nodeColorTable["purple"] = "ROD BC"; //purple
nodeColorTable["grey"] = "TH | PROCESS | others"; //misc
nodeColorTable["#cd0000"] = "YAC"; // Red
nodeColorTable["#8b6914"] = "unnamed";


edgeColorTable["#cd0000"] = "Conventional";
edgeColorTable["#8b6914"] = "Gap Junction";
edgeColorTable["#458b00"] = "Ribbon Synapse | BC Conventional";
edgeColorTable["none"] = "Unknown | Frontier";
edgeColorTable["black"] = "Unknown | Frontier";


//node colors obtained in graph
var originalNodeColors = new Array();
var originalEdgeColors = new Array();

//the ones modified after user editing
var backedUpNodeColors = new Array();
var backedUpEdgeColors = new Array();

var nodeColorLookUp = new Array();
var edgeColorLookUp = new Array();

var userName = "";
var userEmail = "";

function backUp() {

  

    var tempArray = new Array();
    var tempStats = new Array();
    for (key in nodeColorTable) {
        for (tempKey in originalNodeColors) {
            if (tempKey == key) {
                tempArray[key] = nodeColorTable[key];
                tempStats[key] = nodeTypeCount[key];
            }

        }
    }

    originalNodeColors = tempArray;
    nodeTypeCount =  tempStats;

    var tempArray = new Array();
     var tempStats = new Array();
    for (key in edgeColorTable) {
        for (tempKey in originalEdgeColors) {
            if (tempKey == key)
            {
                tempArray[key] = edgeColorTable[key];
                tempStats[key] =  edgeTypeCount[key];
             }
        }
    }


    originalEdgeColors = tempArray;
    edgeTypeCount =  tempStats;

    drawStatsTable('Node');
    drawStatsTable('Edge');


    for (key in originalNodeColors)
        backedUpNodeColors[key] = originalNodeColors[key];
    for (key in originalEdgeColors)
        backedUpEdgeColors[key] = originalEdgeColors[key];

    for (key in originalNodeColors)
        nodeColorLookUp[key] = key;

    for (key in originalEdgeColors)
        edgeColorLookUp[key] = key;

}

function restore() {

    originalNodeColors = new Array();
    originalEdgeColors = new Array();
    for (key in backedUpNodeColors)
        originalNodeColors[key] = backedUpNodeColors[key];
    for (key in backedUpEdgeColors)
        originalEdgeColors[key] = backedUpEdgeColors[key];

    for (key in originalNodeColors)
        nodeColorLookUp[key] = key;

    for (key in originalEdgeColors)
        edgeColorLookUp[key] = key;

    nodeCheckBoxesMap = new Array();
    for (key in originalNodeColors)
        nodeCheckBoxesMap[key] = true;

    for (key in originalEdgeColors)
        edgeCheckBoxesMap[key] = true;


}

var doWhat = 0;

//maintain a global check boxes hashmap
var nodeCheckBoxesMap = new Array();
var edgeCheckBoxesMap = new Array();

var globalMessage = "These Buttons ^ help you customize the graph || If graph gets too messy hit 'Reset Graph'Button";

function colourNameToHex(colour) {
    var colours = { "aliceblue": "#f0f8ff", "antiquewhite": "#faebd7", "aqua": "#00ffff", "aquamarine": "#7fffd4", "azure": "#f0ffff",
        "beige": "#f5f5dc", "bisque": "#ffe4c4", "black": "#000000", "blanchedalmond": "#ffebcd", "blue": "#0000ff", "blueviolet": "#8a2be2", "brown": "#a52a2a", "burlywood": "#deb887",
        "cadetblue": "#5f9ea0", "chartreuse": "#7fff00", "chocolate": "#d2691e", "coral": "#ff7f50", "cornflowerblue": "#6495ed", "cornsilk": "#fff8dc", "crimson": "#dc143c", "cyan": "#00ffff",
        "darkblue": "#00008b", "darkcyan": "#008b8b", "darkgoldenrod": "#b8860b", "darkgray": "#a9a9a9", "darkgreen": "#006400", "darkkhaki": "#bdb76b", "darkmagenta": "#8b008b", "darkolivegreen": "#556b2f",
        "darkorange": "#ff8c00", "darkorchid": "#9932cc", "darkred": "#8b0000", "darksalmon": "#e9967a", "darkseagreen": "#8fbc8f", "darkslateblue": "#483d8b", "darkslategray": "#2f4f4f", "darkturquoise": "#00ced1",
        "darkviolet": "#9400d3", "deeppink": "#ff1493", "deepskyblue": "#00bfff", "dimgray": "#696969", "dodgerblue": "#1e90ff",
        "firebrick": "#b22222", "floralwhite": "#fffaf0", "forestgreen": "#228b22", "fuchsia": "#ff00ff",
        "gainsboro": "#dcdcdc", "ghostwhite": "#f8f8ff", "gold": "#ffd700", "goldenrod": "#daa520", "grey": "#808080", "green": "#008000", "greenyellow": "#adff2f",
        "honeydew": "#f0fff0", "hotpink": "#ff69b4",
        "indianred ": "#cd5c5c", "indigo ": "#4b0082", "ivory": "#fffff0", "khaki": "#f0e68c",
        "lavender": "#e6e6fa", "lavenderblush": "#fff0f5", "lawngreen": "#7cfc00", "lemonchiffon": "#fffacd", "lightblue": "#add8e6", "lightcoral": "#f08080", "lightcyan": "#e0ffff", "lightgoldenrodyellow": "#fafad2",
        "lightgrey": "#d3d3d3", "lightgreen": "#90ee90", "lightpink": "#ffb6c1", "lightsalmon": "#ffa07a", "lightseagreen": "#20b2aa", "lightskyblue": "#87cefa", "lightslategray": "#778899", "lightsteelblue": "#b0c4de",
        "lightyellow": "#ffffe0", "lime": "#00ff00", "limegreen": "#32cd32", "linen": "#faf0e6",
        "magenta": "#ff00ff", "maroon": "#800000", "mediumaquamarine": "#66cdaa", "mediumblue": "#0000cd", "mediumorchid": "#ba55d3", "mediumpurple": "#9370d8", "mediumseagreen": "#3cb371", "mediumslateblue": "#7b68ee",
        "mediumspringgreen": "#00fa9a", "mediumturquoise": "#48d1cc", "mediumvioletred": "#c71585", "midnightblue": "#191970", "mintcream": "#f5fffa", "mistyrose": "#ffe4e1", "moccasin": "#ffe4b5",
        "navajowhite": "#ffdead", "navy": "#000080",
        "oldlace": "#fdf5e6", "olive": "#808000", "olivedrab": "#6b8e23", "orange": "#ffa500", "orangered": "#ff4500", "orchid": "#da70d6",
        "palegoldenrod": "#eee8aa", "palegreen": "#98fb98", "paleturquoise": "#afeeee", "palevioletred": "#d87093", "papayawhip": "#ffefd5", "peachpuff": "#ffdab9", "peru": "#cd853f", "pink": "#ffc0cb", "plum": "#dda0dd", "powderblue": "#b0e0e6", "purple": "#800080",
        "red": "#ff0000", "rosybrown": "#bc8f8f", "royalblue": "#4169e1",
        "saddlebrown": "#8b4513", "salmon": "#fa8072", "sandybrown": "#f4a460", "seagreen": "#2e8b57", "seashell": "#fff5ee", "sienna": "#a0522d", "silver": "#c0c0c0", "skyblue": "#87ceeb", "slateblue": "#6a5acd", "slategray": "#708090", "snow": "#fffafa", "springgreen": "#00ff7f", "steelblue": "#4682b4",
        "tan": "#d2b48c", "teal": "#008080", "thistle": "#d8bfd8", "tomato": "#ff6347", "turquoise": "#40e0d0",
        "violet": "#ee82ee",
        "wheat": "#f5deb3", "white": "#ffffff", "whitesmoke": "#f5f5f5",
        "yellow": "#ffff00", "yellowgreen": "#9acd32"
    };

    return colours[colour.toLowerCase()] || false;
}

function drawStatsTable(type) {

    var element = document.getElementById("nodeStats");

    var colorTable = new Array();
    var typecount = new Array();

    if (type == "Node") {
        colorTable = nodeColorTable;
        typeCount = nodeTypeCount;
        element = document.getElementById("nodeStats");
    }
    else {
        colorTable = edgeColorTable;
        typeCount = edgeTypeCount;
        element = document.getElementById("edgeStats");
    }

    var tr = document.createElement("tr")
    element.appendChild(tr);

    for (key in typeCount) {

        var th = document.createElement("th");
        th.innerHTML = colorTable[key];
        tr.appendChild(th);

    }

    var tr = document.createElement("tr")
    element.appendChild(tr);

    for (key in typeCount) {

        var td = document.createElement("td");
        td.align = "center";
        td.innerHTML = typeCount[key];
        tr.appendChild(td);

    }


}


function exportExcel(grid) {

    var dataFromGrid = { row: grid.jqGrid('getGridParam', 'data') };
    var xmldata = '<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<rows>\n' +
                                       xmlJsonClass.json2xml(dataFromGrid, '\t') + '</rows>';

    var paras = new Array();
    paras = eval(dataFromGrid.row)

    var csvData = "";
    var first = 1;
    for (var i in dataFromGrid.row) {
        obj = dataFromGrid.row[i];

        for (attr in obj) {

            if (csvData == undefined)
                continue;
            csvData += obj[attr] + ",";
        }
        csvData = csvData.substring(0, csvData.length - 1) + "\n";

    }


    var postUrl = '<%=webPath%>/FormRequest/ExportToExcel';


    //                          $.ajax({
    //                              type: 'POST',
    //                              url: '<%=webPath%>/FormRequest/ExportToExcel',
    //                              data: xmldata,
    //                              success: '',
    //                              dataType: 'xml'
    //                          });
    //                         

    //             var mya = new Array();
    //             mya = $(id).getDataIDs();  // Get All IDs
    //             var data = $(id).getRowData(mya[0]);     // Get First row to get the labels
    //             var colNames = new Array();
    //             var ii = 0;
    //             for (var i in data) { colNames[ii++] = i; }    // capture col names
    //             var html = "";
    //             for (k = 0; k < colNames.length; k++) {
    //                 html = html + colNames[k] + "\t";     // output each Column as tab delimited
    //             }
    //             html = html + "\n";                    // Output header with end of line
    //             for (i = 0; i < mya.length; i++) {
    //                 data = $(id).getRowData(mya[i]); // get each row
    //                 for (j = 0; j < colNames.length; j++) {
    //                     html = html + data[colNames[j]] + "\t"; // output each Row as tab delimited
    //                 }
    //                 html = html + "\n";  // output each row with end of line

    //             }
    //             html = html + "\n";  // end of line at the end
    document.forms[0].gridData.value = csvData;
    document.forms[0].method = 'POST';
    document.forms[0].action = Url + 'FormRequest/ExportToExcel';  // send it to server which will open this contents in excel file
    document.forms[0].target = '';
    document.forms[0].submit();
}