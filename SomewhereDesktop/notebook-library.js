// The HTML element of the active code
var activeCode = null;
// Configuration for where to generate diagrams
var generateDiagramsAtEndOfDocument = false;

// Depends on Plotly
function plot(x, y) {
    var div = document.createElement('div');
    Plotly.plot(div, [{
        x: x,
        y: y
    }], {
            margin: { t: 0 }
        });

    // Insert the generated diagram
    if (activeCode != null && !generateDiagramsAtEndOfDocument) {
        activeCode.parentNode.insertBefore(div, activeCode.nextSibling);
    }
    else {
        document.body.appendChild(div);
    }
}