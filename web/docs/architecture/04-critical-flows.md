# Critical flows

[Diagram index](README.md) · [Architecture](01-architecture.md) · [Composition](02-composition.md) · [How it works](03-how-it-works.md)

Each flow is traced through the code at a3ba75b. Sequences summarize async calls: they leave out re-renders and most TanStack Query callbacks, and where `apiClient` adds nothing to the story the call is drawn straight to the API.

## 1. Sign-in

Signing in turns a development token into a session: the token is stored for the tab, the identity is loaded with it, and the user lands on a tenant's overview. The drawing follows a submit of the login form.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Login as LoginPage
    participant Session as SessionProvider
    participant Auth as authClient
    participant Store as sessionStorage
    participant Cache as queryClient
    participant Client as apiClient
    participant Api as Admin API
    participant Guard as RequireSession
    participant Redirect as TenantRedirect
    User->>Login: submit the tenant id and the SystemAdmin box
    Login->>Session: signInMutation.mutate runs signIn(request)
    Session->>Auth: signIn(request)
    Auth->>Api: fetch POST {VITE_API_BASE_URL}/development/token
    Note over Auth,Api: Plain fetch, outside the middleware. The base URL is empty in development, so the call goes to the page's own origin and Vite proxies it.
    Api-->>Auth: accessToken, tenantId, systemAdmin, expiresUtc
    Auth-->>Session: StoredSession, after unwrap
    Session->>Store: writeSession
    Session->>Cache: fetchQuery of the me query with staleTime 0
    Cache->>Client: GET /api/me
    Client->>Store: readSession in authMiddleware.onRequest
    Client->>Api: GET /api/me with the Bearer token
    Api-->>Cache: SessionInfo with subject, tenantId and isSystemAdmin
    Session->>Session: setToken(signedIn), so session is the token plus me
    Session-->>Login: re-render with a session
    Login->>Guard: Navigate with replace to state.from, or to /
    alt state.from names a page
        Guard->>Guard: Outlet renders the requested page
    else the target is /
        Guard->>Redirect: index route
        alt me.tenantId is set
            Redirect->>Redirect: Navigate to /t/{me.tenantId}/overview
        else the token has no tenant
            Redirect->>Client: useTenants, GET /api/tenants
            Client-->>Redirect: the accessible tenants
            Redirect->>Redirect: Navigate to the first tenant's overview, or show a 404 page
        end
    end
```

- `unwrap` throws an `ApiProblem` when the token request fails; `signIn` rejects, and `LoginPage` shows the error in an `ApiErrorAlert` under the form.
- `state.from` is set when `RequireSession` sent the user to `/login`, so a deep link opens again after sign-in.
- The form starts with tenant `development` and the SystemAdmin box ticked. The token endpoint exists only on a Development API host.

**Sources:** [LoginPage.tsx](../../src/auth/LoginPage.tsx), [SessionProvider.tsx](../../src/auth/SessionProvider.tsx), [AuthClient.ts](../../src/auth/AuthClient.ts), [session.ts](../../src/auth/session.ts), [client.ts](../../src/api/client.ts), [problemDetails.ts](../../src/api/problemDetails.ts), [RequireSession.tsx](../../src/auth/RequireSession.tsx), [TenantRedirect.tsx](../../src/app/TenantRedirect.tsx), [useTenants.ts](../../src/shared/useTenants.ts), [vite.config.ts](../../vite.config.ts), [DevelopmentTokenController.cs](../../../api/src/Quantalytix.QueryEngine.Admin.Api/Development/DevelopmentTokenController.cs), [SessionController.cs](../../../api/src/Quantalytix.QueryEngine.Admin.Api/Session/SessionController.cs).

## 2. Restoring the session on reload

A reload keeps the user signed in for as long as the tab's token is valid. The drawing shows how `SessionProvider` restores the token and how `RequireSession` renders each state of the identity query.

```mermaid
sequenceDiagram
    autonumber
    participant Browser
    participant Session as SessionProvider
    participant Store as sessionStorage
    participant Cache as queryClient
    participant Api as Admin API
    participant Guard as RequireSession
    Browser->>Session: first render after a reload
    Session->>Store: readSession, the initial token state
    alt no token stored, or expiresUtc has passed
        Store-->>Session: null
        Session-->>Guard: session null, isLoading false
        Guard->>Browser: Navigate to /login with state.from set to the requested location
    else a valid token
        Store-->>Session: StoredSession
        Session->>Cache: me query, enabled because there is a token
        Session-->>Guard: isLoading true
        Guard->>Browser: Loading session...
        Cache->>Api: GET /api/me with the Bearer token
        alt 200
            Api-->>Cache: SessionInfo
            Session-->>Guard: session, the token plus me
            Guard->>Browser: Outlet renders the requested page
        else 401
            Api-->>Cache: 401
            Note over Cache,Api: authMiddleware clears the token and loads /login, as in flow 3
        else any other failure
            Api-->>Cache: an ApiProblem, or a network error that is retried twice first
            Session-->>Guard: error
            Guard->>Browser: ErrorPage with the problem's status or 503, and Sign in again
        end
    end
```

- "Sign in again" calls `signOut`: `clearSession`, `queryClient.clear()` and a null token, after which `RequireSession` redirects to `/login`.
- `LoginPage` waits on the same `isLoading` ("Restoring session...") and sends a user who already has a session to `state.from` or `/`.
- The me query has `staleTime: Infinity`, so it loads once per page load; the token lives in sessionStorage, so it is per tab and ends with the tab.

**Sources:** [SessionProvider.tsx](../../src/auth/SessionProvider.tsx), [session.ts](../../src/auth/session.ts), [useSession.ts](../../src/auth/useSession.ts), [RequireSession.tsx](../../src/auth/RequireSession.tsx), [LoginPage.tsx](../../src/auth/LoginPage.tsx), [client.ts](../../src/api/client.ts), [queryClient.ts](../../src/api/queryClient.ts).

## 3. A 401 response

Any 401 from the admin API ends the session, whatever page made the call. The drawing shows the middleware's response handler; the sign-in `fetch` does not pass through it.

```mermaid
sequenceDiagram
    autonumber
    participant Hook as any query or mutation
    participant Client as apiClient
    participant Mw as authMiddleware
    participant Store as sessionStorage
    participant Api as Admin API
    participant Location as window.location
    Hook->>Client: GET, POST, PUT or DELETE
    Client->>Mw: onRequest
    Mw->>Store: readSession
    Mw->>Api: request with the Bearer token
    Api-->>Mw: 401
    Mw->>Store: clearSession
    Mw->>Location: assign /login
    Mw-->>Client: the 401 response, unchanged
    Client-->>Hook: unwrap throws ApiProblem with status 401
    Location->>Location: full page load of /login
    Note over Location: The load discards the query cache and the router state. SessionProvider finds no token, so LoginPage shows the form, and after sign-in the user lands on /.
```

- The middleware runs outside React, so it navigates with `window.location.assign` rather than the router, and the reload resets every in-memory state.
- `queryClient` does not retry the 401, because it is an `ApiProblem`.

**Sources:** [client.ts](../../src/api/client.ts), [session.ts](../../src/auth/session.ts), [problemDetails.ts](../../src/api/problemDetails.ts), [problemDetails.test.ts](../../src/api/problemDetails.test.ts), [queryClient.ts](../../src/api/queryClient.ts), [SessionProvider.tsx](../../src/auth/SessionProvider.tsx), [LoginPage.tsx](../../src/auth/LoginPage.tsx).

## 4. Switching tenants

A SystemAdmin changes tenant from the header and stays on the same page. The drawing shows how the route change re-keys every tenant-scoped query.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as SystemAdmin
    participant Switcher as TenantSwitcher
    participant Cache as queryClient
    participant Api as Admin API
    participant Router as React Router
    participant Page as current page and its hooks
    Switcher->>Switcher: me.isSystemAdmin, otherwise a TenantBadge with the route's tenant
    Switcher->>Cache: useTenants, key tenants
    Cache->>Api: GET /api/tenants
    Api-->>Switcher: tenantId and isEnabled of every tenant
    Note over Switcher: A TenantBadge until the list loads, then a Combobox that marks disabled tenants
    Admin->>Switcher: pick another tenant
    Switcher->>Router: navigate(pathname with /t/{tenantId} replaced by /t/{next})
    Router->>Page: same route, new tenantId parameter
    Page->>Cache: useTenantId changes every key to resource, next, ...
    Cache->>Api: load the keys not cached yet, refetch the cached ones in the background
    Api-->>Page: the other tenant's data
```

- `navigate` gets only the pathname, so the search string, such as `?version=`, is dropped, while IDs in the path, such as a dataset ID, are kept for the other tenant.
- Users who are not SystemAdmins see a badge, and the API's tenant policy keeps them on their token's tenant.
- The old tenant's queries stay cached until TanStack Query collects them.

**Sources:** [TenantSwitcher.tsx](../../src/app/TenantSwitcher.tsx), [useTenants.ts](../../src/shared/useTenants.ts), [useTenantId.ts](../../src/shared/useTenantId.ts), [queryClient.ts](../../src/api/queryClient.ts), [datasets/queries.ts](../../src/features/datasets/queries.ts).

## 5. Loading a list page

Every list page is a lazy route whose page runs the feature's list query and hands the result to `EntityListPage`. The drawing follows the datasets list from the sidebar link to a row click.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Sidebar as AppSidebar
    participant Router as React Router
    participant Page as DatasetListPage
    participant Cache as queryClient
    participant Api as Admin API
    participant Table as EntityListPage and EntityTable
    User->>Sidebar: select Datasets
    Sidebar->>Router: Link to /t/{tenantId}/datasets
    Router->>Router: lazy Component imports DatasetListPage on the first visit
    Router->>Page: render in the AppShell Outlet, with the Datasets crumb
    Page->>Cache: useDatasets, key datasets, tenantId
    Cache->>Api: GET /api/tenants/{tenantId}/datasets
    Page->>Table: data undefined, isLoading true
    Note over Table: The empty row shows Loading
    alt 200
        Api-->>Cache: the tenant's datasets
        Cache-->>Page: data
        Page->>Table: rows
        Note over Table: Search keeps rows with a matching text or number value, sortable headers toggle the sort, no rows shows emptyMessage
    else failure
        Api-->>Cache: ProblemDetails
        Cache-->>Page: error, an ApiProblem
        Page->>Table: error
        Note over Table: ApiErrorAlert above the table
    end
    User->>Table: select a row
    Table->>Page: onRowClick(dataset)
    Page->>Router: navigate(String(datasetId)), relative to datasets
    Router->>Router: lazy DatasetDetailPage, and DatasetCrumb shows the dataset key
```

- `EntityTable` filters and sorts on the client: the list endpoints take no paging or filter parameters, so the table holds every row.
- Other list pages follow the same shape: `DimensionListPage`, `MappingSetListPage`, `BridgeListPage`, `ReleasesPage`, `AssumptionSetsPage`, `CalculationModelListPage`, `ReportListPage` and `TenantsPage`.

**Sources:** [AppSidebar.tsx](../../src/app/AppSidebar.tsx), [datasets/routes.tsx](../../src/features/datasets/routes.tsx), [DatasetListPage.tsx](../../src/features/datasets/DatasetListPage.tsx), [datasets/queries.ts](../../src/features/datasets/queries.ts), [EntityListPage.tsx](../../src/shared/EntityListPage.tsx), [EntityTable.tsx](../../src/shared/EntityTable.tsx), [DatasetCrumb.tsx](../../src/features/datasets/DatasetCrumb.tsx), [AppBreadcrumbs.tsx](../../src/app/AppBreadcrumbs.tsx), [admin-api.json](../../../api/openapi/admin-api.json).

## 6. Saving a form answered with 400

The API owns every validation rule, so a form learns about invalid input from a 400 answer and shows each message on its field. The drawing follows a sheet such as `FieldSheet` whose save the API rejects.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Sheet as EntityFormSheet
    participant Submit as useProblemSubmit
    participant Form as react-hook-form
    participant Save as save mutation
    participant Client as apiClient and unwrap
    participant Api as Admin API
    User->>Sheet: Save
    Sheet->>Submit: submit(event)
    Submit->>Form: clearErrors, then handleSubmit(save)
    Form->>Submit: save(values)
    Submit->>Save: onSubmit(values), which is save.mutateAsync
    Save->>Client: POST or PUT with the typed body
    Client->>Api: request with the Bearer token
    Api-->>Client: 400 ValidationProblemDetails
    Client-->>Save: unwrap throws ApiProblem with errors
    Save-->>Submit: mutateAsync rejects
    Submit->>Submit: showFieldErrors(form, error)
    Submit->>Submit: toFieldErrors, components[0].fieldId becomes components.0.fieldId
    loop each field path
        Submit->>Form: setError(path, type server, message)
    end
    Submit->>Sheet: setProblem(error)
    Sheet->>User: messages under the fields, the summary in ApiErrorAlert, the sheet still open
```

- `useFormField` reads each field's error from its `fieldState`, and `FormFieldLayout` shows it under the control; `ComponentsField` shows a component row's errors under that row.
- On success `useProblemSubmit` calls `onSuccess`, which closes the sheet; the mutation's own `onSuccess` has already invalidated the tenant's queries by then.
- `FormDialog`, the dataset `SettingsTab`, the dimension `SettingsTab` and `RangeBandsTab` take the same path. A problem without `errors`, such as a 409, only fills `ApiErrorAlert`.

**Sources:** [EntityFormSheet.tsx](../../src/shared/EntityFormSheet.tsx), [FormDialog.tsx](../../src/shared/FormDialog.tsx), [useProblemSubmit.ts](../../src/shared/form/useProblemSubmit.ts), [showFieldErrors.ts](../../src/shared/form/showFieldErrors.ts), [problemDetails.ts](../../src/api/problemDetails.ts), [useFormField.ts](../../src/shared/form/useFormField.ts), [FormFieldLayout.tsx](../../src/shared/form/FormFieldLayout.tsx), [ApiErrorAlert.tsx](../../src/shared/ApiErrorAlert.tsx), [FieldSheet.tsx](../../src/features/datasets/FieldSheet.tsx), [ComponentsField.tsx](../../src/features/measures/ComponentsField.tsx).

## 7. A version transition answered with 422

The API checks a version's rules when its status changes and answers a failure with the list of violated rules. The drawing shows how `VersionHeader` requests a change and keeps that list with the version it was for; the rules themselves are in [the API's version lifecycle](../../../api/docs/architecture/04-version-lifecycle.md).

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Header as VersionHeader
    participant Confirm as ConfirmDialog
    participant Transition as useVersionTransition
    participant Client as apiClient and unwrap
    participant Api as Admin API
    participant Alert as ApiErrorAlert
    User->>Header: Validate or Publish on the selected version
    opt the transition is confirmed first, as Publish and Retire are
        Header->>Confirm: title, and the description or its confirmDescriptions override
        User->>Confirm: confirm
    end
    Header->>Transition: mutate with id and status
    Transition->>Client: PUT /api/tenants/{tenantId}/versions/{kind}/{id}/status
    Client->>Api: body with the status
    alt rules fail
        Api-->>Client: 422 ProblemDetails with violations
        Client-->>Transition: unwrap throws ApiProblem with violations
        Header->>Alert: the error, while transition.variables.id is the selected version
        Alert->>User: title, detail, and each violation's message with its rule
    else accepted
        Api-->>Client: VersionState
        Transition->>Transition: onSuccess, invalidateTenantQueries(tenantId)
        Header->>User: toast, key is now status, then onTransitioned(result)
    end
```

- Selecting another version hides the alert; selecting the version again shows it until the next transition, because the mutation keeps its last variables and error.
- A published living version offers "Validate" instead: `useVersionCheck` posts to `.../versions/{kind}/{id}/validation`, which runs the same rules without changing the version and answers 200 with the violations. `VersionHeader` lists them with `RuleViolationList` in a destructive alert, or shows a toast when there are none, and keeps the answer with the version it was for.
- The transition buttons are disabled while a change is pending. A 409 shows in the same alert, without a violation list.

**Sources:** [VersionHeader.tsx](../../src/shared/VersionHeader.tsx), [useVersionTransition.ts](../../src/shared/useVersionTransition.ts), [versionTransitions.ts](../../src/shared/versionTransitions.ts), [ConfirmDialog.tsx](../../src/shared/ConfirmDialog.tsx), [ApiErrorAlert.tsx](../../src/shared/ApiErrorAlert.tsx), [problemDetails.ts](../../src/api/problemDetails.ts), [problemDetails.test.ts](../../src/api/problemDetails.test.ts), [queryClient.ts](../../src/api/queryClient.ts), [ReleaseEditorPage.tsx](../../src/features/releases/ReleaseEditorPage.tsx).

## 8. Cloning a version

"Clone selected" in `VersionHeader` hands the selected version to the page, and each page clones in its own way. The drawing shows the dataset page, which clones at once, and the mapping set page, which confirms in a dialog first.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Header as VersionHeader
    participant DatasetPage as DatasetDetailPage
    participant MappingPage as MappingSetDetailPage
    participant Dialog as NewMappingVersionDialog
    participant Api as Admin API
    participant Cache as queryClient
    User->>Header: Clone selected
    alt dataset page
        Header->>DatasetPage: onCloneVersion(selected.id)
        DatasetPage->>Api: cloneVersion, POST .../datasets/{datasetId}/versions with cloneFromVersionId
        Api-->>DatasetPage: the new draft
        DatasetPage->>DatasetPage: toast, setSearchParams version to the new datasetVersionId
        DatasetPage->>Cache: invalidateTenantQueries(tenantId)
    else mapping set page
        Header->>MappingPage: onCloneVersion(selected.id)
        MappingPage->>Dialog: open with the source version
        User->>Dialog: Create draft
        Dialog->>Api: useCreateMappingVersion, POST .../mapping-sets/{mappingSetId}/versions with cloneFromVersionId
        Api-->>Dialog: the new MappingVersion
        Dialog->>Cache: invalidate the mapping-versions lookups
        Dialog->>MappingPage: onCreated(version), then the dialog closes
        MappingPage->>MappingPage: toast, setSearchParams version to the new mappingVersionId, replace
    end
```

- A clone copies the source's rows as they are. Entries, bridge mappings, binding groupings, report columns and lines name members, metrics and measures by key, so nothing is dropped; the publication rules check that the keys resolve in the published versions requests read them in.
- Dimensions clone through `NewVersionSheet`, which copies levels, members and range bands. Bridges clone through `NewBridgeVersionDialog`, which copies the mappings. Calculation models clone through `NewModelVersionDialog`, which copies every content row; reports clone through `NewReportVersionDialog`, which starts from the source's model and default profile and copies the columns and lines. The release editor's "Clone selected" opens `NewReleaseSheet` with `copyFromReleaseId`, then opens the new release. Assumption sets are not versioned; "Capture" snapshots the active set and the active release under a new key.
- What the API does for a clone is drawn in [the API's key actions](../../../api/docs/architecture/06-key-actions.md).

**Sources:** [VersionHeader.tsx](../../src/shared/VersionHeader.tsx), [DatasetDetailPage.tsx](../../src/features/datasets/DatasetDetailPage.tsx), [MappingSetDetailPage.tsx](../../src/features/mappings/MappingSetDetailPage.tsx), [NewMappingVersionDialog.tsx](../../src/features/mappings/NewMappingVersionDialog.tsx), [mappings/api.ts](../../src/features/mappings/api.ts), [DimensionDetailPage.tsx](../../src/features/dimensions/DimensionDetailPage.tsx), [NewVersionSheet.tsx](../../src/features/dimensions/NewVersionSheet.tsx), [dimensions/api.ts](../../src/features/dimensions/api.ts), [BridgeDetailPage.tsx](../../src/features/bridges/BridgeDetailPage.tsx), [NewBridgeVersionDialog.tsx](../../src/features/bridges/NewBridgeVersionDialog.tsx), [NewReportVersionDialog.tsx](../../src/features/reports/NewReportVersionDialog.tsx), [ReleaseEditorPage.tsx](../../src/features/releases/ReleaseEditorPage.tsx), [NewReleaseSheet.tsx](../../src/features/releases/NewReleaseSheet.tsx), [CaptureSheet.tsx](../../src/features/assumption-sets/CaptureSheet.tsx).

## 9. Bulk paste of mapping entries

Bulk paste replaces every entry of an editable mapping version, a draft or the published one, with pasted lines. The drawing shows how the lines are read as they are typed, sent as one replace, and how the API's item errors come back on the lines they concern.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Entries as MappingEntries
    participant Paste as EntriesPasteDialog
    participant Bulk as BulkPasteDialog
    participant Lines as entryLines and bulkLists
    participant Submit as FormDialog and useProblemSubmit
    participant Replace as useReplaceEntries
    participant Api as Admin API
    User->>Entries: Bulk paste, on an editable version
    Entries->>Paste: the version and its entries
    Paste->>Bulk: initialText from writeEntryLines, read is readEntryLines
    loop while the user edits the lines
        Bulk->>Lines: readEntryLines(text), through readPastedLines
        Lines-->>Bulk: rows, lineNumbers and unreadable lines
        Bulk->>User: the unreadable lines with reasons, and Replace with N entries
    end
    User->>Submit: Replace
    Submit->>Bulk: submit(values), after clearErrors and handleSubmit
    Bulk->>Lines: read(values.text)
    Bulk->>Replace: replace(rows), which is mutateAsync
    Replace->>Api: PUT /api/tenants/{tenantId}/mapping-versions/{versionId}/entries with entries
    alt 400 with errors keyed like entries[3].sourceKeyValue
        Api-->>Replace: ValidationProblemDetails
        Replace-->>Bulk: ApiProblem
        Bulk->>Lines: problemOnLines(error, entries, lineNumbers, text)
        Lines-->>Bulk: an ApiProblem with every message on text, item messages prefixed with their line number
        Bulk-->>Submit: rethrows it
        Submit->>User: messages under the Lines field, the summary in ApiErrorAlert
    else 200
        Api-->>Replace: the stored entries
        Replace->>Replace: setQueryData on the mapping-entries key
        Bulk->>User: toast, Saved N entries
        Submit->>Entries: onSuccess closes the dialog
    end
```

- `readPastedLines` skips blank lines, splits each line at its commas and trims the values, so leading zeros survive. `readEntryLines` expects `source_key,member_key` and keeps both keys as typed; a line without two values is listed with its reason and left out of the replace. A member key the dimension's published version lacks is reported by the publication rules, and on the published version by "Validate".
- The text starts with the current entries, so the lines the user leaves alone are kept.
- Bridge mappings use the same `BulkPasteDialog` through `BridgeMappingsPasteDialog` and `readMappingLines`.

**Sources:** [MappingEntries.tsx](../../src/features/mappings/MappingEntries.tsx), [EntriesPasteDialog.tsx](../../src/features/mappings/EntriesPasteDialog.tsx), [BulkPasteDialog.tsx](../../src/shared/BulkPasteDialog.tsx), [entryLines.ts](../../src/features/mappings/entryLines.ts), [entryLines.test.ts](../../src/features/mappings/entryLines.test.ts), [bulkLists.ts](../../src/shared/bulkLists.ts), [bulkLists.test.ts](../../src/shared/bulkLists.test.ts), [mappings/api.ts](../../src/features/mappings/api.ts), [FormDialog.tsx](../../src/shared/FormDialog.tsx), [useProblemSubmit.ts](../../src/shared/form/useProblemSubmit.ts), [BridgeMappingsPasteDialog.tsx](../../src/features/bridges/BridgeMappingsPasteDialog.tsx).

## 10. Activating a release

Activation switches the tenant's queries to a published release's dataset versions, so it is confirmed first and refreshes every query of the tenant. The drawing follows the button on the release editor.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Editor as ReleaseEditorPage
    participant Button as ActivateReleaseButton
    participant Confirm as ConfirmDialog
    participant ActivateHook as useActivateRelease
    participant Api as Admin API
    participant Cache as queryClient
    Editor->>Button: ReleaseActivation renders it for a published release that is not active
    User->>Button: Activate
    Button->>Confirm: open, Activate releaseKey?
    User->>Confirm: confirm
    Confirm->>Button: onConfirm
    Button->>Editor: onActivate, which runs activateRelease(release)
    Editor->>ActivateHook: mutate(releaseId)
    ActivateHook->>Api: POST /api/tenants/{tenantId}/releases/{releaseId}/activate
    alt accepted
        Api-->>ActivateHook: success
        ActivateHook->>Cache: invalidateTenantQueries(tenantId)
        Cache->>Api: refetch the tenant's active queries, the release summaries among them
        Editor->>User: toast, and the Active badge replaces the button
    else refused
        Api-->>ActivateHook: ProblemDetails
        Editor->>User: ApiErrorAlert under the version header
    end
```

- `isActive` comes from the release summaries of `useReleases`; the active release shows an "Active" badge, and its Retire confirmation says it must not be retired while active.
- What the API changes on activation is drawn in [the API's key actions](../../../api/docs/architecture/06-key-actions.md).

**Sources:** [ReleaseEditorPage.tsx](../../src/features/releases/ReleaseEditorPage.tsx), [ActivateReleaseButton.tsx](../../src/features/releases/ActivateReleaseButton.tsx), [ConfirmDialog.tsx](../../src/shared/ConfirmDialog.tsx), [useReleases.ts](../../src/features/releases/useReleases.ts), [queryClient.ts](../../src/api/queryClient.ts).

## 11. Editable and read-only versions

A version takes content writes while it is a draft, and a living kind's published version keeps taking them, because requests read it. Every other version renders read-only while keeping its lifecycle actions. The drawing shows how the selected version's kind and status reach the tables, sheets and forms; arrows mean "decides" or "renders".

```mermaid
flowchart TB
    Selected["selected version: selectedVersion on detail pages, the record on the release editor"]
    IsEditable{"isEditable(kind, status): a draft, or a living kind's published version"}
    Editable["editable: toolbars, row actions, Save buttons, drag handles"]
    ReadOnly["read-only: isEditable false"]
    Hidden["toolbars, row-action cells, Edit buttons, drag handles: not rendered"]
    Sheets["selecting a row still opens its sheet, with readOnly"]
    SheetReadOnly["EntityFormSheet readOnly: FormReadOnlyContext true, fieldset disabled, Close and no submit button"]
    Inline["SettingsTab, dimension SettingsTab, RangeBandsTab: FormReadOnlyContext, no Save button"]
    Fields["useFormField: isDisabled, so the Monaco and CodeMirror editors turn read-only too"]
    Pinned["PinnedVersionsTable readOnly: no add picker, no remove column, no Save or Discard, version pickers disabled"]
    Header["VersionHeader: transitions for the status, Validate on a published living version, Clone selected, New version"]
    Selected --> IsEditable
    IsEditable -->|"yes"| Editable
    IsEditable -->|"no"| ReadOnly
    ReadOnly --> Hidden
    ReadOnly --> Sheets
    Sheets --> SheetReadOnly
    SheetReadOnly --> Fields
    ReadOnly --> Inline
    Inline --> Fields
    ReadOnly --> Pinned
    Selected -->|"regardless of status"| Header
```

- Dimension, mapping set, bridge, calculation model and report pages pass `isEditable(kind, selected.status)` down for the version that `selectedVersion` picks. Dataset versions are strict, so the dataset page passes `isDraft`; the release editor compares the loaded record's `status` with `draft`.
- `PinnedVersionsTable` shows a release's manifest. The report column and line editors hide their drag handles, add, remove, Save and Discard controls, and their sheets open read-only; `MembersTab` turns off dragging.
- Assumption sets are not versioned and their pins are never edited by hand: `AssumptionPinsTable` lists them read-only, the active set follows every publish and reads the active release, and a historical set keeps the versions and the release it captured, which its page links to with the release's status. Either set can be relabelled, and a historical one deleted.
- `VersionHeader` does not read `isEditable`: it offers what `getVersionTransitions` allows for the kind and status, "Validate" on a published living version, "Clone selected" and "New version" when the page passes them, and "Delete draft" only on drafts.

**Sources:** [DatasetDetailPage.tsx](../../src/features/datasets/DatasetDetailPage.tsx), [DatasetVersionTabs.tsx](../../src/features/datasets/DatasetVersionTabs.tsx), [FieldsTab.tsx](../../src/features/datasets/FieldsTab.tsx), [FieldSheet.tsx](../../src/features/datasets/FieldSheet.tsx), [SettingsTab.tsx](../../src/features/datasets/SettingsTab.tsx), [EntityFormSheet.tsx](../../src/shared/EntityFormSheet.tsx), [FormReadOnlyContext.ts](../../src/shared/form/FormReadOnlyContext.ts), [useFormField.ts](../../src/shared/form/useFormField.ts), [ExpressionField.tsx](../../src/shared/form/ExpressionField.tsx), [JsonField.tsx](../../src/shared/form/JsonField.tsx), [PinnedVersionsTable.tsx](../../src/shared/PinnedVersionsTable.tsx), [ReleaseEditorPage.tsx](../../src/features/releases/ReleaseEditorPage.tsx), [AssumptionSetEditorPage.tsx](../../src/features/assumption-sets/AssumptionSetEditorPage.tsx), [AssumptionPinsTable.tsx](../../src/features/assumption-sets/AssumptionPinsTable.tsx), [versionTransitions.ts](../../src/shared/versionTransitions.ts), [MembersTab.tsx](../../src/features/dimensions/MembersTab.tsx), [RangeBandsTab.tsx](../../src/features/dimensions/RangeBandsTab.tsx), [dimensions/SettingsTab.tsx](../../src/features/dimensions/SettingsTab.tsx), [MappingEntries.tsx](../../src/features/mappings/MappingEntries.tsx), [BridgeVersionPanel.tsx](../../src/features/bridges/BridgeVersionPanel.tsx), [BridgeMappings.tsx](../../src/features/bridges/BridgeMappings.tsx), [VersionHeader.tsx](../../src/shared/VersionHeader.tsx).
