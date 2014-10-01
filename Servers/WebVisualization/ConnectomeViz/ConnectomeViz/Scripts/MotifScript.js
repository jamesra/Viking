
var data = [];
var actual = [];

var first = 0;

var submitted = "false";
var source;
var lab;
var count = 0;

var typeOfCall = 1; // 1-Network, 0-Structure

var labelPatternListClientID;

var volumeNameClientID;
var serverNameClientID;


// remove progress animation and message unonload
window.onunload = function () {
    document.getElementById("progress").style.visibility = "hidden";
    document.getElementById("message").innerHTML = "";
};

// on load, focus and disable animations
function RunAfterLoad() {
    labelPatternListClientID = "ctl00_MainContent_labelPatternList";
    serverNameClientID = "ctl00_MainContent_serverName";
    volumeNameClientID = "ctl00_MainContent_volumeName"; 

    document.getElementById("progress").style.visibility = "hidden";
    document.getElementById("message").innerHTML = "";
    document.getElementById("message").innerHTML = " <b>[ Enter / Choose a label ]</b> ";

    updateList();


    //    jQuery.noConflict();

    $("#getRequestLink").qtip({
        content: { text: 'Quickly Request a graph for 3 hops ' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    for (key in originalNodeColors)
        createToolTip(key + "NodeCheckbox");

    $("#labelPattern").qtip({
        content: { text: 'Class of cells to graph' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    $("#freshQuery").qtip({
        content: { text: 'Request graph with updated connections' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });


    $("#svgGraph").qtip({
        content: { text: 'An Interactive SVG graph is generated' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    $("#flashGraph").qtip({
        content: { text: 'Graph is Rendered as Flash' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    $("#reduceEdges").qtip({
        content: { text: 'All multiple Edges are removed in the graph' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    $("#arrow").qtip({
        content: { text: 'Explore Cells in a popup DataGrid' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });

    $("#pinNodes").qtip({
        content: { text: 'Request graph with Anatomical Node positions' },
        position: {
            corner: {
                tooltip: 'bottomMiddle', // Use the corner.
                target: 'topMiddle' // ...and opposite corner
            }
        },
        solo: false,
        delay: 0,
        style: 'mystyle',
        show: 'mouseover',
        hide: 'mouseout'

    });
};


//update Go button for cellID updates
function update_button() {

   // $("#labelPattern").qtip('hide');

    var labelPattern = document.getElementById("labelPattern").value;
    var id = document.getElementById("submitButton");
    if (actual.indexOf(labelPatternListClientID) != -1 && labelPatternListClientID.length > 0) {
        document.getElementById("message").innerHTML = " <b style='color: #0000FF'>Hit Go! to generate Graph</b> ";
        id.disabled = false;
    }

    else {
        id.disabled = false;
        document.getElementById("message").innerHTML = " <b style='color: #E41B17'>Enter / Choose a Valid Cell ID</b> ";

    }
};

//update Go button for dropdown selection
function update_button1() {

    var list = document.getElementById("ctl00_MainContent_labelPatternList");
    if (list == null)
        return;

    if (list.value != 0) {
        list.disabled = false;
        document.getElementById("submitButton").disabled = false;
        document.getElementById("message").innerHTML = " <b style='color: #0000FF'>Hit Go! to generate Graph</b> | <b style='color: #6698FF'>Choose first option here ^ to enable textbox</b> ";
    }
    else {
        list.disabled = false;
        if (list.value.length == 0) {
            document.getElementById("submitButton").disabled = false;
            document.getElementById("message").innerHTML = " <b>Enter or Choose a Valid Cell ID</b> ";
        }

        else {
            update_button();
        }
    }

};


// animate progress icon
function animate() {
    var id = document.getElementById("progress");
    id.style.visibility = "visible";
    var msg = document.getElementById("message");
    msg.innerHTML = " <b>Generating Graph...</b>";
};

//update darasources list when lab is updated
function updateList() {
    var list = document.getElementById("ctl00_MainContent_labelPatternList");
    if (list == null)
        return;

    list.value = "One Moment...";
    list.disabled = false;
     
    callServer();
};

//call server to get IDs 
function callServer() {

    serverName = SelectedServer();
    volumeName = SelectedVolume();

    
    var oSelField = document.getElementById("<%=labelPatternList.ClientID%>");
    if (oSelField == null)
        return;

    removeOptionSelected(oSelField);

    var elOptNew = document.createElement('option');
    elOptNew.text = "...Fetching Cell Labels...";
    elOptNew.value = 0;
    try {
        oSelField.add(elOptNew, null); // Other browsers
    }
    catch (ex) {
        oSelField.add(elOptNew);
    }

    var urlAppend = "FormRequest/GetStructureLabelsForType?request=";
    var root = window.location

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re)[0];

    var Url = mat + urlAppend + serverName + "," + volumeName + "," + typeOfCall;
    //alert(Url);

    document.getElementById('networkButton').style.display = "none";

    xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = ProcessRequest;
    xmlHttp.open("GET", Url, true);

    xmlHttp.send(null);

};

// update IDs list after callback
function ProcessRequest() {

    if (xmlHttp.readyState == 4 && xmlHttp.status == 200) {


        if (xmlHttp.responseText == "Not found") {
            alert("Sorry couldn't connect to server, try again after some time");
        }
        else {
            var info = eval("(" + xmlHttp.responseText + ")");
            removeOptionSelected(labelPatternListClientID);
            // No parsing necessary with JSON!
            var updatedData = [];
            networkIDMap = new Array();
            actual = [];

            count = info.length - 1;

            var ctr = 0;
            for (var i in info) {

                var oSelField = document.getElementById(labelPatternListClientID);
                var elOptNew = document.createElement('option');
                elOptNew.text = info[i];

                if (i == 0) {
                    elOptNew.value = 0;
                    elOptNew.text = info[i];
                }

                else {
                    elOptNew.value = info[i];
                }


                try {
                    oSelField.add(elOptNew, null); // Other browsers
                }
                catch (ex) {
                    oSelField.add(elOptNew); // IE only
                }

            }

            if (first == 0) {
                first = 1;
            }
            else {
                $("#labelPattern").unautocomplete();
            }

            $("#labelPattern").autocomplete(updatedData, { minChars: 1,

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

            var labelPattern = document.getElementById("labelPattern")
            if (labelPattern != null) {
                document.getElementById("labelPattern").value = "";
                document.getElementById("labelPattern").disabled = false;
                document.getElementById("labelPattern").focus();
            }


            $("#labelPattern").qtip({
                content: { title: { text: 'Choose label or use a regular expression to select multiple cell classes' },
                    text: ' ',
                    prerender: false
                },
                position: {
                    corner: {
                        tooltip: 'topMiddle', // Use the corner.
                        target: 'bottomMiddle' // ...and opposite corner
                    }
                },
                style: 'mystyle',
                show: false,
                solo: false,
                hide: { when: 'unfocus', delay: 1000 }

            });
            $("#labelPattern").qtip('show');

            enableGrid();

        }
    }
};

function enableGrid() {
    var networkData = [];

    for (key in networkIDMap) {

        networkData.push({ id: key, type: networkIDMap[key].replace(',', ''), count: networkIDSort[key] });
    }

    jQuery("#networkList").jqGrid('GridUnload');
    jQuery("#networkList").jqGrid({
        data: networkData,
        datatype: "local",
        height: 'auto',
        rowNum: 20,
        rowList: [10, 20, 30],
        colNames: ['ID', 'Type', 'Connections Count'],
        colModel: [
   		            { name: 'id', index: 'id', width: 250, align: "center", sortable: true, sorttype: 'int' },
   		            { name: 'type', index: 'type', width: 250, align: "center", sortable: true, sorttype: 'text' },
                    { name: 'count', index: 'count', width: 250, align: "center", sortable: true, sorttype: 'int' }
   		            ],
        viewrecords: true,
        sortname: 'count',
        sortorder: 'desc',
        caption: "Cells Explorer",
        ignoreCase: true,
        searchoptions: { sopt: ['cn', 'bw', 'ew', 'eq'] },
        pager: $("#networkPager"),
        width: 640,
        height: 440,
        viewrecords: true,


        ignoreCase: true,
        url: '<%=webPath%>/FormRequest/ExportToExcel',
        rownumbers: true,
        imgpath: '<%=webPath%>/Content/redmond/images'

    });



    jQuery("#networkList").jqGrid('navGrid', '#networkPager', { edit: false, add: false, del: false, search: true, refresh: true, excel: true },
           {}, // default settings for edit
           {}, // default settings for add
           {}, // delete
           {closeOnEscape: true, multipleSearch: true }, // search options
           {}
         );

    jQuery('#networkList').jqGrid('navButtonAdd', '#networkPager',
        { caption: '', title: 'Export Grid data to Excel', buttonicon: 'ui-icon-newwin',
            onClickButton: function (e) {

                exportExcel($('#networkList'));
                //                jQuery("#nodeStatsTable").jqGrid('excelExport', { tag: 'excel', url: '<%=webPath%>/FormRequest/ExportToExcel' });
            }

        });

    jQuery("#networkList").jqGrid('filterToolbar', { defaultSearch: 'cn' });

    $(jQuery("#networkList")[0].grid.cDiv).click(function () {
        $(".ui-jqgrid-titlebar-close", this).click();
    });

    //now enable the button

    document.getElementById('networkButton').style.display = "inline";

}

function format_input(a) {
    return a;
};

function format_result(a) {
    return a;
};



