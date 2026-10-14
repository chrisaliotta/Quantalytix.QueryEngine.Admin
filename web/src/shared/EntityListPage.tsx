import { EntityTable, type EntityTableProps } from './EntityTable';
import { PageHeader, type PageHeaderProps } from './PageHeader';

export type EntityListPageProps<TRow extends object> = PageHeaderProps & EntityTableProps<TRow>;

/** Standard list page: a PageHeader above an EntityTable. */
export function EntityListPage<TRow extends object>({
  title,
  description,
  actions,
  ...tableProps
}: EntityListPageProps<TRow>) {
  return (
    <div data-slot="entity-list-page" className="space-y-6">
      <PageHeader title={title} description={description} actions={actions} />
      <EntityTable {...tableProps} />
    </div>
  );
}
