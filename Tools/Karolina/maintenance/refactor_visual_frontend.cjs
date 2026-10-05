/* One-time preservation-oriented migration. Uses Node's bundled parser, no product dependency.
 * Run with node --expose-internals. The resulting ES modules have explicit state/action boundaries. */
const fs = require('node:fs');
const path = require('node:path');
const acorn = require('internal/deps/acorn/acorn/dist/acorn');
const root = path.resolve(__dirname, '../Karolina.Desktop/Web');
const parse = source => acorn.parse(source, {ecmaVersion: 'latest', sourceType: 'script'});
const sourceFiles = ['app', 'review', 'workspace', 'tools'];
const files = Object.fromEntries(sourceFiles.map(name => {
    const source = fs.readFileSync(path.join(root, name + '.js'), 'utf8');
    return [name, {source, tree: parse(source)}];
}));
if (files.app.source.includes('registerNavigation')) throw new Error('Migration already applied');
const stateDeclarations = [];
const stateNames = new Set();
const functionNames = new Set();
const primitiveNames = new Set(['toast', 'api', 'action', 'inline', 'markdown', 'drawer']);
for (const {source, tree} of Object.values(files)) {
    for (const node of tree.body) {
        if (node.type === 'FunctionDeclaration' && !primitiveNames.has(node.id.name)) functionNames.add(node.id.name);
        if (node.type === 'VariableDeclaration' && (node.kind === 'let' || node.declarations.some(d => ['connectionRequests', 'connectionAttempts', 'connectionErrors', 'ruleSections'].includes(d.id.name)))) {
            for (const d of node.declarations) {
                stateNames.add(d.id.name);
                stateDeclarations.push(`${d.id.name}: ${source.slice(d.init.start, d.init.end)}`);
            }
        }
    }
}
function walk(node, callback, parent = null, field = null) {
    if (!node || typeof node.type !== 'string') return;
    callback(node, parent, field);
    for (const [key, value] of Object.entries(node)) {
        if (['start', 'end', 'loc'].includes(key)) continue;
        if (Array.isArray(value)) value.forEach(child => walk(child, callback, node, key));
        else if (value && typeof value === 'object') walk(value, callback, node, key);
    }
}
function rewrite(source, rootNode, docsOnly = false) {
    const edits = new Map();
    walk(rootNode, (node, parent, field) => {
        if (node.type !== 'Identifier' || !parent) return;
        if ((parent.type === 'MemberExpression' && field === 'property' && !parent.computed)
            || (parent.type === 'Property' && field === 'key' && !parent.computed)
            || ((parent.type === 'FunctionDeclaration' || parent.type === 'FunctionExpression') && (field === 'id' || field === 'params'))
            || (parent.type === 'ArrowFunctionExpression' && field === 'params')
            || (parent.type === 'VariableDeclarator' && field === 'id')) return;
        let value;
        if (docsOnly && node.name === 'docs') value = 'getDocuments()';
        else if (!docsOnly && stateNames.has(node.name)) value = 'ctx.' + node.name;
        else if (!docsOnly && functionNames.has(node.name)) value = 'actions.' + node.name;
        if (!value) return;
        if (parent.type === 'Property' && parent.shorthand && field === 'value') value = node.name + ': ' + value;
        edits.set(node.start, {start:node.start, end:node.end, value});
    });
    let text = source.slice(rootNode.start, rootNode.end);
    for (const edit of [...edits.values()].sort((a,b) => b.start - a.start))
        text = text.slice(0,edit.start-rootNode.start) + edit.value + text.slice(edit.end-rootNode.start);
    return text;
}
function pretty(source) {
    // Insert whitespace only at parser-proven statement/block boundaries, preserving literals and templates.
    const tree = acorn.parse(source, {ecmaVersion:'latest', sourceType:'module'});
    const breaks = new Set();
    walk(tree, node => {
        if (node.type === 'BlockStatement') { breaks.add(node.start + 1); breaks.add(node.end - 1); }
        if (node.type.endsWith('Statement') || node.type === 'VariableDeclaration' || node.type === 'FunctionDeclaration') {
            if (node.type !== 'BlockStatement') breaks.add(node.start);
            if (node.type !== 'EmptyStatement') breaks.add(node.end);
        }
    });
    let output = '';
    for (let i=0;i<=source.length;i++) { if(breaks.has(i) && output && !output.endsWith('\n')) output+='\n'; if(i<source.length)output+=source[i]; }
    return output.split('\n').map(line=>line.trim()).filter(Boolean).join('\n')+'\n';
}
const groups = {navigation:[], documents:[], chat:[], providers:[], review:[], workspace:[], tools:[]};
const functionGroup = {
    renderDirectory:'navigation',navigate:'navigation',syncEditorDirty:'navigation',
    paintDocument:'documents',loadDocument:'documents',documentMetadata:'documents',renderReferences:'documents',metaValue:'documents',
    addMessage:'chat',loadChat:'chat',renderApprovals:'chat',receive:'chat',
    updateControls:'providers',connectProvider:'providers',autoConnect:'providers',efforts:'providers',refreshState:'providers',poll:'providers',unityDrawer:'providers'
};
for (const [name, {source, tree}] of Object.entries(files)) {
    for (const node of tree.body) {
        const raw = source.slice(node.start,node.end);
        if (node.type === 'ExpressionStatement' && node.directive) continue;
        if (node.type === 'VariableDeclaration' && node.declarations.some(d => stateNames.has(d.id.name) || ['$','session','esc'].includes(d.id.name))) continue;
        if (node.type === 'FunctionDeclaration' && primitiveNames.has(node.id.name)) continue;
        if (raw.startsWith('window.KarolinaMarkdown') || raw.includes('karolina-theme') || raw.includes('DOMContentLoaded')) continue;
        let group = name;
        if (name === 'app') {
            if (node.type === 'FunctionDeclaration') group = functionGroup[node.id.name];
            else if (raw.includes("'metadataButton'") || raw.includes("'historyButton'") || raw.includes("'newDocument'") || raw.includes("'saveDocument'") || raw.includes("'sourceMode'") || raw.includes("'documentSource'") || raw.includes('metaLabels') || raw.includes('a[data-document]')) group='documents';
            else if (raw.includes("'send'") || raw.includes("'stop'") || raw.includes("'newChat'") || raw.includes('[data-prompt]')) group='chat';
            else if (raw.includes("'connectCodex'") || raw.includes("'unityStatus'") || raw.includes("'diagnosticsButton'")) group='providers';
            else group='navigation';
        }
        if (!groups[group]) throw new Error('Unclassified: '+raw.slice(0,80));
        groups[group].push(rewrite(source,node));
    }
}
const write = (relative, source) => {const file=path.join(root,relative);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,source);};
write('core/context.js', `/** Per-window business state. Controllers receive this explicitly; there are no implicit script globals. */\nexport function createContext() {\n return {state: {\n ${stateDeclarations.join(',\n')}\n }, actions: {}};\n}\n`);
const {source:appSource,tree:appTree}=files.app;
const markdownNodes=appTree.body.filter(n=>n.type==='FunctionDeclaration'&&['inline','markdown'].includes(n.id.name));
write('ui/markdown.js', `/** Markdown never accepts raw HTML. Document link resolution is injected. */\nexport function createMarkdown(getDocuments = () => []) {\n${markdownNodes.map(n=>rewrite(appSource,n,true)).join('\n')}\nreturn markdown;\n}\n`);
for(const [name,nodes] of Object.entries(groups)) {
    const register='register'+name[0].toUpperCase()+name.slice(1);
    const functions=nodes.flatMap(source=>{const node=parse(source).body[0];return node.type==='FunctionDeclaration'?[node.id.name]:[];});
    const header=`/** ${name}: business handlers with explicit dependencies. */\nexport function ${register}({state: ctx, actions, ui, api}) {\nconst {$, esc, toast, action, markdown, drawer} = ui;\nObject.assign(actions, {${functions.join(', ')}});\n`;
    const target=['review','workspace','tools'].includes(name)?name+'.js':'features/'+name+'.js';
    write(target,pretty(header+nodes.join('\n')+'\n}\n'));
}
// Keep app.js as the composition root, not a global function container.
write('app.js', `import {createContext} from './core/context.js';
import {api} from './core/api.js';
import {createUI} from './ui/primitives.js';
import {createMarkdown} from './ui/markdown.js';
import {registerNavigation} from './features/navigation.js';
import {registerDocuments} from './features/documents.js';
import {registerChat} from './features/chat.js';
import {registerProviders} from './features/providers.js';
import {registerReview} from './review.js';
import {registerWorkspace} from './workspace.js';
import {registerTools} from './tools.js';
import {createAppearance} from './appearance/controller.js';
import {createEffects} from './effects/controller.js';
import {createMascot} from './ui/mascot.js';
import {createComponents} from './ui/components.js';

const context = createContext();
context.api = api;
context.ui = createUI(() => context.actions.updateControls?.());
context.ui.markdown = createMarkdown(() => context.state.docs);
for (const register of [registerNavigation, registerDocuments, registerChat, registerProviders, registerReview, registerWorkspace, registerTools]) register(context);
const components = createComponents(context.ui);
const effects = createEffects(context.ui.toast);
const mascot = createMascot(context.ui);
const appearance = createAppearance({api, ui:context.ui, components, effects, mascot});
context.actions.appearance = appearance;
window.KarolinaMarkdown = context.ui.markdown;

async function boot() {
 try {
  await appearance.initialize();
  context.state.docs = await api('documents');
  context.actions.renderReferences();
  await context.actions.refreshState();
  await context.actions.restoreWorkspace();
  const diagnostic = await api('diagnostics');
  for (const request of diagnostic.approvals || []) context.state.approvalQueue.set(JSON.stringify(request.id), request);
  context.actions.renderApprovals();
  context.actions.renderDirectory();
  context.actions.autoConnect();
 } catch (error) { context.ui.toast(error.message); }
 window.addEventListener('focus', () => {if(context.state.page==='review')context.actions.refreshReviews().catch(error=>context.ui.toast(error.message));context.actions.autoConnect();});
 const timer = setInterval(async()=>{await context.actions.poll();mascot.update(context.state.state);},1000);
 window.addEventListener('pagehide',()=>{clearInterval(timer);effects.dispose();components.dispose();},{once:true});
}
boot();
`);
console.log('Created explicit context, seven controllers and composition root');
