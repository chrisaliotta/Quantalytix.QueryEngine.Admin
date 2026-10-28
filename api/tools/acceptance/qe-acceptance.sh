#!/usr/bin/env bash
# End-to-end acceptance of Query Engine Admin (contract-v1.2): a dataset authored through the admin API reaches the query engine.
#
#   PREFIX=zz_w2_ ./qe-acceptance.sh
#     Creates dataset ${PREFIX}loans over loan_daily (draft version 1 gets a field per readable column) and commits one change set
#     on the draft: 2 grains, a mapped binding of the seed branch role through the seed mapping set loan.branch, a time binding on
#     the seed Calendar dimension, 4 measures and the default time binding; then validates and publishes it. It creates calculation
#     model ${PREFIX}margin in US dollars and commits its 2 variables, a yield measure and enabling it. It creates report
#     ${PREFIX}margin over the model, its report dimension a copy of the seed MarginStatement dimension, and commits one change set:
#     2 GL monthly bridge sources on the seed account role (gl_balance takes the seed report's gl_monthly rules row by row,
#     gl_income copies gl_balance's rules), their variables, profile default picking both, a branch calculation dimension, 3
#     columns, ASSETS as a header and a formula row; then it enables the report. It captures assumption set ${PREFIX}backtest,
#     which keeps the release active at that moment. Then it creates release ${PREFIX}acceptance as a copy of the active release,
#     commits its manifest plus the dataset version, validates, publishes and activates it, renames the report's yield column
#     through a report change set, checks GetCatalog, Execute, GetReportCatalog (which must show the renamed column) and
#     ExecuteReport on the engine, checks that GetCatalog naming ${PREFIX}backtest still reads the release it captured, and
#     re-activates the release that was active before.
#   PREFIX=zz_live_ DATASET_KEY=zz_bm_loans MEASURES="zzBmBalance zzBmRate zzBmBalanceRate zzBmEndingBalance" ./qe-acceptance.sh
#     Releases an existing dataset's latest published version instead of authoring one.
#   PREFIX=zz_w2_ AUTHOR_CHECK_ONLY=1 ./qe-acceptance.sh
#     Authors and validates ${PREFIX}loans, reopens it to draft, then deletes it: the authoring path without leaving objects.
#
# Ids are strings in every request and response. Run it outside the shell sandbox: it calls localhost and StarRocks. It needs
# curl, python3 and the .NET 10 SDK, and the admin API running in Development at $ADMIN. When $ENGINE does not answer, it starts
# the engine host from its built DLL under $QueryEngineRoot (or $ENGINE_HOST_DIR) and stops it at the end. It writes to tenant
# $TENANT, development by default, and never prints the control-plane connection string. While it runs, its release is the
# tenant's active release, so don't run the engine's golden cases at the same time.
# It leaves ${PREFIX}loans (published v1, when authored), the ${PREFIX}margin model and report (enabled) with the report's
# dimension and bridges, ${PREFIX}acceptance (published, inactive) and ${PREFIX}backtest until the seed kit is reloaded. Their keys
# stay taken, so every run needs a new PREFIX.
set -euo pipefail
: "${PREFIX:?Set PREFIX, for example PREFIX=zz_w2_}"
ADMIN=${ADMIN:-http://localhost:5186}
ENGINE=${ENGINE:-http://localhost:5098}
TENANT=${TENANT:-development}
ENGINE_HOST_DIR=${ENGINE_HOST_DIR:-${QueryEngineRoot:?Set QueryEngineRoot to the query engine checkout}/src/Quantalytix.QueryEngine.Host}
ENGINE_DLL=${ENGINE_DLL:-bin/Debug/net10.0/Quantalytix.QueryEngine.Host.dll}
MEASURES=${MEASURES:-balance avgRate balanceRate endingBalance}
RANGE_START=${RANGE_START:-2026-01-01}
RANGE_END=${RANGE_END:-2026-04-01}
REPORT_RANGE_START=${REPORT_RANGE_START:-2024-01-01}
REPORT_RANGE_END=${REPORT_RANGE_END:-2024-04-01}
LIVE_COLUMN_NAME="Yield (live edit)"
SECRETS=$HOME/.microsoft/usersecrets/Quantalytix.QueryEngine.Admin.WebApi.Development/secrets.json
API=$ADMIN/api/tenants/$TENANT
W=$(mktemp -d /tmp/qe-acceptance.XXXXXX)
ORIGINAL=""
PASSED=0

TOKEN=$(curl -sf -m 30 -X POST "$ADMIN/development/token" -H 'Content-Type: application/json' \
  -d "{\"tenantId\":\"$TENANT\",\"systemAdmin\":true}" | python3 -c 'import json, sys; print(json.load(sys.stdin)["accessToken"])')

# call METHOD PATH STATUS [BODY]: stops the script unless the admin API answers STATUS; the body lands in $W/last.json.
call() {
  local args=(-s -m 120 -o "$W/last.json" -w '%{http_code}' -X "$1" "$API$2" -H "Authorization: Bearer $TOKEN")
  if [ -n "${4:-}" ]; then args+=(-H 'Content-Type: application/json' -d "$4"); fi
  local status
  status=$(curl "${args[@]}")
  echo "$1 $2 -> $status"
  if [ "$status" != "$3" ]; then cat "$W/last.json"; echo; exit 1; fi
}

# py EXPR: prints a Python expression over the last response, r.
py() { python3 -c "import json; r = json.load(open('$W/last.json')); print($1)"; }

# keep NAME: keeps the last response as $W/NAME.json, for scripts that read several responses.
keep() { cp "$W/last.json" "$W/$1.json"; }

# changes PATH BODY_SCRIPT [ARGS...]: commits the change set a Python script prints; the script reads $W/*.json as it needs.
changes() {
  local path=$1 script=$2
  shift 2
  call POST "$path/changes" 200 "$(python3 -c "$script" "$W" "$@")"
}

# The seed objects every mode names, by key: datasets, dimensions, roles, mapping sets, reports and releases.
read_catalog() {
  call GET /catalog 200
  keep catalog
}

# cat_id LIST KEY: the id of the catalog item of a list with a key.
cat_id() { python3 -c "import json; print([x['id'] for x in json.load(open('$W/catalog.json'))['$1'] if x['key'] == '$2'][0])"; }

author_dataset() {
  DATASET_KEY=${PREFIX}loans
  call POST /datasets 201 "{\"mode\":\"existing\",\"key\":\"$DATASET_KEY\",\"displayName\":\"$DATASET_KEY acceptance\",\"sourceObjectKind\":\"table\",\"sourceObject\":\"loan_daily\"}"
  DATASET=$(py "r['id']")
  VERSION=$(py "r['versions'][0]['id']")
  local V=/dataset-versions/$VERSION
  call GET "$V" 200
  keep version

  # Seed objects are only referenced: the Calendar time dimension, the loan.branch mapping set onto Branch, and Branch's branch
  # role. Items the change set creates are named by temporary ids, which later operations of the same change set reference.
  changes "$V" '
import json, sys
w, calendar, mapset, branch_role = sys.argv[1:5]
version = json.load(open(f"{w}/version.json"))
field = {f["key"]: f["id"] for f in version["fields"]}
common = {"raggedBehavior": "null", "defaultPeriodAlignment": "observation", "unmappedBehavior": "null", "unknownMemberKey": None,
          "isGroupable": True, "isFilterable": True, "isEnabled": True, "bridgeId": None, "sourceDimensionBindingId": None}
def measure(key, kind, **overrides):
    body = {"key": key, "displayName": key, "isEnabled": True, "measureKind": kind, "aggregationFunction": None, "expressionText": None,
            "implementationKey": None, "implementationVersion": None, "configurationJson": None, "components": [],
            "rowFilterExpression": None, "valueNullBehavior": "exclude", "negativeWeightBehavior": "allow", "emptySetBehavior": "null",
            "bridgeFactorBehavior": "ignore", "bridgeAllocationBehavior": "apply", "totalBehavior": "recompute",
            "outputDataType": "decimal", "outputPrecision": 20, "outputScale": 2, "roundingMode": None, "unitKey": None,
            "accumulatorPrecision": 38, "accumulatorScale": 12, "timePolicy": None}
    body.update(overrides)
    return {"op": "saveMeasure", "input": body}
operations = [
    {"op": "saveGrain", "input": {"id": "tmp:series", "key": "loanSeries", "grainKind": "series", "fieldIds": [field["loanId"]]}},
    {"op": "saveGrain", "input": {"key": "rowIdentity", "grainKind": "row", "fieldIds": [field["loanId"], field["asOfDate"]]}},
    {"op": "saveBinding", "input": dict(common, bindingKind="mapped", dimensionRoleId=branch_role, dimensionId=None, sourceFieldId=field["branchCode"],
                                        mappingSetId=mapset, isRequired=False, time=None)},
    {"op": "saveBinding", "input": dict(common, id="tmp:time", bindingKind="time", dimensionRoleId=None, dimensionId=calendar, sourceFieldId=field["asOfDate"],
                                        mappingSetId=None, isRequired=True, time={
        "timeValueType": "date", "sourceTimeZone": None, "defaultQueryTimeZone": "UTC", "observationKind": "snapshot", "nativePeriodicity": "day",
        "intervalEndFieldId": None, "allowedPeriodicities": ["day", "month", "quarter", "year"], "cumulativeOrigin": "output_range_start",
        "cumulativeFixedStart": None})},
    measure("balance", "aggregate", aggregationFunction="sum", components=[{"role": "value", "fieldId": field["endingBalance"]}]),
    measure("avgRate", "weighted_average", outputPrecision=9, outputScale=6,
            components=[{"role": "value", "fieldId": field["interestRate"]}, {"role": "weight", "fieldId": field["endingBalance"]}]),
    measure("balanceRate", "formula", outputPrecision=28, outputScale=6, expressionText="[m.balance] * [m.avgRate]"),
    measure("endingBalance", "aggregate", aggregationFunction="sum", components=[{"role": "value", "fieldId": field["endingBalance"]}], timePolicy={
        "requiredTimeBindingId": "tmp:time", "selectionDatasetGrainId": "tmp:series", "observationSelection": "at_end", "valueTimeRole": "point",
        "boundaryMatch": "on_or_before", "maxLookbackSeconds": 86400, "durationUnit": "calendar_day", "partialIntervalBehavior": "exact_only",
        "missingPeriodBehavior": "null"}),
    {"op": "saveSettings", "settings": dict(version["settings"], defaultTimeBindingId="tmp:time")},
]
print(json.dumps({"operations": operations}))' "$(cat_id dimensions Calendar)" "$(cat_id mappingSets loan.branch)" "$(cat_id roles branch)"
  echo "Dataset content: $(py "[(k, len(r['state'][k])) for k in ['fields', 'grains', 'bindings', 'measures']]")"

  call POST "$V/validate" 200
  if [ "${AUTHOR_CHECK_ONLY:-}" = 1 ]; then
    call POST "$V/reopen" 200
    call DELETE "/datasets/$DATASET" 204
    echo "PASS (authoring only): $DATASET_KEY validated, reopened, then deleted."
    rm -rf "$W"
    exit 0
  fi
  call POST "$V/publish" 200
}

# Creates model ${PREFIX}margin, which holds the metrics and has no versions to publish, and builds report ${PREFIX}margin over it
# in one report change set. The report's own report dimension starts as a copy of the seed MarginStatement dimension, whose
# members are its rows. Two sources bridge from the seed account role, which each GL dataset binds: gl_balance takes the rules of
# the seed report's gl_monthly source row by row, and gl_income starts as a copy of gl_balance's rules. Profile default picks both
# for the variables they supply. The branch calculation dimension on the seed branch role, the columns, ASSETS as a header and a
# formula row follow, and enabling the report checks it against every report rule.
# Sources name keys and roles only: the active release decides which dataset versions they resolve in, and the active assumption
# set which dimension and bridge versions.
author_report() {
  REPORT_KEY=${PREFIX}margin
  call POST /calculation-models 201 "{\"key\":\"$REPORT_KEY\",\"displayName\":\"$REPORT_KEY acceptance\",\"currencyCode\":\"USD\",\"formulaLanguage\":\"1.0\",\"attributionMethod\":\"shapley\"}"
  MODEL=$(py "r['id']")
  changes "/calculation-models/$MODEL" '
import json
variable = lambda key, name: {"op": "saveVariable", "input": {"key": key, "displayName": name, "displayFormat": "currency", "unitKey": None, "missingBehavior": "null"}}
print(json.dumps({"operations": [
    variable("AVERAGE_BALANCE", "Average balance"),
    variable("INTEREST_INCOME", "Interest income"),
    {"op": "saveMeasure", "input": {"key": "YIELD", "displayName": "Yield", "formula": "[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays",
                                    "outputDataType": "decimal", "outputPrecision": 28, "outputScale": 6, "roundingMode": None, "unitKey": None,
                                    "displayFormat": "percent"}},
    {"op": "updateModel", "patch": {"isEnabled": True}}]}))'

  call GET "/reports/$(cat_id reports bank.margin_statement)" 200
  keep seed
  call POST /reports 201 "{\"key\":\"$REPORT_KEY\",\"displayName\":\"$REPORT_KEY acceptance\",\"reportType\":\"margin_analysis\",\"modelId\":\"$MODEL\",\"startFromDimensionId\":\"$(cat_id dimensions MarginStatement)\"}"
  REPORT=$(py "r['id']")
  local R=/reports/$REPORT
  call GET "$R" 200
  keep document
  changes "$R" '
import json, sys
w, balances, income, account_role, branch_role = sys.argv[1:6]
seed = json.load(open(f"{w}/seed.json"))
report = json.load(open(f"{w}/document.json"))
gl_monthly = [s for s in seed["sources"] if s["key"] == "gl_monthly"][0]
source = lambda key, name, dataset: {"key": key, "displayName": name, "datasetId": dataset, "timeFieldKey": "reportingPeriod",
                                     "method": "bridge", "roleId": account_role, "fieldKey": None}
variables = lambda key, variable, measure, filter: {"op": "setSourceVariables", "sourceKey": key,
                                                    "variables": [{"variableKey": variable, "measureKey": measure, "filter": filter, "missingBehavior": None}]}
assets = [row for row in report["rows"] if row["key"] == "ASSETS"][0]
siblings = lambda parent: [row["key"] for row in report["rows"] if row["parentKey"] == parent]
column = lambda key, kind, metric: {"key": key, "displayName": None, "valueKind": kind, "metricKey": metric, "displayFormat": None, "isVisible": True}
operations = [
    {"op": "saveSource", "input": source("gl_balance", "GL monthly balances", balances)},
    *[{"op": "setRowRules", "sourceKey": "gl_balance", "rowKey": row, "rules": rules} for row, rules in gl_monthly["rules"].items()],
    {"op": "saveSource", "input": source("gl_income", "GL interest income", income), "copyFromSourceKey": "gl_balance"},
    variables("gl_balance", "AVERAGE_BALANCE", "periodAverage", None),
    variables("gl_income", "INTEREST_INCOME", "totalValue",
              {"operator": "eq", "left": {"type": "field", "id": "accountKey"}, "right": {"type": "literal", "value": "4000"}}),
    {"op": "saveProfile", "profile": {"key": "default", "displayName": "Default", "isEnabled": True,
                                      "picks": {"AVERAGE_BALANCE": "gl_balance", "INTEREST_INCOME": "gl_income"}}},
    {"op": "updateSettings", "patch": {"defaultProfileKey": "default"}},
    {"op": "saveCalculationDimension", "input": {"key": "branch", "displayName": "Branch", "roleId": branch_role, "grouping": {"mode": "member"},
                                                 "isGroupable": True, "isFilterable": True, "isEnabled": True}},
    {"op": "setColumns", "columns": [column("avgBalance", "variable", "AVERAGE_BALANCE"), column("interestIncome", "variable", "INTEREST_INCOME"),
                                     column("yield", "calculated", "YIELD")]},
    {"op": "saveRow", "row": dict(assets, showsValues=False), "index": siblings(assets["parentKey"]).index("ASSETS")},
    {"op": "saveRow", "index": len(siblings(None)), "row": {"key": "reportYield", "label": "Report yield", "kind": "formula", "parentKey": None,
                                                            "isVisible": True, "showsValues": True, "calculatedMeasureKey": "YIELD",
                                                            "valueColumnKey": "yield", "styleKey": None, "parameters": {}}},
]
print(json.dumps({"operations": operations}))' "$(cat_id datasets bank.balance_monthly)" "$(cat_id datasets bank.income_monthly)" "$(cat_id roles account)" "$(cat_id roles branch)"
  echo "Sources and their rule rows: $(py "[(s['key'], len(s['rules'])) for s in r['state']['sources']]")"
  echo "Profile default picks $(py "[sorted(p['picks']) for p in r['state']['profiles'] if p['key'] == 'default'][0]") (expected AVERAGE_BALANCE and INTEREST_INCOME)"
  call PUT "$R/enabled" 200 '{"isEnabled":true}'
}

# Renames the enabled report's yield column through a report change set: a live edit, which advances the semantic revision so the
# engine reloads the report.
rename_yield_column() {
  local R=/reports/$REPORT
  call GET "$R" 200
  keep document
  changes "$R" '
import json, sys
report = json.load(open(f"{sys.argv[1]}/document.json"))
columns = [dict(c, displayName=sys.argv[2]) if c["key"] == "yield" else c for c in report["columns"]]
print(json.dumps({"operations": [{"op": "setColumns", "columns": columns}]}))' "$LIVE_COLUMN_NAME"
}

find_published_version() {
  DATASET=$(cat_id datasets "$DATASET_KEY")
  call GET "/datasets/$DATASET" 200
  VERSION=$(py "max((v for v in r['versions'] if v['status'] == 'published'), key=lambda v: v['versionNumber'])['id']")
}

start_engine() {
  if curl -sf -m 5 "$ENGINE/health" > /dev/null; then return; fi
  # ASPNETCORE_STATICWEBASSETS skips the static web assets manifest of a Host built on another OS; wwwroot still serves.
  (
    cd "$ENGINE_HOST_DIR"
    ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=$ENGINE ASPNETCORE_STATICWEBASSETS="$W/none.json" \
      ControlPlane__Provider=StarRocks \
      ConnectionStrings__ControlPlane="$(python3 -c "import json; print(json.load(open('$SECRETS', encoding='utf-8-sig'))['ConnectionStrings:ControlPlane'])")" \
      nohup dotnet "$ENGINE_DLL" > "$W/engine.log" 2>&1 &
    echo $! > "$W/engine.pid"
  )
  for _ in $(seq 1 60); do curl -sf -m 2 "$ENGINE/health" > /dev/null && return; sleep 1; done
  echo "The engine did not start; see $W/engine.log"; exit 1
}

restore() {
  if [ -n "$ORIGINAL" ]; then
    call POST "/releases/$ORIGINAL/activate" 200
    call GET /overview 200
    echo "Active release: $(py "r['activeReleaseId']") (expected $ORIGINAL, $ORIGINAL_KEY)"
  fi
  if [ -f "$W/engine.pid" ]; then kill "$(cat "$W/engine.pid")"; fi
  if [ "$PASSED" = 1 ]; then rm -rf "$W"; else echo "Kept $W for inspection."; fi
}

read_catalog
if [ -n "${DATASET_KEY:-}" ]; then find_published_version; else author_dataset; fi
echo "Dataset $DATASET_KEY: version $VERSION"
author_report
echo "Report $REPORT_KEY: enabled, over model $MODEL"

ORIGINAL_ID=$(python3 -c "import json; print([x['id'] for x in json.load(open('$W/catalog.json'))['releases'] if x['isActive']][0])")
ORIGINAL_KEY=$(python3 -c "import json; print([x['key'] for x in json.load(open('$W/catalog.json'))['releases'] if x['isActive']][0])")
call POST /assumption-sets 201 "{\"key\":\"${PREFIX}backtest\",\"displayName\":\"${PREFIX}backtest\",\"description\":null}"
echo "Captured ${PREFIX}backtest on release $(py "r['releaseId']") (expected $ORIGINAL_ID)"
call POST /releases 201 "{\"key\":\"${PREFIX}acceptance\",\"copyFromReleaseId\":\"$ORIGINAL_ID\"}"
RELEASE=$(py "r['id']")
call GET "/releases/$RELEASE" 200
keep release
changes "/releases/$RELEASE" '
import json, sys
w, dataset, version = sys.argv[1:4]
manifest = [entry for entry in json.load(open(f"{w}/release.json"))["manifest"] if entry["datasetId"] != dataset]
print(json.dumps({"operations": [{"op": "saveManifest", "manifest": [{"datasetId": e["datasetId"], "datasetVersionId": e["datasetVersionId"]} for e in manifest]
                                  + [{"datasetId": dataset, "datasetVersionId": version}]}]}))' "$DATASET" "$VERSION"
call POST "/releases/$RELEASE/validate" 200
call POST "/releases/$RELEASE/publish" 200
ORIGINAL=$ORIGINAL_ID
trap restore EXIT
call POST "/releases/$RELEASE/activate" 200
echo "Active release: $(py "r['activeReleaseId']") (expected $RELEASE), semantic revision $(py "r['semanticRevision']")"
rename_yield_column

start_engine
python3 - "$DATASET_KEY" "$MEASURES" "$RANGE_START" "$RANGE_END" > "$W/request.json" <<'EOF'
import json, sys
dataset, measures, start, end = sys.argv[1], sys.argv[2].split(), sys.argv[3], sys.argv[4]
print(json.dumps({"requestId": "acceptance-" + dataset, "queries": [{
    "id": "byBranch", "datasetId": dataset, "mode": "aggregate",
    "time": {"dimension": "asOfDate", "periodicity": "month", "timeZone": "UTC", "range": {"start": start, "endExclusive": end}},
    "dimensions": [{"id": "branch"}],
    "measures": [{"kind": "column", "id": key} for key in measures],
    "orderBy": [{"field": "asOfDate"}, {"field": "branch"}]}]}))
EOF
python3 - "$REPORT_KEY" "$REPORT_RANGE_START" "$REPORT_RANGE_END" > "$W/report.json" <<'EOF'
import json, sys
report, start, end = sys.argv[1], sys.argv[2], sys.argv[3]
print(json.dumps({"requestId": "acceptance-" + report, "reportKey": report, "dimensions": [{"id": "branch"}], "includeTotals": True,
    "time": {"periodicity": "month", "timeZone": "UTC", "range": {"start": start, "endExclusive": end}}}))
EOF

mkdir -p "$W/hub"
cat > "$W/hub/hub.cs" <<'EOF'
#:package Microsoft.AspNetCore.SignalR.Client@10.0.10
#:property PublishAot=false
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

// Usage: dotnet run hub.cs -- <engineUrl> <tenant> <expectedReleaseKey> <datasetKey> <requestFile> <reportKey> <reportRequestFile> <liveColumnName> <backtestSet> <backtestReleaseKey>
// Exits 0 when GetCatalog reports the expected release and lists the dataset, every query of the request returns rows,
// GetReportCatalog lists the report with the live-edited column name, every table of the report request returns rows, and
// GetCatalog naming the captured set reports the release that was active when it was captured.
var (engineUrl, tenant, expectedReleaseKey, datasetKey, requestFile) = (args[0], args[1], args[2], args[3], args[4]);
var (reportKey, reportRequestFile, liveColumnName) = (args[5], args[6], args[7]);
var (backtestSet, backtestReleaseKey) = (args[8], args[9]);

using var http = new HttpClient();
var tokenResponse = await http.PostAsJsonAsync($"{engineUrl}/development/token", new { tenantId = tenant, systemAdmin = true });
tokenResponse.EnsureSuccessStatusCode();
var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

await using var connection = new HubConnectionBuilder()
    .WithUrl($"{engineUrl}/hubs/query", options => options.AccessTokenProvider = () => Task.FromResult(token))
    .Build();
await connection.StartAsync();

var catalog = await connection.InvokeAsync<JsonElement>("GetCatalog", new { datasetIds = (string[]?)null, includeMembers = false });
var releaseKey = catalog.GetProperty("releaseKey").GetString();
var datasets = catalog.GetProperty("datasets").EnumerateArray().ToList();
Console.WriteLine($"GetCatalog: releaseKey={releaseKey} semanticRevision={catalog.GetProperty("semanticRevision")} datasets={datasets.Count}");
var dataset = datasets.FirstOrDefault(item => item.GetProperty("id").GetString() == datasetKey);
if (dataset.ValueKind == JsonValueKind.Undefined)
{
    Console.WriteLine($"FAIL: dataset {datasetKey} is not in the catalog.");
    return 1;
}

string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) ? value.ToString() : "";
Console.WriteLine($"  dataset {datasetKey}: defaultTimeDimension={Text(dataset, "defaultTimeDimension")}");
foreach (var field in dataset.GetProperty("fields").EnumerateArray())
{
    Console.WriteLine($"  field {Text(field, "id")} {field.GetProperty("valueType").GetProperty("dataType")} groupable={Text(field, "isGroupable")}");
}
foreach (var dimension in dataset.GetProperty("dimensions").EnumerateArray())
{
    Console.WriteLine($"  dimension {Text(dimension, "id")} binding={Text(dimension, "bindingKind")} dimensionKey={Text(dimension, "dimensionKey")} kind={Text(dimension, "dimensionKind")} version={(dimension.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Object ? Text(version, "versionId") : "none")}");
}
foreach (var measure in dataset.GetProperty("measures").EnumerateArray())
{
    Console.WriteLine($"  measure {Text(measure, "id")} kind={Text(measure, "kind")} function={Text(measure, "aggregationFunction")} output={measure.GetProperty("outputType")} dependencies={measure.GetProperty("dependencies")} requiredTime={Text(measure, "requiredTimeDimension")}");
}
foreach (var time in dataset.GetProperty("timeDimensions").EnumerateArray())
{
    Console.WriteLine($"  timeDimension {Text(time, "id")} {Text(time, "timeValueType")} periodicities={time.GetProperty("allowedPeriodicities")}");
}

var request = JsonDocument.Parse(File.ReadAllText(requestFile)).RootElement;
var response = await connection.InvokeAsync<JsonElement>("Execute", request);
Console.WriteLine($"Execute: status={Text(response, "status")} error={Text(response, "error")}");
var allReturnedRows = true;
foreach (var result in response.GetProperty("queries").EnumerateArray())
{
    var rowCount = result.GetProperty("rowCount").GetInt64();
    allReturnedRows &= rowCount > 0;
    Console.WriteLine($"  query {Text(result, "queryId")}: status={Text(result, "status")} rowCount={rowCount} truncated={Text(result, "truncated")} error={Text(result, "error")}");
    Console.WriteLine($"  columns: {string.Join(", ", result.GetProperty("schema").EnumerateArray().Select(column => $"{Text(column, "name")}:{Text(column, "dataType")}"))}");
    foreach (var row in result.GetProperty("rows").EnumerateArray().Take(12))
    {
        Console.WriteLine($"    {row}");
    }
}

var reportCatalog = await connection.InvokeAsync<JsonElement>("GetReportCatalog", new { reportKeys = new[] { reportKey } });
var catalogReports = reportCatalog.GetProperty("reports").EnumerateArray().ToList();
Console.WriteLine($"GetReportCatalog: releaseKey={Text(reportCatalog, "releaseKey")} reports={catalogReports.Count}");
foreach (var item in catalogReports)
{
    Console.WriteLine($"  report {Text(item, "key")} model={Text(item, "modelKey")} currency={Text(item, "currencyCode")} reportDimension={Text(item, "reportDimensionKey")} columns={item.GetProperty("columns").GetArrayLength()} lines={item.GetProperty("lines").GetArrayLength()}");
}
var liveEditServed = catalogReports.Any(item => item.GetProperty("columns").EnumerateArray().Any(column => Text(column, "displayName") == liveColumnName));
Console.WriteLine($"  live column rename served: {liveEditServed}");

var reportRequest = JsonDocument.Parse(File.ReadAllText(reportRequestFile)).RootElement;
var report = await connection.InvokeAsync<JsonElement>("ExecuteReport", reportRequest);
Console.WriteLine($"ExecuteReport: status={Text(report, "status")} error={Text(report, "error")} sources={report.GetProperty("sources").GetArrayLength()}");
var tables = report.GetProperty("tables").EnumerateArray().ToList();
var everyTableReturnedRows = tables.Count > 0;
foreach (var table in tables)
{
    var rows = table.GetProperty("rows").EnumerateArray().ToList();
    everyTableReturnedRows &= rows.Count > 0;
    Console.WriteLine($"  table [{string.Join(", ", table.GetProperty("dimensions").EnumerateArray())}]: rows={rows.Count}");
    foreach (var row in rows.Take(8))
    {
        Console.WriteLine($"    {row}");
    }
}

var backtest = await connection.InvokeAsync<JsonElement>("GetCatalog", new { datasetIds = Array.Empty<string>(), includeMembers = false, assumptionSet = backtestSet });
var capturedReleaseKey = Text(backtest, "releaseKey");
Console.WriteLine($"GetCatalog {backtestSet}: releaseKey={capturedReleaseKey}");

var passed = releaseKey == expectedReleaseKey && allReturnedRows && catalogReports.Count == 1 && liveEditServed && everyTableReturnedRows
    && capturedReleaseKey == backtestReleaseKey;
Console.WriteLine(passed ? "PASS" : $"FAIL: expected release {expectedReleaseKey}, rows from every query, report {reportKey} in the report catalog with column '{liveColumnName}', rows in every report table, and release {backtestReleaseKey} for set {backtestSet}.");
return passed ? 0 : 1;
EOF
dotnet run "$W/hub/hub.cs" -- "$ENGINE" "$TENANT" "${PREFIX}acceptance" "$DATASET_KEY" "$W/request.json" "$REPORT_KEY" "$W/report.json" "$LIVE_COLUMN_NAME" "${PREFIX}backtest" "$ORIGINAL_KEY" 2>&1 | grep -v -E 'warning (NU|IL)[0-9]+'
PASSED=1
