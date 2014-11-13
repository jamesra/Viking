var data1;
var data2
var chart1;
var chart2;

var data = [];
var actual = [];

var first = 0;

var submitted = "false";

var count = 0;


var dataSourceClientID;

var labNameClientID;


var source;
var lab;
var dataSource;

var root = window.location

var statsMap =  new Array();

var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
var mat = root.toString().match(re)[0];

var Url = mat 

function trim(s) {
    s = s.replace(/(^\s*)|(\s*$)/gi, "");
    s = s.replace(/[ ]{2,}/gi, " ");
    s = s.replace(/\n /, "\n");
    return s;
}


// remove progress animation and message unonload
window.onunload = function () {
//    document.getElementById("progress").style.visibility = "hidden";
    
};



// on load, focus and disable animations
function RunAfterLoad() {

    lab = document.getElementById(labNameClientID).value;
    dataSource = document.getElementById(dataSourceClientID).value;

  

    updateStats();
};



// update lab when clicked upon
function updateLab() {
    lab = document.getElementById(labNameClientID).value;
    callLabServer();


};

// Call server to get labs and datasource
function callLabServer() {


    removeOptionSelected(dataSourceClientID);
    var oSelField = document.getElementById(dataSourceClientID);
    var elOptNew = document.createElement('option');
    elOptNew.text = "...Fetching DataSources...";
    elOptNew.value = 0;
    try {
        oSelField.add(elOptNew, null); // Other browsers
    }
    catch (ex) {
        oSelField.add(elOptNew);
    }


    var urlAppend = "FormRequest/GetVolumes?request=";
    var root = window.location

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re)[0];

    var Url = mat + urlAppend + lab;
    //alert(Url);

    xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = ProcessRequestLab;
    xmlHttp.open("GET", Url, true);

    xmlHttp.send(null);

};



//call server to get IDs 
function callServer() {

   

    var urlAppend = "FormRequest/GetTopStructures?request=";
    var root = window.location

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re)[0];

    var Url = mat + urlAppend + lab + "," + source + "," + typeOfCall;
    //alert(Url);

    document.getElementById('networkButton').style.display = "none";

    xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = ProcessRequest;
    xmlHttp.open("GET", Url, true);

    xmlHttp.send(null);

};



         
function format_input(a) {
    return a;
};

function format_result(a) {
    return a;
};



//After lab information is received, populate lab and datasource
function ProcessRequestLab() {

    if (xmlHttp.readyState == 4 && xmlHttp.status == 200) {


        if (xmlHttp.responseText == "Not found") {
            alert("Sorry couldn't connect to server, Please try again after some time");
        }
        else {
            var info = eval("(" + xmlHttp.responseText + ")");
            removeOptionSelected(dataSourceClientID);
            // No parsing necessary with JSON!
            //                 alert(info);
            for (var i in info) {

                var oSelField = document.getElementById(dataSourceClientID);
                var elOptNew = document.createElement('option');
                elOptNew.text = info[i];
                elOptNew.value = info[i];

                try {
                    oSelField.add(elOptNew, null); // Other browsers
                }
                catch (ex) {
                    oSelField.add(elOptNew); // IE only
                }

            }
        }

        dataSource = document.getElementById(dataSourceClientID).value;
        updateStats(); // everything is set, now update stats
    }


};

function stopRKey(evt) {
    var evt = (evt) ? evt : ((event) ? event : null);
    var node = (evt.target) ? evt.target : ((evt.srcElement) ? evt.srcElement : null);
    if ((evt.keyCode == 13) && (node.type == "text")) { return false; }
};

function copyToClipBoard(str) {
    alert(str);
};

document.onkeypress = stopRKey;


function readStats(data)
{

  var items = [];
  $.getJSON('ajax/test.json', function(data) {

  $.each(data, function(key, val) {
    items.push('<li id="' + key + '">' + val + '</li>');
  });

  $('<ul/>', {
    'class': 'my-new-list',
    html: items.join('')
  }).appendTo('body');
});
}

var uniqueElements = new Array();
var nameToSynapsesMap =  new Array();
var synapseToNamesMap = new Array();
var namesMap = new Array();
var synapsesMap = new Array();
var namesCountMap = new Array();
var vizNode;
var backUpVizNode;


function updateStats() {

    //dataSource = document.getElementById(dataSourceClientID).value;
    dataSource = "connectomes.utah.edu"

    vizNode  = document.getElementById('googleSection');
    backUpVizNode = vizNode.cloneNode(true);

        document.getElementById("labDisplay").innerHTML = lab;
        document.getElementById("dataSourceDisplay").innerHTML = dataSource;

        // remove visualizations before posting to server

        jQuery("#statsTable").jqGrid('GridUnload');
        document.getElementById('statsTableProgress').style.visibility = "visible";
        
        
        var flashNode = document.getElementById('Wire');
        var backUpFlashNode = flashNode.cloneNode(true);        
        flashNode.parentNode.removeChild(flashNode);

//        var vizNode = document.getElementById('googleSection');
//        vizNode.parentNode.removeChild(vizNode);
//        var replaceChild = backUpVizNode.cloneNode(true);
//        document.getElementById('googleSectionParent').appendChild(replaceChild);
//       

        document.getElementById('pieChartProgress').style.visibility = "visible";
        document.getElementById('treeMapProgress').style.visibility = "visible";
        

        uniqueElements = new Array();
        nameToSynapsesMap = new Array();
        synapseToNamesMap = new Array();
        namesMap = new Array();
        synapsesMap = new Array();
        namesCountMap = new Array();


        $.ajax({
            type: 'GET',
            url: Url + "Stats/StatsJSON",
            dataType: 'json',
            contentType: "application/json; charset=utf-8",
            data: "lab=" + lab + "&dataSource=" + dataSource,
            success: function (data, status) {

                //hide animation
                document.getElementById('statsTableProgress').style.visibility = "hidden";


                var strData = data;
                var jsonData = eval(data);
                for (row in jsonData) {
                    var temp = jsonData[row].id.replace("[", ",").replace("]", "").split(",");
                    namesMap[temp[0]] = temp[1];
                    if (namesCountMap[temp[1]] == undefined)
                        namesCountMap[temp[1]] = 1;
                    else
                        namesCountMap[temp[1]] = namesCountMap[temp[1]] + 1; 

                    nameToSynapsesMap[temp[0]] = new Array();
                    for (i in jsonData[row].synapses) {
                        var tmp2 = jsonData[row].synapses[i].split(",");
                        var num = parseInt(tmp2[1]);
                        var typeOfSynapse = tmp2[0];
                        typeOfSynapse = typeOfSynapse.replace(" ", "_");
                        nameToSynapsesMap[temp[0]][typeOfSynapse] = num;
                        if (synapsesMap[typeOfSynapse] == undefined)
                            synapsesMap[typeOfSynapse] = num;
                        else
                            synapsesMap[typeOfSynapse] = synapsesMap[typeOfSynapse] + num;

                        if (synapseToNamesMap[typeOfSynapse] == undefined) {
                            synapseToNamesMap[typeOfSynapse] = new Array();
                            synapseToNamesMap[typeOfSynapse][temp[0]] = num;
                        }
                        else {
                            synapseToNamesMap[typeOfSynapse][temp[0]] = num;
                        }
                    }

                }


                statsData = [];
                statsColModel = [];
                statsColNames = [];

                for (id in nameToSynapsesMap) {

                    var obj = new Object();
                    obj["ID"] = id;
                    obj["Type"] = namesMap[id];

                    for (i in synapsesMap) {
                        if (nameToSynapsesMap[id][i] == undefined)
                            obj[i] = 0;
                        else
                            obj[i] = nameToSynapsesMap[id][i];
                    }
                    statsData.push(obj);
                }


                statsColNames.push('ID');
                statsColNames.push('Type');
                for (i in synapsesMap) {
                    statsColNames.push(i);
                }

                statsColModel.push({ name: 'ID', index: 'ID', width: 50, align: "left", sortable: true, sorttype: 'int' });
                statsColModel.push({ name: 'Type', index: 'Type', width: 50, align: "left", sortable: true, sorttype: 'text' });
                for (i in synapsesMap) {
                    statsColModel.push({ name: i, index: i, width: 50, align: "left", sortable: true, sorttype: 'int' });
                }




                jQuery("#statsTable").jqGrid({
                    data: statsData,
                    datatype: "local",
                    height: 'auto',
                    rowNum: 10,
                    rowList: [10, 20, 30],
                    colNames: statsColNames,
                    colModel: statsColModel,
                    searchoptions: { sopt: ['cn', 'bw', 'ew', 'eq'] },
                    pager: $("#statsPager"),
                    viewrecords: true,
                    sortname: 'Total',
                    sortorder: 'desc',
                    caption: "Statistics Datagrid (Click to Expand/Collapse)",
                    ignoreCase: true,
                    url: '<%=webPath%>/FormRequest/ExportToExcel',
                    rownumbers: true,
                    //             grouping: false,
                    //             groupingView: { groupField: ['nodetype'],
                    //                 groupColumnShow: [true], groupText: ['<b>{0} - {1} Item(s)</b>'], groupCollapse: false, groupOrder: ['desc']
                    //             },
                    imgpath: '<%=webPath%>/Content/redmond/images'
                });



                jQuery("#statsTable").jqGrid('navGrid', '#statsPager', { edit: false, add: false, del: false, search: true, refresh: true, excel: true },
           {}, // default settings for edit
           {}, // default settings for add
           {}, // delete
           {closeOnEscape: true, multipleSearch: true }, // search options
           {}
         );


                jQuery('#statsTable').jqGrid('navButtonAdd', '#statsPager',
        { caption: '', title: 'Export Grid data to Excel', buttonicon: 'ui-icon-newwin',
            onClickButton: function (e) {

                exportExcel($('#statsTable'));
                //                jQuery("#nodeStatsTable").jqGrid('excelExport', { tag: 'excel', url: '<%=webPath%>/FormRequest/ExportToExcel' });
            }

        });

                jQuery("#statsTable").jqGrid('filterToolbar', { defaultSearch: 'cn' });


                var statsGrid = jQuery('#statsTable');

                $(statsGrid[0].grid.cDiv).click(function () {
                    $(".ui-jqgrid-titlebar-close", this).click();
                });

                finalizeDrawing();
            }



        });

        document.getElementById('flashViz').appendChild(backUpFlashNode);
       
        


    }

    function drawChart() {
        data = new google.visualization.DataTable();
        data.addColumn('string', 'Task');
        data.addColumn('number', 'Hours per Day');
        data.addRows(5);
        data.setValue(0, 0, 'Work');
        data.setValue(0, 1, 11);
        data.setValue(1, 0, 'Eat');
        data.setValue(1, 1, 2);
        data.setValue(2, 0, 'Commute');
        data.setValue(2, 1, 2);
        data.setValue(3, 0, 'Watch TV');
        data.setValue(3, 1, 2);
        data.setValue(4, 0, 'Sleep');
        data.setValue(4, 1, 7);

        finishDrawing();
    }

    function finishDrawing() {
       

    }

    function finalizeDrawing() {
        data1 = new google.visualization.DataTable();

        document.getElementById('pieChartProgress').style.visibility = "hidden";
        document.getElementById('treeMapProgress').style.visibility = "hidden";


       

        data1.addColumn('string', 'Synapse');
        data1.addColumn('number', 'Count');
        data1.addRows(Object.size(synapsesMap));

        for (row in Object.size(synapsesMap)) {
            var i = row;
        }
        
        var i=0
        for(key in synapsesMap) {

            if (key == "Total")
                continue;
            data1.setValue(i, 0, key);
            data1.setValue(i, 1, synapsesMap[key]);
            i++;
            
        }

        
        chart1 = new google.visualization.PieChart(document.getElementById('synapsesPieChart'));
        chart1.draw(data1, { width: 450, height: 300, is3D: true, title: 'Synapse Types - Pie Chart' });

        data2 = new google.visualization.DataTable();

        data2.addColumn('string', 'Node');
        data2.addColumn('number', 'Count');
        data2.addRows(Object.size(namesCountMap));

        var i = 0
        for (key in namesCountMap) {

            data2.setValue(i, 0, key);
            data2.setValue(i, 1, namesCountMap[key]);
            i++;

        }

       
        chart2 = new google.visualization.PieChart(document.getElementById('nodesPieChart'));
        chart2.draw(data2, { width: 450, height: 300, is3D: true, title: 'Node Types - Pie Chart' });

       
    }






//remove all options before updating
function removeOptionSelected(param) {
    var elSel = document.getElementById(param);
    var i;
    for (i = elSel.length - 1; i >= 0; i--) {
        elSel.remove(i);
    }
};


//to get length of associative array Object.size(array)
Object.size = function (obj) {
    var size = 0, key;
    for (key in obj) {
        if (obj.hasOwnProperty(key)) size++;
    }
    return size;
};

function sortObj(object, sortFunc) {
    var rv = [];
    for (var k in object) {
        if (object.hasOwnProperty(k)) rv.push({ key: k, value: object[k] });
    }
    rv.sort(function (o1, o2) {
        return sortFunc(o1.key, o2.key);
    });
    return rv;
}
