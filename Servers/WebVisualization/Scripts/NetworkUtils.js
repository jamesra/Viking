
function trim(s) {
    s = s.replace(/(^\s*)|(\s*$)/gi, "");
    s = s.replace(/[ ]{2,}/gi, " ");
    s = s.replace(/\n /, "\n");
    return s;
}  

function ListAddItem(listElement, text, value) {
    var elOptNew = document.createElement('option');
    elOptNew.text = text;

    value = typeof value !== 'undefined' ? value : text;

    elOptNew.value = value;

    try {
        listElement.add(elOptNew, null); // Other browsers
    }
    catch (ex) {
        listElement.add(elOptNew); // IE only
    }
}

function ClearList(listElement) {

    if (listElement == null)
        return;

    listElement.options.length = 0; 
}
 

//remove all options before updating
function removeOptionSelected(param) {
    var elSel = document.getElementById(param);
    var i;

    if (elSel == null)
        throw new Error(param + " getElementByID returned null");

    for (i = elSel.length - 1; i >= 0; i--) {
        elSel.remove(i);
    }
};

function createToolTip(id, message) {

    $(id).qtip({
        content: { text: message },
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
}




function stopRKey(evt) {
    var evt = (evt) ? evt : ((event) ? event : null);
    var node = (evt.target) ? evt.target : ((evt.srcElement) ? evt.srcElement : null);
    if ((evt.keyCode == 13) && (node.type == "text")) { return false; }
};

function copyToClipBoard(str) {
    alert(str);
};

document.onkeypress = stopRKey;