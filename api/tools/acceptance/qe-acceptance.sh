#!/usr/bin/env bash
# End-to-end acceptance of Query Engine Admin: a dataset authored through the admin API reaches the query engine.
#
#   PREFIX=zz_w2_ ./qe-acceptance.sh
#     Authors dataset ${PREFIX}loans on loan_daily (5 fields, 2 grains, a mapped binding through the seed mapping set
#     loan.branch, a time binding on the seed Calendar dimension, 4 measures) and publishes its v1. It authors calculation
#     model ${PREFIX}margin over the seed GL monthly datasets (2 variables, their sources, a profile, a yield formula and a
#     branch calculation dimension) and report ${PREFIX}margin over it (3 columns, member and formula lines), and publishes
#     both. Then it creates release ${PREFIX}acceptance as a copy of the active release plus the dataset version and the
#     report version, validates, publishes and activates it, checks GetCatalog, Execute, GetReportCatalog and ExecuteReport
#     on the engine, and re-activates the release that was active before.
#   PREFIX=zz_live_ DATASET_KEY=zz_bm_loans MEASURES="zzBmBalance zzBmRate zzBmBalanceRate zzBmEndingBalance" ./qe-acceptance.sh
#     Releases an existing dataset's latest published version instead of authoring one.
#   PREFIX=zz_w2_ AUTHOR_CHECK_ONLY=1 ./qe-acceptance.sh
#     Authors and validates ${PREFIX}loans, then deletes it: the authoring path without leaving objects.
#
# Run it outside the shell sandbox: it calls localhost and StarRocks. It needs curl, python3 and the .NET 10 SDK, and the
# admin API running in Development at $ADMIN. When $ENGINE does not answer, it starts the engine host from its built DLL
# under $QueryEngineRoot (or $ENGINE_HOST_DIR) and stops it at the end. It writes to tenant $TENANT, development by
# default, and never prints the control-plane connection string. While it runs, its release is the tenant's active
# release, so don't run the engine's golden cases at the same time.
# It leaves ${PREFIX}loans (published v1, when authored), the ${PREFIX}margin model and report (published v1) and
# ${PREFIX}acceptance (published, inactive) until the seed kit is reloaded. Published objects are immutable, so every run
# needs a new PREFIX.
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
SECRETS=$HOME/.microsoft/usersecrets/Quantalytix.QueryEngine.Admin.Api.Development/secrets.json
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

# json EXPR: prints a Python expression as JSON, for bodies that nest JSON text.
json() { python3 -c "import json; print(json.dumps($1))"; }

# measure KEY KIND OVERRIDES_JSON: a measure body with the defaults of a decimal(20, 2) output.
measure() {
  python3 -c '
import json, sys
key, kind, overrides = sys.argv[1], sys.argv[2], json.loads(sys.argv[3])
body = {"measureKey": key, "displayName": key, "measureKind": kind, "aggregationFunction": None, "expressionText": None,
        "rowFilterExpression": None, "valueNullBehavior": "exclude", "negativeWeightBehavior": "allow", "emptySetBehavior": "null",
        "bridgeFactorBehavior": "ignore", "bridgeAllocationBehavior": "apply", "totalBehavior": "recompute",
        "outputDataType": "decimal", "outputPrecision": 20, "outputScale": 2, "accumulatorPrecision": 38, "accumulatorScale": 12,
        "roundingMode": None, "unitKey": None, "implementationKey": None, "implementationVersion": None,
        "configurationJson": None, "isEnabled": True, "components": [], "timePolicy": None}
body.update(overrides)
print(json.dumps(body))' "$@"
}

author_dataset() {
  DATASET_KEY=${PREFIX}loans
  call POST /datasets 200 "{\"datasetKey\":\"$DATASET_KEY\",\"displayName\":\"$DATASET_KEY acceptance\",\"isEnabled\":true,\"sourceObjectKind\":\"table\",\"sourceObject\":\"loan_daily\"}"
  DATASET=$(py "r['datasetId']")
  call GET "/datasets/$DATASET/versions" 200
  VERSION=$(py "r[0]['datasetVersionId']")
  local V=/dataset-versions/$VERSION

  call POST "$V/fields/from-columns" 200 '{"columns":["loan_id","as_of_date","branch_code","interest_rate","ending_balance"]}'
  local fields
  fields=$(py "' '.join(str(f['fieldId']) for k in ['loanId', 'asOfDate', 'branchCode', 'interestRate', 'endingBalance'] for f in r if f['fieldKey'] == k)")
  read -r F_LOAN F_DATE F_BRANCH F_RATE F_BALANCE <<< "$fields"
  call POST "$V/grains" 200 "{\"grainKey\":\"loanSeries\",\"grainKind\":\"series\",\"fieldIds\":[$F_LOAN]}"
  local series
  series=$(py "r['grainId']")
  call POST "$V/grains" 200 "{\"grainKey\":\"rowIdentity\",\"grainKind\":\"row\",\"fieldIds\":[$F_LOAN,$F_DATE]}"

  # Seed objects are only referenced: the Calendar time dimension and the loan.branch mapping set onto Branch.
  call GET /lookups/dimensions 200
  local calendar
  calendar=$(py "[d['id'] for d in r if d['key'] == 'Calendar'][0]")
  call GET /lookups/mapping-sets 200
  local mapset branch
  mapset=$(py "[s['id'] for s in r if s['key'] == 'loan.branch'][0]")
  branch=$(py "[s['parentId'] for s in r if s['key'] == 'loan.branch'][0]")
  local common='"pinnedDimensionVersionId":null,"groupingMode":"member","raggedBehavior":"null","defaultPeriodAlignment":"observation","unmappedBehavior":"null","isGroupable":true,"isFilterable":true,"isEnabled":true'
  call POST "$V/bindings" 200 "{\"displayName\":\"Branch\",\"dimensionId\":$branch,\"bindingKind\":\"mapped\",\"sourceFieldId\":$F_BRANCH,\"mappingSetId\":$mapset,\"isRequired\":false,$common}"
  call POST "$V/bindings" 200 "{\"displayName\":\"As of\",\"dimensionId\":$calendar,\"bindingKind\":\"time\",\"sourceFieldId\":$F_DATE,\"isRequired\":true,$common,\"timeBinding\":{\"timeValueType\":\"date\",\"observationKind\":\"snapshot\",\"sourceTimeZone\":null,\"nativePeriodicity\":\"day\",\"intervalEndFieldId\":null,\"allowedPeriodicities\":[\"day\",\"month\",\"quarter\",\"year\"],\"defaultQueryTimeZone\":\"UTC\",\"cumulativeOrigin\":\"output_range_start\",\"cumulativeFixedStart\":null}}"
  local time_binding
  time_binding=$(py "r['binding']['bindingId']")

  call POST "$V/measures" 200 "$(measure balance aggregate "{\"aggregationFunction\":\"sum\",\"components\":[{\"componentRole\":\"value\",\"fieldId\":$F_BALANCE}]}")"
  call POST "$V/measures" 200 "$(measure avgRate weighted_average "{\"outputPrecision\":9,\"outputScale\":6,\"components\":[{\"componentRole\":\"value\",\"fieldId\":$F_RATE},{\"componentRole\":\"weight\",\"fieldId\":$F_BALANCE}]}")"
  call POST "$V/measures" 200 "$(measure balanceRate formula '{"outputPrecision":28,"outputScale":6,"expressionText":"[m.balance] * [m.avgRate]"}')"
  call POST "$V/measures" 200 "$(measure endingBalance aggregate "{\"aggregationFunction\":\"sum\",\"components\":[{\"componentRole\":\"value\",\"fieldId\":$F_BALANCE}],\"timePolicy\":{\"requiredTimeBindingId\":$time_binding,\"selectionGrainId\":$series,\"observationSelection\":\"at_end\",\"valueTimeRole\":\"point\",\"boundaryMatch\":\"on_or_before\",\"maxLookbackSeconds\":86400,\"durationUnit\":\"calendar_day\",\"partialIntervalBehavior\":\"exact_only\",\"missingPeriodBehavior\":\"null\"}}")"
  call PUT "$V" 200 "{\"sourceObjectKind\":\"table\",\"sourceObject\":\"loan_daily\",\"supportsAggregate\":true,\"supportsDetail\":true,\"defaultTimeBindingId\":$time_binding,\"expressionLanguageVersion\":\"1.0\",\"securityPolicyKey\":null,\"properties\":null}"

  call PUT "/versions/dataset_version/$VERSION/status" 200 '{"status":"validated"}'
  if [ "${AUTHOR_CHECK_ONLY:-}" = 1 ]; then
    call PUT "/versions/dataset_version/$VERSION/status" 200 '{"status":"draft"}'
    call DELETE "/datasets/$DATASET" 204
    echo "PASS (authoring only): $DATASET_KEY validated, then deleted."
    rm -rf "$W"
    exit 0
  fi
  call PUT "/versions/dataset_version/$VERSION/status" 200 '{"status":"published"}'
}

# Authors model and report ${PREFIX}margin over the seed GL monthly datasets and publishes both. Sources name keys only; the
# release that pins the report decides which dataset and report-item versions they resolve in.
author_report() {
  REPORT_KEY=${PREFIX}margin
  call GET /lookups/datasets 200
  local balances income
  balances=$(py "[d['id'] for d in r if d['key'] == 'bank.balance_monthly'][0]")
  income=$(py "[d['id'] for d in r if d['key'] == 'bank.income_monthly'][0]")
  call GET /lookups/dimensions 200
  local report_items branch
  report_items=$(py "[d['id'] for d in r if d['key'] == 'ReportItem'][0]")
  branch=$(py "[d['id'] for d in r if d['key'] == 'Branch'][0]")

  call POST /calculation-models 200 "$(json "{'modelKey': '$REPORT_KEY', 'displayName': '$REPORT_KEY acceptance', 'isEnabled': True}")"
  call POST "/calculation-models/$(py "r['modelId']")/versions" 200 '{"currencyCode":"USD","cloneFromVersionId":null}'
  MODEL_VERSION=$(py "r['modelVersionId']")
  local M=/model-versions/$MODEL_VERSION
  local variable="'targetMemberKey': None, 'selectionScope': 'all_mapped', 'missingBehavior': 'null'"
  call POST "$M/variables" 200 "$(json "{'variableKey': 'AVERAGE_BALANCE', 'displayName': 'Average balance', 'variableRole': 'average_balance', $variable}")"
  local v_balance v_income s_balance s_income
  v_balance=$(py "r['variableId']")
  call POST "$M/variables" 200 "$(json "{'variableKey': 'INTEREST_INCOME', 'displayName': 'Interest income', 'variableRole': 'interest_income', $variable}")"
  v_income=$(py "r['variableId']")
  call POST "$M/variable-sources" 200 "$(json "{'variableId': $v_balance, 'sourceKey': 'gl_monthly_balance', 'datasetId': $balances, 'reportFieldKey': 'reportItem', 'timeFieldKey': 'reportingPeriod', 'measureKey': 'periodAverage', 'sourceFilterJson': None, 'isEnabled': True}")"
  s_balance=$(py "r['variableSourceId']")
  call POST "$M/variable-sources" 200 "$(json "{'variableId': $v_income, 'sourceKey': 'gl_interest_income', 'datasetId': $income, 'reportFieldKey': 'reportItem', 'timeFieldKey': 'reportingPeriod', 'measureKey': 'totalValue', 'sourceFilterJson': json.dumps({'operator': 'eq', 'left': {'type': 'field', 'id': 'accountKey'}, 'right': {'type': 'literal', 'value': '4000'}}), 'isEnabled': True}")"
  s_income=$(py "r['variableSourceId']")
  call POST "$M/profiles" 200 "$(json "{'profileKey': 'gl_monthly', 'displayName': 'GL monthly balances', 'isEnabled': True, 'picks': [{'variableId': $v_balance, 'variableSourceId': $s_balance}, {'variableId': $v_income, 'variableSourceId': $s_income}]}")"
  local profile dimension
  profile=$(py "r['profile']['sourceProfileId']")
  call POST "$M/calculated-measures" 200 "$(json "{'measureKey': 'YIELD', 'displayName': 'Yield', 'expressionText': '[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays', 'outputDataType': 'decimal', 'outputPrecision': 28, 'outputScale': 6, 'roundingMode': None, 'unitKey': None, 'displayFormat': 'percent'}")"
  call POST "$M/calculation-dimensions" 200 "$(json "{'dimensionKey': 'branch', 'displayName': 'Branch', 'valueKind': 'canonical', 'dimensionId': $branch, 'valueDataType': None, 'isGroupable': True, 'isFilterable': True, 'isEnabled': True}")"
  dimension=$(py "r['calculationDimensionId']")
  for dataset in "$balances" "$income"; do
    call POST "$M/routes" 200 "$(json "{'calculationDimensionId': $dimension, 'datasetId': $dataset, 'variableSourceId': None, 'routeKind': 'semantic', 'fieldKey': 'branch'}")"
  done
  call PUT "/versions/calculation_model_version/$MODEL_VERSION/status" 200 '{"status":"validated"}'
  call PUT "/versions/calculation_model_version/$MODEL_VERSION/status" 200 '{"status":"published"}'

  call POST /reports 200 "$(json "{'reportKey': '$REPORT_KEY', 'displayName': '$REPORT_KEY acceptance', 'reportType': 'margin_analysis', 'isEnabled': True, 'payloadJson': None}")"
  REPORT=$(py "r['reportId']")
  call POST "/reports/$REPORT/versions" 200 "$(json "{'reportDimensionId': $report_items, 'modelVersionId': $MODEL_VERSION, 'defaultSourceProfileId': $profile, 'payloadJson': None, 'defaultFilterJson': None, 'defaultSettingsJson': json.dumps({'periodicity': 'month', 'timeZone': 'UTC'}), 'defaultPivotJson': None, 'cloneFromVersionId': None}")"
  REPORT_VERSION=$(py "r['version']['reportVersionId']")
  local R=/report-versions/$REPORT_VERSION
  call PUT "$R/columns" 200 "$(json "{'columns': [{'reportColumnId': None, 'columnKey': key, 'displayName': name, 'valueKind': kind, 'metricKey': metric, 'displayFormat': display, 'isVisible': True, 'payloadJson': None} for key, name, kind, metric, display in [('avgBalance', 'Average balance', 'variable', 'AVERAGE_BALANCE', 'currency'), ('interestIncome', 'Interest income', 'variable', 'INTEREST_INCOME', 'currency'), ('yield', 'Yield', 'calculated', 'YIELD', 'percent')]]}")"
  call PUT "$R/lines" 200 "$(json "{'lines': [{'reportLineId': None, 'lineKey': key, 'displayName': name, 'lineKind': kind, 'parentLineKey': parent, 'reportMemberKey': member, 'calculatedMeasureKey': measure, 'valueColumnKey': column, 'isVisible': True, 'payloadJson': None, 'settingsJson': None} for key, name, kind, parent, member, measure, column in [('total', 'Total', 'member', None, 'TOTAL', None, None), ('loans', 'Loans', 'member', 'total', 'LOANS', None, None), ('reportYield', 'Report yield', 'formula', None, None, 'YIELD', 'yield')]]}")"
  call PUT "/versions/report_version/$REPORT_VERSION/status" 200 '{"status":"validated"}'
  call PUT "/versions/report_version/$REPORT_VERSION/status" 200 '{"status":"published"}'
}

find_published_version() {
  call GET /lookups/datasets 200
  DATASET=$(py "[d['id'] for d in r if d['key'] == '$DATASET_KEY'][0]")
  call GET "/datasets/$DATASET/versions" 200
  VERSION=$(py "max((v for v in r if v['status'] == 'published'), key=lambda v: v['versionNumber'])['datasetVersionId']")
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
    echo "Active release: $(py "r['activeRelease']['releaseKey']") (expected $ORIGINAL_KEY)"
  fi
  if [ -f "$W/engine.pid" ]; then kill "$(cat "$W/engine.pid")"; fi
  if [ "$PASSED" = 1 ]; then rm -rf "$W"; else echo "Kept $W for inspection."; fi
}

if [ -n "${DATASET_KEY:-}" ]; then find_published_version; else author_dataset; fi
echo "Dataset $DATASET_KEY: version $VERSION"
author_report
echo "Report $REPORT_KEY: version $REPORT_VERSION over model version $MODEL_VERSION"

call GET /overview 200
ORIGINAL_ID=$(py "r['activeRelease']['releaseId']")
ORIGINAL_KEY=$(py "r['activeRelease']['releaseKey']")
call POST /releases 201 "{\"releaseKey\":\"${PREFIX}acceptance\",\"copyFromReleaseId\":$ORIGINAL_ID}"
RELEASE=$(py "r['releaseId']")
call GET "/releases/$RELEASE/datasets" 200
call PUT "/releases/$RELEASE/datasets" 200 "$(py "json.dumps({'datasets': [{'datasetId': x['datasetId'], 'datasetVersionId': x['datasetVersionId']} for x in r] + [{'datasetId': $DATASET, 'datasetVersionId': $VERSION}]})")"
call GET "/releases/$RELEASE/reports" 200
call PUT "/releases/$RELEASE/reports" 200 "$(py "json.dumps({'reports': [{'reportId': x['reportId'], 'reportVersionId': x['reportVersionId']} for x in r] + [{'reportId': $REPORT, 'reportVersionId': $REPORT_VERSION}]})")"
call PUT "/versions/semantic_release/$RELEASE/status" 200 '{"status":"validated"}'
call PUT "/versions/semantic_release/$RELEASE/status" 200 '{"status":"published"}'
ORIGINAL=$ORIGINAL_ID
trap restore EXIT
call POST "/releases/$RELEASE/activate" 200
call GET /overview 200
echo "Active release: $(py "r['activeRelease']['releaseKey']"), semantic revision $(py "r['semanticRevision']")"

start_engine
python3 - "$DATASET_KEY" "$MEASURES" "$RANGE_START" "$RANGE_END" > "$W/request.json" <<'EOF'
import json, sys
dataset, measures, start, end = sys.argv[1], sys.argv[2].split(), sys.argv[3], sys.argv[4]
print(json.dumps({"requestId": "acceptance-" + dataset, "queries": [{
    "id": "byBranch", "datasetId": dataset, "mode": "aggregate",
    "time": {"dimension": "asOfDate", "periodicity": "month", "timeZone": "UTC", "range": {"start": start, "endExclusive": end}},
    "dimensions": [{"id": "branchCode"}],
    "measures": [{"kind": "column", "id": key} for key in measures],
    "orderBy": [{"field": "asOfDate"}, {"field": "branchCode"}]}]}))
EOF
python3 - "$REPORT_KEY" "$REPORT_RANGE_START" "$REPORT_RANGE_END" > "$W/report.json" <<'EOF'
import json, sys
report, start, end = sys.argv[1], sys.argv[2], sys.argv[3]
print(json.dumps({"requestId": "acceptance-" + report, "reportKey": report, "dimensions": ["branch"], "includeTotals": True,
    "time": {"periodicity": "month", "timeZone": "UTC", "range": {"start": start, "endExclusive": end}}}))
EOF

mkdir -p "$W/hub"
cat > "$W/hub/hub.cs" <<'EOF'
#:package Microsoft.AspNetCore.SignalR.Client@10.0.10
#:property PublishAot=false
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

// Usage: dotnet run hub.cs -- <engineUrl> <tenant> <expectedReleaseKey> <datasetKey> <requestFile> <reportKey> <reportRequestFile>
// Exits 0 when GetCatalog reports the expected release and lists the dataset, every query of the request returns rows,
// GetReportCatalog lists the report, and every table of the report request returns rows.
var (engineUrl, tenant, expectedReleaseKey, datasetKey, requestFile) = (args[0], args[1], args[2], args[3], args[4]);
var (reportKey, reportRequestFile) = (args[5], args[6]);

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
    Console.WriteLine($"  dimension {Text(dimension, "id")} binding={Text(dimension, "bindingKind")} dimensionKey={Text(dimension, "dimensionKey")} kind={Text(dimension, "dimensionKind")} versions={dimension.GetProperty("versions").GetArrayLength()}");
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
    Console.WriteLine($"  report {Text(item, "key")} v{Text(item, "version")} model={Text(item, "modelKey")} v{Text(item, "modelVersion")} reportDimension={Text(item, "reportDimensionKey")} columns={item.GetProperty("columns").GetArrayLength()} lines={item.GetProperty("lines").GetArrayLength()}");
}

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

var passed = releaseKey == expectedReleaseKey && allReturnedRows && catalogReports.Count == 1 && everyTableReturnedRows;
Console.WriteLine(passed ? "PASS" : $"FAIL: expected release {expectedReleaseKey}, rows from every query, report {reportKey} in the report catalog and rows in every report table.");
return passed ? 0 : 1;
EOF
dotnet run "$W/hub/hub.cs" -- "$ENGINE" "$TENANT" "${PREFIX}acceptance" "$DATASET_KEY" "$W/request.json" "$REPORT_KEY" "$W/report.json" 2>&1 | grep -v -E 'warning (NU|IL)[0-9]+'
PASSED=1
