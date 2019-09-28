function parse(text) {
    var div = document.createElement('div');
    div.innerHTML = marked(text);
    document.body.appendChild(div);
}

function runAll() {
    var codes = document.querySelectorAll('.language-javascript');
    codes.forEach(code => {
        // Assign book keeping variable of HTML element
        activeCode = code;
        // Evaluate code at the region
        eval(code.innerText);
    });
}