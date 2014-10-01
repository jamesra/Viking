var selectedMap = new Array();


var root = window.location;

if (window.Url != undefined)
{
    var suffix = (root.toString().slice(window.Url.length)).toString();

    var iSlash = suffix.indexOf('/');

    if (iSlash > 0)
        suffix = suffix.slice(0, iSlash); 

    //var regexp = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');

    //var suffix = root.toString().match(regexp)[0];

    //suffix = root.toString().split(suffix)[1];

    //if (suffix.indexOf('/') != -1)
    //    suffix = suffix.substring(0, suffix.length - 1).split('/')[1];

    selectedMap["Network"] = false;
    selectedMap["Motifs"] = false;
    selectedMap["Structure"] = false;
    selectedMap["Stats"] = false;
    selectedMap["VikingPlot"] = false;

    var tabfound = false;
    if (suffix.toLowerCase() == "network") {
        selectedMap["Network"] = true;
        tabfound = true; 
    }
    if (suffix.toLowerCase() == "motifs"){
        selectedMap["Motifs"] = true;
            tabfound = true; 
    }

    if (suffix.toLowerCase() == "structure"){
        selectedMap["Structure"] = true;
        tabfound = true;
    }

    if (suffix.toLowerCase() == "stats"){
        selectedMap["Stats"] = true;
        tabfound = true;
    }

    if (suffix.toLowerCase() == "vikingplot") {
        selectedMap["VikingPlot"] = true;
        tabfound = true;
    }

    if (suffix.toLowerCase() == "account") {
        tabfound = true; 
    }

    if (tabfound == false)
        throw new Error("Tab note found\nRoot: " + root.toString() +"\nSuffix:" + suffix);
 
    updateTabs();
}

//to get length of associative array Object.size(array)
Object.size = function (obj) {
    var size = 0, key;
    for (key in obj) {
        if (obj.hasOwnProperty(key)) size++;
    }
    return size;
};
    



function updateTabs() {


    for (item in selectedMap) {
        if (document.getElementById(item)== undefined)
            continue;
        if (selectedMap[item])
            document.getElementById(item).style.backgroundColor = "#b9db8c";
        else
            document.getElementById(item).style.backgroundColor = "#e8eef4";

    }
}