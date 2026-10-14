import { loader } from '@monaco-editor/react';
import * as monaco from 'monaco-editor';
import EditorWorker from 'monaco-editor/editor/editor.worker?worker';
import { EXPRESSION_LANGUAGE_ID, expressionTokens } from './expressionLanguage';

// Must run before any Monaco editor mounts. @monaco-editor/react otherwise loads Monaco from a CDN,
// and Monaco's own worker URLs do not survive Vite bundling, so every worker label gets the bundled
// editor worker. Languages with a dedicated service (typescript, json, css, html) are unsupported.
self.MonacoEnvironment = { getWorker: () => new EditorWorker() };
loader.config({ monaco });

monaco.languages.register({ id: EXPRESSION_LANGUAGE_ID });
monaco.languages.setMonarchTokensProvider(EXPRESSION_LANGUAGE_ID, expressionTokens);
