
var data = [];
var actual = [];

var first = 0;

var firstO3Dloading = true;

var submitted = "false";
var source;
var lab;
var count = 0;

var typeOfCall = 1; // 1-Network, 0-Structure

var structureClientID;

var dataSourceClientID;

var labNameClientID;

function trim(s) {
    s = s.replace(/(^\s*)|(\s*$)/gi, "");
    s = s.replace(/[ ]{2,}/gi, " ");
    s = s.replace(/\n /, "\n");
    return s;
}


// remove progress animation and message unonload
window.onunload = function () {
    document.getElementById("progress").style.visibility = "hidden";
    document.getElementById("message").innerHTML = "";
};

// on load, focus and disable animations
function RunAfterLoad() {  

    document.getElementById("progress").style.visibility = "hidden";
    document.getElementById("message").innerHTML = "";
    document.getElementById("message").innerHTML = " <b>Enter / Choose a Valid Cell ID</b> ";

   

    updateList();

    $("#2d").qtip({
        content: { text: 'Generates Just the 2D graph' },
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


    $("#3d").qtip({
        content: { text: 'Generates a 3D Interactive Flash rendering along with 2D' },
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

    $("#downloadGraph").qtip({
        content: { text: 'Download Graph' },
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

    $("#generateGraph").qtip({
        content: { text: 'Generate Graph for Viewing' },
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
        content: { text: 'Explore Structures in a popup DataGrid' },
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

    $("#cellID").qtip('hide');

    var cellID = document.getElementById("cellID").value;
    var id = document.getElementById("submitButton");
    if (actual.indexOf(cellID) != -1 && cellID.length > 0) {
        document.getElementById("message").innerHTML = " <b style='color: #0000FF'>Hit Go! to generate Graph</b> ";
        id.disabled = false;
    }

    else {
        id.disabled = true;
        document.getElementById("message").innerHTML = " <b style='color: #E41B17'>Enter / Choose a Valid Cell ID</b> ";

    }

};

//update Go button for dropdown selection
function update_button1() {
    var val = document.getElementById(structureClientID).value;
    if (val != 0) {
        document.getElementById("cellID").disabled = true;
        document.getElementById("submitButton").disabled = false;
        document.getElementById("message").innerHTML = " <b style='color: #0000FF'>Hit Go! to generate Graph</b> | <b style='color: #6698FF'>Choose first option here ^ to enable textbox</b> ";
    }
    else {
        document.getElementById("cellID").disabled = false;
        if (document.getElementById("cellID").value.length == 0) {
            document.getElementById("submitButton").disabled = true;
            document.getElementById("message").innerHTML = " <b>Enter or Choose a Valid Cell ID</b> ";
        }

        else {
            update_button();
        }
    }

};

//remove all options before updating
function removeOptionSelected(param) {
    var elSel = document.getElementById(param);
    var i;
    for (i = elSel.length - 1; i >= 0; i--) {
        elSel.remove(i);
    }
};

// animate progress icon
function animate() {
    var id = document.getElementById("progress");
    id.style.visibility = "visible";
    var msg = document.getElementById("message");
    msg.innerHTML = " <b>Generating Graph...</b>";


};

// update lab when clicked upon
function updateLab() {
    lab = document.getElementById(labNameClientID).value;
    callLabServer();


};

//update darasources list when lab is updated
function updateList() {

    document.getElementById("cellID").value = "One Moment...";
    document.getElementById("cellID").disabled = true;

    source = document.getElementById(dataSourceClientID).value;
    lab = document.getElementById(labNameClientID).value;
    callServer();




};

//call server to get IDs 
function callServer() {

    removeOptionSelected(structureClientID);
    var oSelField = document.getElementById(structureClientID);
    var elOptNew = document.createElement('option');
    elOptNew.text = "...Fetching Cell IDs...";
    elOptNew.value = 0;
    try {
        oSelField.add(elOptNew, null); // Other browsers
    }
    catch (ex) {
        oSelField.add(elOptNew);

    }


    var urlAppend = "FormRequest/GetTopStructures?request=";
    var root = window.location

    var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
    var mat = root.toString().match(re)[0];

    var Url = mat + urlAppend + lab + "," + source + "," + typeOfCall;
    //alert(Url);

    document.getElementById('structureButton').style.display = "none";  

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
            removeOptionSelected(structureClientID);
            // No parsing necessary with JSON!
            var updatedData = [];

            actual = [];

            count = info.length - 1;
            structureIDMap = new Array();

            var ctr = 0;
            for (var i in info) {

                var oSelField = document.getElementById(structureClientID);
                var elOptNew = document.createElement('option');

                if (i == 0) {
                    elOptNew.value = 0;
                    elOptNew.text = document.getElementById(dataSourceClientID).value + " - IDs Ordered by # of Locations";
                }


                else {
                    elOptNew.value = info[i];

                    var arr = info[i].split('~');

                    var idNum = trim(arr[0]);

                    var idType = String(arr[1]);

                    var cnt = String(arr[2]);

                    structureIDMap[arr[0].replace(" ", "")] = arr[1];
                    structureIDSort[arr[0].replace(" ", "")] = arr[2];

                    elOptNew.value = idNum;

                    updatedData[ctr] = idNum + " " + idType + "<br/>> Locations = " + cnt;

                    actual[ctr] = idNum;

                    ctr++;

                    elOptNew.text = idNum + " " + idType + "  > Locations = " + cnt;

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

                $("#cellID").unautocomplete();
            }

            $("#cellID").autocomplete(updatedData, { minChars: 1,

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


            $("#cellID").qtip({
                content: { title: { text: 'Enter Cell ID' },
                    text: ' # of Cells in ' + document.getElementById(dataSourceClientID).value + ' - ' + count,
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
            $("#cellID").qtip('show');



            var structureData = [];

            for (key in structureIDMap) {

                structureData.push({ id: key, type: structureIDMap[key].replace(',', '').replace("<", "&%3C"), count: structureIDSort[key] });
            }

            jQuery("#structureList").jqGrid('GridUnload');
            jQuery("#structureList").jqGrid({
                data: structureData,
                datatype: "local",
                height: 'auto',
                rowNum: 20,
                rowList: [10, 20, 30],
                colNames: ['ID', 'Type', 'Locations Count'],
                colModel: [
   		            { name: 'id', index: 'id', width: 250, align: "center", sortable: true, sorttype: 'int' },
   		            { name: 'type', index: 'type', width: 250, align: "center", sortable: true, sorttype: 'text' },
                    { name: 'count', index: 'count', width: 250, align: "center", sortable: true, sorttype: 'int' }
   		            ],
                viewrecords: true,
                sortname: 'count',
                sortorder: 'desc',
                caption: "Structures Explorer",
                ignoreCase: true,
                searchoptions: { sopt: ['cn', 'bw', 'ew', 'eq'] },
                pager: $("#structurePager"),
                width: 640,
                height: 440,
                viewrecords: true,


                ignoreCase: true,
                url: '<%=webPath%>/FormRequest/ExportToExcel',
                rownumbers: true,
                imgpath: '<%=webPath%>/Content/redmond/images'

            });



            jQuery("#structureList").jqGrid('navGrid', '#structurePager', { edit: false, add: false, del: false, search: true, refresh: true, excel: true },
           {}, // default settings for edit
           {}, // default settings for add
           {}, // delete
           {closeOnEscape: true, multipleSearch: true }, // search options
           {}
         );

            jQuery('#structureList').jqGrid('navButtonAdd', '#structurePager',
        { caption: '', title: 'Export Grid data to Excel', buttonicon: 'ui-icon-newwin',
            onClickButton: function (e) {

                exportExcel($('#structureList'));
                //                jQuery("#nodeStatsTable").jqGrid('excelExport', { tag: 'excel', url: '<%=webPath%>/FormRequest/ExportToExcel' });
            }

        });

        jQuery("#structureList").jqGrid('filterToolbar', { defaultSearch: 'cn' });

        $(jQuery("#structureList")[0].grid.cDiv).click(function () {
                $(".ui-jqgrid-titlebar-close", this).click();
            });

            //now enable the buttons

            document.getElementById('structureButton').style.display = "inline";


            if (firstO3Dloading == true) {

                initO3D();
                firstO3Dloading = false;
            }
           

        }
    }
};

function format_input(a) {
    return a;
};

function format_result(a) {
    return a;
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

        updateList();
    }


};

function stopRKey(evt) {
    var evt = (evt) ? evt : ((event) ? event : null);
    var node = (evt.target) ? evt.target : ((evt.srcElement) ? evt.srcElement : null);
    if ((evt.keyCode == 13) && (node.type == "text")) { return false; }
}

document.onkeypress = stopRKey; 


