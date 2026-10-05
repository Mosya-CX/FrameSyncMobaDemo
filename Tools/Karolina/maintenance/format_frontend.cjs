/* Whitespace-only formatter using Node's bundled parser; no dependency installation. */
const fs = require('fs');
const path = require('path');
const acorn = require('internal/deps/acorn/acorn/dist/acorn');
const root = path.resolve(__dirname, '../Karolina.Desktop/Web');
const canonical = value => JSON.stringify(value, (key, item) => ['start', 'end', 'raw'].includes(key) ? undefined : item);
function format(file) {
    const source = fs.readFileSync(file, 'utf8'), comments = [], tokens = [];
    const ast = acorn.parse(source, {ecmaVersion:'latest', sourceType:'module', onToken:tokens, onComment:comments});
    const blocks = new Set(), boundaries = new Set(), templates = [];
    function visit(node, parent) {
        if (!node || typeof node !== 'object') return;
        if (Array.isArray(node)) return node.forEach(item => visit(item, parent));
        if (node.type === 'TemplateLiteral') {templates.push(node); return;}
        if (['BlockStatement', 'ClassBody'].includes(node.type)) {blocks.add(node.start); blocks.add(node.end-1);}
        if (node.type==='ObjectExpression' && node.end-node.start>160) {
            blocks.add(node.start); blocks.add(node.end-1);
            for(const property of node.properties) {
                const comma=tokens.find(token=>token.start>=property.end);
                if(comma?.type.label===',') boundaries.add(comma.end);
            }
        }
        if (node.type === 'ImportDeclaration' || node.type === 'VariableDeclaration' && !['ForStatement','ForOfStatement','ForInStatement'].includes(parent?.type) || /^(Expression|Return|Throw|Break|Continue)Statement$/.test(node.type)) boundaries.add(node.end);
        for (const [key,value] of Object.entries(node)) if (!['start','end'].includes(key)) visit(value,node);
    }
    visit(ast);
    const pieces = [...tokens.filter(t => t.type.label !== 'eof' && !templates.some(o => t.start >= o.start && t.end <= o.end)), ...comments, ...templates].sort((a,b) => a.start-b.start);
    let result='', indent=0, previous;
    for (const token of pieces) {
        const text=source.slice(token.start,token.end), closing=blocks.has(token.start) && text==='}';
        if (closing) indent=Math.max(0,indent-1);
        if (previous) {
            const last=source.slice(previous.start,previous.end);
            const newline=closing || blocks.has(previous.start) && last==='{' || boundaries.has(previous.end) || ['Line','Block'].includes(previous.type) || blocks.has(previous.start) && last==='}' && !/^(?:[;,).\]]|else$|catch$|finally$)/.test(text);
            const tight=/^[;,).\]]$/.test(text) || /^[.(\[]$/.test(last) || text==='.' || text==='?.' || last==='?.' || text===':' || last==='!' || last==='~' || text==='(' && /[\w)\]]$/.test(last) && !/^(?:if|for|while|switch|catch|with|function|return|async)$/.test(last) || text==='[' && /[\w)\]]$/.test(last);
            result += newline ? '\n'+'    '.repeat(indent) : tight ? '' : ' ';
        }
        result+=text;
        if (blocks.has(token.start) && text==='{') indent++;
        previous=token;
    }
    result+='\n';
    if (canonical(ast)!==canonical(acorn.parse(result,{ecmaVersion:'latest',sourceType:'module'}))) throw new Error('Formatter changed AST: '+file);
    fs.writeFileSync(file,result);
}
function walk(directory) {for(const item of fs.readdirSync(directory,{withFileTypes:true})){const file=path.join(directory,item.name);if(item.isDirectory())walk(file);else if(item.name.endsWith('.js'))format(file);}}
walk(root);
console.log('Formatted frontend; every module retained the same parsed AST.');
