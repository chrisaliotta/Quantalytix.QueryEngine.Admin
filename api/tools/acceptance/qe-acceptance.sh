#!/usr/bin/env bash
# End-to-end acceptance of Query Engine Admin: a dataset authored through the admin API reaches the query engine.
#
#   PREFIX=zz_w2_ ./qe-acceptance.sh
#     Authors dataset ${PREFIX}loans on loan_daily (5 fields, 2 grains, a mapped binding of the seed branch role through the
#     seed mapping set loan.branch, a time binding on the seed Calendar dimension, 4 measures) and publishes its v1. It authors
#     calculation model ${PREFIX}margin in US dollars (2 variables and a yield formula, live as written) and builds report
#     ${PREFIX}margin over it through the report editor: its own report dimension starts as a copy of the seed
#     MarginStatement dimension, 2 GL monthly bridge sources on the seed account role map accounts to rows as the seed
#     report's gl_monthly source does and make the default profile, a branch calculation dimension reads the seed branch
#     role, which each GL dataset binds, and 3 columns, a header row and a formula row follow; then it enables the report. It
#     captures assumption set ${PREFIX}backtest, which keeps the release active at that moment. Then it creates release
#     ${PREFIX}acceptance as a copy of the active release plus the dataset version, validates, publishes and activates it,
#     renames the report's yield column live, checks GetCatalog, Execute, GetReportCatalog (which must show the renamed column)
#     and ExecuteReport on the engine, checks that GetCatalog naming ${PREFIX}backtest still reads the release it captured, and
#     re-activates the release that was active before.
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
# It leaves ${PREFIX}loans (published v1, when authored), the ${PREFIX}margin model and report (enabled) with the report's
# dimension and bridges, ${PREFIX}acceptance (published, inactive) and ${PREFIX}backtest until the seed kit is reloaded. Their keys stay taken, so every run needs a new PREFIX.
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

  # Seed objects are only referenced: the Calendar time dimension, the loan.branch mapping set onto Branch, and Branch's
  # branch role, which requests then name the binding by.
  call GET /lookups/dimensions 200
  local calendar
  calendar=$(py "[d['id'] for d in r if d['key'] == 'Calendar'][0]")
  call GET /lookups/mapping-sets 200
  local mapset branch
  mapset=$(py "[s['id'] for s in r if s['key'] == 'loan.branch'][0]")
  branch=$(py "[s['parentId'] for s in r if s['key'] == 'loan.branch'][0]")
  call GET "/lookups/dimension-roles?parentId=$branch" 200
  local branch_role
  branch_role=$(py "[d['id'] for d in r if d['key'] == 'branch'][0]")
  local common='"raggedBehavior":"null","defaultPeriodAlignment":"observation","unmappedBehavior":"null","isGroupable":true,"isFilterable":true,"isEnabled":true'
  call POST "$V/bindings" 200 "{\"roleId\":$branch_role,\"bindingKind\":\"mapped\",\"sourceFieldId\":$F_BRANCH,\"mappingSetId\":$mapset,\"isRequired\":false,$common}"
  call POST "$V/bindings" 200 "{\"dimensionId\":$calendar,\"bindingKind\":\"time\",\"sourceFieldId\":$F_DATE,\"isRequired\":true,$common,\"timeBinding\":{\"timeValueType\":\"date\",\"observationKind\":\"snapshot\",\"sourceTimeZone\":null,\"nativePeriodicity\":\"day\",\"intervalEndFieldId\":null,\"allowedPeriodicities\":[\"day\",\"month\",\"quarter\",\"year\"],\"defaultQueryTimeZone\":\"UTC\",\"cumulativeOrigin\":\"output_range_start\",\"cumulativeFixedStart\":null}}"
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

# Authors model ${PREFIX}margin, which holds the metrics and has no versions to publish, and builds report ${PREFIX}margin over
# it through the report editor. The report creates its own report dimension as a copy of the seed MarginStatement dimension, whose members
# are its rows. Two sources bridge from the seed account role, which each GL dataset binds: gl_balance maps the GL accounts to
# rows as the seed report's gl_monthly source does, and gl_income starts as a copy of gl_balance's mappings. Binding their
# variables makes profile default, which picks both. The branch calculation dimension on the seed branch role, the columns and
# the rows, with ASSETS as a header and a formula row, follow, and enabling the report checks it against every report rule.
# Sources name keys and roles only: the active release decides which dataset versions they resolve in, and the active
# assumption set which dimension and bridge versions.
author_report() {
  REPORT_KEY=${PREFIX}margin
  call GET /lookups/datasets 200
  local balances income
  balances=$(py "[d['id'] for d in r if d['key'] == 'bank.balance_monthly'][0]")
  income=$(py "[d['id'] for d in r if d['key'] == 'bank.income_monthly'][0]")
  call GET /lookups/dimensions 200
  local statement
  statement=$(py "[d['id'] for d in r if d['key'] == 'MarginStatement'][0]")
  call GET /lookups/dimension-roles 200
  local account_role branch_role
  account_role=$(py "[d['id'] for d in r if d['key'] == 'account'][0]")
  branch_role=$(py "[d['id'] for d in r if d['key'] == 'branch'][0]")
  call GET /reports 200
  local seed
  seed=$(py "[x['reportId'] for x in r if x['reportKey'] == 'bank.margin_statement'][0]")
  call GET "/reports/$seed/editor" 200
  # One rule list per row the seed report's gl_monthly source maps accounts or account folders to; the copied rows keep the
  # seed's keys.
  python3 - "$W/last.json" > "$W/items.jsonl" <<'EOF'
import json, sys
view = json.load(open(sys.argv[1]))
source = [s['source']['reportSourceId'] for s in view['sources'] if s['source']['sourceKey'] == 'gl_monthly'][0]
for row in view['rows']:
    items = [{'itemKey': item['itemKey'], 'selection': item['selection'], 'factor': item['factor']} for item in row['items'] if item['reportSourceId'] == source]
    if items:
        print(json.dumps({'rowKey': row['key'], 'items': items}))
EOF

  call POST /calculation-models 200 "$(json "{'modelKey': '$REPORT_KEY', 'displayName': '$REPORT_KEY acceptance', 'currencyCode': 'USD', 'isEnabled': True, 'formulaLanguageVersion': '1.0', 'attributionMethod': 'shapley'}")"
  MODEL=$(py "r['modelId']")
  local M=/calculation-models/$MODEL
  call POST "$M/variables" 200 "$(json "{'variableKey': 'AVERAGE_BALANCE', 'displayName': 'Average balance', 'variableRole': 'average_balance', 'missingBehavior': 'null', 'displayFormat': 'currency', 'unitKey': None}")"
  call POST "$M/variables" 200 "$(json "{'variableKey': 'INTEREST_INCOME', 'displayName': 'Interest income', 'variableRole': 'interest_income', 'missingBehavior': 'null', 'displayFormat': 'currency', 'unitKey': None}")"
  call POST "$M/calculated-measures" 200 "$(json "{'measureKey': 'YIELD', 'displayName': 'Yield', 'expressionText': '[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays', 'outputDataType': 'decimal', 'outputPrecision': 28, 'outputScale': 6, 'roundingMode': None, 'unitKey': None, 'displayFormat': 'percent'}")"

  call POST /reports 200 "$(json "{'reportKey': '$REPORT_KEY', 'displayName': '$REPORT_KEY acceptance', 'reportType': 'margin_analysis', 'modelId': $MODEL, 'startFromDimensionId': $statement, 'payloadJson': None}")"
  REPORT=$(py "r['reportId']")
  local R=/reports/$REPORT
  local gl="'mappingMethod': 'bridge', 'roleId': $account_role, 'fieldKey': None, 'timeFieldKey': 'reportingPeriod'"
  call POST "$R/sources" 200 "$(json "{'sourceKey': 'gl_balance', 'displayName': 'GL monthly balances', 'datasetId': $balances, $gl, 'copyFromSourceId': None}")"
  local balance_source
  balance_source=$(py "[s['source']['reportSourceId'] for s in r['sources'] if s['source']['sourceKey'] == 'gl_balance'][0]")
  while read -r items; do call PUT "$R/sources/$balance_source/items" 200 "$items"; done < "$W/items.jsonl"
  call POST "$R/sources" 200 "$(json "{'sourceKey': 'gl_income', 'displayName': 'GL interest income', 'datasetId': $income, $gl, 'copyFromSourceId': $balance_source}")"
  local income_source
  income_source=$(py "[s['source']['reportSourceId'] for s in r['sources'] if s['source']['sourceKey'] == 'gl_income'][0]")
  echo "Sources and their mapped items: $(py "[(s['source']['sourceKey'], sum(1 for row in r['rows'] for item in row['items'] if item['reportSourceId'] == s['source']['reportSourceId'])) for s in r['sources']]")"
  call PUT "$R/sources/$balance_source/variables" 200 "$(json "{'variables': [{'variableKey': 'AVERAGE_BALANCE', 'measureKey': 'periodAverage', 'sourceFilterJson': None}]}")"
  call PUT "$R/sources/$income_source/variables" 200 "$(json "{'variables': [{'variableKey': 'INTEREST_INCOME', 'measureKey': 'totalValue', 'sourceFilterJson': json.dumps({'operator': 'eq', 'left': {'type': 'field', 'id': 'accountKey'}, 'right': {'type': 'literal', 'value': '4000'}})}]}")"
  call GET "$R/profiles" 200
  echo "Profile default picks $(py "sorted(p['variableKey'] for x in r if x['profile']['profileKey'] == 'default' for p in x['picks'])") (expected AVERAGE_BALANCE and INTEREST_INCOME)"
  call POST "$R/calculation-dimensions" 200 "$(json "{'dimensionKey': 'branch', 'displayName': 'Branch', 'roleId': $branch_role, 'groupingMode': 'member', 'groupingLevelKey': None, 'groupingAncestorMemberKey': None, 'groupingDepth': None, 'isGroupable': True, 'isFilterable': True, 'isEnabled': True, 'isScenario': False}")"
  call PUT "$R/columns" 200 "$(json "{'columns': [{'reportColumnId': None, 'columnKey': key, 'displayName': None, 'valueKind': kind, 'metricKey': metric, 'displayFormat': None, 'isVisible': True, 'payloadJson': None} for key, kind, metric in [('avgBalance', 'variable', 'AVERAGE_BALANCE'), ('interestIncome', 'variable', 'INTEREST_INCOME'), ('yield', 'calculated', 'YIELD')]]}")"
  # The rows go back as a whole tree: ASSETS becomes a header, and formula row reportYield is added at the top.
  call GET "$R/editor" 200
  call PUT "$R/rows" 200 "$(py "json.dumps({'rows': [dict({k: row[k] for k in ['key', 'label', 'kind', 'parentKey', 'sortOrder', 'isVisible', 'calculatedMeasureKey', 'valueColumnKey']}, showsValues=row['showsValues'] and row['key'] != 'ASSETS') for row in r['rows']] + [{'key': 'reportYield', 'label': 'Report yield', 'kind': 'formula', 'parentKey': None, 'sortOrder': 99, 'isVisible': True, 'showsValues': True, 'calculatedMeasureKey': 'YIELD', 'valueColumnKey': 'yield'}]})")"
  call PUT "$R/status" 200 '{"isEnabled":true}'
}

# Renames the enabled report's yield column: a live edit, which advances the semantic revision so the engine reloads it.
rename_yield_column() {
  local R=/reports/$REPORT
  call GET "$R/columns" 200
  call PUT "$R/columns" 200 "$(py "json.dumps({'columns': [{'reportColumnId': c['reportColumnId'], 'columnKey': c['columnKey'], 'displayName': '$LIVE_COLUMN_NAME' if c['columnKey'] == 'yield' else c['displayName'], 'valueKind': c['valueKind'], 'metricKey': c['metricKey'], 'displayFormat': c['displayFormat'], 'isVisible': c['isVisible'], 'payloadJson': c['payloadJson']} for c in r]})")"
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
echo "Report $REPORT_KEY: enabled, over model $MODEL"

call GET /overview 200
ORIGINAL_ID=$(py "r['activeRelease']['releaseId']")
ORIGINAL_KEY=$(py "r['activeRelease']['releaseKey']")
call POST /assumption-sets 201 "{\"assumptionSetKey\":\"${PREFIX}backtest\",\"displayName\":\"${PREFIX}backtest\"}"
echo "Captured ${PREFIX}backtest on release $(py "r['releaseId']") (expected $ORIGINAL_ID)"
call POST /releases 201 "{\"releaseKey\":\"${PREFIX}acceptance\",\"copyFromReleaseId\":$ORIGINAL_ID}"
RELEASE=$(py "r['releaseId']")
call GET "/releases/$RELEASE/datasets" 200
call PUT "/releases/$RELEASE/datasets" 200 "$(py "json.dumps({'datasets': [{'datasetId': x['datasetId'], 'datasetVersionId': x['datasetVersionId']} for x in r] + [{'datasetId': $DATASET, 'datasetVersionId': $VERSION}]})")"
call PUT "/versions/semantic_release/$RELEASE/status" 200 '{"status":"validated"}'
call PUT "/versions/semantic_release/$RELEASE/status" 200 '{"status":"published"}'
ORIGINAL=$ORIGINAL_ID
trap restore EXIT
call POST "/releases/$RELEASE/activate" 200
call GET /overview 200
echo "Active release: $(py "r['activeRelease']['releaseKey']"), semantic revision $(py "r['semanticRevision']")"
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
