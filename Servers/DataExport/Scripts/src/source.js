var BASE_URL = "http://webdev.connectomes.utah.edu/";
var INTRO = "http://";
var DOMAIN = ".connectomes.utah.edu/";
var POST_XHR = null;


function buildIDListString(id_string) {
    var tokens = id_string.split(/\s+/g);

    formatted_output_array = [];
    for (var i = 0; i < tokens.length; i++) {
        formatted_token = encodeURIComponent(tokens[i]);
        formatted_output_array.push(formatted_token);
    }
     
    return formatted_output_array.join();
}

function getReportType()
{
    var reportTypeSelect = document.getElementById("reportTypeSelect");
    var reportType = reportTypeSelect.value;
    return reportType;
}

function getVolume()
{
    var volumeSelect = document.getElementById("volumeSelect");
    var selectedVolume = volumeSelect.options[volumeSelect.selectedIndex].value;
    return selectedVolume;
}

function getFormatType() {
    var formatSelect = document.getElementById("formatSelect");
    var formatType = formatSelect.options[formatSelect.selectedIndex].value;
    return formatType;
}

function update_message(msg)
{
    var msgElem = document.getElementById("messages");
    msgElem.value = msg + "\n" + msgElem.value;
}

function getQuery()
{
    var volumeSelect = document.getElementById("volumeSelect");
    var selectedVolume = volumeSelect.options[volumeSelect.selectedIndex].value;

    var query = "";
    if (selectedVolume.indexOf("_") != -1) {
        // special case alternate subdomain
        var divIndex = selectedVolume.indexOf("_");

        var subdomain = selectedVolume.substring(0, divIndex);
        console.log(subdomain);

        selectedVolume = selectedVolume.substring(divIndex + 1);
        console.log(selectedVolume);

        query = INTRO + subdomain + DOMAIN + selectedVolume + "/export/" + getReportType();
    }

    else {
        query = BASE_URL + selectedVolume + "/export/" + getReportType();
    }

    return query + "/" + getFormatType();
}


function onReportTypeChanged() {
  var reportTypeSelect = document.getElementById("reportTypeSelect");

  for (var i = 0; i < reportTypeSelect.length; ++i) {
    // Hide every report type example image that wasn't just selected
    var reportTypeImage = document.getElementById(
        reportTypeSelect.options[i].value + "Img");
    
    reportTypeImage.style.display = "none";

    // Hide the controls for every report type that wasn't just selected
    var reportTypeControls = document.getElementById(
        reportTypeSelect.options[i].value + "Params");

    reportTypeControls.style.display = "none";
  }

  var selectedReportTypeImage = document.getElementById(
      reportTypeSelect.value + "Img");

  var selectedReportTypeControls = document.getElementById(
      reportTypeSelect.value + "Params");

  selectedReportTypeImage.style.display = "inline";
  selectedReportTypeControls.style.display = "inline";

  var reportType = reportTypeSelect.value;
  var dotOption = document.getElementById("fileFormat");
  if (reportType == "morphology") {
    // You can't choose this for Morphology export
    //dotOption.style.display = "none";
    var formatSelect = document.getElementById("formatSelect");
  } else {
    // You can for everything else!
    dotOption.style.display = "inline";
  }
}

function onNetworkCellIDsChanged() {
  var networkCellIdBox = document.getElementById("networkCellId");
  var hopsP = document.getElementById("hopsP");

  var idString = networkCellIdBox.value;

  if( idString)
      idString = encodeURIComponent(idString);

  console.log(idString);

  if (idString == "") {
    hopsP.style.display = "none"
  } else
  {
    hopsP.style.display = "inline"
  }
}


function onSubmitClicked() {
  var reportType = getReportType();
  var selectedVolume = getVolume();

  var query = getQuery();

  // TODO select the volume

  if (reportType == "network") {
    var cellIDs = document.getElementById("networkCellId").value;
    var hops = document.getElementById("networkHops").value;

    if (cellIDs)
        cellIDs = buildIDListString(cellIDs);


    if (cellIDs.length > 0) {
      query += "?id=" + cellIDs;

      query += "&hops=" + hops;
    }
  }

  else if (reportType == "motif") {
    query += "s/";
    query += getFormatType();
  }

  else if (reportType == "morphology") {
    var cellIDs = document.getElementById("morphoCellId").value;
    if (cellIDs)
        cellIDs = buildIDListString(cellIDs);

    query += "?id=" + cellIDs;
  }

  console.log(query);
  window.open(query);
}

function onPostStateChange() {
    var DONE = this.DONE || 4;
    var HEADERS_RECEIVED = this.HEADERS_RECEIVED || 2;
    if (this.readyState == DONE && (this.status == 201 || this.status == 200)) {
        update_message("Generated " + this.responseURL);
        window.location = this.responseURL;
        //this.abort(); //Do not download the body from the redirect
    }
}

function uploadFile(endpoint, input_file_elem_id)
{ 
    var xhr = new XMLHttpRequest();
    var formData = new FormData();
    var nif = document.getElementById(input_file_elem_id);
    if (!nif) {
        console.log("uploadFile: No element named " + input_file_elem_id);
        return;
    }

    var files = nif.files;
    if (!nif.files) {
        console.log("uploadFile: No files property on element named " + input_file_elem_id);
        return;
    }

    for (var i = 0; i < files.length; i++) {
        var file = files[i];

        // Check the file type.
        if (!file.type.match('text/*')) {
            continue;
        }

        // Add the file to the request.
        formData.append('ID_file', file, file.name);
    }
    
    xhr.onreadystatechange = onPostStateChange;
    xhr.open("POST", endpoint, true);
    xhr.send(formData);

    update_message("Submitted File"); 
}

function onSubmitNetworkFileClicked() {
    query = getQuery();

    uploadFile(query, "network_ids_file");
}

function onSubmitMorphoFileClicked() {

    query = getQuery();
    uploadFile(query, "morpho_ids_file");
}
