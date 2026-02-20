#!/usr/bin/env bash
#
# run-all.sh - Run all k6 load test scenarios and generate a summary report.
#
# Usage:
#   ./run-all.sh                               # Run all tests
#   ./run-all.sh --skip-stress                  # Skip the stress test
#   ./run-all.sh --skip-soak                    # Skip the soak test
#   BASE_URL=http://myhost:5000 ./run-all.sh    # Custom target
#
# Environment Variables:
#   BASE_URL       - Target URL (default: http://localhost:5000)
#   PROJECT_ID     - Sinch project ID (default: test-project-001)
#   OUTPUT_DIR     - Directory for JSON results (default: ./results)
#   SKIP_STRESS    - Set to "true" to skip stress test
#   SKIP_SOAK      - Set to "true" to skip soak test

set -euo pipefail

# --- Configuration ---
BASE_URL="${BASE_URL:-http://localhost:5000}"
PROJECT_ID="${PROJECT_ID:-test-project-001}"
OUTPUT_DIR="${OUTPUT_DIR:-./results}"
SKIP_STRESS="${SKIP_STRESS:-false}"
SKIP_SOAK="${SKIP_SOAK:-false}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Parse command line arguments
for arg in "$@"; do
    case $arg in
        --skip-stress) SKIP_STRESS="true" ;;
        --skip-soak)   SKIP_SOAK="true" ;;
        --help)
            echo "Usage: $0 [--skip-stress] [--skip-soak]"
            echo ""
            echo "Environment variables:"
            echo "  BASE_URL     Target URL (default: http://localhost:5000)"
            echo "  PROJECT_ID   Sinch project ID (default: test-project-001)"
            echo "  OUTPUT_DIR   Results directory (default: ./results)"
            exit 0
            ;;
    esac
done

# --- Setup ---
mkdir -p "${OUTPUT_DIR}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
REPORT_FILE="${OUTPUT_DIR}/summary_${TIMESTAMP}.txt"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Track results
declare -a TEST_NAMES
declare -a TEST_RESULTS
declare -a TEST_DURATIONS

log_header() {
    echo -e "\n${BLUE}============================================${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}============================================${NC}\n"
}

log_success() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

log_failure() {
    echo -e "${RED}[FAIL]${NC} $1"
}

log_skip() {
    echo -e "${YELLOW}[SKIP]${NC} $1"
}

run_test() {
    local test_name="$1"
    local test_file="$2"
    local output_file="${OUTPUT_DIR}/${test_name}_${TIMESTAMP}.json"

    log_header "Running: ${test_name}"

    local start_time
    start_time=$(date +%s)

    if k6 run \
        --out "json=${output_file}" \
        -e "BASE_URL=${BASE_URL}" \
        -e "PROJECT_ID=${PROJECT_ID}" \
        "${SCRIPT_DIR}/${test_file}" 2>&1 | tee "${OUTPUT_DIR}/${test_name}_${TIMESTAMP}.log"; then
        local end_time
        end_time=$(date +%s)
        local duration=$((end_time - start_time))

        TEST_NAMES+=("${test_name}")
        TEST_RESULTS+=("PASS")
        TEST_DURATIONS+=("${duration}s")
        log_success "${test_name} completed in ${duration}s"
        return 0
    else
        local end_time
        end_time=$(date +%s)
        local duration=$((end_time - start_time))

        TEST_NAMES+=("${test_name}")
        TEST_RESULTS+=("FAIL")
        TEST_DURATIONS+=("${duration}s")
        log_failure "${test_name} failed after ${duration}s"
        return 1
    fi
}

# --- Pre-flight Check ---
log_header "Pre-flight Check"

echo "Target URL:  ${BASE_URL}"
echo "Project ID:  ${PROJECT_ID}"
echo "Output dir:  ${OUTPUT_DIR}"
echo "Timestamp:   ${TIMESTAMP}"
echo ""

# Check k6 is installed
if ! command -v k6 &> /dev/null; then
    echo -e "${RED}ERROR: k6 is not installed. Install it from https://k6.io/docs/getting-started/installation/${NC}"
    exit 1
fi
echo "k6 version: $(k6 version 2>/dev/null || echo 'unknown')"

# Check mock server is reachable
echo -n "Checking mock server health... "
if curl -sf "${BASE_URL}/admin/health" > /dev/null 2>&1; then
    echo -e "${GREEN}OK${NC}"
else
    echo -e "${RED}FAILED${NC}"
    echo "Mock server at ${BASE_URL} is not reachable."
    echo "Start the mock server first:"
    echo "  cd src/Sinch.MessageRouter.MockServer && dotnet run"
    exit 1
fi

# Reset mock server metrics
echo -n "Resetting mock server metrics... "
curl -sf -X POST "${BASE_URL}/admin/reset" > /dev/null 2>&1 && echo -e "${GREEN}OK${NC}" || echo -e "${YELLOW}SKIPPED${NC}"

echo ""

# --- Run Tests ---
OVERALL_START=$(date +%s)
FAILED_COUNT=0

# 1. Smoke Test
run_test "smoke-test" "smoke-test.js" || ((FAILED_COUNT++)) || true

# 2. Load Test
run_test "load-test" "load-test.js" || ((FAILED_COUNT++)) || true

# 3. Multichannel Test
run_test "multichannel-test" "multichannel-test.js" || ((FAILED_COUNT++)) || true

# 4. Cost Estimator
run_test "cost-estimator" "cost-estimator.js" || ((FAILED_COUNT++)) || true

# 5. Stress Test (optional)
if [ "${SKIP_STRESS}" = "true" ]; then
    log_skip "stress-test (skipped via flag)"
    TEST_NAMES+=("stress-test")
    TEST_RESULTS+=("SKIP")
    TEST_DURATIONS+=("-")
else
    run_test "stress-test" "stress-test.js" || ((FAILED_COUNT++)) || true
fi

# 6. Soak Test (optional - takes 30+ minutes)
if [ "${SKIP_SOAK}" = "true" ]; then
    log_skip "soak-test (skipped via flag)"
    TEST_NAMES+=("soak-test")
    TEST_RESULTS+=("SKIP")
    TEST_DURATIONS+=("-")
else
    run_test "soak-test" "soak-test.js" || ((FAILED_COUNT++)) || true
fi

OVERALL_END=$(date +%s)
OVERALL_DURATION=$((OVERALL_END - OVERALL_START))

# --- Generate Summary Report ---
{
    echo "========================================================"
    echo "          LOAD TEST SUITE - SUMMARY REPORT"
    echo "========================================================"
    echo ""
    echo "Date:       $(date)"
    echo "Target:     ${BASE_URL}"
    echo "Project:    ${PROJECT_ID}"
    echo "Duration:   ${OVERALL_DURATION}s ($(( OVERALL_DURATION / 60 ))m $(( OVERALL_DURATION % 60 ))s)"
    echo ""
    echo "--- Test Results ---"
    printf "%-25s %-10s %-10s\n" "Test" "Result" "Duration"
    echo "---------------------------------------------"
    for i in "${!TEST_NAMES[@]}"; do
        printf "%-25s %-10s %-10s\n" "${TEST_NAMES[$i]}" "${TEST_RESULTS[$i]}" "${TEST_DURATIONS[$i]}"
    done
    echo "---------------------------------------------"

    local pass_count=0
    local fail_count=0
    local skip_count=0
    for result in "${TEST_RESULTS[@]}"; do
        case $result in
            PASS) ((pass_count++)) ;;
            FAIL) ((fail_count++)) ;;
            SKIP) ((skip_count++)) ;;
        esac
    done

    echo ""
    echo "Passed:  ${pass_count}"
    echo "Failed:  ${fail_count}"
    echo "Skipped: ${skip_count}"
    echo ""

    # Fetch final server stats
    if curl -sf "${BASE_URL}/admin/stats" > /dev/null 2>&1; then
        echo "--- Mock Server Cumulative Stats ---"
        curl -sf "${BASE_URL}/admin/stats" | python3 -m json.tool 2>/dev/null || curl -sf "${BASE_URL}/admin/stats"
    fi

    echo ""
    echo "--- Output Files ---"
    ls -la "${OUTPUT_DIR}/"*"${TIMESTAMP}"* 2>/dev/null || echo "No output files found."
    echo ""
    echo "========================================================"
} | tee "${REPORT_FILE}"

# --- Final Status ---
echo ""
if [ ${FAILED_COUNT} -eq 0 ]; then
    log_success "All tests passed! Report saved to: ${REPORT_FILE}"
    exit 0
else
    log_failure "${FAILED_COUNT} test(s) failed. Report saved to: ${REPORT_FILE}"
    exit 1
fi
