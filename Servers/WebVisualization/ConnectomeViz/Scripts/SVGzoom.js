
var root = document.documentElement;

var state = 'none', stateTarget, stateOrigin, stateTf;

svgRoot = this.root;
var topElement = svgRoot.getElementById('graph1');
if (topElement != null)
    topElement.setAttribute('id', 'viewport');

CenterGraph(svgRoot);

setupHandlers(root);

function CenterGraph(svgRoot) {
    var GraphWidth = svgRoot.getAttribute('width');
    var GraphHeight = svgRoot.getAttribute('height');

    if (GraphWidth == null || GraphHeight == null)
        return;

    //Strip out the units from the graph width/height
    GraphHeight = GraphHeight.substr(0, GraphHeight.length - 2);
    GraphWidth = GraphWidth.substr(0, GraphWidth.length - 2);
    GraphHeight = parseFloat(GraphHeight);
    GraphWidth = parseFloat(GraphWidth);

    var ViewBox = svgRoot.getAttribute("viewBox");
    var ViewDims = ViewBox.split(" ");

    var ViewWidth = parseFloat(ViewDims[2]);
    var ViewHeight = parseFloat(ViewDims[3]);


    svgRoot.removeAttribute('width');
    svgRoot.removeAttribute('height');
    svgRoot.removeAttribute('viewBox');
    svgRoot.setAttribute("svgns", "http://www.w3.org/2000/svg");

    var g = svgRoot.getElementById("viewport");
    if (g == undefined || g == null)
        return;

    var AdjustedView = g.getCTM().translate(GraphHeight / 2, GraphWidth / 2);

    //g.setAttribute("transform", "scale(1,1) rotate(0) translate(0," + (GraphHeight / 2).toString() + ")");
    var scaleWidth = ViewWidth / GraphWidth;
    var scaleHeight = ViewHeight / GraphHeight;
    var scale = scaleWidth;
    if (scaleWidth < scaleHeight)
        scale = scaleHeight;

    //Scale is not working correctly, not sure why.
    scale = 1;

    g.setAttribute("transform", "scale(" + scale.toString() + ", " + scale.toString() + ") rotate(0) translate(-" + ((ViewWidth / 2) * (1)).toString() + "," + ((ViewHeight / 2) * (1)).toString() + ")");
}


/**
 * Register handlers
 */
function setupHandlers(root) {
    setAttributes(root, {
        "onmouseup": "add(evt)",
        "onmousedown": "handleMouseDown(evt)",
        "onmousemove": "handleMouseMove(evt)",
        "onmouseup": "handleMouseUp(evt)",
        "onkeydown": "handleKeyDown(evt)",
        //"onmouseout" : "handleMouseUp(evt)", // Decomment this to stop the pan functionality when dragging out of the SVG element
    });

    if (navigator.userAgent.toLowerCase().indexOf('webkit') >= 0)
        window.addEventListener('mousewheel', handleMouseWheel, false); // Chrome/Safari
    else
        window.addEventListener('DOMMouseScroll', handleMouseWheel, false); // Others

    window.addEventListener('onkeydown', handleKeyDown, false);

    var g = this.root.getElementById("viewport");
    if (g != null) {
        var ctm = g.getCTM();

        stateTf = g.getCTM().inverse();

    }
}

/**
 * Instance an SVGPoint object with given event coordinates.
 */
function getEventPoint(evt) {
    var p = root.createSVGPoint();

    p.x = evt.clientX;
    p.y = evt.clientY;

    return p;
}

/**
 * Sets the current transform matrix of an element.
 */
function setCTM(element, matrix) {
    var s = "matrix(" + matrix.a + "," + matrix.b + "," + matrix.c + "," + matrix.d + "," + matrix.e + "," + matrix.f + ")";

    element.setAttribute("transform", s);
}

/**
 * Dumps a matrix to a string (useful for debug).
 */
function dumpMatrix(matrix) {
    var s = "[ " + matrix.a + ", " + matrix.c + ", " + matrix.e + "\n  " + matrix.b + ", " + matrix.d + ", " + matrix.f + "\n  0, 0, 1 ]";

    return s;
}

/**
 * Sets attributes of an element.
 */
function setAttributes(element, attributes) {
    for (i in attributes)
        element.setAttributeNS(null, i, attributes[i]);
}

/**
 * Handle key press event.
 */
function handleKeyDown(evt) {
    if (evt.preventDefault)
        evt.preventDefault();

    var LeftArrowCode = 37;
    var UpArrowCode = 38;
    var RightArrowCode = 39;
    var DownArrowCode = 40;

    if (evt.keyCode == LeftArrowCode) {
        setCTM(g, stateTf.inverse().translate(100 - stateOrigin.x, stateOrigin.y));
    }

}

/**
 * Handle mouse move event.
 */
function handleMouseWheel(evt) {
    if (evt.preventDefault)
        evt.preventDefault();

    evt.returnValue = false;

    var svgDoc = evt.target.ownerDocument;

    var delta;

    if (evt.wheelDelta)
        delta = evt.wheelDelta / 3600; // Chrome/Safari
    else
        delta = evt.detail / -90; // Mozilla

    var z = 1 + (delta); // Zoom factor: 0.9/1.1

    var g = svgDoc.getElementById("viewport");

    var p = getEventPoint(evt);

    p = p.matrixTransform(g.getCTM().inverse());

    // Compute new scale matrix in current mouse position
    var k = root.createSVGMatrix().translate(p.x, p.y).scale(z).translate(-p.x, -p.y);

    setCTM(g, g.getCTM().multiply(k));

    stateTf = stateTf.multiply(k.inverse());
}

/**
 * Handle mouse move event.
 */
function handleMouseMove(evt) {
    if (evt.preventDefault)
        evt.preventDefault();

    evt.returnValue = false;

    var svgDoc = evt.target.ownerDocument;

    var g = svgDoc.getElementById("viewport");

    if (state == 'pan') {
        // Pan mode
        var p = getEventPoint(evt).matrixTransform(stateTf);

        setCTM(g, stateTf.inverse().translate(p.x - stateOrigin.x, p.y - stateOrigin.y));
    } else if (state == 'move') {
        // Move mode
        var p = getEventPoint(evt).matrixTransform(g.getCTM().inverse());

        setCTM(stateTarget, root.createSVGMatrix().translate(p.x - stateOrigin.x, p.y - stateOrigin.y).multiply(g.getCTM().inverse()).multiply(stateTarget.getCTM()));

        stateOrigin = p;
    }
}

/**
 * Handle click event.
 */
function handleMouseDown(evt) {
    if (evt.preventDefault)
        evt.preventDefault();

    evt.returnValue = false;

    var svgDoc = evt.target.ownerDocument;

    var g = svgDoc.getElementById("viewport");
    stateTf = g.getCTM().inverse();
    stateOrigin = getEventPoint(evt).matrixTransform(stateTf);

    if (evt.target.tagName != "g") {
        // Pan mode
        state = 'pan';

    } else {
        // Move mode
        state = 'move';
        stateTarget = evt.target;
    }
}

/**
 * Handle mouse button release event.
 */
function handleMouseUp(evt) {
    if (evt.preventDefault)
        evt.preventDefault();

    evt.returnValue = false;

    var svgDoc = evt.target.ownerDocument;

    if (state == 'pan' || state == 'move') {
        // Quit pan mode
        state = '';
    }
}
