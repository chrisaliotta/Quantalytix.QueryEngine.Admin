import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Check,
  CheckSquare,
  ChevronDown,
  ChevronRight,
  Coffee,
  FileCode2,
  FileJson,
  FileText,
  FolderGit2,
  Loader2,
  Sparkles,
  Square,
} from 'lucide-react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { type TreeNode, TreeView, type TreeViewRef } from './TreeView';
import type { TreeViewRenderProps } from './TreeView';

// ─────────────────────────────────────────────────────────────────────────────
// Sample Data
// ─────────────────────────────────────────────────────────────────────────────

interface FileNodeData {
  type: 'folder' | 'file';
  extension?: string;
  size?: number;
}

const sampleTreeData: TreeNode<FileNodeData>[] = [
  {
    id: 'src',
    label: 'src',
    data: { type: 'folder' },
    children: [
      {
        id: 'components',
        label: 'components',
        data: { type: 'folder' },
        children: [
          {
            id: 'tree',
            label: 'TreeView',
            data: { type: 'folder' },
            children: [
              {
                id: 'tree-tsx',
                label: 'TreeView.tsx',
                data: { type: 'file', extension: 'tsx', size: 8420 },
              },
              {
                id: 'tree-hook',
                label: 'useTreeMachine.ts',
                data: { type: 'file', extension: 'ts', size: 4210 },
              },
              {
                id: 'tree-demo',
                label: 'TreeView.demo.tsx',
                data: { type: 'file', extension: 'tsx', size: 6800 },
              },
            ],
          },
          {
            id: 'ui',
            label: 'ui',
            data: { type: 'folder' },
            children: [
              {
                id: 'button',
                label: 'Button.tsx',
                data: { type: 'file', extension: 'tsx', size: 2100 },
              },
              {
                id: 'input',
                label: 'Input.tsx',
                data: { type: 'file', extension: 'tsx', size: 1800 },
              },
              {
                id: 'card',
                label: 'Card.tsx',
                data: { type: 'file', extension: 'tsx', size: 1200 },
              },
            ],
          },
        ],
      },
      {
        id: 'lib',
        label: 'lib',
        data: { type: 'folder' },
        children: [
          { id: 'utils', label: 'utils.ts', data: { type: 'file', extension: 'ts', size: 450 } },
        ],
      },
      { id: 'app', label: 'App.tsx', data: { type: 'file', extension: 'tsx', size: 1240 } },
      { id: 'main', label: 'main.tsx', data: { type: 'file', extension: 'tsx', size: 320 } },
      { id: 'index-css', label: 'index.css', data: { type: 'file', extension: 'css', size: 2800 } },
    ],
  },
  {
    id: 'public',
    label: 'public',
    data: { type: 'folder' },
    children: [
      { id: 'favicon', label: 'favicon.ico', data: { type: 'file', extension: 'ico', size: 4200 } },
      { id: 'robots', label: 'robots.txt', data: { type: 'file', extension: 'txt', size: 67 } },
    ],
  },
  { id: 'package', label: 'package.json', data: { type: 'file', extension: 'json', size: 1420 } },
  { id: 'tsconfig', label: 'tsconfig.json', data: { type: 'file', extension: 'json', size: 580 } },
  { id: 'readme', label: 'README.md', data: { type: 'file', extension: 'md', size: 3200 } },
  { id: 'gitignore', label: '.gitignore', data: { type: 'file', extension: '', size: 280 } },
];

// Large dataset for virtualization demo
function generateLargeTree(depth: number, breadth: number, prefix = ''): TreeNode<FileNodeData>[] {
  if (depth === 0) return [];

  return Array.from({ length: breadth }, (_, i) => {
    const id = `${prefix}${i}`;
    const hasChildren = depth > 1;
    return {
      id,
      label: hasChildren ? `Folder ${id}` : `File ${id}.ts`,
      data: { type: hasChildren ? 'folder' : 'file', extension: hasChildren ? undefined : 'ts' },
      children: hasChildren ? generateLargeTree(depth - 1, breadth, `${id}-`) : undefined,
    } as TreeNode<FileNodeData>;
  });
}

const largeTreeData = generateLargeTree(4, 20);

// ─────────────────────────────────────────────────────────────────────────────
// Icon Helpers
// ─────────────────────────────────────────────────────────────────────────────

function getFileIcon(extension?: string) {
  switch (extension) {
    case 'tsx':
    case 'ts':
    case 'js':
    case 'jsx':
      return <FileCode2 className="w-4 h-4 text-blue-500" />;
    case 'json':
      return <FileJson className="w-4 h-4 text-yellow-500" />;
    case 'md':
      return <FileText className="w-4 h-4 text-gray-400" />;
    case 'css':
      return <FileCode2 className="w-4 h-4 text-pink-500" />;
    default:
      return <FileText className="w-4 h-4 text-muted-foreground" />;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Default TreeView (single select)
// ─────────────────────────────────────────────────────────────────────────────

export function DefaultDemo() {
  const [selectedId, setSelectedId] = useState<string | null>('app');

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Default</h3>
      <div className="border rounded-lg p-2 bg-card h-[280px]">
        <TreeView
          data={sampleTreeData}
          defaultOpenIds={new Set(['src'])}
          selectedId={selectedId}
          onSelect={setSelectedId}
          className="h-full"
        />
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Multi-Select Mode
// ─────────────────────────────────────────────────────────────────────────────

export function MultiSelectDemo() {
  const treeRef = useRef<TreeViewRef>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const handleSelectionChange = useCallback((ids: Set<string>) => {
    setSelectedIds(ids);
  }, []);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Multi-Select Mode</h3>
        <div className="flex gap-2">
          <button
            onClick={() => treeRef.current?.selectAll()}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors"
          >
            Select All
          </button>
          <button
            onClick={() => treeRef.current?.clearSelection()}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors"
          >
            Clear Selection
          </button>
        </div>
      </div>

      <div className="grid grid-cols-[1fr_220px] gap-4">
        <div className="border rounded-lg p-2 bg-card h-[320px]">
          <TreeView
            ref={treeRef}
            data={sampleTreeData}
            selectionMode="multiple"
            defaultOpenIds={new Set(['src', 'components', 'tree', 'ui'])}
            selectedIds={selectedIds}
            onSelectionChange={handleSelectionChange}
            className="h-full"
          />
        </div>

        <div className="space-y-2">
          <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
            Selected ({selectedIds.size})
          </div>
          <div className="space-y-1 font-mono text-xs max-h-[280px] overflow-auto">
            {selectedIds.size === 0 ? (
              <div className="text-muted-foreground italic">No items selected</div>
            ) : (
              Array.from(selectedIds).map(id => (
                <div
                  key={id}
                  className="flex items-center justify-between px-2 py-1 bg-muted rounded"
                >
                  <span className="truncate">{id}</span>
                  <button
                    onClick={() => {
                      const newIds = new Set(selectedIds);
                      newIds.delete(id);
                      setSelectedIds(newIds);
                    }}
                    className="ml-2 text-muted-foreground hover:text-foreground shrink-0"
                  >
                    ×
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      <div className="text-sm text-muted-foreground space-y-1">
        <p>
          <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Click</kbd> to select
          single item,{' '}
          <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Ctrl/⌘+Click</kbd> to
          toggle,{' '}
          <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Shift+Click</kbd> for
          range select.
        </p>
        <p>
          <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Ctrl/⌘+A</kbd> to select
          all, <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Escape</kbd> to
          clear, <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded font-mono">Space</kbd> to
          toggle focused item.
        </p>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Multi-Select with Checkboxes
// ─────────────────────────────────────────────────────────────────────────────

export function MultiSelectCheckboxDemo() {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set(['tree-tsx', 'tree-hook']));

  const handleSelectionChange = useCallback((ids: Set<string>) => {
    setSelectedIds(ids);
  }, []);

  const customItem = useCallback(
    ({
      node,
      toggle,
      select,
      isExpanded,
      isSelected,
      isFocused,
      hasChildren,
      depth,
    }: TreeViewRenderProps<FileNodeData>) => (
      <div
        className={cn(
          'group flex items-center gap-2 px-2 py-1.5 rounded-md transition-colors',
          isFocused && 'ring-1 ring-ring ring-offset-1 ring-offset-background',
          !isSelected && 'hover:bg-accent/50'
        )}
        style={{ paddingLeft: depth * 16 + 8 }}
      >
        <button
          onClick={e => {
            e.stopPropagation();
            if (hasChildren) toggle();
          }}
          className={cn(
            'w-4 h-4 flex items-center justify-center',
            !hasChildren && 'opacity-0 pointer-events-none'
          )}
        >
          {hasChildren && (
            <ChevronRight
              className={cn(
                'w-3.5 h-3.5 text-muted-foreground transition-transform duration-150',
                isExpanded && 'rotate-90'
              )}
            />
          )}
        </button>

        <button
          onClick={e => {
            e.stopPropagation();
            select();
          }}
          className="shrink-0"
        >
          {isSelected ? (
            <CheckSquare className="w-4 h-4 text-primary" />
          ) : (
            <Square className="w-4 h-4 text-muted-foreground group-hover:text-foreground transition-colors" />
          )}
        </button>

        {hasChildren ? (
          <FolderGit2
            className={cn('w-4 h-4 shrink-0', isExpanded ? 'text-amber-500' : 'text-amber-600')}
          />
        ) : (
          getFileIcon(node.node.data?.extension)
        )}

        <span className={cn('truncate text-sm', isSelected && 'font-medium')}>{node.label}</span>
      </div>
    ),
    []
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Multi-Select with Checkboxes</h3>
        <div className="px-3 py-1.5 text-xs rounded-md bg-primary/10 text-primary font-medium">
          {selectedIds.size} selected
        </div>
      </div>

      <div className="border rounded-lg p-2 bg-card h-[320px]">
        <TreeView
          data={sampleTreeData}
          selectionMode="multiple"
          defaultOpenIds={new Set(['src', 'components', 'tree', 'ui'])}
          selectedIds={selectedIds}
          onSelectionChange={handleSelectionChange}
          renderItem={customItem}
          indentSize={0}
          className="h-full"
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Custom <code className="px-1 py-0.5 bg-muted rounded text-xs">renderItem</code> with
        checkbox UI. Click checkboxes or use keyboard shortcuts for selection.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Custom Render Props
// ─────────────────────────────────────────────────────────────────────────────

export function CustomRenderDemo(props: any) {
  const [selected, setSelected] = useState<string | null>(null);

  const customToggle = useCallback(
    ({ hasChildren, isExpanded }: TreeViewRenderProps<FileNodeData>) => {
      if (!hasChildren) return <span className="w-3.5 h-3.5" />;
      return isExpanded ? (
        <ChevronDown className="w-3.5 h-3.5 text-emerald-500" />
      ) : (
        <ChevronRight className="w-3.5 h-3.5 text-emerald-500" />
      );
    },
    []
  );

  const customIcon = useCallback(
    ({ node, hasChildren, isExpanded }: TreeViewRenderProps<FileNodeData>) => {
      if (hasChildren) {
        return isExpanded ? (
          <FolderGit2 className="w-4 h-4 text-emerald-400" />
        ) : (
          <FolderGit2 className="w-4 h-4 text-emerald-600" />
        );
      }
      return getFileIcon(node.node.data?.extension);
    },
    []
  );

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Custom Toggle & Icons</h3>

      <div className="border rounded-lg p-2 bg-zinc-950 h-[320px]">
        <TreeView
          {...props}
          data={sampleTreeData}
          defaultOpenIds={new Set(['src', 'components', 'tree'])}
          selectedId={selected}
          onSelect={setSelected}
          renderToggle={customToggle}
          renderIcon={customIcon}
          className="h-full"
          itemClassName="text-zinc-300  data-[selected=true]:bg-emerald-500/20 data-[selected=true]:text-emerald-300"
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Override <code className="px-1 py-0.5 bg-muted rounded text-xs">renderToggle</code> and{' '}
        <code className="px-1 py-0.5 bg-muted rounded text-xs">renderIcon</code> for custom icons
        and animations.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Adding check state with selected item (spacer for parents, Check on selected leaf)
// Reference: GroupByDimensionTreePicker in qtx-ebm LoanAnalysis Widget.tsx
// ─────────────────────────────────────────────────────────────────────────────

const CHECK_STATE_SELECTED_ITEM_SAMPLE_TREE: TreeNode[] = [
  {
    id: 'loan',
    label: 'Loan',
    children: [
      {
        id: 'loan-type',
        label: 'Loan type',
        children: [
          { id: 'lt-commercial', label: 'Commercial' },
          { id: 'lt-residential', label: 'Residential' },
        ],
      },
    ],
  },
  {
    id: 'geography',
    label: 'Geography',
    children: [
      {
        id: 'region',
        label: 'Region',
        children: [
          { id: 'reg-west', label: 'West' },
          { id: 'reg-east', label: 'East' },
        ],
      },
    ],
  },
  { id: 'product', label: 'Product' },
];

function filterSampleTreeBySearch(nodes: TreeNode[], search: string): TreeNode[] {
  const q = search.trim().toLowerCase();
  if (!q) return nodes;

  const go = (list: TreeNode[]): TreeNode[] =>
    list
      .map(node => {
        const label = String(node.label ?? '').toLowerCase();
        const match = label.includes(q);
        const filteredChildren = node.children ? go(node.children) : undefined;
        const hasChildMatch = filteredChildren && filteredChildren.length > 0;
        if (match || hasChildMatch) {
          return { ...node, children: filteredChildren } as TreeNode;
        }
        return null;
      })
      .filter(Boolean) as TreeNode[];

  return go(nodes);
}

function collectExpandableIdsFromTree(nodes: TreeNode[]): string[] {
  const ids: string[] = [];
  const walk = (list: TreeNode[]) => {
    for (const node of list) {
      if (node.children && node.children.length > 0) {
        ids.push(node.id);
        walk(node.children);
      }
    }
  };
  walk(nodes);
  return ids;
}

export function AddingCheckStateWithSelectedItemDemo() {
  const [searchValue, setSearchValue] = useState('');
  const [selectedId, setSelectedId] = useState('product');
  const [openIds, setOpenIds] = useState<Set<string>>(new Set());

  const filteredTreeData = useMemo(
    () => filterSampleTreeBySearch(CHECK_STATE_SELECTED_ITEM_SAMPLE_TREE, searchValue),
    [searchValue]
  );

  useEffect(() => {
    setOpenIds(new Set(collectExpandableIdsFromTree(filteredTreeData)));
  }, [filteredTreeData]);

  const handleToggle = useCallback((nodeId: string, isOpen: boolean) => {
    setOpenIds(previous => {
      const next = new Set(previous);
      if (isOpen) {
        next.add(nodeId);
      } else {
        next.delete(nodeId);
      }
      return next;
    });
  }, []);

  const renderCheckStateIcon = useCallback(
    ({ hasChildren, isSelected }: TreeViewRenderProps<unknown>) => {
      if (hasChildren) {
        return <span className="inline-flex w-4 shrink-0" aria-hidden />;
      }
      return (
        <span
          className="inline-flex w-4 shrink-0 items-center justify-center"
          aria-hidden={!isSelected}
        >
          {isSelected ? (
            <Check className="h-3.5 w-3.5 shrink-0 text-foreground" strokeWidth={2.5} aria-hidden />
          ) : null}
        </span>
      );
    },
    []
  );

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold">Adding check state with selected item</h3>
        <p className="text-sm text-muted-foreground">
          Custom <code className="rounded bg-muted px-1 py-0.5 text-xs">renderIcon</code>: parent rows
          use a fixed-width spacer; leaf rows show a <strong>Check</strong> aligned with the selected
          item. Use <code className="rounded bg-muted px-1 py-0.5 text-xs">disableFolderSelection</code>{' '}
          when only leaves are selectable (e.g. Loan Analysis group-by dimension picker).
        </p>
      </div>

      <div className="flex w-[min(100vw-2rem,22rem)] flex-col gap-2">
        <Input
          value={searchValue}
          onChange={event => setSearchValue(event.target.value)}
          placeholder="Search dimensions..."
          aria-label="Search dimensions"
          className="h-8 !bg-transparent"
          onPointerDown={event => event.stopPropagation()}
        />
        {filteredTreeData.length === 0 ? (
          <div className="rounded-md border px-3 py-2 text-sm text-muted-foreground">No matches.</div>
        ) : (
          <div className="min-h-0">
            <TreeView
              data={filteredTreeData}
              selectionMode="single"
              disableFolderSelection
              openIds={openIds}
              onToggle={handleToggle}
              selectedId={selectedId.length > 0 ? selectedId : undefined}
              onSelect={id => {
                if (!id) {
                  return;
                }
                setSelectedId(id);
              }}
              renderIcon={renderCheckStateIcon}
              className="max-h-64 min-h-[8rem] overflow-auto rounded-md border border-border bg-card p-1"
            />
          </div>
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Full Custom Item Renderer
// ─────────────────────────────────────────────────────────────────────────────

export function FullCustomDemo() {
  const [selected, setSelected] = useState<string | null>(null);

  const formatSize = (bytes?: number) => {
    if (!bytes) return '';
    if (bytes < 1024) return `${bytes} B`;
    return `${(bytes / 1024).toFixed(1)} KB`;
  };

  const customItem = useCallback(
    ({
      node,
      toggle,
      isExpanded,
      isSelected,
      isFocused,
      hasChildren,
      depth,
      isLoading,
    }: TreeViewRenderProps<FileNodeData>) => (
      <div
        className={cn(
          'group flex items-center gap-2 px-3 py-2 rounded-lg transition-all duration-150',
          'border border-transparent',
          isSelected &&
            'bg-gradient-to-r from-violet-500/20 to-fuchsia-500/20 border-violet-500/30',
          isFocused && !isSelected && 'border-violet-500/50',
          !isSelected && !isFocused && 'hover:bg-white/5'
        )}
        style={{ marginLeft: depth * 20 }}
      >
        <button
          onClick={async e => {
            e.stopPropagation();
            if (hasChildren || isLoading !== undefined) {
              await toggle();
            }
          }}
          className={cn(
            'w-5 h-5 flex items-center justify-center rounded transition-colors',
            (hasChildren || isLoading !== undefined) && 'hover:bg-white/10',
            !hasChildren && isLoading === undefined && 'opacity-0'
          )}
          disabled={isLoading}
        >
          {isLoading ? (
            <Loader2 className="h-4 w-4 shrink-0 text-muted-foreground animate-spin" />
          ) : hasChildren ? (
            <ChevronRight
              className={cn(
                'w-3.5 h-3.5 text-violet-400 transition-transform duration-200',
                isExpanded && 'rotate-90'
              )}
            />
          ) : null}
        </button>

        <div className="flex items-center gap-2 flex-1 min-w-0">
          {hasChildren ? (
            <div className="relative">
              <FolderGit2
                className={cn(
                  'w-5 h-5 transition-colors',
                  isExpanded ? 'text-fuchsia-400' : 'text-violet-400'
                )}
              />
              {isExpanded && (
                <Sparkles className="absolute -top-1 -right-1 w-2.5 h-2.5 text-amber-400 animate-pulse" />
              )}
            </div>
          ) : (
            getFileIcon(node.node.data?.extension)
          )}

          <span className="truncate font-medium">{node.label}</span>

          {node.node.data?.size && (
            <span className="ml-auto text-xs text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity">
              {formatSize(node.node.data.size)}
            </span>
          )}
        </div>
      </div>
    ),
    []
  );

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Full Custom Item Renderer</h3>

      <div className="border rounded-xl p-3 bg-gradient-to-br from-slate-900 via-violet-950/30 to-slate-900 h-[320px]">
        <TreeView
          data={sampleTreeData}
          defaultOpenIds={new Set(['src', 'components', 'tree'])}
          selectedId={selected}
          onSelect={setSelected}
          renderItem={customItem}
          className="h-full text-white/90"
          indentSize={0}
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Use <code className="px-1 py-0.5 bg-muted rounded text-xs">renderItem</code> for complete
        control over row rendering.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Async Loading Children
// ─────────────────────────────────────────────────────────────────────────────

export function AsyncLoadingDemo() {
  const [selected, setSelected] = useState<string | null>(null);
  const [loadingNodes, setLoadingNodes] = useState<Set<string>>(new Set());
  const [isExpandingAll, setIsExpandingAll] = useState(false);
  const treeRef = useRef<TreeViewRef>(null);
  const [treeData, setTreeData] = useState<TreeNode<FileNodeData>[]>([
    {
      id: 'root',
      label: 'Root',
      data: { type: 'folder' },
      // No children initially - will be loaded on demand
    },
    {
      id: 'src',
      label: 'src',
      data: { type: 'folder' },
      // No children initially
    },
  ]);

  // Helper to find all folder nodes that need loading
  const findFolderNodes = useCallback(
    (nodes: TreeNode<FileNodeData>[]): TreeNode<FileNodeData>[] => {
      const folders: TreeNode<FileNodeData>[] = [];

      const traverse = (nodeList: TreeNode<FileNodeData>[]) => {
        for (const node of nodeList) {
          if (node.data?.type === 'folder' && (!node.children || node.children.length === 0)) {
            folders.push(node);
          }
          if (node.children) {
            traverse(node.children);
          }
        }
      };

      traverse(nodes);
      return folders;
    },
    []
  );

  // Helper to update node children in tree data
  const updateNodeChildren = useCallback((nodeId: string, children: TreeNode<FileNodeData>[]) => {
    setTreeData(prev => {
      const updateNode = (nodes: TreeNode<FileNodeData>[]): TreeNode<FileNodeData>[] => {
        return nodes.map(node => {
          if (node.id === nodeId) {
            return { ...node, children };
          }
          if (node.children) {
            return { ...node, children: updateNode(node.children) };
          }
          return node;
        });
      };
      return updateNode(prev);
    });
  }, []);

  // Simulate API call to load children
  const loadChildren = useCallback(
    async (
      nodeId: string,
      _node: TreeNode<FileNodeData>
    ): Promise<TreeNode<FileNodeData>[] | undefined> => {
      // Simulate API delay - 2 seconds
      await new Promise(resolve => setTimeout(resolve, 2000));

      // Mock data based on node ID
      const mockChildren: Record<string, TreeNode<FileNodeData>[]> = {
        root: [
          {
            id: 'root-file1',
            label: 'README.md 22',
            data: { type: 'file', extension: 'md', size: 1200 },
          },
          {
            id: 'root-file2',
            label: 'package.json',
            data: { type: 'file', extension: 'json', size: 800 },
          },
        ],
        src: [
          {
            id: 'src-components',
            label: 'components',
            data: { type: 'folder' },
            // This can also be loaded on demand
          },
          { id: 'src-app', label: 'App.tsx', data: { type: 'file', extension: 'tsx', size: 1240 } },
        ],
        'src-components': [
          {
            id: 'comp-button',
            label: 'Button.tsx',
            data: { type: 'file', extension: 'tsx', size: 2100 },
          },
          {
            id: 'comp-input',
            label: 'Input.tsx',
            data: { type: 'file', extension: 'tsx', size: 1800 },
          },
        ],
      };
      updateNodeChildren(nodeId, mockChildren[nodeId]);
      return mockChildren[nodeId];
    },
    [updateNodeChildren]
  );

  // Expand all folders with parallel API calls
  const expandAllAsync = useCallback(async () => {
    setIsExpandingAll(true);
    try {
      // Find all folder nodes that need loading
      const folderNodes = findFolderNodes(treeData);

      if (folderNodes.length === 0) {
        // If no folders need loading, just expand all
        treeRef.current?.expandAll();
        return;
      }

      // Load all folders in parallel
      const loadPromises = folderNodes.map(node =>
        loadChildren(node.id, node).then(children => {
          if (children) {
            return { nodeId: node.id, children };
          }
          return null;
        })
      );

      // Wait for all API calls to complete
      const results = await Promise.all(loadPromises);

      // Update all nodes with their children
      results.forEach(result => {
        if (result) {
          updateNodeChildren(result.nodeId, result.children);
        }
      });

      // Expand all nodes after loading
      setTimeout(() => {
        treeRef.current?.expandAll();
      }, 100);
    } catch (error) {
      console.error('Error expanding all:', error);
    } finally {
      setIsExpandingAll(false);
    }
  }, [treeData, findFolderNodes, loadChildren, updateNodeChildren]);

  const customItem = useCallback(
    ({
      node,
      toggle,
      isExpanded,
      isSelected,
      isFocused,
      hasChildren,
      depth,
      onLoadChildren,
    }: TreeViewRenderProps<FileNodeData>) => {
      const isLoading = loadingNodes.has(node.id);
      // With onLoadChildren, folders automatically have children (can be expanded)
      // Otherwise, check if node actually has children
      const isFolder = node.node.data?.type === 'folder';
      const canExpand = isFolder || hasChildren;
      const needsLoad =
        onLoadChildren && isFolder && (!node.node.children || node.node.children.length === 0);

      const handleToggle = async () => {
        if (!canExpand) return;

        // If expanding and needs to load children
        if (!isExpanded && needsLoad && onLoadChildren) {
          setLoadingNodes(prev => new Set(prev).add(node.id));
          try {
            const children = await onLoadChildren(node.id, node.node);
            if (children) {
              updateNodeChildren(node.id, children);
            }
            toggle();
          } catch (error) {
            console.error('Error loading children:', error);
          } finally {
            setLoadingNodes(prev => {
              const next = new Set(prev);
              next.delete(node.id);
              return next;
            });
          }
        } else {
          toggle();
        }
      };

      return (
        <div
          className={cn(
            'group flex items-center gap-2 px-3 py-2 rounded-lg transition-all duration-150',
            'border border-transparent',
            isSelected &&
              'bg-gradient-to-r from-violet-500/20 to-fuchsia-500/20 border-violet-500/30',
            isFocused && !isSelected && 'border-violet-500/50',
            !isSelected && !isFocused && 'hover:bg-white/5'
          )}
          style={{ marginLeft: depth * 20 }}
        >
          <button
            onClick={async e => {
              e.stopPropagation();
              await handleToggle();
            }}
            className={cn(
              'w-5 h-5 flex items-center justify-center rounded transition-colors',
              canExpand && 'hover:bg-white/10',
              !canExpand && 'opacity-0 cursor-default'
            )}
            disabled={isLoading}
          >
            {isLoading || isExpandingAll ? (
              <Loader2 className="w-3.5 h-3.5 text-violet-400 animate-spin" />
            ) : canExpand ? (
              <ChevronRight
                className={cn(
                  'w-3.5 h-3.5 text-violet-400 transition-transform duration-200',
                  isExpanded && 'rotate-90'
                )}
              />
            ) : null}
          </button>

          <div className="flex items-center gap-2 flex-1 min-w-0">
            <div className="relative">
              <FolderGit2
                className={cn(
                  'w-5 h-5 transition-colors',
                  isExpanded ? 'text-fuchsia-400' : 'text-violet-400'
                )}
              />
              {isExpanded && !isLoading && (
                <Sparkles className="absolute -top-1 -right-1 w-2.5 h-2.5 text-amber-400 animate-pulse" />
              )}
            </div>

            <span className="truncate font-medium">{node.label}</span>

            {node.node.data?.size && (
              <span className="ml-auto text-xs text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity">
                {node.node.data.size}
              </span>
            )}
          </div>
        </div>
      );
    },
    [loadingNodes, updateNodeChildren, isExpandingAll]
  );

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Async Loading Children</h3>
      <p className="text-sm text-muted-foreground">
        Click on folders to load their children asynchronously. The toggle function supports
        async/await.
      </p>
      <div className="flex items-center justify-between">
        <div className="flex gap-2">
          <button
            onClick={expandAllAsync}
            disabled={isExpandingAll}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isExpandingAll ? (
              <>
                <Loader2 className="w-3 h-3 animate-spin" />
                Loading...
              </>
            ) : (
              'Expand All'
            )}
          </button>
          <button
            onClick={() => treeRef.current?.collapseAll()}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors"
          >
            Collapse All
          </button>
        </div>
      </div>
      <div className="border rounded-xl p-3 bg-gradient-to-br from-slate-900 via-violet-950/30 to-slate-900 h-[320px]">
        <TreeView
          data={treeData}
          ref={treeRef}
          selectedId={selected}
          onSelect={setSelected}
          renderItem={props => customItem({ ...props, onLoadChildren: loadChildren })}
          className="h-full text-white/90"
          indentSize={0}
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Use <code className="px-1 py-0.5 bg-muted rounded text-xs">onLoadChildren</code> to load
        children asynchronously when a node is expanded.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Virtualized Large Tree
// ─────────────────────────────────────────────────────────────────────────────

export function VirtualizedDemo() {
  const treeRef = useRef<TreeViewRef>(null);
  const [selected, setSelected] = useState<string | null>(null);

  // Calculate total node count
  const countNodes = (nodes: TreeNode[]): number =>
    nodes.reduce((acc, n) => acc + 1 + (n.children ? countNodes(n.children) : 0), 0);
  const totalNodes = countNodes(largeTreeData);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Virtualized (Large Tree)</h3>
          <p className="text-sm text-muted-foreground">{totalNodes.toLocaleString()} total nodes</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => treeRef.current?.expandAll()}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors"
          >
            Expand All
          </button>
          <button
            onClick={() => treeRef.current?.collapseAll()}
            className="px-3 py-1.5 text-xs rounded-md bg-secondary hover:bg-secondary/80 transition-colors"
          >
            Collapse All
          </button>
        </div>
      </div>

      <div className="border rounded-lg p-2 bg-card h-[320px]">
        <TreeView
          ref={treeRef}
          data={largeTreeData}
          virtualized
          estimatedRowHeight={32}
          overscan={10}
          selectedId={selected}
          onSelect={setSelected}
          className="h-full"
        />
      </div>

      <p className="text-sm text-muted-foreground">
        Virtualization renders only visible rows + overscan for smooth scrolling with large
        datasets.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Demo: Controlled State
// ─────────────────────────────────────────────────────────────────────────────

export function ControlledDemo() {
  const [openIds, setOpenIds] = useState<Set<string>>(new Set(['src']));
  const [selected, setSelected] = useState<string | null>('app');

  const handleToggle = useCallback((id: string, isOpen: boolean) => {
    setOpenIds(prev => {
      const next = new Set(prev);
      if (isOpen) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }, []);

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Controlled State</h3>

      <div className="grid grid-cols-[1fr_200px] gap-4">
        <div className="border rounded-lg p-2 bg-card h-[280px]">
          <TreeView
            data={sampleTreeData}
            openIds={openIds}
            selectedId={selected}
            onToggle={handleToggle}
            onSelect={setSelected}
            className="h-full"
          />
        </div>

        <div className="space-y-3">
          <div>
            <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
              Selected
            </div>
            <div className="px-2 py-1 bg-muted rounded font-mono text-sm truncate">
              {selected ?? 'none'}
            </div>
          </div>

          <div>
            <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-1">
              Open IDs
            </div>
            <div className="space-y-1 text-xs font-mono">
              {Array.from(openIds).map(id => (
                <div
                  key={id}
                  className="flex items-center justify-between px-2 py-1 bg-muted rounded"
                >
                  <span className="truncate">{id}</span>
                  <button
                    onClick={() =>
                      setOpenIds(prev => {
                        const next = new Set(prev);
                        next.delete(id);
                        return next;
                      })
                    }
                    className="ml-2 text-muted-foreground hover:text-foreground"
                  >
                    ×
                  </button>
                </div>
              ))}
              {openIds.size === 0 && (
                <div className="text-muted-foreground italic px-2 py-1">No open folders</div>
              )}
            </div>
          </div>
        </div>
      </div>

      <p className="text-sm text-muted-foreground">
        Pass <code className="px-1 py-0.5 bg-muted rounded text-xs">openIds</code> and{' '}
        <code className="px-1 py-0.5 bg-muted rounded text-xs">selectedId</code> for fully
        controlled state.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Demo Component
// ─────────────────────────────────────────────────────────────────────────────

export function TreeViewDemo() {
  return (
    <div className="overflow-x-auto">
      <div className="min-h-screen bg-background p-8 mx-auto space-y-12">
        <header className="space-y-2">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-gradient-to-br from-violet-500 to-fuchsia-500">
              <Coffee className="w-6 h-6 text-white" />
            </div>
            <h1 className="text-3xl font-bold tracking-tight">TreeView Component</h1>
          </div>
          <p className="text-muted-foreground max-w-2xl">
            A headless, accessible tree view with render-prop customization, keyboard navigation,
            single/multi-select modes, and optional virtualization for large datasets.
          </p>
        </header>

        <div className="space-y-12">
          <DefaultDemo />
          <hr className="border-border" />
          <MultiSelectDemo />
          <hr className="border-border" />
          <MultiSelectCheckboxDemo />
          <hr className="border-border" />
          <CustomRenderDemo />
          <hr className="border-border" />
          <FullCustomDemo />
          <hr className="border-border" />
          <VirtualizedDemo />
          <hr className="border-border" />
          <ControlledDemo />
        </div>

        <footer className="text-center text-sm text-muted-foreground py-8 border-t">
          Built with React, Tailwind CSS, and Lucide icons
        </footer>
      </div>
    </div>
  );
}

export default TreeViewDemo;
